using TechDocAI.Application.Abstractions;
using TechDocAI.Application.Common;
using TechDocAI.Infrastructure.Persistence;

namespace TechDocAI.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork, IScopedDependency
{
    private readonly TechDocDbContext _dbContext;

    public UnitOfWork(TechDocDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _dbContext.SaveChangesAsync(ct);
}
