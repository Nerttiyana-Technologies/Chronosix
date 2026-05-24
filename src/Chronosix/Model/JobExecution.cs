namespace Chronosix.Model;

/// <summary>
/// A single run of a job. One <see cref="JobExecution"/> is created every time a job
/// starts (whether by schedule or by a manual "Trigger Now"), and it is the unit the
/// timeline visualiser draws as a bar.
/// </summary>
public sealed class JobExecution
{
    private readonly object _sync = new();
    private readonly List<string> _log = new();
    private readonly int _maxLogLines;

    internal JobExecution(
        string jobId,
        string jobName,
        JobKind kind,
        TriggerReason trigger,
        DateTimeOffset startedAtUtc,
        int maxLogLines)
    {
        Id = Guid.NewGuid();
        JobId = jobId;
        JobName = jobName;
        Kind = kind;
        Trigger = trigger;
        StartedAtUtc = startedAtUtc;
        Status = ExecutionStatus.Running;
        _maxLogLines = maxLogLines < 1 ? 1 : maxLogLines;
    }

    /// <summary>Stable unique id for this execution.</summary>
    public Guid Id { get; }

    /// <summary>Id of the job that produced this execution.</summary>
    public string JobId { get; }

    /// <summary>Human-readable job name, copied at execution time.</summary>
    public string JobName { get; }

    /// <summary>How the owning job is wired into the host.</summary>
    public JobKind Kind { get; }

    /// <summary>Why this execution started.</summary>
    public TriggerReason Trigger { get; }

    /// <summary>Monotonic sequence number assigned by the execution tracker.</summary>
    public long Sequence { get; internal set; }

    /// <summary>When the execution started (UTC).</summary>
    public DateTimeOffset StartedAtUtc { get; }

    /// <summary>When the execution finished (UTC), or null while still running.</summary>
    public DateTimeOffset? FinishedAtUtc { get; private set; }

    /// <summary>Current status of the execution.</summary>
    public ExecutionStatus Status { get; private set; }

    /// <summary>CLR type name of the unhandled exception, when <see cref="Status"/> is Failed.</summary>
    public string? ErrorType { get; private set; }

    /// <summary>Message of the unhandled exception, when failed.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Full exception detail (ToString) captured for the failure tracer.</summary>
    public string? ErrorDetail { get; private set; }

    /// <summary>Wall-clock duration in milliseconds, or null while still running.</summary>
    public double? DurationMs =>
        FinishedAtUtc is { } finished ? (finished - StartedAtUtc).TotalMilliseconds : null;

    /// <summary>True once the execution has reached a terminal state.</summary>
    public bool IsFinished => FinishedAtUtc is not null;

    /// <summary>A bounded, timestamped log captured via <c>ChronosixJobContext.Log</c>.</summary>
    public IReadOnlyList<string> Log
    {
        get
        {
            lock (_sync)
            {
                return _log.ToArray();
            }
        }
    }

    internal void AppendLog(string message)
    {
        var line = $"{DateTimeOffset.UtcNow:HH:mm:ss.fff}  {message}";
        lock (_sync)
        {
            _log.Add(line);
            while (_log.Count > _maxLogLines)
            {
                _log.RemoveAt(0);
            }
        }
    }

    internal void Complete(ExecutionStatus status, DateTimeOffset finishedAtUtc, Exception? error)
    {
        lock (_sync)
        {
            Status = status;
            FinishedAtUtc = finishedAtUtc;
            if (error is not null)
            {
                ErrorType = error.GetType().FullName ?? error.GetType().Name;
                ErrorMessage = error.Message;
                ErrorDetail = error.ToString();
            }
        }
    }
}
