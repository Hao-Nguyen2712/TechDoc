using System.Text;
using TechDocAI.Core.Common;
using TechDocAI.Core.Interfaces;

namespace TechDocAI.Infrastructure.Extractors;

public class PlainTextExtractor : ITransientDependency
{
    public async Task<Result<ExtractionResult>> ExtractAsync(Stream stream, CancellationToken ct = default)
    {
        try
        {
            stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var lines = new List<ExtractedLine>();
            string? line;
            int lineNumber = 1;

            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    lines.Add(new ExtractedLine(trimmed, FontSize: 0, LineNumber: lineNumber));
                }
                lineNumber++;
            }

            if (lines.Count == 0)
            {
                return Result.Failure<ExtractionResult>(
                    Error.Validation("Txt.Empty", "File contained no extractable text."));
            }

            return Result.Success(new ExtractionResult(
                Pages: Array.Empty<ExtractedPage>(),
                Lines: lines,
                TotalNeedsOcr: false));
        }
        catch (Exception ex)
        {
            return Result.Failure<ExtractionResult>(
                Error.Failure("Txt.ExtractionFailed", ex.Message));
        }
    }
}
