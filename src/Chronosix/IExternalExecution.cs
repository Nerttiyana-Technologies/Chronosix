namespace Chronosix;

/// <summary>
/// A handle for recording one execution of a job that Chronosix does not run itself —
/// for example a Quartz.NET or Hangfire job surfaced through an adapter.
/// </summary>
/// <remarks>
/// Obtain one from <see cref="ChronosixDiagnostics.BeginExecution"/>, then call exactly
/// one of <see cref="Succeeded"/> or <see cref="Failed"/>. Disposing without doing so
/// records the execution as succeeded.
/// </remarks>
public interface IExternalExecution : IDisposable
{
    /// <summary>The id of the execution being recorded.</summary>
    Guid ExecutionId { get; }

    /// <summary>Appends a line to the execution's captured log.</summary>
    void Log(string message);

    /// <summary>Marks the execution as completed successfully.</summary>
    void Succeeded();

    /// <summary>Marks the execution as failed and feeds the error to the failure tracer.</summary>
    void Failed(Exception error);

    /// <summary>Marks the execution as cancelled (for example, host shutdown).</summary>
    void Cancelled();
}
