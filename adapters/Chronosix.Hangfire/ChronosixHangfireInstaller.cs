using global::Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chronosix.Hangfire;

/// <summary>
/// Registers the <see cref="ChronosixHangfireFilter"/> as a global Hangfire job filter
/// once the host has started.
/// </summary>
internal sealed class ChronosixHangfireInstaller : IHostedService
{
    private readonly ChronosixDiagnostics _diagnostics;
    private readonly ILogger<ChronosixHangfireInstaller> _logger;

    public ChronosixHangfireInstaller(ChronosixDiagnostics diagnostics, ILogger<ChronosixHangfireInstaller> logger)
    {
        _diagnostics = diagnostics;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        GlobalJobFilters.Filters.Add(new ChronosixHangfireFilter(_diagnostics));
        _logger.LogInformation("Chronosix Hangfire adapter registered as a global job filter.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
