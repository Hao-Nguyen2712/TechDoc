using TechDocAI.Application.Abstractions;
using TechDocAI.Application.Common;
using TechDocAI.Application.UseCases.Documents;
using TechDocAI.Core.Common;

namespace TechDocAI.Application.UseCases.Documents.GetJobStatus;

public class GetJobStatusHandler : ITransientDependency
{
    private readonly IIngestionJobRepository _jobRepository;

    public GetJobStatusHandler(IIngestionJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<Result<IngestionJobStatusResult>> HandleAsync(
        GetJobStatusQuery query,
        CancellationToken ct = default)
    {
        var job = await _jobRepository.FindByIdAsync(query.JobId, ct);

        if (job == null)
        {
            return Result.Failure<IngestionJobStatusResult>(
                Error.NotFound("Job.NotFound", "Ingestion job not found."));
        }

        var result = new IngestionJobStatusResult(
            job.Id,
            job.DocumentId,
            job.Status.ToString().ToLowerInvariant(),
            job.ErrorDetails,
            job.CreatedAt,
            job.UpdatedAt);

        return Result.Success(result);
    }
}
