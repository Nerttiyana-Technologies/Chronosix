using Chronosix.Quartz;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency-injection entry point for the Chronosix Quartz.NET adapter.</summary>
public static class ChronosixQuartzServiceCollectionExtensions
{
    /// <summary>
    /// Registers Chronosix (if not already registered) and attaches a Quartz.NET job
    /// listener so every Quartz job appears on the Chronosix dashboard. Call this after
    /// <c>services.AddQuartz(...)</c>.
    /// </summary>
    public static IServiceCollection AddChronosixQuartz(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddChronosix();
        services.TryAddSingleton<ChronosixQuartzJobListener>();
        services.AddHostedService<ChronosixQuartzListenerInstaller>();
        return services;
    }
}
