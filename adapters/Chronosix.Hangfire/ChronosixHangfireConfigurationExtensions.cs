using Chronosix;
using Chronosix.Hangfire;

namespace Hangfire;

/// <summary>
/// Hangfire <see cref="IGlobalConfiguration"/> entry point for the Chronosix adapter, for
/// applications that prefer to wire filters through <c>AddHangfire(config =&gt; ...)</c>.
/// </summary>
public static class ChronosixHangfireConfigurationExtensions
{
    /// <summary>
    /// Adds the Chronosix server filter to the Hangfire configuration. Resolve the
    /// <see cref="ChronosixDiagnostics"/> instance from the application's service provider.
    /// </summary>
    public static IGlobalConfiguration UseChronosix(
        this IGlobalConfiguration configuration,
        ChronosixDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(diagnostics);

        return configuration.UseFilter(new ChronosixHangfireFilter(diagnostics));
    }
}
