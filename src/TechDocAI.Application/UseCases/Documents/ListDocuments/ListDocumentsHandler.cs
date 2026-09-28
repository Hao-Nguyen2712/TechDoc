using TechDocAI.Application.Abstractions;
using TechDocAI.Application.Common;
using TechDocAI.Application.UseCases.Documents;
using TechDocAI.Core.Common;

namespace TechDocAI.Application.UseCases.Documents.ListDocuments;

public class ListDocumentsHandler : ITransientDependency
{
    private readonly IDocumentRepository _documentRepository;

    public ListDocumentsHandler(IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    public async Task<Result<IReadOnlyList<DocumentResponse>>> HandleAsync(
        ListDocumentsQuery query,
        CancellationToken ct = default)
    {
        var documents = await _documentRepository.ListAsync(ct);

        var response = documents
            .Select(d => new DocumentResponse(
                d.Id,
                d.FileName,
                d.ContentType,
                d.FileSizeBytes,
                d.ContentHash,
                d.NeedsOcr,
                d.CreatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<DocumentResponse>>(response);
    }
}
