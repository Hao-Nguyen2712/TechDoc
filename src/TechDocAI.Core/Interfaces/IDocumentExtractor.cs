namespace TechDocAI.Core.Interfaces;

public record ExtractedPage(int PageNumber,
                            string Text,
                            bool NeedsOcr);
public record ExtractionResult(IReadOnlyList<ExtractedPage> Pages,
                               bool TotalNeedsOcr);

public interface IDocumentExtractor
{
    Task<ExtractionResult> ExtractAsync(Stream pdfStream, CancellationToken ct = default);
}
