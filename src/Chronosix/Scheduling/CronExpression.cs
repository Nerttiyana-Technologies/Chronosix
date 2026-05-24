using System.Globalization;

namespace Chronosix.Scheduling;

/// <summary>
/// A small, dependency-free cron parser. Chronosix ships its own so the core library
/// has zero NuGet dependencies beyond the shared framework.
/// </summary>
/// <remarks>
/// Supported syntax:
/// <list type="bullet">
///   <item>Five fields: <c>minute hour day-of-month month day-of-week</c>.</item>
///   <item>Six fields: a leading <c>second</c> field is also accepted.</item>
///   <item><c>*</c> and <c>?</c> (any), ranges <c>a-b</c>, steps <c>*/n</c> and <c>a-b/n</c>, lists <c>a,b,c</c>.</item>
///   <item>Wrap-around ranges such as <c>22-2</c> for hours.</item>
///   <item>Three-letter month names (JAN..DEC) and day names (SUN..SAT), case-insensitive.</item>
///   <item>Day-of-week <c>0</c> and <c>7</c> both mean Sunday.</item>
/// </list>
/// Quartz-only tokens (<c>L</c>, <c>W</c>, <c>#</c>) are not supported.
/// </remarks>
public sealed class CronExpression
{
    private static readonly string[] MonthNames =
        { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };

    private static readonly string[] DayNames =
        { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" };

    private readonly bool[] _seconds;   // 0-59
    private readonly bool[] _minutes;   // 0-59
    private readonly bool[] _hours;     // 0-23
    private readonly bool[] _daysOfMonth; // 1-31
    private readonly bool[] _months;    // 1-12
    private readonly bool[] _daysOfWeek;  // 0-6 (Sunday = 0)
    private readonly bool _domUnrestricted;
    private readonly bool _dowUnrestricted;

    /// <summary>The original expression text.</summary>
    public string Expression { get; }

    /// <summary>True when the expression includes an explicit seconds field.</summary>
    public bool HasSecondsField { get; }

    private CronExpression(
        string expression,
        bool hasSeconds,
        bool[] seconds,
        bool[] minutes,
        bool[] hours,
        bool[] daysOfMonth,
        bool[] months,
        bool[] daysOfWeek,
        bool domUnrestricted,
        bool dowUnrestricted)
    {
        Expression = expression;
        HasSecondsField = hasSeconds;
        _seconds = seconds;
        _minutes = minutes;
        _hours = hours;
        _daysOfMonth = daysOfMonth;
        _months = months;
        _daysOfWeek = daysOfWeek;
        _domUnrestricted = domUnrestricted;
        _dowUnrestricted = dowUnrestricted;
    }

    /// <summary>Parses a cron expression, throwing <see cref="FormatException"/> on bad input.</summary>
    public static CronExpression Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new FormatException("Cron expression is empty.");
        }

