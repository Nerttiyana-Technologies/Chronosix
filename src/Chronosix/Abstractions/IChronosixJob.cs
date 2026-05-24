namespace Chronosix.Abstractions;

/// <summary>
/// A unit of scheduled work. Implement this interface and register it with
/// <c>services.AddChronosixJob&lt;T&gt;("cron")</c> to have the built-in scheduler run it,
/// instrument every execution, and surface it on the dashboard.
/// </summary>
/// <remarks>
/// Implementations are resolved from a fresh dependency-injection scope for every
/// execution, so they may safely depend on scoped services.
/// </remarks>
public interface IChronosixJob
{
    /// <summary>
    /// Performs one execution of the job.
    /// </summary>
    /// <param name="context">Per-execution context (scoped services, logging, metadata).</param>
    /// <param name="cancellationToken">Cancelled on host shutdown.</param>
    Task ExecuteAsync(ChronosixJobContext context, CancellationToken cancellationToken);
}
