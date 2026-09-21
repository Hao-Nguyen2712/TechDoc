using TechDocAI.Core.Interfaces;

namespace TechDocAI.Infrastructure.Chunkers;

public record HeadingAnalysisResult(
    bool HasHeadings,
    double DominantFontSize,
    IReadOnlyDictionary<int, string> HeadingPathByIndex,
    HashSet<int> HeadingIndices)
{
    public bool IsHeading(int lineIndex) => HeadingIndices.Contains(lineIndex);

    public string GetHeadingPath(int lineIndex) =>
        HeadingPathByIndex.TryGetValue(lineIndex, out var path) ? path : string.Empty;
}

public static class HeadingDetector
{
    private const int MaxHeadingLength = 120;
    private const double HeadingSizeRatio = 1.15;

    public static HeadingAnalysisResult Analyze(IReadOnlyList<ExtractedLine> lines)
    {
        if (lines.Count == 0)
        {
            return new HeadingAnalysisResult(false, 0, new Dictionary<int, string>(), new HashSet<int>());
        }

        // 1. Calculate dominant font size (weighted by character count of non-empty lines)
        var fontCounts = new Dictionary<double, int>();
        foreach (var line in lines)
        {
            if (line.FontSize > 0 && !string.IsNullOrWhiteSpace(line.Text))
            {
                var roundedSize = Math.Round(line.FontSize, 1);
                fontCounts[roundedSize] = fontCounts.GetValueOrDefault(roundedSize) + line.Text.Length;
            }
        }

        if (fontCounts.Count == 0)
        {
            // All font sizes are 0 (e.g. TXT file)
            return new HeadingAnalysisResult(false, 0, new Dictionary<int, string>(), new HashSet<int>());
        }

        var dominantSize = fontCounts.OrderByDescending(kvp => kvp.Value).First().Key;

        // 2. Identify heading candidates
        // A line is a heading if:
        // - FontSize >= dominantSize * HeadingSizeRatio
        // - Text length <= MaxHeadingLength
        // - Not ending in continuation punctuation (e.g. comma)
        var headingIndices = new HashSet<int>();
        var uniqueHeadingSizes = new SortedSet<double>(Comparer<double>.Create((a, b) => b.CompareTo(a))); // descending

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Text.Trim();
            if (line.FontSize >= dominantSize * HeadingSizeRatio &&
                trimmed.Length <= MaxHeadingLength &&
                !trimmed.EndsWith(',') &&
                !trimmed.EndsWith(';'))
            {
                headingIndices.Add(i);
                uniqueHeadingSizes.Add(Math.Round(line.FontSize, 1));
            }
        }

        if (headingIndices.Count == 0)
        {
            return new HeadingAnalysisResult(false, dominantSize, new Dictionary<int, string>(), new HashSet<int>());
        }

        // Map font sizes to levels: largest = 1, second = 2, etc.
        var sizeToLevel = uniqueHeadingSizes.Select((size, index) => (size, level: index + 1))
            .ToDictionary(x => x.size, x => x.level);

        // 3. Build heading paths for each line index
        // Stack of (level, text)
        var headingStack = new List<(int level, string text)>();
        var pathByIndex = new Dictionary<int, string>();

        for (int i = 0; i < lines.Count; i++)
        {
            if (headingIndices.Contains(i))
            {
                var line = lines[i];
                var level = sizeToLevel[Math.Round(line.FontSize, 1)];

                // Pop headings at or below this level
                while (headingStack.Count > 0 && headingStack[^1].level >= level)
                {
                    headingStack.RemoveAt(headingStack.Count - 1);
                }

                headingStack.Add((level, line.Text.Trim()));
                var currentPath = string.Join(" > ", headingStack.Select(h => h.text));
                pathByIndex[i] = currentPath;
            }
            else
            {
                var currentPath = string.Join(" > ", headingStack.Select(h => h.text));
                pathByIndex[i] = currentPath;
            }
        }

        return new HeadingAnalysisResult(true, dominantSize, pathByIndex, headingIndices);
    }
}
