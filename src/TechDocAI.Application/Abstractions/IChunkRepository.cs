using TechDocAI.Core.Entities;

namespace TechDocAI.Application.Abstractions;

public interface IChunkRepository
{
    void AddRange(IEnumerable<Chunk> chunks);
}
