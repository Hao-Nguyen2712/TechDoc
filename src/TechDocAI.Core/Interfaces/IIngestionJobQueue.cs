namespace TechDocAI.Core.Interfaces;

public interface IIngestionJobQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default);
    ValueTask<Guid> DequeueAsync(CancellationToken ct = default);
}
