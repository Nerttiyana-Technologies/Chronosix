using Chronosix.Model;
using Microsoft.AspNetCore.SignalR;

namespace Chronosix.Realtime;

/// <summary>
/// Default <see cref="IDashboardNotifier"/>: fans job activity out to every connected
/// dashboard over SignalR. Pushes are fire-and-forget — a slow or absent dashboard must
/// never slow down or break a background job.
/// </summary>
internal sealed class DashboardNotifier : IDashboardNotifier
{
    private readonly IHubContext<ChronosixHub> _hub;

    public DashboardNotifier(IHubContext<ChronosixHub> hub)
    {
        _hub = hub;
    }

    public void ExecutionStarted(JobExecution execution) => Push("executionStarted", execution);

    public void ExecutionFinished(JobExecution execution) => Push("executionFinished", execution);

    public void JobUpdated(JobDescriptor descriptor) => Push("jobUpdated", descriptor.Clone());

    public void FailureCaptured(CapturedFailure failure) => Push("failureCaptured", failure);

    private void Push(string method, object payload)
    {
        // Detached so a job thread is never blocked on dashboard delivery.
        _ = PushCoreAsync(method, payload);
    }

    private async Task PushCoreAsync(string method, object payload)
    {
        try
        {
            await _hub.Clients.All.SendAsync(method, payload).ConfigureAwait(false);
        }
        catch
        {
            // Dashboard delivery is best-effort by design.
        }
    }
}
