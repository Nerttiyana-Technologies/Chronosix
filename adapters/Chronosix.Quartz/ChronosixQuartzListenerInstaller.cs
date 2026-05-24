using global::Quartz;
using global::Quartz.Impl.Matchers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chronosix.Quartz;

/// <summary>
/// Attaches the <see cref="ChronosixQuartzJobListener"/> to the running Quartz scheduler
/// once the host has started.
/// </summary>
internal sealed class ChronosixQuartzListenerInstaller : IHostedService
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ChronosixQuartzJobListener _listener;
    private readonly ILogger<ChronosixQuartzListenerInstaller> _logger;

    public ChronosixQuartzListenerInstaller(
        ISchedulerFactory schedulerFactory,
        ChronosixQuartzJobListener listener,
        ILogger<ChronosixQuartzListenerInstaller> logger)
    {
        _schedulerFactory = schedulerFactory;
        _listener = listener;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);
        scheduler.ListenerManager.AddJobListener(_listener, GroupMatcher<JobKey>.AnyGroup());
        _logger.LogInformation("Chronosix Quartz adapter attached to scheduler '{Scheduler}'.", scheduler.SchedulerName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
