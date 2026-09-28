using System.Security.Cryptography;
using TechDocAI.Application.Abstractions;
using TechDocAI.Application.Common;
using TechDocAI.Application.UseCases.Documents;
using TechDocAI.Core.Common;
using TechDocAI.Core.Entities;

namespace TechDocAI.Application.UseCases.Documents.UploadDocument;

public class UploadDocumentHandler : ITransientDependency
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly byte[] PdfMagicBytes = "%PDF-"u8.ToArray();

    private readonly IDocumentRepository _documentRepository;
    private readonly IIngestionJobRepository _jobRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDocumentStorage _storage;
    private readonly IIngestionJobQueue _queue;

    public UploadDocumentHandler(
        IDocumentRepository documentRepository,
        IIngestionJobRepository jobRepository,
        IUnitOfWork unitOfWork,
        IDocumentStorage storage,
        IIngestionJobQueue queue)
    {
        _documentRepository = documentRepository;
        _jobRepository = jobRepository;
        _unitOfWork = unitOfWork;
        _storage = storage;
        _queue = queue;
    }

    public async Task<Result<DocumentUploadResult>> HandleAsync(
        UploadDocumentCommand command,
        CancellationToken ct = default)
    {
        if (command.Stream == null || command.FileLength <= 0)
        {
            return Result.Failure<DocumentUploadResult>(
                Error.Validation("File.Required", "File is required."));
        }

        if (command.FileLength > MaxFileSizeBytes)
        {
            return Result.Failure<DocumentUploadResult>(
                Error.Validation("File.TooLarge", "File size exceeds maximum allowed limit of 50MB."));
        }

        var extension = Path.GetExtension(command.FileName).ToLowerInvariant();
        if (extension != ".pdf" && extension != ".txt")
        {
            return Result.Failure<DocumentUploadResult>(
                Error.Validation("File.InvalidType", "Only PDF and TXT files are supported."));
        }

        string effectiveContentType;
        if (extension == ".pdf")
        {
            effectiveContentType = string.IsNullOrWhiteSpace(command.ContentType) ? "application/pdf" : command.ContentType;
            var buffer = new byte[PdfMagicBytes.Length];
            var bytesRead = await command.Stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
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

        command.Stream.Position = 0;
        var hashBytes = await SHA256.HashDataAsync(command.Stream, ct);
        var contentHash = Convert.ToHexStringLower(hashBytes);

        var existingDoc = await _documentRepository.FindByContentHashWithJobsAsync(contentHash, ct);

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

            _jobRepository.Add(recoveryJob);
            await _unitOfWork.SaveChangesAsync(ct);

            await _queue.EnqueueAsync(recoveryJobId, ct);

            return Result.Success(new DocumentUploadResult(
                existingDoc.Id,
                recoveryJobId,
                UploadOutcome.Enqueued));
        }

        var documentId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var storageKey = $"documents/{documentId}/original{extension}";

        command.Stream.Position = 0;
        await _storage.SaveAsync(storageKey, command.Stream, effectiveContentType, ct);

        var document = new Document
        {
            Id = documentId,
            FileName = command.FileName,
            ContentType = effectiveContentType,
            FileSizeBytes = command.FileLength,
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

        _documentRepository.Add(document);
        _jobRepository.Add(job);
        await _unitOfWork.SaveChangesAsync(ct);

        await _queue.EnqueueAsync(jobId, ct);

        return Result.Success(new DocumentUploadResult(documentId, jobId));
    }
}
