namespace TechDocAI.Infrastructure.VectorStore;

public static class TokenHasher
{
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;

    public static uint HashToken(string token)
    {
        uint hash = FnvOffsetBasis;
        foreach (char c in token)
        {
            hash ^= c;
            hash *= FnvPrime;
        }
        return hash;
    }
}
