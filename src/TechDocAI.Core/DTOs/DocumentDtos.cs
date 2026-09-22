namespace TechDocAI.Core.DTOs;

public enum UploadOutcome
{
    Enqueued,
    AlreadyIngested
}

public record DocumentUploadResult(
    Guid DocumentId,
    Guid? JobId,
    UploadOutcome Outcome = UploadOutcome.Enqueued,
    DocumentDetailsResponse? ExistingDocument = null);

public record DocumentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string ContentHash,
    bool NeedsOcr,
    DateTimeOffset CreatedAt);

public record DocumentDetailsResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string ContentHash,
    bool NeedsOcr,
    DateTimeOffset CreatedAt,
    IReadOnlyList<IngestionJobSummaryResponse> IngestionJobs);

public record IngestionJobSummaryResponse(
    Guid Id,
    string Status,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record IngestionJobStatusResult(
    Guid Id,
    Guid DocumentId,
    string Status,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
