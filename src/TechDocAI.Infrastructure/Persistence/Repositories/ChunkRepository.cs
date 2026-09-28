using TechDocAI.Application.Abstractions;
using TechDocAI.Application.Common;
using TechDocAI.Core.Entities;
using TechDocAI.Infrastructure.Persistence;

namespace TechDocAI.Infrastructure.Persistence.Repositories;

public class ChunkRepository : IChunkRepository, IScopedDependency
{
    private readonly TechDocDbContext _dbContext;

    public ChunkRepository(TechDocDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void AddRange(IEnumerable<Chunk> chunks) => _dbContext.Chunks.AddRange(chunks);
}
