using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechDocAI.Infrastructure.Persistence;

namespace TechDocAI.Api.Controllers;

[ApiController]
[Route("ingestion-jobs")]
public class IngestionJobsController : ControllerBase
{
    private readonly TechDocDbContext _dbContext;

    public IngestionJobsController(TechDocDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetJobStatus(Guid id)
    {
        var job = await _dbContext.IngestionJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
        {
            return NotFound(new { error = "Ingestion job not found." });
        }

        return Ok(new
        {
            id = job.Id,
            documentId = job.DocumentId,
            status = job.Status.ToString().ToLowerInvariant(),
            error = job.ErrorDetails,
            createdAt = job.CreatedAt,
            updatedAt = job.UpdatedAt
        });
    }
}
