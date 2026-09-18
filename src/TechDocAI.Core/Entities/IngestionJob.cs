namespace TechDocAI.Core.Entities;

public enum IngestionStatus
{
    Pending,
    Extracting,
    Chunking,
    Embedding,
    Done,
    Failed
}

public class IngestionJob
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public IngestionStatus Status { get; set; } = IngestionStatus.Pending;
    public string? ErrorDetails { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Document? Document { get; set; }
}
