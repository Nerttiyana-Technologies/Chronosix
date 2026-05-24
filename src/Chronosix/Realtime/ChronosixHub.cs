using Chronosix.Internal;
using Chronosix.Model;
using Microsoft.AspNetCore.SignalR;

namespace Chronosix.Realtime;

/// <summary>
/// The SignalR hub backing the live dashboard. Browsers connect to <c>/chronosix/hub</c>,
/// receive a full snapshot on connect, and are then kept current by server pushes.
/// </summary>
public sealed class ChronosixHub : Hub
{
    private readonly ChronosixDiagnostics _diagnostics;
    private readonly ChronosixAccessGuard _guard;

    /// <summary>Constructs the hub. Parameters are supplied by dependency injection.</summary>
    public ChronosixHub(ChronosixDiagnostics diagnostics, ChronosixAccessGuard guard)
    {
        _diagnostics = diagnostics;
        _guard = guard;
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        if (!_guard.IsAllowed(Context.GetHttpContext()))
        {
            Context.Abort();
            return;
        }

        await Clients.Caller.SendAsync("snapshot", _diagnostics.CreateSnapshot()).ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <summary>Returns a fresh, full snapshot on demand (used by the dashboard's refresh button).</summary>
    public DashboardSnapshot GetSnapshot() => _diagnostics.CreateSnapshot();

    /// <summary>Fires a job immediately, regardless of its schedule.</summary>
    public TriggerResult TriggerJob(string jobId)
    {
        if (!_guard.IsAllowed(Context.GetHttpContext()))
        {
            return new TriggerResult(false, "Not authorized.");
        }

        return _diagnostics.TriggerJob(jobId);
    }
}
