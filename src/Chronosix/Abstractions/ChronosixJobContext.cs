using Chronosix.Model;

namespace Chronosix.Abstractions;

/// <summary>
/// The context handed to a job for the duration of a single execution. It exposes a
/// scoped <see cref="IServiceProvider"/> for resolving dependencies and a
/// <see cref="Log(string)"/> method whose output is captured against this execution and
/// shown on the dashboard.
/// </summary>
public sealed class ChronosixJobContext
{
    private readonly JobExecution _execution;

    internal ChronosixJobContext(
        JobExecution execution,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        _execution = execution;
        Services = services;
        CancellationToken = cancellationToken;
        Items = new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    /// <summary>The id of the running job.</summary>
    public string JobId => _execution.JobId;

    /// <summary>The name of the running job.</summary>
    public string JobName => _execution.JobName;

    /// <summary>The id of this specific execution.</summary>
    public Guid ExecutionId => _execution.Id;

    /// <summary>Why this execution started (schedule, manual, startup).</summary>
    public TriggerReason Trigger => _execution.Trigger;

    /// <summary>When this execution started (UTC).</summary>
    public DateTimeOffset StartedAtUtc => _execution.StartedAtUtc;

    /// <summary>
    /// A service provider scoped to this execution. Resolve scoped dependencies
    /// (DbContext, repositories, ...) from here rather than capturing them.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>Signalled when the host is shutting down or the execution is cancelled.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>A free-form per-execution property bag.</summary>
    public IDictionary<string, object?> Items { get; }

    /// <summary>
    /// Writes a line to this execution's captured log. The line is timestamped and shown
    /// on the dashboard's execution detail panel.
    /// </summary>
    public void Log(string message) => _execution.AppendLog(message);
}
