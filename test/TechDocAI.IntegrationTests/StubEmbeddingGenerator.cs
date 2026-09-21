using Microsoft.Extensions.AI;

namespace TechDocAI.IntegrationTests;

public class StubEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public const int DefaultDimensions = 1536;
    public bool ShouldFail { get; set; }

    public EmbeddingGeneratorMetadata Metadata { get; } = new("stub");

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
        {
            throw new InvalidOperationException("Simulated embedding generation failure.");
        }

        var results = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var value in values)
        {
            var vector = new float[DefaultDimensions];
            // Deterministic non-zero values based on text
            Array.Fill(vector, 0.05f);
            results.Add(new Embedding<float>(vector));
        }

        return Task.FromResult(results);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
