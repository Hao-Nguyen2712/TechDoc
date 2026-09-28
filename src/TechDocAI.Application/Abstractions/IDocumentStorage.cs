namespace TechDocAI.Application.Abstractions;

public interface IDocumentStorage
{
    Task SaveAsync(string storageKey, Stream fileStream, string contentType, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);
}
