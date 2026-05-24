using Chronosix.Model;

namespace Chronosix.Internal;

/// <summary>
/// The in-process catalogue of every known job and its rolling statistics. All access
/// is synchronised; callers outside the registry receive detached clones.
/// </summary>
internal sealed class JobRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<string, JobDescriptor> _jobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DurationAggregate> _durations = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a descriptor. If the id is already known the existing descriptor is
    /// returned so the caller (and its scheduling loop) shares one canonical instance.
    /// </summary>
    public JobDescriptor Register(JobDescriptor descriptor)
    {
        lock (_sync)
        {
            if (_jobs.TryGetValue(descriptor.Id, out var existing))
            {
                return existing;
            }

            _jobs[descriptor.Id] = descriptor;
            return descriptor;
        }
    }

    /// <summary>Returns the live descriptor for an id, or null.</summary>
    public JobDescriptor? Get(string id)
    {
        lock (_sync)
        {
            return _jobs.TryGetValue(id, out var descriptor) ? descriptor : null;
        }
    }

    /// <summary>Returns detached, serialisation-safe copies of every descriptor.</summary>
    public IReadOnlyList<JobDescriptor> GetSnapshots()
    {
        lock (_sync)
        {
            return _jobs.Values
                .Select(j => j.Clone())
                .OrderBy(j => j.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    /// <summary>Updates the cached "next run" time for a job.</summary>
    public void SetNextRun(string id, DateTimeOffset? next)
    {
        lock (_sync)
        {
            if (_jobs.TryGetValue(id, out var descriptor))
            {
                descriptor.NextRunUtc = next;
            }
        }
    }

    /// <summary>Enables or disables automatic scheduling for a job.</summary>
    public bool SetEnabled(string id, bool enabled)
    {
        lock (_sync)
        {
            if (!_jobs.TryGetValue(id, out var descriptor))
            {
                return false;
            }

            descriptor.Enabled = enabled;
            return true;
        }
    }

    /// <summary>Records that an execution has started.</summary>
    public void OnExecutionStarted(string jobId, JobExecution execution)
    {
        lock (_sync)
        {
            if (!_jobs.TryGetValue(jobId, out var descriptor))
            {
                return;
            }

            descriptor.TotalRuns++;
            descriptor.ActiveRuns++;
            descriptor.LastStartedUtc = execution.StartedAtUtc;
        }
    }

    /// <summary>Records that an execution has reached a terminal state.</summary>
    public void OnExecutionFinished(string jobId, JobExecution execution)
    {
        lock (_sync)
        {
            if (!_jobs.TryGetValue(jobId, out var descriptor))
            {
                return;
            }

            if (descriptor.ActiveRuns > 0)
            {
                descriptor.ActiveRuns--;
            }

            descriptor.LastFinishedUtc = execution.FinishedAtUtc;
            descriptor.LastStatus = execution.Status;
            descriptor.LastDurationMs = execution.DurationMs;

            switch (execution.Status)
            {
                case ExecutionStatus.Succeeded:
                    descriptor.SuccessCount++;
                    break;
                case ExecutionStatus.Failed:
                    descriptor.FailureCount++;
                    break;
            }

            if (execution.DurationMs is { } durationMs && execution.Status != ExecutionStatus.Skipped)
            {
                var aggregate = _durations.TryGetValue(jobId, out var existing)
                    ? existing
                    : new DurationAggregate();
                aggregate.Add(durationMs);
                _durations[jobId] = aggregate;
                descriptor.AverageDurationMs = aggregate.Average;
            }
        }
    }

    private sealed class DurationAggregate
    {
        private double _sum;
        private long _count;

        public void Add(double value)
        {
            _sum += value;
            _count++;
        }

        public double Average => _count == 0 ? 0 : _sum / _count;
    }
}
