using TechDocAI.Core.Interfaces;
using UglyToad.PdfPig;

namespace TechDocAI.Infrastructure.Extractors;

public class PdfPigExtractor : IDocumentExtractor
{
    private const int MinimumPageTextThreshold = 50;

    public Task<ExtractionResult> ExtractAsync(Stream pdfStream, CancellationToken ct = default)
    {
        pdfStream.Position = 0;
        using var pdfDocument = PdfDocument.Open(pdfStream);

        var pages = new List<ExtractedPage>();
        var totalTextLength = 0;

        foreach (var page in pdfDocument.GetPages())
        {
            var text = page.Text ?? string.Empty;
            var trimmedText = text.Trim();
            var needsOcr = trimmedText.Length < MinimumPageTextThreshold;

            pages.Add(new ExtractedPage(
                PageNumber: page.Number,
                Text: trimmedText,
                NeedsOcr: needsOcr
            ));

            totalTextLength += trimmedText.Length;
        }

        var totalNeedsOcr = totalTextLength < MinimumPageTextThreshold || pages.Count == 0;
        return Task.FromResult(new ExtractionResult(pages, totalNeedsOcr));
    }
}
