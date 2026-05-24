using Chronosix.Abstractions;
using Chronosix.Scheduling;

namespace Chronosix.Internal;

/// <summary>Builds <see cref="ISchedule"/> instances from raw registration data.</summary>
internal static class ScheduleFactory
{
    /// <summary>
    /// Creates a schedule, preferring a cron expression, then a fixed interval, and
    /// otherwise falling back to a manual-only schedule.
    /// </summary>
    public static ISchedule Create(string? cron, TimeSpan? interval, TimeZoneInfo timeZone)
    {
        if (!string.IsNullOrWhiteSpace(cron))
        {
            return new CronSchedule(cron!, timeZone);
        }

        if (interval is { } value && value > TimeSpan.Zero)
        {
            return new IntervalSchedule(value);
        }

        return ManualSchedule.Instance;
    }
}

/// <summary>Helpers for reading <see cref="ChronosixJobAttribute"/> metadata from a type.</summary>
internal static class JobMetadata
{
    public static ChronosixJobAttribute? GetAttribute(Type type) =>
        (ChronosixJobAttribute?)Attribute.GetCustomAttribute(type, typeof(ChronosixJobAttribute));
}
