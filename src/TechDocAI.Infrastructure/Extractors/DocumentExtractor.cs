using TechDocAI.Core.Common;
using TechDocAI.Core.Interfaces;

namespace TechDocAI.Infrastructure.Extractors;

public class DocumentExtractor : IDocumentExtractor, ITransientDependency
{
    private readonly PdfPigExtractor _pdfExtractor;
    private readonly PlainTextExtractor _txtExtractor;

    public DocumentExtractor(PdfPigExtractor pdfExtractor, PlainTextExtractor txtExtractor)
    {
        _pdfExtractor = pdfExtractor;
        _txtExtractor = txtExtractor;
    }

    public async Task<Result<ExtractionResult>> ExtractAsync(Stream stream, string contentType, CancellationToken ct = default)
    {
        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return await _pdfExtractor.ExtractAsync(stream, ct);
        }

        if (contentType.Equals("text/plain", StringComparison.OrdinalIgnoreCase))
        {
            return await _txtExtractor.ExtractAsync(stream, ct);
        }

        return Result.Failure<ExtractionResult>(
            Error.Validation("Extraction.UnsupportedType", $"Unsupported content type: {contentType}"));
    }
}
