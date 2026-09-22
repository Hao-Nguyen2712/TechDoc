using TechDocAI.Core.DTOs;
using TechDocAI.Core.Interfaces;

namespace TechDocAI.Api.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/documents", async (
            IFormFile? file,
            IDocumentService documentService,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "File is required." });
            }

            await using var stream = file.OpenReadStream();
            var defaultContentType = Path.GetExtension(file.FileName).Equals(".txt", StringComparison.OrdinalIgnoreCase)
                ? "text/plain"
                : "application/pdf";

            var result = await documentService.UploadAsync(
                stream,
                file.FileName,
                string.IsNullOrWhiteSpace(file.ContentType) ? defaultContentType : file.ContentType,
                file.Length,
                ct);

            if (result.IsFailure)
            {
                return Results.BadRequest(new { error = result.Error.Description });
            }

            if (result.Value.Outcome == UploadOutcome.AlreadyIngested)
            {
                return Results.Ok(result.Value.ExistingDocument);
            }

            return Results.Accepted($"/ingestion-jobs/{result.Value.JobId}", new
            {
                documentId = result.Value.DocumentId,
                jobId = result.Value.JobId
            });
        })
        .DisableAntiforgery();



        app.MapGet("/documents", async (
            IDocumentService documentService,
            CancellationToken ct) =>
        {
            var result = await documentService.GetDocumentsAsync(ct);
            return Results.Ok(result.Value);
        });

        app.MapGet("/documents/{id:guid}", async (
            Guid id,
            IDocumentService documentService,
            CancellationToken ct) =>
        {
            var result = await documentService.GetDocumentByIdAsync(id, ct);
            if (result.IsFailure)
            {
                return Results.NotFound(new { error = result.Error.Description });
            }

            return Results.Ok(result.Value);
        });

        return app;
    }
}
