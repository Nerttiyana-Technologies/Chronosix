using Chronosix.Model;
using global::Hangfire.Server;

namespace Chronosix.Hangfire;

/// <summary>
/// A Hangfire server filter that records every background job execution into Chronosix:
/// runs appear on the live timeline and unhandled exceptions feed the failure tracer.
/// </summary>
public sealed class ChronosixHangfireFilter : IServerFilter
{
    private const string ItemKey = "chronosix.execution";

    private readonly ChronosixDiagnostics _diagnostics;

    /// <summary>Creates the filter. The dependency is supplied by DI.</summary>
    public ChronosixHangfireFilter(ChronosixDiagnostics diagnostics)
    {
        _diagnostics = diagnostics;
    }

    /// <inheritdoc />
    public void OnPerforming(PerformingContext context)
    {
        var job = context.BackgroundJob.Job;
        var typeName = job?.Type?.Name ?? "Job";
        var methodName = job?.Method?.Name ?? "Invoke";

        var jobId = "hangfire:" + (job?.Type?.FullName ?? typeName) + "." + methodName;
        var displayName = typeName + "." + methodName;

        _diagnostics.RegisterExternalJob(jobId, displayName, JobKind.HangfireJob, scheduleSummary: "Hangfire queue");

        var execution = _diagnostics.BeginExecution(jobId, TriggerReason.External);
        execution.Log($"Hangfire job '{context.BackgroundJob.Id}' started.");
        context.Items[ItemKey] = execution;
    }

    /// <inheritdoc />
    public void OnPerformed(PerformedContext context)
    {
        if (!context.Items.TryGetValue(ItemKey, out var raw) || raw is not IExternalExecution execution)
        {
            return;
        }

        if (context.Exception is not null)
        {
            execution.Failed(context.Exception);
        }
        else if (context.Canceled)
        {
            execution.Log("Hangfire reported the job as canceled.");
            execution.Succeeded();
        }
        else
        {
            execution.Succeeded();
        }
    }
}
