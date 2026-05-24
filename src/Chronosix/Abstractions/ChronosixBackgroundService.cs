using Chronosix.Internal;
using Chronosix.Model;
using Chronosix.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronosix.Abstractions;

/// <summary>
/// A drop-in, instrumented replacement for <see cref="BackgroundService"/>. Derive from
/// this instead of <c>BackgroundService</c> and implement
/// <see cref="ExecuteJobAsync"/>: Chronosix then owns the scheduling loop, times every
/// iteration, captures unhandled exceptions and surfaces the worker on the dashboard —
/// including a working "Trigger Now" button.
/// </summary>
/// <remarks>
/// Register the worker with <c>services.AddChronosixWorker&lt;T&gt;()</c>. If Chronosix is
/// not registered the worker still runs (uninstrumented), so it is always safe to ship.
/// </remarks>
public abstract class ChronosixBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>Creates the worker. The constructor parameter is supplied by DI.</summary>
    protected ChronosixBackgroundService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>The root application service provider.</summary>
    protected IServiceProvider ApplicationServices => _serviceProvider;

    /// <summary>Stable id for the worker. Defaults to the attribute id or the type name.</summary>
    protected virtual string JobId => JobAttribute?.Id ?? GetType().Name;

    /// <summary>Display name for the worker. Defaults to the attribute name or the type name.</summary>
    protected virtual string JobName => JobAttribute?.Name ?? GetType().Name;

    /// <summary>Optional description shown on the dashboard.</summary>
    protected virtual string? JobDescription => JobAttribute?.Description;

    /// <summary>A cron expression driving the schedule, or null for interval / manual.</summary>
    protected virtual string? Cron => JobAttribute?.Cron;

    /// <summary>A fixed interval driving the schedule, used only when <see cref="Cron"/> is null.</summary>
    protected virtual TimeSpan? Interval =>
        JobAttribute is { IntervalSeconds: > 0 } attribute
            ? TimeSpan.FromSeconds(attribute.IntervalSeconds)
            : null;

    /// <summary>When true, the worker also runs once immediately at startup.</summary>
    protected virtual bool RunImmediatelyOnStartup => JobAttribute?.RunOnStartup ?? false;

    /// <summary>When true, a new iteration may start before the previous one finishes.</summary>
    protected virtual bool AllowConcurrentExecution => JobAttribute?.AllowConcurrentExecution ?? false;

    private ChronosixJobAttribute? JobAttribute => JobMetadata.GetAttribute(GetType());

    /// <summary>Performs one iteration of the worker's job.</summary>
    /// <param name="context">Per-execution context (scoped services, logging).</param>
    /// <param name="cancellationToken">Cancelled on host shutdown.</param>
    protected abstract Task ExecuteJobAsync(ChronosixJobContext context, CancellationToken cancellationToken);

    /// <inheritdoc />
    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var registry = _serviceProvider.GetService<JobRegistry>();
        var loop = _serviceProvider.GetService<JobLoopRunner>();

        if (registry is null || loop is null)
        {
            // Chronosix was not registered; keep the worker fully functional regardless.
            await RunUninstrumentedAsync(stoppingToken).ConfigureAwait(false);
            return;
        }

        var options = _serviceProvider.GetService<IOptions<ChronosixOptions>>()?.Value ?? new ChronosixOptions();
        var schedule = ScheduleFactory.Create(Cron, Interval, options.SchedulerTimeZone);

        var descriptor = registry.Register(new JobDescriptor
        {
            Id = JobId,
            Name = JobName,
            Description = JobDescription,
            Kind = JobKind.BackgroundWorker,
            Schedule = schedule,
            ScheduleSummary = schedule.Describe(),
            CronExpression = Cron,
            AllowConcurrentExecution = AllowConcurrentExecution,
            RunOnStartup = RunImmediatelyOnStartup,
            SupportsManualTrigger = true
        });

        await loop.RunAsync(descriptor, ExecuteJobAsync, stoppingToken).ConfigureAwait(false);
    }

    private async Task RunUninstrumentedAsync(CancellationToken stoppingToken)
    {
        var schedule = ScheduleFactory.Create(Cron, Interval, TimeZoneInfo.Utc);

        if (RunImmediatelyOnStartup && !stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken).ConfigureAwait(false);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!schedule.IsAutomatic)
            {
                return;
            }

            var next = schedule.GetNextOccurrence(DateTimeOffset.UtcNow);
            if (next is null)
            {
                return;
            }

            var delay = next.Value - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            await RunOnceAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var execution = new JobExecution(
            JobId, JobName, JobKind.BackgroundWorker, TriggerReason.Schedule, DateTimeOffset.UtcNow, 200);
        var context = new ChronosixJobContext(execution, scope.ServiceProvider, stoppingToken);

        try
        {
            await ExecuteJobAsync(context, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during shutdown.
        }
        catch (Exception ex)
        {
            _serviceProvider.GetService<ILoggerFactory>()
                ?.CreateLogger(GetType())
                .LogError(ex, "Background job '{JobName}' threw an unhandled exception.", JobName);
        }
    }
}
