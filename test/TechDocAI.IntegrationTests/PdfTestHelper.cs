using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace TechDocAI.IntegrationTests;

public static class PdfTestHelper
{
    public static byte[] CreatePdfWithText(string text)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        page.AddText(text, 12, new PdfPoint(50, 700), font);
        return builder.Build();
    }

    public static byte[] CreateEmptyPdf()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText(" ", 12, new PdfPoint(50, 700), font);
        return builder.Build();
    }
}
