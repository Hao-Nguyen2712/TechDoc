namespace TechDocAI.Core.Chunking;

public record ChunkDraft(
    int? PageIndex,
    int? StartLine,
    int? EndLine,
    string HeadingPath,
    string Text,
    bool NeedsOcr);
