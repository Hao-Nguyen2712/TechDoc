using TechDocAI.Infrastructure.Extractors;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace TechDocAI.UnitTests;

public class PdfPigExtractorTests
{
    private static byte[] CreateMultiPagePdfWithShortText(int pageCount, string shortTextPerPage)
    {
        var builder = new PdfDocumentBuilder();
        for (int i = 0; i < pageCount; i++)
        {
            var page = builder.AddPage(PageSize.A4);
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            page.AddText(shortTextPerPage, 12, new PdfPoint(50, 700), font);
        }
        return builder.Build();
    }

    [Fact]
    public async Task ExtractAsync_WhenAllPagesBelowThreshold_TotalNeedsOcrIsTrueEvenIfTotalExceedsThreshold()
    {
        // Arrange: 5 pages, each page with 15 characters ("Short line text").
        // Total text length = 75 characters.
        // Single-page threshold is 50.
        // Old buggy logic: 75 < 50 was false (so TotalNeedsOcr was false, despite all pages needing OCR).
        // New fixed logic: pages.Any(p => p.NeedsOcr) is true -> TotalNeedsOcr is true.
        var pdfBytes = CreateMultiPagePdfWithShortText(5, "Short line text");
        using var stream = new MemoryStream(pdfBytes);

        var extractor = new PdfPigExtractor();

        // Act
        var result = await extractor.ExtractAsync(stream);

        // Assert
        Assert.True(result.IsSuccess);
        var extraction = result.Value;
        Assert.Equal(5, extraction.Pages.Count);
        Assert.All(extraction.Pages, p => Assert.True(p.NeedsOcr));
        Assert.True(extraction.TotalNeedsOcr);
    }

    [Fact]
    public async Task ExtractAsync_WhenAllPagesHaveSufficientText_TotalNeedsOcrIsFalse()
    {
        // Arrange: 2 pages, each page has ~120 characters (> 50 threshold)
        var longText = "This is a detailed technical documentation paragraph with plenty of clear characters and words for testing.";
        var pdfBytes = CreateMultiPagePdfWithShortText(2, longText);
        using var stream = new MemoryStream(pdfBytes);

        var extractor = new PdfPigExtractor();

        // Act
        var result = await extractor.ExtractAsync(stream);

        // Assert
        Assert.True(result.IsSuccess);
        var extraction = result.Value;
        Assert.Equal(2, extraction.Pages.Count);
        Assert.All(extraction.Pages, p => Assert.False(p.NeedsOcr));
        Assert.False(extraction.TotalNeedsOcr);
    }

    [Fact]
    public async Task ExtractAsync_WhenCancellationRequested_ReturnsFailureResultInsteadOfThrowing()
    {
        var pdfBytes = CreateMultiPagePdfWithShortText(3, "Sample content for cancel test");
        using var stream = new MemoryStream(pdfBytes);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-canceled token

        var extractor = new PdfPigExtractor();

        // Act
        var result = await extractor.ExtractAsync(stream, cts.Token);

        // Assert: consistent with Result pattern, returns Failure instead of unhandled exception
        Assert.True(result.IsFailure);
        Assert.Equal("Pdf.ExtractionCanceled", result.Error.Code);
    }
}
