using Chronosix.Internal;
using Chronosix.Model;
using Chronosix.Realtime;
using Chronosix.Scheduling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Chronosix;

/// <summary>
/// The public façade over Chronosix's in-memory state. The dashboard, the REST API and the
/// SignalR hub all go through it, and application code (and scheduler adapters) may resolve
/// it from dependency injection to query job activity or report external executions.
/// </summary>
public sealed class ChronosixDiagnostics
{
    private readonly JobRegistry _registry;
    private readonly ExecutionTracker _tracker;
    private readonly FailureTracer _failures;
    private readonly TriggerCoordinator _triggers;
    private readonly IDashboardNotifier _notifier;
    private readonly IHostEnvironment _environment;
    private readonly ChronosixOptions _options;

    internal ChronosixDiagnostics(
        JobRegistry registry,
        ExecutionTracker tracker,
        FailureTracer failures,
        TriggerCoordinator triggers,
        IDashboardNotifier notifier,
        IHostEnvironment environment,
        IOptions<ChronosixOptions> options)
    {
        _registry = registry;
        _tracker = tracker;
        _failures = failures;
        _triggers = triggers;
        _notifier = notifier;
        _environment = environment;
        _options = options.Value;
    }

    /// <summary>Builds a complete, point-in-time view of every job, execution and failure.</summary>
    public DashboardSnapshot CreateSnapshot()
    {
        var jobs = _registry.GetSnapshots();
        var executions = _tracker.GetRecent();
        var failures = _failures.GetRecent();

        var stats = new DashboardStats
        {
            JobCount = jobs.Count,
            ActiveRuns = jobs.Sum(j => j.ActiveRuns),
            TotalRuns = jobs.Sum(j => j.TotalRuns),
            TotalSuccesses = jobs.Sum(j => j.SuccessCount),
            TotalFailures = jobs.Sum(j => j.FailureCount),
            UnacknowledgedFailures = failures.Count(f => !f.Acknowledged)
        };

        return new DashboardSnapshot
        {
            ServerTimeUtc = DateTimeOffset.UtcNow,
            ProcessName = _environment.ApplicationName,
            Environment = _environment.EnvironmentName,
            Version = typeof(ChronosixDiagnostics).Assembly.GetName().Version?.ToString(3) ?? "0.1.0",
            Jobs = jobs,
            Executions = executions,
            Failures = failures,
            Stats = stats
        };
    }

    /// <summary>Returns the recent executions recorded for a single job, newest first.</summary>
    public IReadOnlyList<JobExecution> GetJobExecutions(string jobId) => _tracker.GetForJob(jobId);

    /// <summary>Fires a job immediately, regardless of its schedule.</summary>
    public TriggerResult TriggerJob(string jobId)
    {
        var descriptor = _registry.Get(jobId);
        if (descriptor is null)
        {
            return new TriggerResult(false, $"No job with id '{jobId}' is registered.");
        }

        if (!descriptor.SupportsManualTrigger)
        {
            return new TriggerResult(false, $"Job '{descriptor.Name}' does not support manual triggering.");
        }

        var accepted = _triggers.RequestTrigger(jobId);
        return accepted
            ? new TriggerResult(true, $"'{descriptor.Name}' has been queued for an immediate run.")
            : new TriggerResult(true, $"'{descriptor.Name}' already has a manual run queued.");
    }

    /// <summary>Marks a captured failure as reviewed.</summary>
    public bool AcknowledgeFailure(Guid failureId) => _failures.Acknowledge(failureId);

    /// <summary>Clears every captured failure. Returns the number removed.</summary>
    public int ClearFailures() => _failures.Clear();

    /// <summary>Enables or disables automatic scheduling for a job.</summary>
    public bool SetJobEnabled(string jobId, bool enabled)
    {
        var changed = _registry.SetEnabled(jobId, enabled);
        if (changed)
        {
            var descriptor = _registry.Get(jobId);
            if (descriptor is not null)
            {
                // Nudge the scheduling loop so the change takes effect immediately.
                _triggers.RequestTrigger(jobId);
                _notifier.JobUpdated(descriptor);
            }
        }

        return changed;
    }

    /// <summary>
    /// Registers a job that Chronosix does not run itself (e.g. a Quartz.NET or Hangfire
    /// job) so it appears on the dashboard. Safe to call repeatedly for the same id.
    /// </summary>
    public JobDescriptor RegisterExternalJob(
        string id,
        string name,
        JobKind kind,
        string? scheduleSummary = null,
        string? description = null)
    {
        var descriptor = new JobDescriptor
        {
            Id = id,
            Name = name,
            Description = description,
            Kind = kind,
            Schedule = ManualSchedule.Instance,
            ScheduleSummary = scheduleSummary ?? "external scheduler",
            SupportsManualTrigger = false
        };

        var registered = _registry.Register(descriptor);
        _notifier.JobUpdated(registered);
        return registered;
    }

    /// <summary>
    /// Begins recording an execution of an externally-scheduled job. Call exactly one of
    /// <see cref="IExternalExecution.Succeeded"/> / <see cref="IExternalExecution.Failed"/>,
    /// or dispose the handle (which records success).
    /// </summary>
    public IExternalExecution BeginExecution(string jobId, TriggerReason trigger = TriggerReason.External)
    {
        var descriptor = _registry.Get(jobId)
            ?? RegisterExternalJob(jobId, jobId, JobKind.BackgroundWorker);

        var execution = new JobExecution(
            descriptor.Id,
            descriptor.Name,
            descriptor.Kind,
            trigger,
            DateTimeOffset.UtcNow,
            _options.MaxLogLinesPerExecution);

        _tracker.Add(execution);
        _registry.OnExecutionStarted(descriptor.Id, execution);
        _notifier.ExecutionStarted(execution);

        return new ExternalExecution(this, descriptor, execution);
    }

    /// <summary>
    /// Runs <paramref name="work"/> as a single tracked execution of the job
    /// <paramref name="jobId"/>. This is the lightest way to instrument an existing
    /// <c>IHostedService</c> / <c>BackgroundService</c> worker without changing its base
    /// class — inject <see cref="ChronosixDiagnostics"/> and call this once per loop
    /// iteration.
    /// </summary>
    /// <remarks>
    /// An unhandled exception is captured into the failure tracer and is <b>not</b>
    /// rethrown, so the calling loop keeps running. A cancellation triggered by
    /// <paramref name="cancellationToken"/> (host shutdown) is recorded and rethrown so
    /// the worker can unwind.
    /// </remarks>
    public async Task TrackAsync(
        string jobId,
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentNullException.ThrowIfNull(work);

        var execution = BeginExecution(jobId, TriggerReason.Schedule);
        try
        {
            await work(cancellationToken).ConfigureAwait(false);
            execution.Succeeded();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            execution.Cancelled();
            throw;
        }
        catch (Exception ex)
        {
            execution.Failed(ex);
        }
    }

    internal void CompleteExternal(
        JobExecution execution,
        JobDescriptor descriptor,
        ExecutionStatus status,
        Exception? error)
    {
        execution.Complete(status, DateTimeOffset.UtcNow, error);
        _registry.OnExecutionFinished(descriptor.Id, execution);

        if (status == ExecutionStatus.Failed && error is not null)
        {
            var failure = _failures.Capture(descriptor, execution, error);
            _notifier.FailureCaptured(failure);
        }

        _notifier.ExecutionFinished(execution);
        _notifier.JobUpdated(_registry.Get(descriptor.Id) ?? descriptor);
    }
}
