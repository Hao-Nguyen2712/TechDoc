using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TechDocAI.Core.Interfaces;
using TechDocAI.Infrastructure.Persistence;
using TechDocAI.Infrastructure.Services;

namespace TechDocAI.Infrastructure.Workers;

public class IngestionJobBackgroundWorker : BackgroundService
{
    private readonly IIngestionJobQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IngestionJobBackgroundWorker> _logger;

    public IngestionJobBackgroundWorker(
        IIngestionJobQueue queue,
        IServiceProvider serviceProvider,
        ILogger<IngestionJobBackgroundWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ingestion Job Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await _queue.DequeueAsync(stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TechDocDbContext>();
                var processor = scope.ServiceProvider.GetRequiredService<IngestionPipelineProcessor>();

                await processor.ProcessJobAsync(dbContext, jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ingestion job.");
            }
        }
    }
}
