using Chronosix.Model;

namespace Chronosix.Internal;

/// <summary>
/// A bounded, in-memory ring buffer of recent executions across every job. This is the
/// data the timeline visualiser draws from.
/// </summary>
internal sealed class ExecutionTracker
{
    private readonly object _sync = new();
    private readonly LinkedList<JobExecution> _executions = new();
    private readonly int _capacity;
    private long _sequence;

    public ExecutionTracker(int capacity)
    {
        _capacity = Math.Max(10, capacity);
    }

    /// <summary>Adds a new execution, assigning it a monotonic sequence number.</summary>
    public void Add(JobExecution execution)
    {
        lock (_sync)
        {
            execution.Sequence = ++_sequence;
            _executions.AddFirst(execution);
            while (_executions.Count > _capacity)
            {
                _executions.RemoveLast();
            }
        }
    }

    /// <summary>Returns recent executions, newest first.</summary>
    public IReadOnlyList<JobExecution> GetRecent()
    {
        lock (_sync)
        {
            return _executions.ToArray();
        }
    }

    /// <summary>Returns recent executions for a single job, newest first.</summary>
    public IReadOnlyList<JobExecution> GetForJob(string jobId)
    {
        lock (_sync)
        {
            return _executions
                .Where(e => string.Equals(e.JobId, jobId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }
}
