using Chronosix.Hangfire;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency-injection entry point for the Chronosix Hangfire adapter.</summary>
public static class ChronosixHangfireServiceCollectionExtensions
{
    /// <summary>
    /// Registers Chronosix (if not already registered) and installs a global Hangfire
    /// job filter so every Hangfire background job appears on the Chronosix dashboard.
    /// </summary>
    public static IServiceCollection AddChronosixHangfire(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddChronosix();
        services.AddHostedService<ChronosixHangfireInstaller>();
        return services;
    }
}
