namespace Chronosix.Model;

/// <summary>The outcome of a single job execution.</summary>
public enum ExecutionStatus
{
    /// <summary>The execution is currently in progress.</summary>
    Running,

    /// <summary>The execution completed without throwing.</summary>
    Succeeded,

    /// <summary>The execution threw an unhandled exception.</summary>
    Failed,

    /// <summary>The execution was cancelled (host shutdown or token cancellation).</summary>
    Cancelled,

    /// <summary>
    /// A scheduled occurrence was skipped because a previous execution of the
    /// same job was still running and concurrency is disabled.
    /// </summary>
    Skipped
}

/// <summary>Describes how a job is wired into the host process.</summary>
public enum JobKind
{
    /// <summary>An <c>IChronosixJob</c> driven by the built-in scheduler.</summary>
    ScheduledJob,

    /// <summary>A <c>ChronosixBackgroundService</c> (an instrumented <c>BackgroundService</c>).</summary>
    BackgroundWorker,

    /// <summary>A Quartz.NET job surfaced through the Chronosix Quartz adapter.</summary>
    QuartzJob,

    /// <summary>A Hangfire job surfaced through the Chronosix Hangfire adapter.</summary>
    HangfireJob
}

/// <summary>Explains why a particular execution started.</summary>
public enum TriggerReason
{
    /// <summary>The job's schedule (cron or interval) elapsed.</summary>
    Schedule,

    /// <summary>A developer pressed "Trigger Now" on the dashboard or called the API.</summary>
    Manual,

    /// <summary>The execution ran once immediately because the job is configured to run on startup.</summary>
    Startup,

    /// <summary>The execution was reported by an external scheduler (Quartz / Hangfire).</summary>
    External
}
