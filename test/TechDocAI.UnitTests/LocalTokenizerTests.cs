using TechDocAI.Infrastructure.Chunkers;
using Xunit;

namespace TechDocAI.UnitTests;

public class LocalTokenizerTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    public void EstimateTokens_EmptyOrWhitespace_ReturnsZero(string? input, int expected)
    {
        var tokens = LocalTokenizer.EstimateTokens(input);
        Assert.Equal(expected, tokens);
    }

    [Fact]
    public void EstimateTokens_ShortSentence_ReturnsExpectedTokens()
    {
        var text = "TechDoc is a retrieval-augmented QA assistant.";
        var tokens = LocalTokenizer.EstimateTokens(text);

        // 6 words * ~1.3 = ~8 tokens
        Assert.InRange(tokens, 6, 12);
    }

    [Fact]
    public void EstimateTokens_LargeText_ScalesProportionally()
    {
        var words = Enumerable.Range(0, 500).Select(i => $"word{i}");
        var text = string.Join(" ", words);

        var tokens = LocalTokenizer.EstimateTokens(text);

        // 500 words should be roughly 600 - 750 tokens
        Assert.InRange(tokens, 550, 800);
    }
}
