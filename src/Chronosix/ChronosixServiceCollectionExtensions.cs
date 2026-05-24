using System.Text.Json.Serialization;
using Chronosix;
using Chronosix.Abstractions;
using Chronosix.Internal;
using Chronosix.Realtime;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency-injection entry points for Chronosix: <c>AddChronosix</c>,
/// <c>AddChronosixJob</c> and <c>AddChronosixWorker</c>.
/// </summary>
public static class ChronosixServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Chronosix engine, dashboard, REST API and SignalR hub. Safe to call
    /// more than once; the core services are registered exactly once.
    /// </summary>
    public static IServiceCollection AddChronosix(
        this IServiceCollection services,
        Action<ChronosixOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureCore(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }

    /// <summary>Registers an <see cref="IChronosixJob"/> on a cron schedule.</summary>
    public static IServiceCollection AddChronosixJob<TJob>(
        this IServiceCollection services,
        string cronExpression,
        Action<ChronosixJobRegistration>? configure = null)
        where TJob : class, IChronosixJob
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);

        return services.AddChronosixJob<TJob>(registration =>
        {
            registration.Cron = cronExpression;
            configure?.Invoke(registration);
        });
    }

    /// <summary>Registers an <see cref="IChronosixJob"/> on a fixed interval.</summary>
    public static IServiceCollection AddChronosixJob<TJob>(
        this IServiceCollection services,
        TimeSpan interval,
        Action<ChronosixJobRegistration>? configure = null)
        where TJob : class, IChronosixJob
    {
        return services.AddChronosixJob<TJob>(registration =>
        {
            registration.Interval = interval;
            configure?.Invoke(registration);
        });
    }

    /// <summary>
    /// Registers an <see cref="IChronosixJob"/> with full control over its registration.
    /// With no schedule the job is manual-only and can still be fired from the dashboard.
    /// </summary>
    public static IServiceCollection AddChronosixJob<TJob>(
        this IServiceCollection services,
        Action<ChronosixJobRegistration>? configure = null)
        where TJob : class, IChronosixJob
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureCore(services);
        services.TryAddScoped<TJob>();

        var registration = new ChronosixJobRegistration();
        configure?.Invoke(registration);

        GetOrCreateStore(services).ScheduledJobs.Add(new ScheduledJobEntry
        {
            JobType = typeof(TJob),
            Registration = registration
        });

        return services;
    }

    /// <summary>
    /// Registers an instrumented <see cref="ChronosixBackgroundService"/> worker. The
    /// worker self-registers with the dashboard when it starts.
    /// </summary>
    public static IServiceCollection AddChronosixWorker<TWorker>(this IServiceCollection services)
        where TWorker : ChronosixBackgroundService
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureCore(services);
        services.AddHostedService<TWorker>();
        return services;
    }

    private static void EnsureCore(IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(JobRegistry)))
        {
            return;
        }

        services.AddOptions<ChronosixOptions>();

        // The registration store must exist even when no jobs are added, because
        // ScheduledJobHost depends on it.
        GetOrCreateStore(services);

        services.TryAddSingleton<JobRegistry>();
        services.TryAddSingleton<TriggerCoordinator>();
        services.TryAddSingleton(sp =>
            new ExecutionTracker(sp.GetRequiredService<IOptions<ChronosixOptions>>().Value.MaxExecutionHistory));
        services.TryAddSingleton(sp =>
            new FailureTracer(sp.GetRequiredService<IOptions<ChronosixOptions>>().Value.MaxFailureHistory));
        services.TryAddSingleton<IDashboardNotifier, DashboardNotifier>();
        services.TryAddSingleton<ChronosixRunner>();
        services.TryAddSingleton<JobLoopRunner>();
        services.TryAddSingleton<ChronosixAccessGuard>();
        services.TryAddSingleton(sp => new ChronosixDiagnostics(
            sp.GetRequiredService<JobRegistry>(),
            sp.GetRequiredService<ExecutionTracker>(),
            sp.GetRequiredService<FailureTracer>(),
            sp.GetRequiredService<TriggerCoordinator>(),
            sp.GetRequiredService<IDashboardNotifier>(),
            sp.GetRequiredService<IHostEnvironment>(),
            sp.GetRequiredService<IOptions<ChronosixOptions>>()));

        services.AddHostedService<ScheduledJobHost>();

        services.AddRazorPages();
        services.AddSignalR()
            .AddJsonProtocol(jsonOptions =>
                jsonOptions.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    }

    private static ChronosixRegistrationStore GetOrCreateStore(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ChronosixRegistrationStore));
        if (descriptor?.ImplementationInstance is ChronosixRegistrationStore existing)
        {
            return existing;
        }

        var store = new ChronosixRegistrationStore();
        services.AddSingleton(store);
        return store;
    }
}
