using System.Collections.Concurrent;
using Chronosix.Model;
using global::Quartz;

namespace Chronosix.Quartz;

/// <summary>
/// A Quartz.NET <see cref="IJobListener"/> that mirrors every job execution into
/// Chronosix: runs appear on the live timeline and unhandled job exceptions are routed
/// into the failure tracer.
/// </summary>
public sealed class ChronosixQuartzJobListener : IJobListener
{
    private readonly ChronosixDiagnostics _diagnostics;
    private readonly ConcurrentDictionary<string, IExternalExecution> _running = new();

    /// <summary>Creates the listener. The dependency is supplied by DI.</summary>
    public ChronosixQuartzJobListener(ChronosixDiagnostics diagnostics)
    {
        _diagnostics = diagnostics;
    }

    /// <inheritdoc />
    public string Name => "Chronosix";

    /// <inheritdoc />
    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var key = context.JobDetail.Key;
        var jobId = "quartz:" + key;

        _diagnostics.RegisterExternalJob(
            jobId,
            key.Name,
            JobKind.QuartzJob,
            scheduleSummary: DescribeTrigger(context.Trigger),
            description: context.JobDetail.Description);

        var execution = _diagnostics.BeginExecution(jobId, TriggerReason.External);
        execution.Log($"Quartz fired '{key}' (fire instance {context.FireInstanceId}).");
        _running[context.FireInstanceId] = execution;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (_running.TryRemove(context.FireInstanceId, out var execution))
        {
            execution.Log("Execution vetoed by a trigger listener.");
            execution.Succeeded();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken cancellationToken = default)
    {
        if (_running.TryRemove(context.FireInstanceId, out var execution))
        {
            if (jobException is not null)
            {
                execution.Failed(jobException.InnerException ?? jobException);
            }
            else
            {
                execution.Log($"Completed in {context.JobRunTime}.");
                execution.Succeeded();
            }
        }

        return Task.CompletedTask;
    }

    private static string DescribeTrigger(ITrigger trigger) => trigger switch
    {
        ICronTrigger cron => $"cron ‘{cron.CronExpressionString}’",
        ISimpleTrigger simple => $"every {simple.RepeatInterval}",
        _ => trigger.Key.Name
    };
}
