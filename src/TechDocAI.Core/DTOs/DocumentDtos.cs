namespace TechDocAI.Core.DTOs;

public record DocumentUploadResult(Guid DocumentId, Guid JobId);

public record IngestionJobStatusResult(
    Guid Id,
    Guid DocumentId,
    string Status,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
