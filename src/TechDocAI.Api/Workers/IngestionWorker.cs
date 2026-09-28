using TechDocAI.Application.Abstractions;
using TechDocAI.Application.UseCases.Ingestion.ProcessIngestionJob;

namespace TechDocAI.Api.Workers;

public class IngestionWorker : BackgroundService
{
    private readonly IIngestionJobQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IngestionWorker> _logger;

    public IngestionWorker(
        IIngestionJobQueue queue,
        IServiceProvider serviceProvider,
        ILogger<IngestionWorker> logger)
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
                var handler = scope.ServiceProvider.GetRequiredService<ProcessIngestionJobHandler>();

                await handler.HandleAsync(new ProcessIngestionJobCommand(jobId), stoppingToken);
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
