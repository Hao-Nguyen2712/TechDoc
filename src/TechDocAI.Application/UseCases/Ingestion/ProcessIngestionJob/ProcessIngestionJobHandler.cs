using Microsoft.Extensions.AI;
using TechDocAI.Application.Abstractions;
using TechDocAI.Application.Common;
using TechDocAI.Core.Chunking;
using TechDocAI.Core.Common;
using TechDocAI.Core.Entities;
using TechDocAI.Core.Extraction;
using TechDocAI.Core.SparseVector;

namespace TechDocAI.Application.UseCases.Ingestion.ProcessIngestionJob;

public class ProcessIngestionJobHandler : ITransientDependency
{
    private readonly IIngestionJobRepository _jobRepository;
    private readonly IChunkRepository _chunkRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDocumentStorage _storage;
    private readonly IPdfExtractor _pdfExtractor;
    private readonly IPlainTextExtractor _plainTextExtractor;
    private readonly StructureAwareChunker _chunker;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorStore _vectorStore;
    private readonly EmbeddingSettings _embeddingSettings;

    public ProcessIngestionJobHandler(
        IIngestionJobRepository jobRepository,
        IChunkRepository chunkRepository,
        IUnitOfWork unitOfWork,
        IDocumentStorage storage,
        IPdfExtractor pdfExtractor,
        IPlainTextExtractor plainTextExtractor,
        StructureAwareChunker chunker,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IVectorStore vectorStore,
        EmbeddingSettings embeddingSettings)
    {
        _jobRepository = jobRepository;
        _chunkRepository = chunkRepository;
        _unitOfWork = unitOfWork;
        _storage = storage;
        _pdfExtractor = pdfExtractor;
        _plainTextExtractor = plainTextExtractor;
        _chunker = chunker;
        _embeddingGenerator = embeddingGenerator;
        _vectorStore = vectorStore;
        _embeddingSettings = embeddingSettings;
    }

    public async Task HandleAsync(ProcessIngestionJobCommand command, CancellationToken ct = default)
    {
        var job = await _jobRepository.FindByIdWithDocumentAsync(command.JobId, ct);

        if (job == null || job.Document == null)
        {
            return;
        }

        try
        {
            await using var pdfStream = await _storage.OpenReadAsync(job.Document.StorageKey, ct);
            var extractionResult = job.Document.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
                ? await _pdfExtractor.ExtractAsync(pdfStream, ct)
                : job.Document.ContentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase)
                    ? await _plainTextExtractor.ExtractAsync(pdfStream, ct)
                    : Result.Failure<ExtractionResult>(
                        Error.Validation("Extraction.UnsupportedType", $"Unsupported content type: {job.Document.ContentType}"));

            if (extractionResult.IsFailure)
            {
                job.Status = IngestionStatus.Failed;
                job.ErrorDetails = extractionResult.Error.Description;
                job.UpdatedAt = DateTimeOffset.UtcNow;
                job.Document.NeedsOcr = true;
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);
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
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);
                return;
            }

            // 1. Batch generate dense embeddings via AI abstractions
            var chunkTexts = chunkDrafts.Select(c => c.Text).ToList();
            var embeddingOptions = new EmbeddingGenerationOptions
            {
                ModelId = _embeddingSettings.ModelId,
                Dimensions = _embeddingSettings.Dimensions
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
                    EmbeddingModel: _embeddingSettings.ModelId,
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
                    EmbeddingModel = _embeddingSettings.ModelId,
                    EmbeddingDimensions = _embeddingSettings.Dimensions
                });
            }

            // 3. Upsert to Vector Store (Qdrant) first
            await _vectorStore.UpsertChunksAsync(vectorRecords, ct);

            // 4. Commit to PostgreSQL in single transaction
            _chunkRepository.AddRange(chunksToPersist);

            job.Document.NeedsOcr = extraction.TotalNeedsOcr;
            job.Status = IngestionStatus.Done;
            job.UpdatedAt = DateTimeOffset.UtcNow;

            await _unitOfWork.SaveChangesAsync(ct);
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
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
    }
}
