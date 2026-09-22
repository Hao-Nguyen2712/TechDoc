namespace TechDocAI.Core.Interfaces;

public record VectorChunkRecord(
    Guid ChunkId,
    Guid DocumentId,
    Guid WorkspaceId,
    int ChunkIndex,
    int? PageIndex,
    int? StartLine,
    int? EndLine,
    string HeadingPath,
    string Text,
    string EmbeddingModel,
    ReadOnlyMemory<float> DenseVector,
    uint[] SparseIndices,
    float[] SparseValues);

public interface IVectorStore
{
    Task EnsureCollectionAsync(CancellationToken ct = default);
    Task UpsertChunksAsync(IReadOnlyList<VectorChunkRecord> chunks, CancellationToken ct = default);
    Task DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default);
}
