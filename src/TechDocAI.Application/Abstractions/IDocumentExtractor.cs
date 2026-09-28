using TechDocAI.Core.Common;
using TechDocAI.Core.Extraction;

namespace TechDocAI.Application.Abstractions;

public interface IDocumentExtractor
{
    Task<Result<ExtractionResult>> ExtractAsync(Stream stream, string contentType, CancellationToken ct = default);
}
