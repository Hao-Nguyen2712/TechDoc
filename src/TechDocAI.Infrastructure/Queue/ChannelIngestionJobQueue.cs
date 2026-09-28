using System.Threading.Channels;
using TechDocAI.Application.Abstractions;
using TechDocAI.Application.Common;

namespace TechDocAI.Infrastructure.Queue;

public class ChannelIngestionJobQueue : IIngestionJobQueue, ISingletonDependency
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true
    });

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default)
    {
        return _channel.Writer.WriteAsync(jobId, ct);
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAsync(ct);
    }
}
