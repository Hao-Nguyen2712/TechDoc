using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using TechDocAI.Core.Common;
using TechDocAI.Core.DTOs;
using TechDocAI.Core.Entities;
using TechDocAI.Core.Interfaces;
using TechDocAI.Infrastructure.Persistence;

namespace TechDocAI.Infrastructure.Services;

public class DocumentService : IDocumentService, IScopedDependency
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly byte[] PdfMagicBytes = "%PDF-"u8.ToArray();

    private readonly IDocumentStorage _storage;
    private readonly IIngestionJobQueue _queue;
    private readonly TechDocDbContext _dbContext;

    public DocumentService(
        IDocumentStorage storage,
        IIngestionJobQueue queue,
        TechDocDbContext dbContext)
    {
        _storage = storage;
        _queue = queue;
        _dbContext = dbContext;
    }

    public async Task<Result<DocumentUploadResult>> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        long fileLength,
        CancellationToken ct = default)
    {
        if (stream == null || fileLength <= 0)
        {
            return Result.Failure<DocumentUploadResult>(
                Error.Validation("File.Required", "File is required."));
        }

        if (fileLength > MaxFileSizeBytes)
        {
            return Result.Failure<DocumentUploadResult>(
                Error.Validation("File.TooLarge", "File size exceeds maximum allowed limit of 50MB."));
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension != ".pdf" && extension != ".txt")
        {
            return Result.Failure<DocumentUploadResult>(
                Error.Validation("File.InvalidType", "Only PDF and TXT files are supported."));
        }

        string effectiveContentType;
        if (extension == ".pdf")
        {
            effectiveContentType = string.IsNullOrWhiteSpace(contentType) ? "application/pdf" : contentType;
            var buffer = new byte[PdfMagicBytes.Length];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (bytesRead < PdfMagicBytes.Length || !buffer.SequenceEqual(PdfMagicBytes))
            {
                return Result.Failure<DocumentUploadResult>(
                    Error.Validation("File.InvalidHeader", "Invalid PDF file header."));
            }
        }
        else
        {
            effectiveContentType = "text/plain";
        }

        stream.Position = 0;
        var hashBytes = await SHA256.HashDataAsync(stream, ct);
        var contentHash = Convert.ToHexStringLower(hashBytes);

        var existingDoc = await _dbContext.Documents
            .Include(d => d.IngestionJobs)
            .FirstOrDefaultAsync(d => d.ContentHash == contentHash, ct);

        if (existingDoc != null)
        {
            // Case 1: Successfully ingested document -> idempotent 200 OK
            if (existingDoc.IngestionJobs.Any(j => j.Status == IngestionStatus.Done))
            {
                var jobSummaries = existingDoc.IngestionJobs
                    .OrderByDescending(j => j.CreatedAt)
                    .Select(j => new IngestionJobSummaryResponse(
                        j.Id,
                        j.Status.ToString().ToLowerInvariant(),
                        j.ErrorDetails,
                        j.CreatedAt,
                        j.UpdatedAt))
                    .ToList();

                var docDetails = new DocumentDetailsResponse(
                    existingDoc.Id,
                    existingDoc.FileName,
                    existingDoc.ContentType,
                    existingDoc.FileSizeBytes,
                    existingDoc.ContentHash,
                    existingDoc.NeedsOcr,
                    existingDoc.CreatedAt,
                    jobSummaries);

                return Result.Success(new DocumentUploadResult(
                    existingDoc.Id,
                    null,
                    UploadOutcome.AlreadyIngested,
                    docDetails));
            }

            // Case 2: Document whose ingestion is currently active -> return 202 with existing active job
            var activeJob = existingDoc.IngestionJobs
                .FirstOrDefault(j => j.Status is IngestionStatus.Pending or IngestionStatus.Extracting or IngestionStatus.Chunking or IngestionStatus.Embedding);
            if (activeJob != null)
            {
                return Result.Success(new DocumentUploadResult(
                    existingDoc.Id,
                    activeJob.Id,
                    UploadOutcome.Enqueued));
            }

            // Case 3: Document whose previous ingestion failed -> recovery path (ADR 0009):
            // Enqueue a fresh IngestionJob over the already-stored binary (202, same Document)
            var recoveryJobId = Guid.NewGuid();
            var recoveryJob = new IngestionJob
            {
                Id = recoveryJobId,
                DocumentId = existingDoc.Id,
                Status = IngestionStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.IngestionJobs.Add(recoveryJob);
            await _dbContext.SaveChangesAsync(ct);

            await _queue.EnqueueAsync(recoveryJobId, ct);

            return Result.Success(new DocumentUploadResult(
                existingDoc.Id,
                recoveryJobId,
                UploadOutcome.Enqueued));
        }

        var documentId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var storageKey = $"documents/{documentId}/original{extension}";

        stream.Position = 0;
        await _storage.SaveAsync(documentId, stream, effectiveContentType, ct);

        var document = new Document
        {
            Id = documentId,
            FileName = fileName,
            ContentType = effectiveContentType,
            FileSizeBytes = fileLength,
            ContentHash = contentHash,
            StorageKey = storageKey,
            NeedsOcr = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var job = new IngestionJob
        {
            Id = jobId,
            DocumentId = documentId,
            Status = IngestionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Documents.Add(document);
        _dbContext.IngestionJobs.Add(job);
        await _dbContext.SaveChangesAsync(ct);

        await _queue.EnqueueAsync(jobId, ct);

        return Result.Success(new DocumentUploadResult(documentId, jobId));
    }

    public async Task<Result<IngestionJobStatusResult>> GetJobStatusAsync(
        Guid jobId,
        CancellationToken ct = default)
    {
        var job = await _dbContext.IngestionJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job == null)
        {
            return Result.Failure<IngestionJobStatusResult>(
                Error.NotFound("Job.NotFound", "Ingestion job not found."));
        }

        var result = new IngestionJobStatusResult(
            job.Id,
            job.DocumentId,
            job.Status.ToString().ToLowerInvariant(),
            job.ErrorDetails,
            job.CreatedAt,
            job.UpdatedAt);

        return Result.Success(result);
    }

    public async Task<Result<IReadOnlyList<DocumentResponse>>> GetDocumentsAsync(CancellationToken ct = default)
    {
        var rawDocs = await _dbContext.Documents
            .AsNoTracking()
            .Select(d => new DocumentResponse(
                d.Id,
                d.FileName,
                d.ContentType,
                d.FileSizeBytes,
                d.ContentHash,
                d.NeedsOcr,
                d.CreatedAt))
            .ToListAsync(ct);

        var documents = rawDocs.OrderByDescending(d => d.CreatedAt).ToList();

        return Result.Success<IReadOnlyList<DocumentResponse>>(documents);
    }

    public async Task<Result<DocumentDetailsResponse>> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
    {
        var doc = await _dbContext.Documents
            .AsNoTracking()
            .Include(d => d.IngestionJobs)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (doc == null)
        {
            return Result.Failure<DocumentDetailsResponse>(
                Error.NotFound("Document.NotFound", "Document not found."));
        }

        var jobSummaries = doc.IngestionJobs
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new IngestionJobSummaryResponse(
                j.Id,
                j.Status.ToString().ToLowerInvariant(),
                j.ErrorDetails,
                j.CreatedAt,
                j.UpdatedAt))
            .ToList();

        var response = new DocumentDetailsResponse(
            doc.Id,
            doc.FileName,
            doc.ContentType,
            doc.FileSizeBytes,
            doc.ContentHash,
            doc.NeedsOcr,
            doc.CreatedAt,
            jobSummaries);

        return Result.Success(response);
    }
}
