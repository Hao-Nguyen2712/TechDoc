using Microsoft.EntityFrameworkCore;
using TechDocAI.Core.Entities;
using TechDocAI.Core.Interfaces;
using TechDocAI.Infrastructure.Persistence;

namespace TechDocAI.Infrastructure.Services;

public class IngestionPipelineProcessor
{
    private readonly IDocumentStorage _storage;
    private readonly IDocumentExtractor _extractor;
    private readonly IDocumentChunker _chunker;

    public IngestionPipelineProcessor(
        IDocumentStorage storage,
        IDocumentExtractor extractor,
        IDocumentChunker chunker)
    {
        _storage = storage;
        _extractor = extractor;
        _chunker = chunker;
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
            // Status: Extracting
            job.Status = IngestionStatus.Extracting;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            await using var pdfStream = await _storage.OpenReadAsync(job.DocumentId, ct);
            var extraction = await _extractor.ExtractAsync(pdfStream, ct);

            // Status: Chunking
            job.Status = IngestionStatus.Chunking;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            var chunkDrafts = _chunker.Chunk(extraction);
            if (chunkDrafts.Count == 0)
            {
                job.Status = IngestionStatus.Failed;
                job.ErrorDetails = "PDF contained no extractable text.";
                job.UpdatedAt = DateTimeOffset.UtcNow;
                job.Document.NeedsOcr = true;
                await dbContext.SaveChangesAsync(ct);
                return;
            }

            foreach (var draft in chunkDrafts)
            {
                var chunk = new Chunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = job.DocumentId,
                    PageIndex = draft.PageIndex,
                    HeadingPath = draft.HeadingPath,
                    Text = draft.Text,
                    NeedsOcr = draft.NeedsOcr
                };
                dbContext.Chunks.Add(chunk);
            }

            job.Document.NeedsOcr = extraction.TotalNeedsOcr;
            job.Status = IngestionStatus.Done;
            job.UpdatedAt = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            job.Status = IngestionStatus.Failed;
            job.ErrorDetails = ex.Message;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
