using Qdrant.Client;
using Qdrant.Client.Grpc;
using TechDocAI.Core.Common;
using TechDocAI.Core.Interfaces;

namespace TechDocAI.Infrastructure.VectorStore;

public class QdrantVectorStore : IVectorStore, ITransientDependency
{
    public const string DefaultCollectionName = "techdoc-chunks";
    public const string DenseVectorName = "dense";
    public const string SparseVectorName = "sparse";
    public const ulong DenseDimensions = 1536;

    private readonly QdrantClient _client;
    private readonly string _collectionName;

    public QdrantVectorStore(QdrantClient client, string collectionName = DefaultCollectionName)
    {
        _client = client;
        _collectionName = collectionName;
    }

    public async Task EnsureCollectionAsync(CancellationToken ct = default)
    {
        var exists = await _client.CollectionExistsAsync(_collectionName, ct);
        if (!exists)
        {
            var vectorsConfig = new VectorParamsMap
            {
                Map =
                {
                    [DenseVectorName] = new VectorParams { Size = DenseDimensions, Distance = Distance.Cosine }
                }
            };

            var sparseConfig = new SparseVectorConfig
            {
                Map =
                {
                    [SparseVectorName] = new SparseVectorParams { Modifier = Modifier.Idf }
                }
            };

            await _client.CreateCollectionAsync(
                collectionName: _collectionName,
                vectorsConfig: vectorsConfig,
                sparseVectorsConfig: sparseConfig,
                cancellationToken: ct);

            await _client.CreatePayloadIndexAsync(_collectionName, "document_id", PayloadSchemaType.Keyword, cancellationToken: ct);
            await _client.CreatePayloadIndexAsync(_collectionName, "workspace_id", PayloadSchemaType.Keyword, cancellationToken: ct);
        }
    }

    public async Task UpsertChunksAsync(IReadOnlyList<VectorChunkRecord> chunks, CancellationToken ct = default)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        await EnsureCollectionAsync(ct);

        var points = new List<PointStruct>();

        foreach (var chunk in chunks)
        {
            var sparseVec = new SparseVector();
            sparseVec.Indices.AddRange(chunk.SparseIndices);
            sparseVec.Values.AddRange(chunk.SparseValues);

            var namedVectors = new NamedVectors();
            namedVectors.Vectors[DenseVectorName] = chunk.DenseVector.ToArray();
            namedVectors.Vectors[SparseVectorName] = new Vector { Sparse = sparseVec };

            var point = new PointStruct
            {
                Id = chunk.ChunkId,
                Vectors = new Vectors { Vectors_ = namedVectors },
                Payload =
                {
                    ["workspace_id"] = chunk.WorkspaceId.ToString(),
                    ["document_id"] = chunk.DocumentId.ToString(),
                    ["chunk_id"] = chunk.ChunkId.ToString(),
                    ["chunk_index"] = chunk.ChunkIndex,
                    ["heading_path"] = chunk.HeadingPath,
                    ["text"] = chunk.Text,
                    ["embedding_model"] = chunk.EmbeddingModel
                }
            };

            if (chunk.PageIndex.HasValue)
            {
                point.Payload["page_index"] = chunk.PageIndex.Value;
            }

            if (chunk.StartLine.HasValue)
            {
                point.Payload["start_line"] = chunk.StartLine.Value;
            }

            if (chunk.EndLine.HasValue)
            {
                point.Payload["end_line"] = chunk.EndLine.Value;
            }

            points.Add(point);
        }

        await _client.UpsertAsync(_collectionName, points, cancellationToken: ct);
    }

    public async Task DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default)
    {
        var filter = Conditions.MatchKeyword("document_id", documentId.ToString());
        await _client.DeleteAsync(_collectionName, filter, cancellationToken: ct);
    }
}
