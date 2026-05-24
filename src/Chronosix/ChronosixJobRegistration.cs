namespace Chronosix;

/// <summary>
/// Mutable registration details for a scheduler-driven <c>IChronosixJob</c>, populated
/// through the <c>AddChronosixJob</c> configuration callback.
/// </summary>
public sealed class ChronosixJobRegistration
{
    /// <summary>Stable id for the job. Defaults to the attribute id or the CLR type name.</summary>
    public string? Id { get; set; }

    /// <summary>Friendly display name. Defaults to the attribute name or the CLR type name.</summary>
    public string? Name { get; set; }

    /// <summary>Optional longer description.</summary>
    public string? Description { get; set; }

    /// <summary>A cron expression (5 or 6 fields) driving the schedule.</summary>
    public string? Cron { get; set; }

    /// <summary>A fixed interval driving the schedule. Used only when <see cref="Cron"/> is unset.</summary>
    public TimeSpan? Interval { get; set; }

    /// <summary>When true, the job also runs once immediately at host startup.</summary>
    public bool RunOnStartup { get; set; }

    /// <summary>When true, a new execution may start while a previous one is still running.</summary>
    public bool AllowConcurrentExecution { get; set; }

    /// <summary>Whether the job starts out enabled. Default <c>true</c>.</summary>
    public bool Enabled { get; set; } = true;
}
