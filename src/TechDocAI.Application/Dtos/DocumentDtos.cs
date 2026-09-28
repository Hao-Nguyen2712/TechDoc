using TechDocAI.Application.UseCases.Documents;

namespace TechDocAI.Application.Dtos;

public enum UploadOutcome
{
    Enqueued,
    AlreadyIngested
}

public record DocumentUploadResult(
    Guid DocumentId,
    Guid? JobId,
    UploadOutcome Outcome = UploadOutcome.Enqueued,
    DocumentDetailsResponse? ExistingDocument = null);
