using Chronosix.Abstractions;

namespace Chronosix.Sample.Web.Jobs;

/// <summary>
/// A fast, always-succeeding job. It demonstrates a healthy, high-frequency entry on the
/// timeline.
/// </summary>
public sealed class HeartbeatJob : IChronosixJob
{
    private readonly ILogger<HeartbeatJob> _logger;

    public HeartbeatJob(ILogger<HeartbeatJob> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(ChronosixJobContext context, CancellationToken cancellationToken)
    {
        context.Log("Pinging dependencies…");
        await Task.Delay(Random.Shared.Next(60, 380), cancellationToken);

        _logger.LogDebug("Heartbeat completed for execution {ExecutionId}.", context.ExecutionId);
        context.Log("All dependencies responded.");
    }
}
