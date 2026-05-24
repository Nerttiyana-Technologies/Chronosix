using Chronosix.Abstractions;
using Chronosix.Model;
using Chronosix.Scheduling;
using Microsoft.Extensions.Logging;

namespace Chronosix.Internal;

/// <summary>
/// Drives the perpetual scheduling loop for a single job: it waits for either the next
/// scheduled occurrence or a manual "Trigger Now", runs the job through
/// <see cref="ChronosixRunner"/>, then repeats until the host shuts down.
/// </summary>
internal sealed class JobLoopRunner
{
    private readonly ChronosixRunner _runner;
    private readonly JobRegistry _registry;
    private readonly TriggerCoordinator _triggers;
    private readonly ILogger<JobLoopRunner> _logger;

    public JobLoopRunner(
        ChronosixRunner runner,
        JobRegistry registry,
        TriggerCoordinator triggers,
        ILogger<JobLoopRunner> logger)
    {
        _runner = runner;
        _registry = registry;
        _triggers = triggers;
        _logger = logger;
    }

    /// <summary>Runs the scheduling loop for <paramref name="descriptor"/> until cancelled.</summary>
    public async Task RunAsync(
        JobDescriptor descriptor,
        Func<ChronosixJobContext, CancellationToken, Task> work,
        CancellationToken stoppingToken)
    {
        var reader = _triggers.GetReader(descriptor.Id);

        if (descriptor.RunOnStartup && !stoppingToken.IsCancellationRequested)
        {
            await SafeExecuteAsync(descriptor, work, TriggerReason.Startup, stoppingToken).ConfigureAwait(false);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var schedule = descriptor.Schedule ?? ManualSchedule.Instance;
            var next = descriptor.Enabled && schedule.IsAutomatic
                ? schedule.GetNextOccurrence(DateTimeOffset.UtcNow)
                : null;
            _registry.SetNextRun(descriptor.Id, next);

            using var iterationCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var triggerTask = reader.WaitToReadAsync(iterationCts.Token).AsTask();

            TimeSpan delay;
            if (next is { } occurrence)
            {
                delay = occurrence - DateTimeOffset.UtcNow;
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }
            }
            else
            {
                delay = Timeout.InfiniteTimeSpan;
            }

            var delayTask = Task.Delay(delay, iterationCts.Token);
            var completed = await Task.WhenAny(delayTask, triggerTask).ConfigureAwait(false);

            var runReason = TriggerReason.Schedule;
            var shouldRun = false;

            if (ReferenceEquals(completed, triggerTask))
            {
                if (triggerTask.Status == TaskStatus.RanToCompletion && triggerTask.Result)
                {
                    reader.TryRead(out _);
                    runReason = TriggerReason.Manual;
                    shouldRun = true;
                }
            }
            else if (next is not null && delayTask.Status == TaskStatus.RanToCompletion)
            {
                runReason = TriggerReason.Schedule;
                shouldRun = true;
            }

            // Cancel and drain whichever wait did not win, so nothing is left dangling.
            iterationCts.Cancel();
            await ObserveAsync(delayTask).ConfigureAwait(false);
            await ObserveAsync(triggerTask).ConfigureAwait(false);

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (shouldRun)
            {
                await SafeExecuteAsync(descriptor, work, runReason, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task SafeExecuteAsync(
        JobDescriptor descriptor,
        Func<ChronosixJobContext, CancellationToken, Task> work,
        TriggerReason reason,
        CancellationToken stoppingToken)
    {
        try
        {
            await _runner.ExecuteAsync(descriptor, work, reason, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // ChronosixRunner already captures job exceptions; this only guards the loop itself.
            _logger.LogError(ex, "Chronosix scheduling loop for job '{JobId}' failed unexpectedly.", descriptor.Id);
        }
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // The losing wait is expected to end in cancellation.
        }
    }
}
