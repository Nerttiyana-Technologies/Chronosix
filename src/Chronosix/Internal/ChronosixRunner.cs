using System.Collections.Concurrent;
using Chronosix.Abstractions;
using Chronosix.Model;
using Chronosix.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronosix.Internal;

/// <summary>
/// Runs a single job execution end to end: it opens a DI scope, times the work, captures
/// any unhandled exception, updates statistics and pushes the result to the dashboard.
/// This is where Chronosix "intercepts background task states in-process".
/// </summary>
internal sealed class ChronosixRunner
{
    private readonly ExecutionTracker _tracker;
    private readonly JobRegistry _registry;
    private readonly FailureTracer _failures;
    private readonly IDashboardNotifier _notifier;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChronosixOptions _options;
    private readonly ILogger<ChronosixRunner> _logger;
    private readonly ConcurrentDictionary<string, int> _active = new(StringComparer.OrdinalIgnoreCase);

    public ChronosixRunner(
        ExecutionTracker tracker,
        JobRegistry registry,
        FailureTracer failures,
        IDashboardNotifier notifier,
        IServiceScopeFactory scopeFactory,
        IOptions<ChronosixOptions> options,
        ILogger<ChronosixRunner> logger)
    {
        _tracker = tracker;
        _registry = registry;
        _failures = failures;
        _notifier = notifier;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>The number of executions of a job currently in flight.</summary>
    public int GetActiveCount(string jobId) => _active.TryGetValue(jobId, out var count) ? count : 0;

    /// <summary>
    /// Executes <paramref name="work"/> once, fully instrumented. Honours the descriptor's
    /// concurrency setting: a non-concurrent job whose previous run is still in flight
    /// records a <see cref="ExecutionStatus.Skipped"/> marker instead of running again.
    /// </summary>
    public async Task ExecuteAsync(
        JobDescriptor descriptor,
        Func<ChronosixJobContext, CancellationToken, Task> work,
        TriggerReason reason,
        CancellationToken stoppingToken)
    {
        var id = descriptor.Id;
        var inFlight = _active.AddOrUpdate(id, 1, static (_, current) => current + 1);

        if (!descriptor.AllowConcurrentExecution && inFlight > 1)
        {
            Decrement(id);
            RecordSkipped(descriptor, reason, "Skipped — a previous execution was still running.");
            return;
        }

        IServiceScope? scope = null;
        try
        {
            scope = _scopeFactory.CreateScope();

            var execution = new JobExecution(
                descriptor.Id,
                descriptor.Name,
                descriptor.Kind,
                reason,
                DateTimeOffset.UtcNow,
                _options.MaxLogLinesPerExecution);

            _tracker.Add(execution);
            _registry.OnExecutionStarted(id, execution);
            _notifier.ExecutionStarted(execution);
            execution.AppendLog($"Started (trigger: {reason}).");

            Exception? error = null;
            ExecutionStatus status;
            var context = new ChronosixJobContext(execution, scope.ServiceProvider, stoppingToken);

            try
            {
                await work(context, stoppingToken).ConfigureAwait(false);
                status = ExecutionStatus.Succeeded;
                execution.AppendLog("Completed successfully.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                status = ExecutionStatus.Cancelled;
                execution.AppendLog("Cancelled — the host is shutting down.");
            }
            catch (Exception ex)
            {
                error = ex;
                status = ExecutionStatus.Failed;
                execution.AppendLog($"FAILED — {ex.GetType().Name}: {ex.Message}");
                _logger.LogError(
                    ex,
                    "Chronosix job '{JobName}' ({JobId}) threw an unhandled exception.",
                    descriptor.Name,
                    descriptor.Id);
            }

            execution.Complete(status, DateTimeOffset.UtcNow, error);
            _registry.OnExecutionFinished(id, execution);

            if (status == ExecutionStatus.Failed && error is not null)
            {
                var failure = _failures.Capture(descriptor, execution, error);
                _notifier.FailureCaptured(failure);
            }

            _notifier.ExecutionFinished(execution);
            _notifier.JobUpdated(_registry.Get(id) ?? descriptor);
        }
        finally
        {
            Decrement(id);
            scope?.Dispose();
        }
    }

    /// <summary>Records a skipped occurrence so the timeline still shows the bottleneck.</summary>
    public void RecordSkipped(JobDescriptor descriptor, TriggerReason reason, string note)
    {
        var now = DateTimeOffset.UtcNow;
        var execution = new JobExecution(
            descriptor.Id,
            descriptor.Name,
            descriptor.Kind,
            reason,
            now,
            _options.MaxLogLinesPerExecution);

        execution.AppendLog(note);
        execution.Complete(ExecutionStatus.Skipped, now, null);

        _tracker.Add(execution);
        _notifier.ExecutionStarted(execution);
        _notifier.ExecutionFinished(execution);
    }

    private void Decrement(string id) =>
        _active.AddOrUpdate(id, 0, static (_, current) => current > 0 ? current - 1 : 0);
}
