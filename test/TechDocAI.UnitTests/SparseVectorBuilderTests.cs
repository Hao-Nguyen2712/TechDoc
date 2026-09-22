using TechDocAI.Infrastructure.VectorStore;
using Xunit;

namespace TechDocAI.UnitTests;

public class SparseVectorBuilderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_EmptyOrWhitespace_ReturnsEmptyArrays(string? input)
    {
        var (indices, values) = SparseVectorBuilder.Build(input);

        Assert.Empty(indices);
        Assert.Empty(values);
    }

    [Fact]
    public void Build_NormalizesCaseAndStripsPunctuation_CountsFrequency()
    {
        var text = "Hello, world! HELLO?";
        var (indices, values) = SparseVectorBuilder.Build(text);

        Assert.Equal(2, indices.Length);
        Assert.Equal(2, values.Length);

        var helloId = TokenHasher.HashToken("hello");
        var worldId = TokenHasher.HashToken("world");

        var helloIndex = Array.IndexOf(indices, helloId);
        var worldIndex = Array.IndexOf(indices, worldId);

        Assert.True(helloIndex >= 0);
        Assert.True(worldIndex >= 0);
        Assert.Equal(2.0f, values[helloIndex]);
        Assert.Equal(1.0f, values[worldIndex]);
    }

    [Fact]
    public void Build_IndicesAreStrictlyAscending()
    {
        var text = "TechDoc retrieval-augmented question answering assistant with hybrid search.";
        var (indices, values) = SparseVectorBuilder.Build(text);

        Assert.True(indices.Length > 1);
        Assert.Equal(indices.Length, values.Length);

        for (int i = 0; i < indices.Length - 1; i++)
        {
            Assert.True(indices[i] < indices[i + 1],
                $"Indices must be strictly increasing for Qdrant: {indices[i]} is not < {indices[i + 1]}");
        }
    }
}
