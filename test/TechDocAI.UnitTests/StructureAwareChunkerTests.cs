using TechDocAI.Core.Interfaces;
using TechDocAI.Infrastructure.Chunkers;
using Xunit;

namespace TechDocAI.UnitTests;

public class StructureAwareChunkerTests
{
    [Fact]
    public void Chunk_WithHeadings_ProducesSectionChunksWithHeadingPath()
    {
        var lines = new List<ExtractedLine>
        {
            new("1. Introduction", 18.0, PageNumber: 1),
            new("TechDoc is an AI assistant for technical documentation.", 12.0, PageNumber: 1),
            new("It helps engineers search documentation quickly.", 12.0, PageNumber: 1),
            new("2. Architecture", 18.0, PageNumber: 2),
            new("The architecture consists of API, Core, and Infrastructure layers.", 12.0, PageNumber: 2)
        };

        var extraction = new ExtractionResult(
            Pages: new List<ExtractedPage>
            {
                new(1, "1. Introduction\nTechDoc is an AI assistant...", lines.Take(3).ToList(), false),
                new(2, "2. Architecture\nThe architecture...", lines.Skip(3).ToList(), false)
            },
            Lines: lines,
            TotalNeedsOcr: false);

        var chunker = new StructureAwareChunker();
        var chunks = chunker.Chunk(extraction);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("1. Introduction", chunks[0].HeadingPath);
        Assert.Contains("TechDoc is an AI assistant", chunks[0].Text);
        Assert.Equal(1, chunks[0].PageIndex);

        Assert.Equal("2. Architecture", chunks[1].HeadingPath);
        Assert.Contains("The architecture consists of API", chunks[1].Text);
        Assert.Equal(2, chunks[1].PageIndex);
    }

    [Fact]
    public void Chunk_LargeSection_SplitsToTokenBudget_WithZeroOverlapAndSameHeadingPath()
    {
        // Generate a large section (> 1200 words / ~1500 tokens)
        var lines = new List<ExtractedLine>
        {
            new("Big Section", 18.0, PageNumber: 1)
        };

        for (int i = 0; i < 40; i++)
        {
            var paragraph = string.Join(" ", Enumerable.Range(0, 30).Select(w => $"word{i}_{w}"));
            lines.Add(new ExtractedLine(paragraph, 12.0, PageNumber: 1));
        }

        var extraction = new ExtractionResult(
            Pages: new List<ExtractedPage>
            {
                new(1, "Big Section...", lines, false)
            },
            Lines: lines,
            TotalNeedsOcr: false);

        var chunker = new StructureAwareChunker();
        var chunks = chunker.Chunk(extraction);

        Assert.True(chunks.Count >= 2, $"Expected at least 2 chunks, got {chunks.Count}");
        foreach (var chunk in chunks)
        {
            Assert.Equal("Big Section", chunk.HeadingPath);
            var tokens = LocalTokenizer.EstimateTokens(chunk.Text);
            Assert.True(tokens <= 1050, $"Chunk exceeded budget: {tokens} tokens");
        }

        // Verify zero overlap: words in chunk 0 should not appear in chunk 1
        var wordsChunk0 = chunks[0].Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordsChunk1 = chunks[1].Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var overlap = wordsChunk0.Intersect(wordsChunk1).ToList();
        Assert.Empty(overlap);
    }

    [Fact]
    public void Chunk_UnstructuredDocument_FallsBackToBlockCuttingWithEmptyHeadingPath()
    {
        var lines = new List<ExtractedLine>
        {
            new("First body line with uniform font.", 12.0, PageNumber: 1),
            new("Second body line with uniform font.", 12.0, PageNumber: 1),
            new("Third body line with uniform font.", 12.0, PageNumber: 1)
        };

        var extraction = new ExtractionResult(
            Pages: new List<ExtractedPage>
            {
                new(1, "First body line...", lines, false)
            },
            Lines: lines,
            TotalNeedsOcr: false);

        var chunker = new StructureAwareChunker();
        var chunks = chunker.Chunk(extraction);

        Assert.Single(chunks);
        Assert.Equal(string.Empty, chunks[0].HeadingPath);
        Assert.Equal(1, chunks[0].PageIndex);
        Assert.Null(chunks[0].StartLine);
        Assert.Null(chunks[0].EndLine);
    }

    [Fact]
    public void Chunk_TxtDocument_ProducesChunksWithLineNumbers_AndNullPageIndex()
    {
        var lines = new List<ExtractedLine>();
        for (int i = 1; i <= 100; i++)
        {
            lines.Add(new ExtractedLine($"Log line {i}: process executed successfully with status 200.", FontSize: 0, LineNumber: i));
        }

        var extraction = new ExtractionResult(
            Pages: Array.Empty<ExtractedPage>(),
            Lines: lines,
            TotalNeedsOcr: false);

        var chunker = new StructureAwareChunker();
        var chunks = chunker.Chunk(extraction);

        Assert.NotEmpty(chunks);
        foreach (var chunk in chunks)
        {
            Assert.Equal(string.Empty, chunk.HeadingPath);
            Assert.Null(chunk.PageIndex);
            Assert.NotNull(chunk.StartLine);
            Assert.NotNull(chunk.EndLine);
            Assert.True(chunk.StartLine <= chunk.EndLine);
        }

        // Verify sequential line coverage with zero overlap
        for (int i = 0; i < chunks.Count - 1; i++)
        {
            Assert.Equal(chunks[i].EndLine + 1, chunks[i + 1].StartLine);
        }
    }
}
