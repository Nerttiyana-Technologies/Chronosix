using Chronosix.Model;

namespace Chronosix.Realtime;

/// <summary>
/// Pushes live job activity to connected dashboards. The default implementation fans
/// out over SignalR; the abstraction keeps the execution engine free of a hard SignalR
/// dependency and makes it trivially testable.
/// </summary>
internal interface IDashboardNotifier
{
    /// <summary>An execution has just started.</summary>
    void ExecutionStarted(JobExecution execution);

    /// <summary>An execution has reached a terminal state.</summary>
    void ExecutionFinished(JobExecution execution);

    /// <summary>A job's descriptor or statistics changed.</summary>
    void JobUpdated(JobDescriptor descriptor);

    /// <summary>A new failure was captured by the failure tracer.</summary>
    void FailureCaptured(CapturedFailure failure);
}
