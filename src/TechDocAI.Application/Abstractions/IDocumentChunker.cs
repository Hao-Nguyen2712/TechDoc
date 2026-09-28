using TechDocAI.Core.Chunking;
using TechDocAI.Core.Extraction;

namespace TechDocAI.Application.Abstractions;

public interface IDocumentChunker
{
    IReadOnlyList<ChunkDraft> Chunk(ExtractionResult extraction);
}
