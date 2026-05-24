using System.Text.Json.Serialization;
using Chronosix.Scheduling;

namespace Chronosix.Model;

/// <summary>
/// The dashboard-facing description of a registered job, together with the rolling
/// statistics Chronosix maintains for it. One descriptor exists per job for the life
/// of the process.
/// </summary>
public sealed class JobDescriptor
{
    /// <summary>Stable, unique id for the job (used in URLs and the API).</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable name shown on the dashboard.</summary>
    public required string Name { get; set; }

    /// <summary>Optional longer description.</summary>
    public string? Description { get; set; }

    /// <summary>How the job is wired into the host process.</summary>
    public JobKind Kind { get; init; }

    /// <summary>A short, human-readable summary of the schedule (e.g. "every 30s").</summary>
    public string ScheduleSummary { get; set; } = "Manual only";

    /// <summary>The raw cron expression, when the job is cron-scheduled.</summary>
    public string? CronExpression { get; set; }

    /// <summary>Whether the job is currently eligible to run on its schedule.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether overlapping executions of this job are allowed.</summary>
    public bool AllowConcurrentExecution { get; set; }

    /// <summary>Whether the "Trigger Now" button is offered for this job.</summary>
    public bool SupportsManualTrigger { get; set; } = true;

    /// <summary>Whether the job runs once immediately on host startup.</summary>
    public bool RunOnStartup { get; set; }

    /// <summary>Next scheduled run (UTC), or null if manual-only / disabled.</summary>
    public DateTimeOffset? NextRunUtc { get; set; }

    /// <summary>Start time of the most recent execution.</summary>
    public DateTimeOffset? LastStartedUtc { get; set; }

    /// <summary>Finish time of the most recent completed execution.</summary>
    public DateTimeOffset? LastFinishedUtc { get; set; }

    /// <summary>Status of the most recent completed execution.</summary>
    public ExecutionStatus? LastStatus { get; set; }

    /// <summary>Duration in milliseconds of the most recent completed execution.</summary>
    public double? LastDurationMs { get; set; }

    /// <summary>Rolling average duration in milliseconds.</summary>
    public double? AverageDurationMs { get; set; }

    /// <summary>Total executions started since process start.</summary>
    public long TotalRuns { get; set; }

    /// <summary>Total successful executions.</summary>
    public long SuccessCount { get; set; }

    /// <summary>Total failed executions.</summary>
    public long FailureCount { get; set; }

    /// <summary>Number of executions of this job currently in flight.</summary>
    public int ActiveRuns { get; set; }

    /// <summary>The CLR job type, for scheduler-driven <c>IChronosixJob</c> jobs.</summary>
    [JsonIgnore]
    internal Type? JobType { get; set; }

    /// <summary>The schedule that drives this job, when scheduler-driven.</summary>
    [JsonIgnore]
    internal ISchedule? Schedule { get; set; }

    /// <summary>Produces a detached copy safe to serialise outside the registry lock.</summary>
    internal JobDescriptor Clone() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        Kind = Kind,
        ScheduleSummary = ScheduleSummary,
        CronExpression = CronExpression,
        Enabled = Enabled,
        AllowConcurrentExecution = AllowConcurrentExecution,
        SupportsManualTrigger = SupportsManualTrigger,
        RunOnStartup = RunOnStartup,
        NextRunUtc = NextRunUtc,
        LastStartedUtc = LastStartedUtc,
        LastFinishedUtc = LastFinishedUtc,
        LastStatus = LastStatus,
        LastDurationMs = LastDurationMs,
        AverageDurationMs = AverageDurationMs,
        TotalRuns = TotalRuns,
        SuccessCount = SuccessCount,
        FailureCount = FailureCount,
        ActiveRuns = ActiveRuns,
        JobType = JobType,
        Schedule = Schedule
    };
}
