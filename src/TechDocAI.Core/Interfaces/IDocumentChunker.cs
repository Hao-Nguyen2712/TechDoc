namespace TechDocAI.Core.Interfaces;

public record ChunkDraft(
    int? PageIndex,
    int? StartLine,
    int? EndLine,
    string HeadingPath,
    string Text,
    bool NeedsOcr);

public interface IDocumentChunker
{
    IReadOnlyList<ChunkDraft> Chunk(ExtractionResult extraction);
}