        var fields = expression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length is not (5 or 6))
        {
            throw new FormatException(
                $"Cron expression '{expression}' must have 5 or 6 fields but had {fields.Length}.");
        }

        var hasSeconds = fields.Length == 6;
        var i = 0;
        var seconds = hasSeconds
            ? ParseField(fields[i++], 0, 59, null, "second")
            : OnlyZero(60);
        var minutes = ParseField(fields[i++], 0, 59, null, "minute");
        var hours = ParseField(fields[i++], 0, 23, null, "hour");

        var domText = fields[i++];
        var daysOfMonth = ParseField(domText, 1, 31, null, "day-of-month");

        var months = ParseField(fields[i++], 1, 12, MonthNames, "month");

        var dowText = fields[i];
        var daysOfWeek = ParseDayOfWeek(dowText);

        var domUnrestricted = IsWildcard(domText);
        var dowUnrestricted = IsWildcard(dowText);

        return new CronExpression(
            expression.Trim(), hasSeconds, seconds, minutes, hours,
            daysOfMonth, months, daysOfWeek, domUnrestricted, dowUnrestricted);
    }

    /// <summary>
    /// Returns the next occurrence strictly after <paramref name="afterUtc"/>, evaluated in
    /// <paramref name="timeZone"/>, or null if none falls within the next five years.
    /// </summary>
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset afterUtc, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(afterUtc, timeZone).DateTime;

        // Start strictly after the supplied instant, at the resolution of the expression.
        DateTime t = HasSecondsField
            ? new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second).AddSeconds(1)
            : new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0).AddMinutes(1);

        var limit = t.AddYears(5);

        while (t < limit)
        {
            if (!_months[t.Month])
            {
                t = new DateTime(t.Year, t.Month, 1).AddMonths(1);
                continue;
            }

            if (!DayMatches(t))
            {
                t = t.Date.AddDays(1);
                continue;
            }

            if (!_hours[t.Hour])
            {
                t = new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0).AddHours(1);
                continue;
            }

            if (!_minutes[t.Minute])
            {
                t = new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0).AddMinutes(1);
                continue;
            }

            if (HasSecondsField && !_seconds[t.Second])
            {
                t = t.AddSeconds(1);
                continue;
            }

            // All fields match. Convert the wall-clock time back to UTC.
            var unspecified = DateTime.SpecifyKind(t, DateTimeKind.Unspecified);
            try
            {
                var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
                return new DateTimeOffset(utc, TimeSpan.Zero);
            }
            catch (ArgumentException)
            {
                // The wall-clock time does not exist (spring-forward gap); skip past it.
                t = t.AddMinutes(1);
            }
        }

        return null;
    }

    private bool DayMatches(DateTime t)
    {
        var domOk = _daysOfMonth[t.Day];
        var dowOk = _daysOfWeek[(int)t.DayOfWeek];

        // Standard cron rule: when both day fields are restricted, either may match.
        if (!_domUnrestricted && !_dowUnrestricted)
        {
            return domOk || dowOk;
        }

        return domOk && dowOk;
    }

    private static bool IsWildcard(string field) => field is "*" or "?";

    private static bool[] OnlyZero(int size)
    {
        var set = new bool[size];
        set[0] = true;
        return set;
    }

    private static bool[] ParseField(string field, int min, int max, string[]? names, string fieldName)
    {
        var set = new bool[max + 1];
        foreach (var part in field.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            ParsePart(part.Trim(), set, min, max, names, fieldName);
        }

        if (Array.IndexOf(set, true) < 0)
        {
            throw new FormatException($"Cron {fieldName} field '{field}' did not match any value.");
        }

        return set;
    }

    private static void ParsePart(string part, bool[] set, int min, int max, string[]? names, string fieldName)
    {
        var step = 1;
        var slash = part.IndexOf('/');
        var rangeText = part;
        if (slash >= 0)
        {
            rangeText = part[..slash];
            var stepText = part[(slash + 1)..];
            if (!int.TryParse(stepText, NumberStyles.Integer, CultureInfo.InvariantCulture, out step) || step < 1)
            {
                throw new FormatException($"Cron {fieldName} field has an invalid step '{part}'.");
            }
        }

        int rangeStart;
        int rangeEnd;

        if (rangeText is "*" or "?")
        {
            rangeStart = min;
            rangeEnd = max;
        }
        else
        {
            var dash = rangeText.IndexOf('-');
            if (dash > 0)
            {
                rangeStart = ParseValue(rangeText[..dash], names, fieldName, min, max);
                rangeEnd = ParseValue(rangeText[(dash + 1)..], names, fieldName, min, max);
            }
            else
            {
                rangeStart = ParseValue(rangeText, names, fieldName, min, max);
                // "5/10" means "5, then every 10th"; a bare "5" means just 5.
                rangeEnd = slash >= 0 ? max : rangeStart;
            }
        }

        if (rangeStart <= rangeEnd)
        {
            for (var v = rangeStart; v <= rangeEnd; v += step)
            {
                set[v] = true;
            }
        }
        else
        {
            // Wrap-around range, e.g. hours 22-2.
            var v = rangeStart;
            while (true)
            {
                set[v] = true;
                if (v == rangeEnd)
                {
                    break;
                }

                v += step;
                if (v > max)
                {
                    v = min + (v - max - 1);
                }
            }
        }
    }

    private static int ParseValue(string token, string[]? names, string fieldName, int min, int max)
    {
        token = token.Trim();
        if (names is not null && !int.TryParse(token, out _))
        {
            var idx = Array.IndexOf(names, token.ToUpperInvariant());
            if (idx < 0)
            {
                throw new FormatException($"Cron {fieldName} field has an unknown name '{token}'.");
            }

            return idx + min; // months are 1-based, names array is 0-based
        }

        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"Cron {fieldName} field has an invalid value '{token}'.");
        }

        if (value < min || value > max)
        {
            throw new FormatException(
                $"Cron {fieldName} field value '{token}' is outside the range {min}-{max}.");
        }

        return value;
    }

    private static bool[] ParseDayOfWeek(string field)
    {
        // Day-of-week is 0-6 (Sunday = 0); 7 is also accepted as Sunday.
        var set = new bool[7];
        foreach (var part in field.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = part.Trim();
            var working = token;

            var step = 1;
            var slash = working.IndexOf('/');
            if (slash >= 0)
            {
                if (!int.TryParse(working[(slash + 1)..], out step) || step < 1)
                {
                    throw new FormatException($"Cron day-of-week field has an invalid step '{token}'.");
                }

                working = working[..slash];
            }

            int start;
            int end;
            if (working is "*" or "?")
            {
                start = 0;
                end = 6;
            }
            else
            {
                var dash = working.IndexOf('-');
                if (dash > 0)
                {
                    start = NormalizeDow(ParseDowValue(working[..dash]));
                    end = NormalizeDow(ParseDowValue(working[(dash + 1)..]));
                }
                else
                {
                    start = NormalizeDow(ParseDowValue(working));
                    end = slash >= 0 ? 6 : start;
                }
            }

            if (start <= end)
            {
                for (var v = start; v <= end; v += step)
                {
                    set[v] = true;
                }
            }
            else
            {
                var v = start;
                while (true)
                {
                    set[v] = true;
                    if (v == end)
                    {
                        break;
                    }

                    v += step;
                    if (v > 6)
                    {
                        v -= 7;
                    }
                }
            }
        }

        if (Array.IndexOf(set, true) < 0)
        {
            throw new FormatException($"Cron day-of-week field '{field}' did not match any value.");
        }

        return set;
    }

    private static int ParseDowValue(string token)
    {
        token = token.Trim();
        if (!int.TryParse(token, out var value))
        {
            var idx = Array.IndexOf(DayNames, token.ToUpperInvariant());
            if (idx < 0)
            {
                throw new FormatException($"Cron day-of-week field has an unknown name '{token}'.");
            }

            return idx;
        }

        if (value is < 0 or > 7)
        {
            throw new FormatException($"Cron day-of-week value '{token}' is outside the range 0-7.");
        }

        return value;
    }

    private static int NormalizeDow(int value) => value == 7 ? 0 : value;

    /// <inheritdoc />
    public override string ToString() => Expression;
}
