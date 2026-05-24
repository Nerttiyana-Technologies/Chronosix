using Chronosix.Model;

namespace Chronosix.Internal;

/// <summary>
/// The default <see cref="IExternalExecution"/>: records one run of a job that an external
/// scheduler (Quartz / Hangfire) executes. Finalisation is idempotent, so calling
/// <see cref="Succeeded"/>/<see cref="Failed"/> and then disposing is safe.
/// </summary>
internal sealed class ExternalExecution : IExternalExecution
{
    private readonly ChronosixDiagnostics _diagnostics;
    private readonly JobDescriptor _descriptor;
    private readonly JobExecution _execution;
    private int _finalized;

    public ExternalExecution(ChronosixDiagnostics diagnostics, JobDescriptor descriptor, JobExecution execution)
    {
        _diagnostics = diagnostics;
        _descriptor = descriptor;
        _execution = execution;
    }

    /// <inheritdoc />
    public Guid ExecutionId => _execution.Id;

    /// <inheritdoc />
    public void Log(string message) => _execution.AppendLog(message);

    /// <inheritdoc />
    public void Succeeded() => Finalize(ExecutionStatus.Succeeded, null);

    /// <inheritdoc />
    public void Failed(Exception error) => Finalize(ExecutionStatus.Failed, error);

    /// <inheritdoc />
    public void Cancelled() => Finalize(ExecutionStatus.Cancelled, null);

    /// <inheritdoc />
    public void Dispose() => Finalize(ExecutionStatus.Succeeded, null);

    private void Finalize(ExecutionStatus status, Exception? error)
    {
        if (Interlocked.Exchange(ref _finalized, 1) != 0)
        {
            return;
        }

        _diagnostics.CompleteExternal(_execution, _descriptor, status, error);
    }
}
