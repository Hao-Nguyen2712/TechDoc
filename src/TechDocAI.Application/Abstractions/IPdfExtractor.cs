using TechDocAI.Core.Common;
using TechDocAI.Core.Extraction;

namespace TechDocAI.Application.Abstractions;

public interface IPdfExtractor
{
    Task<Result<ExtractionResult>> ExtractAsync(Stream stream, CancellationToken ct = default);
}
