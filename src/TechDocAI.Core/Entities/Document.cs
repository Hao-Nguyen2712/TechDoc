namespace TechDocAI.Core.Entities;

public class Document
{
    public Guid Id { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public required string ContentHash { get; set; }
    public required string StorageKey { get; set; }
    public bool NeedsOcr { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<IngestionJob> IngestionJobs { get; set; } = new();
    public List<Chunk> Chunks { get; set; } = new();
}
