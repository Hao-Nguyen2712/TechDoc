namespace TechDocAI.Core.Entities;

public class Chunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int PageIndex { get; set; }
    public string HeadingPath { get; set; } = string.Empty;
    public required string Text { get; set; }
    public bool NeedsOcr { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Document? Document { get; set; }
}
