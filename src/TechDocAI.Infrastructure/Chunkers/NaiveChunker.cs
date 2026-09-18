using TechDocAI.Core.Interfaces;

namespace TechDocAI.Infrastructure.Chunkers;

public class NaiveChunker : IDocumentChunker
{
    private const int ChunkCharacterSize = 1000;

    public IReadOnlyList<ChunkDraft> Chunk(ExtractionResult extraction)
    {
        var chunks = new List<ChunkDraft>();

        foreach (var page in extraction.Pages)
        {
            if (string.IsNullOrWhiteSpace(page.Text))
            {
                continue;
            }

            for (int i = 0; i < page.Text.Length; i += ChunkCharacterSize)
            {
                var length = Math.Min(ChunkCharacterSize, page.Text.Length - i);
                var textChunk = page.Text.Substring(i, length);

                chunks.Add(new ChunkDraft(
                    PageIndex: page.PageNumber,
                    HeadingPath: string.Empty,
                    Text: textChunk,
                    NeedsOcr: page.NeedsOcr
                ));
            }
        }

        return chunks;
    }
}
