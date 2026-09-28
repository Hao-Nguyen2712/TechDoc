namespace TechDocAI.Application.UseCases.Documents.UploadDocument;

public record UploadDocumentCommand(
    Stream Stream,
    string FileName,
    string ContentType,
    long FileLength);
