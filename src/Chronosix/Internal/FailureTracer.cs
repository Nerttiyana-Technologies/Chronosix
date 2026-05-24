using Chronosix.Model;

namespace Chronosix.Internal;

/// <summary>
/// A bounded, in-memory queue of unhandled exceptions thrown inside job executions.
/// This powers the Failure Tracer panel on the dashboard.
/// </summary>
internal sealed class FailureTracer
{
    private readonly object _sync = new();
    private readonly LinkedList<CapturedFailure> _failures = new();
    private readonly int _capacity;

    public FailureTracer(int capacity)
    {
        _capacity = Math.Max(5, capacity);
    }

    /// <summary>Captures a failure that occurred during a tracked <see cref="JobExecution"/>.</summary>
    public CapturedFailure Capture(JobDescriptor descriptor, JobExecution execution, Exception error)
    {
        var failure = new CapturedFailure
        {
            Id = Guid.NewGuid(),
            JobId = descriptor.Id,
            JobName = descriptor.Name,
            Kind = descriptor.Kind,
            ExecutionId = execution.Id,
            OccurredAtUtc = execution.FinishedAtUtc ?? DateTimeOffset.UtcNow,
            ExceptionType = error.GetType().FullName ?? error.GetType().Name,
            Message = error.Message,
            Detail = error.ToString(),
            Trigger = execution.Trigger
        };

        Store(failure);
        return failure;
    }

    /// <summary>
    /// Captures a failure reported by an external scheduler adapter (Quartz / Hangfire),
    /// where no Chronosix <see cref="JobExecution"/> exists.
    /// </summary>
    public CapturedFailure Capture(
        string jobId,
        string jobName,
        JobKind kind,
        Exception error,
        TriggerReason trigger = TriggerReason.External,
        Guid? executionId = null)
    {
        var failure = new CapturedFailure
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            JobName = jobName,
            Kind = kind,
            ExecutionId = executionId,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ExceptionType = error.GetType().FullName ?? error.GetType().Name,
            Message = error.Message,
            Detail = error.ToString(),
            Trigger = trigger
        };

        Store(failure);
        return failure;
    }

    /// <summary>Returns recent failures, newest first.</summary>
    public IReadOnlyList<CapturedFailure> GetRecent()
    {
        lock (_sync)
        {
            return _failures.ToArray();
        }
    }

    /// <summary>Marks a single failure as reviewed.</summary>
    public bool Acknowledge(Guid id)
    {
        lock (_sync)
        {
            foreach (var failure in _failures)
            {
                if (failure.Id == id)
                {
                    failure.Acknowledged = true;
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Removes every captured failure.</summary>
    public int Clear()
    {
        lock (_sync)
        {
            var count = _failures.Count;
            _failures.Clear();
            return count;
        }
    }

    private void Store(CapturedFailure failure)
    {
        lock (_sync)
        {
            _failures.AddFirst(failure);
            while (_failures.Count > _capacity)
            {
                _failures.RemoveLast();
            }
        }
    }
}
