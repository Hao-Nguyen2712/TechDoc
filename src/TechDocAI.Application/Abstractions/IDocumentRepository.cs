using TechDocAI.Core.Entities;

namespace TechDocAI.Application.Abstractions;

public interface IDocumentRepository
{
    Task<Document?> FindByIdWithJobsAsync(Guid documentId, CancellationToken ct = default);
    Task<Document?> FindByContentHashWithJobsAsync(string contentHash, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> ListAsync(CancellationToken ct = default);
    void Add(Document document);
}
