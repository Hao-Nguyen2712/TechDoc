using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TechDocAI.Core.Common;

namespace TechDocAI.Infrastructure.Services;

public class GeminiEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>, ITransientDependency
{
    public const string DefaultModel = "gemini-embedding-001";
    public const int DefaultDimensions = 1536;

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiEmbeddingGenerator(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
        _model = configuration["Gemini:Model"] ?? DefaultModel;
        Metadata = new EmbeddingGeneratorMetadata("gemini");
    }

    public EmbeddingGeneratorMetadata Metadata { get; }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var texts = values.ToList();
        if (texts.Count == 0)
        {
            return new GeneratedEmbeddings<Embedding<float>>();
        }

        var dimensions = options?.Dimensions ?? DefaultDimensions;
        var model = options?.ModelId ?? _model;

        // If no API key configured, generate deterministic local vectors for dev
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            var fallback = new GeneratedEmbeddings<Embedding<float>>();
            foreach (var text in texts)
            {
                var vec = new float[dimensions];
                Array.Fill(vec, 0.01f);
                fallback.Add(new Embedding<float>(vec));
            }
            return fallback;
        }

        // Call Gemini batchEmbedContents endpoint
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:batchEmbedContents?key={_apiKey}";
        var requestPayload = new
        {
            requests = texts.Select(t => new
            {
                model = $"models/{model}",
                content = new { parts = new[] { new { text = t } } },
                outputDimensionality = dimensions
            }).ToArray()
        };

        using var response = await _httpClient.PostAsJsonAsync(url, requestPayload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<GeminiBatchEmbedResponse>(cancellationToken);
        var results = new GeneratedEmbeddings<Embedding<float>>();

        if (body?.Embeddings != null)
        {
            foreach (var emb in body.Embeddings)
            {
                results.Add(new Embedding<float>(emb.Values));
            }
        }

        return results;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }

    private record GeminiBatchEmbedResponse(
        [property: JsonPropertyName("embeddings")] List<GeminiContentEmbedding>? Embeddings);

    private record GeminiContentEmbedding(
        [property: JsonPropertyName("values")] float[] Values);
}
