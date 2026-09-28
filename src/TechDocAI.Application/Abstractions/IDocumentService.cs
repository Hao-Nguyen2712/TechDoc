using TechDocAI.Application.Dtos;
using TechDocAI.Application.UseCases.Documents;
using TechDocAI.Core.Common;

namespace TechDocAI.Application.Abstractions;

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

    Task<Result<IReadOnlyList<DocumentResponse>>> GetDocumentsAsync(
        CancellationToken ct = default);

    Task<Result<DocumentDetailsResponse>> GetDocumentByIdAsync(
        Guid documentId,
        CancellationToken ct = default);
}
