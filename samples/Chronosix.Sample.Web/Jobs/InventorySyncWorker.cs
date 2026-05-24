namespace Chronosix.Sample.Web.Jobs;

/// <summary>
/// A plain <see cref="BackgroundService"/> — note it does <b>not</b> derive from
/// <c>ChronosixBackgroundService</c>. It shows how an existing native worker (the kind
/// you might already have, perhaps driven by a Cronos expression) is surfaced on the
/// dashboard simply by injecting <see cref="ChronosixDiagnostics"/> and wrapping each
/// loop iteration in <c>TrackAsync</c> — the base class and structure are untouched.
/// </summary>
public sealed class InventorySyncWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(40);

    private readonly ChronosixDiagnostics _chronosix;

    public InventorySyncWorker(ChronosixDiagnostics chronosix)
    {
        _chronosix = chronosix;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // One line: the iteration is timed, shown on the timeline, and any
            // exception is captured by the failure tracer instead of crashing the loop.
            await _chronosix.TrackAsync("InventorySync", SyncAsync, stoppingToken);

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static async Task SyncAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Random.Shared.Next(500, 2_600), cancellationToken);

        if (Random.Shared.NextDouble() < 0.2)
        {
            throw new TimeoutException("Inventory service did not respond within the SLA window.");
        }
    }
}
