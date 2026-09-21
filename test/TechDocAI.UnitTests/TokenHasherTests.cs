using TechDocAI.Infrastructure.VectorStore;
using Xunit;

namespace TechDocAI.UnitTests;

public class TokenHasherTests
{
    [Fact]
    public void HashToken_SameInput_ReturnsSameHash()
    {
        var token = "technical";
        var hash1 = TokenHasher.HashToken(token);
        var hash2 = TokenHasher.HashToken(token);

        Assert.Equal(hash1, hash2);
        Assert.NotEqual(0u, hash1);
    }

    [Fact]
    public void HashToken_DifferentInputs_ReturnsDifferentHashes()
    {
        var hash1 = TokenHasher.HashToken("document");
        var hash2 = TokenHasher.HashToken("assistant");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HashToken_EmptyString_ReturnsOffsetBasis()
    {
        var hash = TokenHasher.HashToken("");
        Assert.Equal(2166136261u, hash);
    }
}
