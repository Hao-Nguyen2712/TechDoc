using TechDocAI.Core.Interfaces;

namespace TechDocAI.Api.Endpoints;

public static class IngestionJobEndpoints
{
    public static IEndpointRouteBuilder MapIngestionJobEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/ingestion-jobs/{id:guid}", async (
            Guid id,
            IDocumentService documentService,
            CancellationToken ct) =>
        {
            var result = await documentService.GetJobStatusAsync(id, ct);

            if (result.IsFailure)
            {
                return Results.NotFound(new { error = result.Error.Description });
            }

            return Results.Ok(result.Value);
        });

        return app;
    }
}
