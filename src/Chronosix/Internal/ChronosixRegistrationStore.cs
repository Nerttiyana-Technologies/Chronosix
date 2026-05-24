namespace Chronosix.Internal;

/// <summary>
/// Holds job registrations gathered at <c>IServiceCollection</c> configuration time, before
/// the dependency-injection container exists. <see cref="ScheduledJobHost"/> drains it at
/// startup to build descriptors and launch scheduling loops.
/// </summary>
internal sealed class ChronosixRegistrationStore
{
    /// <summary>Scheduler-driven <c>IChronosixJob</c> registrations.</summary>
    public List<ScheduledJobEntry> ScheduledJobs { get; } = new();
}

/// <summary>A single scheduled-job registration: the CLR type plus its declared options.</summary>
internal sealed class ScheduledJobEntry
{
    /// <summary>The <c>IChronosixJob</c> implementation type.</summary>
    public required Type JobType { get; init; }

    /// <summary>The registration options supplied by the caller.</summary>
    public ChronosixJobRegistration Registration { get; init; } = new();
}
