using Microsoft.EntityFrameworkCore;
using TechDocAI.Application.Abstractions;
using TechDocAI.Application.Common;
using TechDocAI.Core.Entities;
using TechDocAI.Infrastructure.Persistence;

namespace TechDocAI.Infrastructure.Persistence.Repositories;

public class IngestionJobRepository : IIngestionJobRepository, IScopedDependency
{
    private readonly TechDocDbContext _dbContext;

    public IngestionJobRepository(TechDocDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IngestionJob?> FindByIdAsync(Guid jobId, CancellationToken ct = default) =>
        _dbContext.IngestionJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

    public Task<IngestionJob?> FindByIdWithDocumentAsync(Guid jobId, CancellationToken ct = default) =>
        _dbContext.IngestionJobs
            .Include(j => j.Document)
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

    public void Add(IngestionJob job) => _dbContext.IngestionJobs.Add(job);
}
