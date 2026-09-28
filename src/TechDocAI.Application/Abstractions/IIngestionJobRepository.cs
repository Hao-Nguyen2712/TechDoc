using TechDocAI.Core.Entities;

namespace TechDocAI.Application.Abstractions;

public interface IIngestionJobRepository
{
    Task<IngestionJob?> FindByIdAsync(Guid jobId, CancellationToken ct = default);
    Task<IngestionJob?> FindByIdWithDocumentAsync(Guid jobId, CancellationToken ct = default);
    void Add(IngestionJob job);
}
