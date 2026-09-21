using System.Collections.Concurrent;
using TechDocAI.Core.Interfaces;

namespace TechDocAI.IntegrationTests;

public class InMemoryVectorStore : IVectorStore
{
    public ConcurrentDictionary<Guid, VectorChunkRecord> Points { get; } = new();
    public bool IsCollectionCreated { get; private set; }

    public Task EnsureCollectionAsync(CancellationToken ct = default)
    {
        IsCollectionCreated = true;
        return Task.CompletedTask;
    }

    public Task UpsertChunksAsync(IReadOnlyList<VectorChunkRecord> chunks, CancellationToken ct = default)
    {
        IsCollectionCreated = true;
        foreach (var chunk in chunks)
        {
            Points[chunk.ChunkId] = chunk;
        }

        return Task.CompletedTask;
    }

    public Task DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default)
    {
        var keysToRemove = Points
            .Where(kvp => kvp.Value.DocumentId == documentId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            Points.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
