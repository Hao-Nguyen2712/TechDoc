using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using TechDocAI.Core.Common;
using TechDocAI.Core.Entities;
using TechDocAI.Core.Interfaces;
using TechDocAI.Infrastructure.Persistence;
using TechDocAI.Infrastructure.VectorStore;

namespace TechDocAI.Infrastructure.Services;

public class IngestionPipelineProcessor : ITransientDependency
{
    private readonly IDocumentStorage _storage;
    private readonly IDocumentExtractor _extractor;
    private readonly IDocumentChunker _chunker;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorStore _vectorStore;

    public IngestionPipelineProcessor(
        IDocumentStorage storage,
        IDocumentExtractor extractor,
        IDocumentChunker chunker,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IVectorStore vectorStore)
    {
        _storage = storage;
        _extractor = extractor;
        _chunker = chunker;
        _embeddingGenerator = embeddingGenerator;
        _vectorStore = vectorStore;
    }

    public async Task ProcessJobAsync(TechDocDbContext dbContext, Guid jobId, CancellationToken ct = default)
    {
        var job = await dbContext.IngestionJobs
            .Include(j => j.Document)
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job == null || job.Document == null)
        {
            return;
        }

        try
        {
            await using var pdfStream = await _storage.OpenReadAsync(job.DocumentId, ct);
            var extractionResult = await _extractor.ExtractAsync(pdfStream, job.Document.ContentType, ct);

            if (extractionResult.IsFailure)
            {
                job.Status = IngestionStatus.Failed;
                job.ErrorDetails = extractionResult.Error.Description;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                job.Document.NeedsOcr = true;
                await dbContext.SaveChangesAsync(ct);
                return;
            }

            var extraction = extractionResult.Value;
            var chunkDrafts = _chunker.Chunk(extraction);
            if (chunkDrafts.Count == 0)
            {
                job.Status = IngestionStatus.Failed;
                job.ErrorDetails = "Document contained no extractable text.";
                job.UpdatedAt = DateTimeOffset.UtcNow;
                job.Document.NeedsOcr = true;
                await dbContext.SaveChangesAsync(ct);
                return;
            }

            // 1. Batch generate dense embeddings via AI abstractions
            var chunkTexts = chunkDrafts.Select(c => c.Text).ToList();
            var embeddingOptions = new EmbeddingGenerationOptions
            {
                ModelId = "gemini-embedding-001",
                Dimensions = 1536
            };

            var embeddings = await _embeddingGenerator.GenerateAsync(chunkTexts, embeddingOptions, ct);
            var embeddingList = embeddings.ToList();

            if (embeddingList.Count != chunkDrafts.Count)
            {
                throw new InvalidOperationException(
                    $"Embedding count mismatch: expected {chunkDrafts.Count}, got {embeddingList.Count}.");
            }

            // 2. Build vector chunk records with dense + sparse vectors and ADR 0005 payload
            var vectorRecords = new List<VectorChunkRecord>();
            var chunksToPersist = new List<Chunk>();
            var defaultWorkspaceId = Guid.Empty; // Default single workspace for MVP

            for (int i = 0; i < chunkDrafts.Count; i++)
            {
                var draft = chunkDrafts[i];
                var chunkId = Guid.NewGuid();
                var denseVec = embeddingList[i].Vector;
                var (sparseIndices, sparseValues) = SparseVectorBuilder.Build(draft.Text);

                vectorRecords.Add(new VectorChunkRecord(
                    ChunkId: chunkId,
                    DocumentId: job.DocumentId,
                    WorkspaceId: defaultWorkspaceId,
                    ChunkIndex: i,
                    PageIndex: draft.PageIndex,
                    StartLine: draft.StartLine,
                    EndLine: draft.EndLine,
                    HeadingPath: draft.HeadingPath,
                    Text: draft.Text,
                    EmbeddingModel: "gemini-embedding-001",
                    DenseVector: denseVec,
                    SparseIndices: sparseIndices,
                    SparseValues: sparseValues
                ));

                chunksToPersist.Add(new Chunk
                {
                    Id = chunkId,
                    DocumentId = job.DocumentId,
                    PageIndex = draft.PageIndex,
                    StartLine = draft.StartLine,
                    EndLine = draft.EndLine,
                    HeadingPath = draft.HeadingPath,
                    Text = draft.Text,
                    NeedsOcr = draft.NeedsOcr,
                    EmbeddingModel = "gemini-embedding-001",
                    EmbeddingDimensions = 1536
                });
            }

            // 3. Upsert to Vector Store (Qdrant) first
            await _vectorStore.UpsertChunksAsync(vectorRecords, ct);

            // 4. Commit to PostgreSQL in single transaction
            foreach (var chunk in chunksToPersist)
            {
                dbContext.Chunks.Add(chunk);
            }

            job.Document.NeedsOcr = extraction.TotalNeedsOcr;
            job.Status = IngestionStatus.Done;
            job.UpdatedAt = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // All-or-nothing rollback: clean up any points in vector store for this document
            try
            {
                await _vectorStore.DeleteDocumentChunksAsync(job.DocumentId, CancellationToken.None);
            }
            catch
            {
                // Ignore rollback failure during error handling
            }

            job.Status = IngestionStatus.Failed;
            job.ErrorDetails = ex.Message;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
