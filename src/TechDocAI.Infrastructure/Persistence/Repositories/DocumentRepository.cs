using Microsoft.EntityFrameworkCore;
using TechDocAI.Application.Abstractions;
using TechDocAI.Application.Common;
using TechDocAI.Core.Entities;
using TechDocAI.Infrastructure.Persistence;

namespace TechDocAI.Infrastructure.Persistence.Repositories;

public class DocumentRepository : IDocumentRepository, IScopedDependency
{
    private readonly TechDocDbContext _dbContext;

    public DocumentRepository(TechDocDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Document?> FindByIdWithJobsAsync(Guid documentId, CancellationToken ct = default) =>
        _dbContext.Documents
            .Include(d => d.IngestionJobs)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

    public Task<Document?> FindByContentHashWithJobsAsync(string contentHash, CancellationToken ct = default) =>
        _dbContext.Documents
            .Include(d => d.IngestionJobs)
            .FirstOrDefaultAsync(d => d.ContentHash == contentHash, ct);

    public async Task<IReadOnlyList<Document>> ListAsync(CancellationToken ct = default)
    {
        // Ordered client-side: DateTimeOffset ordering is not translatable by the SQLite test provider.
        var documents = await _dbContext.Documents
            .AsNoTracking()
            .ToListAsync(ct);

        return documents
            .OrderByDescending(d => d.CreatedAt)
            .ToList();
    }

    public void Add(Document document) => _dbContext.Documents.Add(document);
}
