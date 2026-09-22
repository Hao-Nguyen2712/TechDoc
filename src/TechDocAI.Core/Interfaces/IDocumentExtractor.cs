using TechDocAI.Core.Common;

namespace TechDocAI.Core.Interfaces;

public record ExtractedLine(
    string Text,
    double FontSize = 0,
    int? PageNumber = null,
    int? LineNumber = null);

public record ExtractedPage(
    int PageNumber,
    string Text,
    IReadOnlyList<ExtractedLine> Lines,
    bool NeedsOcr);

public record ExtractionResult(
    IReadOnlyList<ExtractedPage> Pages,
    IReadOnlyList<ExtractedLine> Lines,
    bool TotalNeedsOcr);

public interface IDocumentExtractor
{
    Task<Result<ExtractionResult>> ExtractAsync(Stream stream, string contentType, CancellationToken ct = default);
}
