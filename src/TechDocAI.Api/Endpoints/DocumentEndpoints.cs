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

            return Results.Accepted($"/ingestion-jobs/{result.Value.JobId}", result.Value);
        })
        .DisableAntiforgery();

        return app;
    }
}
