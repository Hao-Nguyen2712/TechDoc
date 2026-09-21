using System.Text;
using TechDocAI.Core.Common;
using TechDocAI.Core.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace TechDocAI.Infrastructure.Extractors;

public class PdfPigExtractor : ITransientDependency
{
    private const int MinimumPageTextThreshold = 50;
    private const double LineTolerance = 3.0;

    public async Task<Result<ExtractionResult>> ExtractAsync(Stream pdfStream, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                pdfStream.Position = 0;
                using var pdfDocument = PdfDocument.Open(pdfStream);

                var pages = new List<ExtractedPage>();
                var allLines = new List<ExtractedLine>();
                var totalTextLength = 0;

                foreach (var page in pdfDocument.GetPages())
                {
                    ct.ThrowIfCancellationRequested();
                    var pageLines = ExtractLinesFromPage(page);

                    var pageTextBuilder = new StringBuilder();
                    foreach (var line in pageLines)
                    {
                        pageTextBuilder.AppendLine(line.Text);
                        allLines.Add(line);
                    }

                    var pageText = pageTextBuilder.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(pageText) && !string.IsNullOrWhiteSpace(page.Text))
                    {
                        pageText = page.Text.Trim();
                    }

                    var needsOcr = pageText.Length < MinimumPageTextThreshold;

                    pages.Add(new ExtractedPage(
                        PageNumber: page.Number,
                        Text: pageText,
                        Lines: pageLines,
                        NeedsOcr: needsOcr
                    ));

                    totalTextLength += pageText.Length;
                }

                var totalNeedsOcr = totalTextLength < MinimumPageTextThreshold || pages.Count == 0;
                return Result.Success(new ExtractionResult(pages, allLines, totalNeedsOcr));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Result.Failure<ExtractionResult>(
                    Error.Failure("Pdf.ExtractionFailed", ex.Message));
            }
        }, ct);
    }

    private static List<ExtractedLine> ExtractLinesFromPage(Page page)
    {
        var words = page.GetWords().ToList();
        if (words.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(page.Text))
            {
                return page.Text.Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => new ExtractedLine(l, FontSize: 12.0, PageNumber: page.Number))
                    .ToList();
            }
            return new List<ExtractedLine>();
        }

        var sortedWords = words
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        var lines = new List<ExtractedLine>();
        var currentLineWords = new List<Word>();
        double? currentLineBottom = null;

        foreach (var word in sortedWords)
        {
            if (currentLineBottom == null)
            {
                currentLineWords.Add(word);
                currentLineBottom = word.BoundingBox.Bottom;
            }
            else if (Math.Abs(word.BoundingBox.Bottom - currentLineBottom.Value) <= LineTolerance)
            {
                currentLineWords.Add(word);
            }
            else
            {
                AddLine(lines, currentLineWords, page.Number);
                currentLineWords.Clear();
                currentLineWords.Add(word);
                currentLineBottom = word.BoundingBox.Bottom;
            }
        }

        if (currentLineWords.Count > 0)
        {
            AddLine(lines, currentLineWords, page.Number);
        }

        return lines;
    }

    private static void AddLine(List<ExtractedLine> lines, List<Word> words, int pageNumber)
    {
        var orderedWords = words.OrderBy(w => w.BoundingBox.Left).ToList();
        var text = string.Join(" ", orderedWords.Select(w => w.Text)).Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        var letters = orderedWords.SelectMany(w => w.Letters).ToList();
        var fontSize = letters.Count > 0 ? letters.Average(l => l.PointSize) : 12.0;

        lines.Add(new ExtractedLine(text, fontSize, PageNumber: pageNumber));
    }
}
