using TechDocAI.Core.Common;
using TechDocAI.Core.DTOs;

namespace TechDocAI.Core.Interfaces;

public interface IDocumentService
{
    Task<Result<DocumentUploadResult>> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        long fileLength,
        CancellationToken ct = default);

    Task<Result<IngestionJobStatusResult>> GetJobStatusAsync(
        Guid jobId,
        CancellationToken ct = default);
}
