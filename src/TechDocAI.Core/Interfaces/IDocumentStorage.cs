namespace TechDocAI.Core.Interfaces;

public interface IDocumentStorage
{
    Task SaveAsync(Guid documentId, Stream fileStream, string contentType, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(Guid documentId, CancellationToken ct = default);
}
