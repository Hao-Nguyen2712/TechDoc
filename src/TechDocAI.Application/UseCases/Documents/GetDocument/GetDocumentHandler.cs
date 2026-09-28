using TechDocAI.Application.Abstractions;
using TechDocAI.Application.Common;
using TechDocAI.Application.UseCases.Documents;
using TechDocAI.Core.Common;

namespace TechDocAI.Application.UseCases.Documents.GetDocument;

public class GetDocumentHandler : ITransientDependency
{
    private readonly IDocumentRepository _documentRepository;

    public GetDocumentHandler(IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    public async Task<Result<DocumentDetailsResponse>> HandleAsync(
        GetDocumentQuery query,
        CancellationToken ct = default)
    {
        var doc = await _documentRepository.FindByIdWithJobsAsync(query.DocumentId, ct);

        if (doc == null)
        {
            return Result.Failure<DocumentDetailsResponse>(
                Error.NotFound("Document.NotFound", "Document not found."));
        }

        var jobSummaries = doc.IngestionJobs
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new IngestionJobSummaryResponse(
                j.Id,
                j.Status.ToString().ToLowerInvariant(),
                j.ErrorDetails,
                j.CreatedAt,
                j.UpdatedAt))
            .ToList();

        var response = new DocumentDetailsResponse(
            doc.Id,
            doc.FileName,
            doc.ContentType,
            doc.FileSizeBytes,
            doc.ContentHash,
            doc.NeedsOcr,
            doc.CreatedAt,
            jobSummaries);

        return Result.Success(response);
    }
}
