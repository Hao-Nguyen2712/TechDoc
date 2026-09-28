using TechDocAI.Application.UseCases.Documents.GetJobStatus;

namespace TechDocAI.Api.Endpoints;

public static class IngestionJobEndpoints
{
    public static IEndpointRouteBuilder MapIngestionJobEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/ingestion-jobs/{id:guid}", async (
            Guid id,
            GetJobStatusHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetJobStatusQuery(id), ct);

            if (result.IsFailure)
            {
                return Results.NotFound(new { error = result.Error.Description });
            }

            return Results.Ok(result.Value);
        });

        return app;
    }
}
