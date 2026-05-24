using Chronosix.Abstractions;
using Chronosix.Model;
using Chronosix.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronosix.Internal;

/// <summary>
/// The single <see cref="BackgroundService"/> that orchestrates every scheduler-driven
/// <see cref="IChronosixJob"/>. It drains the registration store at startup, builds a
/// descriptor per job and launches an independent scheduling loop for each.
/// </summary>
internal sealed class ScheduledJobHost : BackgroundService
{
    private readonly ChronosixRegistrationStore _store;
    private readonly JobRegistry _registry;
    private readonly JobLoopRunner _loop;
    private readonly IDashboardNotifier _notifier;
    private readonly ChronosixOptions _options;
    private readonly ILogger<ScheduledJobHost> _logger;

    public ScheduledJobHost(
        ChronosixRegistrationStore store,
        JobRegistry registry,
        JobLoopRunner loop,
        IDashboardNotifier notifier,
        IOptions<ChronosixOptions> options,
        ILogger<ScheduledJobHost> logger)
    {
        _store = store;
        _registry = registry;
        _loop = loop;
        _notifier = notifier;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_store.ScheduledJobs.Count == 0)
        {
            return;
        }

        var loops = new List<Task>(_store.ScheduledJobs.Count);

        foreach (var entry in _store.ScheduledJobs)
        {
            var descriptor = _registry.Register(BuildDescriptor(entry));
            _notifier.JobUpdated(descriptor);

            _logger.LogInformation(
                "Chronosix scheduled job '{JobName}' registered ({Schedule}).",
                descriptor.Name,
                descriptor.ScheduleSummary);

            loops.Add(_loop.RunAsync(descriptor, BuildWork(descriptor), stoppingToken));
        }

        await Task.WhenAll(loops).ConfigureAwait(false);
    }

    private JobDescriptor BuildDescriptor(ScheduledJobEntry entry)
    {
        var type = entry.JobType;
        var registration = entry.Registration;
        var attribute = JobMetadata.GetAttribute(type);

        var cron = registration.Cron ?? attribute?.Cron;
        var interval = registration.Interval
            ?? (attribute is { IntervalSeconds: > 0 } ? TimeSpan.FromSeconds(attribute.IntervalSeconds) : null);
        var schedule = ScheduleFactory.Create(cron, interval, _options.SchedulerTimeZone);

        return new JobDescriptor
        {
            Id = registration.Id ?? attribute?.Id ?? type.Name,
            Name = registration.Name ?? attribute?.Name ?? type.Name,
            Description = registration.Description ?? attribute?.Description,
            Kind = JobKind.ScheduledJob,
            Schedule = schedule,
            ScheduleSummary = schedule.Describe(),
            CronExpression = cron,
            Enabled = registration.Enabled,
            RunOnStartup = registration.RunOnStartup || (attribute?.RunOnStartup ?? false),
            AllowConcurrentExecution =
                registration.AllowConcurrentExecution || (attribute?.AllowConcurrentExecution ?? false),
            SupportsManualTrigger = true,
            JobType = type
        };
    }

    private static Func<ChronosixJobContext, CancellationToken, Task> BuildWork(JobDescriptor descriptor)
    {
        var jobType = descriptor.JobType
            ?? throw new InvalidOperationException($"Scheduled job '{descriptor.Id}' has no CLR type.");

        return async (context, cancellationToken) =>
        {
            // Resolved from the per-execution scope, so scoped dependencies are honoured.
            var job = (IChronosixJob)context.Services.GetRequiredService(jobType);
            await job.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        };
    }
}
