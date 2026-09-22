using System.Text;
using TechDocAI.Core.Common;
using TechDocAI.Core.Interfaces;

namespace TechDocAI.Infrastructure.Chunkers;

public class StructureAwareChunker : IDocumentChunker, ITransientDependency
{
    private const int MaxTokensPerChunk = 900;

    public IReadOnlyList<ChunkDraft> Chunk(ExtractionResult extraction)
    {
        var lines = extraction.Lines;
        if (lines == null || lines.Count == 0)
        {
            // Fallback: build lines from pages if Lines wasn't populated
            lines = extraction.Pages
                .SelectMany(p => (p.Text ?? string.Empty).Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => new ExtractedLine(l, FontSize: 12.0, PageNumber: p.PageNumber)))
                .ToList();
        }

        if (lines.Count == 0)
        {
            return Array.Empty<ChunkDraft>();
        }

        var pageNeedsOcrMap = extraction.Pages?.ToDictionary(p => p.PageNumber, p => p.NeedsOcr)
            ?? new Dictionary<int, bool>();

        var headingAnalysis = HeadingDetector.Analyze(lines);

        return headingAnalysis.HasHeadings
            ? ChunkStructured(lines, headingAnalysis, pageNeedsOcrMap, extraction.TotalNeedsOcr)
            : ChunkUnstructured(lines, pageNeedsOcrMap, extraction.TotalNeedsOcr);
    }

    private static List<ChunkDraft> ChunkStructured(
        IReadOnlyList<ExtractedLine> lines,
        HeadingAnalysisResult headingAnalysis,
        IReadOnlyDictionary<int, bool> pageNeedsOcrMap,
        bool defaultNeedsOcr)
    {
        var chunks = new List<ChunkDraft>();

        var currentChunkText = new StringBuilder();
        var currentTokens = 0;
        string currentHeadingPath = string.Empty;
        int? currentPageIndex = null;

        bool ResolveNeedsOcr(int? pageIndex) =>
            (pageIndex.HasValue && pageNeedsOcrMap.TryGetValue(pageIndex.Value, out var needsOcr))
                ? needsOcr
                : defaultNeedsOcr;

        void FlushCurrentChunk()
        {
            if (currentChunkText.Length > 0)
            {
                chunks.Add(new ChunkDraft(
                    PageIndex: currentPageIndex,
                    StartLine: null,
                    EndLine: null,
                    HeadingPath: currentHeadingPath,
                    Text: currentChunkText.ToString().Trim(),
                    NeedsOcr: ResolveNeedsOcr(currentPageIndex)
                ));
                currentChunkText.Clear();
                currentTokens = 0;
                currentPageIndex = null;
            }
        }

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            if (headingAnalysis.IsHeading(i))
            {
                // New heading boundary: flush previous section
                FlushCurrentChunk();
                currentHeadingPath = headingAnalysis.GetHeadingPath(i);
                currentPageIndex = line.PageNumber;
                continue;
            }

            var lineText = line.Text.Trim();
            if (string.IsNullOrWhiteSpace(lineText))
            {
                continue;
            }

            var lineTokens = LocalTokenizer.EstimateTokens(lineText);

            // Handle very large single line
            if (lineTokens > MaxTokensPerChunk)
            {
                FlushCurrentChunk();
                var subChunks = SplitLargeText(lineText, MaxTokensPerChunk);
                foreach (var sub in subChunks)
                {
                    chunks.Add(new ChunkDraft(
                        PageIndex: line.PageNumber,
                        StartLine: null,
                        EndLine: null,
                        HeadingPath: currentHeadingPath,
                        Text: sub,
                        NeedsOcr: ResolveNeedsOcr(line.PageNumber)
                    ));
                }
                continue;
            }

            if (currentTokens + lineTokens > MaxTokensPerChunk && currentChunkText.Length > 0)
            {
                FlushCurrentChunk();
            }

            if (currentChunkText.Length > 0)
            {
                currentChunkText.AppendLine();
            }
            else
            {
                currentPageIndex = line.PageNumber;
            }

            currentChunkText.Append(lineText);
            currentTokens += lineTokens;
        }

        FlushCurrentChunk();
        return chunks;
    }

    private static List<ChunkDraft> ChunkUnstructured(
        IReadOnlyList<ExtractedLine> lines,
        IReadOnlyDictionary<int, bool> pageNeedsOcrMap,
        bool defaultNeedsOcr)
    {
        var chunks = new List<ChunkDraft>();

        var currentChunkText = new StringBuilder();
        var currentTokens = 0;
        int? currentPageIndex = null;
        int? currentStartLine = null;
        int? currentEndLine = null;

        bool ResolveNeedsOcr(int? pageIndex) =>
            (pageIndex.HasValue && pageNeedsOcrMap.TryGetValue(pageIndex.Value, out var needsOcr))
                ? needsOcr
                : defaultNeedsOcr;

        void FlushCurrentChunk()
        {
            if (currentChunkText.Length > 0)
            {
                chunks.Add(new ChunkDraft(
                    PageIndex: currentPageIndex,
                    StartLine: currentStartLine,
                    EndLine: currentEndLine,
                    HeadingPath: string.Empty,
                    Text: currentChunkText.ToString().Trim(),
                    NeedsOcr: ResolveNeedsOcr(currentPageIndex)
                ));
                currentChunkText.Clear();
                currentTokens = 0;
                currentPageIndex = null;
                currentStartLine = null;
                currentEndLine = null;
            }
        }

        foreach (var line in lines)
        {
            var lineText = line.Text.Trim();
            if (string.IsNullOrWhiteSpace(lineText))
            {
                continue;
            }

            var lineTokens = LocalTokenizer.EstimateTokens(lineText);

            if (lineTokens > MaxTokensPerChunk)
            {
                FlushCurrentChunk();
                var subChunks = SplitLargeText(lineText, MaxTokensPerChunk);
                foreach (var sub in subChunks)
                {
                    chunks.Add(new ChunkDraft(
                        PageIndex: line.PageNumber,
                        StartLine: line.LineNumber,
                        EndLine: line.LineNumber,
                        HeadingPath: string.Empty,
                        Text: sub,
                        NeedsOcr: ResolveNeedsOcr(line.PageNumber)
                    ));
                }
                continue;
            }

            if (currentTokens + lineTokens > MaxTokensPerChunk && currentChunkText.Length > 0)
            {
                FlushCurrentChunk();
            }

            if (currentChunkText.Length > 0)
            {
                currentChunkText.AppendLine();
            }
            else
            {
                currentPageIndex = line.PageNumber;
                currentStartLine = line.LineNumber;
            }

            currentEndLine = line.LineNumber;
            currentChunkText.Append(lineText);
            currentTokens += lineTokens;
        }

        FlushCurrentChunk();
        return chunks;
    }

    private static List<string> SplitLargeText(string text, int maxTokens)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var subChunks = new List<string>();
        var current = new StringBuilder();
        var tokens = 0;

        foreach (var word in words)
        {
            var wordTokens = LocalTokenizer.EstimateTokens(word);
            if (tokens + wordTokens > maxTokens && current.Length > 0)
            {
                subChunks.Add(current.ToString());
                current.Clear();
                tokens = 0;
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }
            current.Append(word);
            tokens += wordTokens;
        }

        if (current.Length > 0)
        {
            subChunks.Add(current.ToString());
        }

        return subChunks;
    }
}
