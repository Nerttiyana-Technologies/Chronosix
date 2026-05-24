namespace Chronosix.Abstractions;

/// <summary>
/// Optionally decorates an <see cref="IChronosixJob"/> or a
/// <see cref="ChronosixBackgroundService"/> to declare its identity and schedule
/// without writing registration code by hand.
/// </summary>
/// <remarks>
/// Explicit registration arguments and overridden members always win over values
/// supplied through this attribute.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ChronosixJobAttribute : Attribute
{
    /// <summary>A stable id for the job. Defaults to the CLR type name.</summary>
    public string? Id { get; set; }

    /// <summary>A friendly display name. Defaults to the CLR type name.</summary>
    public string? Name { get; set; }

    /// <summary>An optional longer description shown on the dashboard.</summary>
    public string? Description { get; set; }

    /// <summary>A cron expression (5 or 6 fields) driving the schedule.</summary>
    public string? Cron { get; set; }

    /// <summary>A fixed interval, in seconds, driving the schedule. Ignored when 0.</summary>
    public double IntervalSeconds { get; set; }

    /// <summary>When true, the job also runs once immediately at host startup.</summary>
    public bool RunOnStartup { get; set; }

    /// <summary>When true, a new execution may start while a previous one is still running.</summary>
    public bool AllowConcurrentExecution { get; set; }
}
