namespace TechDocAI.Infrastructure.VectorStore;

public static class SparseVectorBuilder
{
    private static readonly char[] PunctuationChars =
    [
        '.', ',', '!', '?', ';', ':', '-', '—', '(', ')', '[', ']', '{', '}',
        '\'', '"', '`', '/', '\\', '<', '>', '@', '#', '$', '%', '^', '&', '*', '_', '~', '|'
    ];

    public static (uint[] indices, float[] values) Build(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (Array.Empty<uint>(), Array.Empty<float>());
        }

        var words = text.ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim(PunctuationChars))
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToList();

        if (words.Count == 0)
        {
            return (Array.Empty<uint>(), Array.Empty<float>());
        }

        var frequencies = new Dictionary<uint, float>();
        foreach (var word in words)
        {
            var id = TokenHasher.HashToken(word);
            frequencies[id] = frequencies.GetValueOrDefault(id) + 1.0f;
        }

        // Qdrant requires sparse vector indices to be sorted strictly in ascending order
        var sorted = frequencies.OrderBy(kvp => kvp.Key).ToList();
        var indices = sorted.Select(kvp => kvp.Key).ToArray();
        var values = sorted.Select(kvp => kvp.Value).ToArray();

        return (indices, values);
    }
}
