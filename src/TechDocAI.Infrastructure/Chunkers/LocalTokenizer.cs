namespace TechDocAI.Infrastructure.Chunkers;

public static class LocalTokenizer
{
    public static int EstimateTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return Math.Max(1, (int)Math.Ceiling(words.Length * 1.3));
    }
}
