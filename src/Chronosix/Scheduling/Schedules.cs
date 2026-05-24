namespace Chronosix.Scheduling;

/// <summary>
/// Describes when a job should run. Chronosix ships three implementations: a cron
/// schedule, a fixed-interval schedule, and a manual-only schedule.
/// </summary>
public interface ISchedule
{
    /// <summary>
    /// Returns the next run time strictly after <paramref name="afterUtc"/>, or null
    /// if the job never runs automatically.
    /// </summary>
    DateTimeOffset? GetNextOccurrence(DateTimeOffset afterUtc);

    /// <summary>A short, human-readable description for the dashboard.</summary>
    string Describe();

    /// <summary>True when the schedule fires on its own (cron / interval).</summary>
    bool IsAutomatic { get; }
}

/// <summary>A schedule driven by a cron expression evaluated in a specific time zone.</summary>
public sealed class CronSchedule : ISchedule
{
    private readonly CronExpression _expression;
    private readonly TimeZoneInfo _timeZone;

    /// <summary>Creates a cron schedule from raw expression text.</summary>
    public CronSchedule(string expression, TimeZoneInfo? timeZone = null)
        : this(CronExpression.Parse(expression), timeZone)
    {
    }

    /// <summary>Creates a cron schedule from an already-parsed expression.</summary>
    public CronSchedule(CronExpression expression, TimeZoneInfo? timeZone = null)
    {
        _expression = expression;
        _timeZone = timeZone ?? TimeZoneInfo.Utc;
    }

    /// <summary>The raw cron expression text.</summary>
    public string Expression => _expression.Expression;

    /// <inheritdoc />
    public bool IsAutomatic => true;

    /// <inheritdoc />
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset afterUtc) =>
        _expression.GetNextOccurrence(afterUtc, _timeZone);

    /// <inheritdoc />
    public string Describe()
    {
        var zone = _timeZone.Equals(TimeZoneInfo.Utc) ? "UTC" : _timeZone.Id;
        return $"cron ‘{_expression.Expression}’ ({zone})";
    }
}

/// <summary>A schedule that fires on a fixed interval.</summary>
public sealed class IntervalSchedule : ISchedule
{
    private readonly TimeSpan _interval;

    /// <summary>Creates a fixed-interval schedule. The interval must be positive.</summary>
    public IntervalSchedule(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive.");
        }

        _interval = interval;
    }

    /// <summary>The configured interval.</summary>
    public TimeSpan Interval => _interval;

    /// <inheritdoc />
    public bool IsAutomatic => true;

    /// <inheritdoc />
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset afterUtc) => afterUtc + _interval;

    /// <inheritdoc />
    public string Describe() => $"every {Humanize(_interval)}";

    internal static string Humanize(TimeSpan span)
    {
        if (span.TotalDays >= 1)
        {
            return Trim(span.TotalDays, "d");
        }

        if (span.TotalHours >= 1)
        {
            return Trim(span.TotalHours, "h");
        }

        if (span.TotalMinutes >= 1)
        {
            return Trim(span.TotalMinutes, "m");
        }

        return Trim(span.TotalSeconds, "s");

        static string Trim(double value, string unit)
        {
            var rounded = Math.Round(value, 2);
            return rounded == Math.Floor(rounded)
                ? $"{(long)rounded}{unit}"
                : $"{rounded}{unit}";
        }
    }
}

/// <summary>A schedule that never fires automatically; the job only runs on demand.</summary>
public sealed class ManualSchedule : ISchedule
{
    /// <summary>A shared, reusable instance.</summary>
    public static readonly ManualSchedule Instance = new();

    /// <inheritdoc />
    public bool IsAutomatic => false;

    /// <inheritdoc />
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset afterUtc) => null;

    /// <inheritdoc />
    public string Describe() => "manual only";
}
