namespace Chronosix.Model;

/// <summary>
/// A complete, point-in-time view of everything Chronosix knows. The dashboard
/// requests one of these on load; thereafter it is kept fresh by SignalR pushes.
/// </summary>
public sealed class DashboardSnapshot
{
    /// <summary>Server time when the snapshot was taken (UTC).</summary>
    public DateTimeOffset ServerTimeUtc { get; init; }

    /// <summary>The host process name.</summary>
    public string ProcessName { get; init; } = "";

    /// <summary>The hosting environment name (Development, Staging, ...).</summary>
    public string Environment { get; init; } = "";

    /// <summary>The Chronosix package version.</summary>
    public string Version { get; init; } = "";

    /// <summary>All registered jobs.</summary>
    public IReadOnlyList<JobDescriptor> Jobs { get; init; } = Array.Empty<JobDescriptor>();

    /// <summary>Recent executions, newest first, bounded by the configured history size.</summary>
    public IReadOnlyList<JobExecution> Executions { get; init; } = Array.Empty<JobExecution>();

    /// <summary>Recent captured failures, newest first.</summary>
    public IReadOnlyList<CapturedFailure> Failures { get; init; } = Array.Empty<CapturedFailure>();

    /// <summary>Aggregate counters.</summary>
    public DashboardStats Stats { get; init; } = new();
}

/// <summary>Aggregate counters shown in the dashboard header.</summary>
public sealed class DashboardStats
{
    /// <summary>Number of registered jobs.</summary>
    public int JobCount { get; set; }

    /// <summary>Executions currently in flight across all jobs.</summary>
    public int ActiveRuns { get; set; }

    /// <summary>Total executions started since process start.</summary>
    public long TotalRuns { get; set; }

    /// <summary>Total successful executions.</summary>
    public long TotalSuccesses { get; set; }

    /// <summary>Total failed executions.</summary>
    public long TotalFailures { get; set; }

    /// <summary>Failures not yet acknowledged by a developer.</summary>
    public int UnacknowledgedFailures { get; set; }
}
