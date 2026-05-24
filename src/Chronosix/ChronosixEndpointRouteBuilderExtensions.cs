using Chronosix;
using Chronosix.Internal;
using Chronosix.Realtime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Endpoint-routing entry point for Chronosix. A single <c>app.MapChronosix()</c> call
/// mounts the dashboard at <c>/chronosix</c>, the SignalR hub at <c>/chronosix/hub</c> and
/// the REST API under <c>/chronosix/api</c>.
/// </summary>
public static class ChronosixEndpointRouteBuilderExtensions
{
    /// <summary>Mounts the Chronosix dashboard, hub and API.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="mapRazorPages">
    /// When true (the default) Chronosix also calls <c>MapRazorPages()</c>. Pass false if
    /// your application already maps its own Razor Pages, to avoid a duplicate mapping.
    /// </param>
    public static IEndpointRouteBuilder MapChronosix(this IEndpointRouteBuilder endpoints, bool mapRazorPages = true)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        if (mapRazorPages)
        {
            endpoints.MapRazorPages();
        }

        endpoints.MapHub<ChronosixHub>("/chronosix/hub");

        var api = endpoints.MapGroup("/chronosix/api");

        api.MapGet("/snapshot", (ChronosixDiagnostics diagnostics, ChronosixAccessGuard guard, HttpContext http) =>
            guard.IsAllowed(http)
                ? Results.Json(diagnostics.CreateSnapshot(), ChronosixJson.Options)
                : Results.NotFound());

        api.MapGet("/jobs/{id}/executions",
            (string id, ChronosixDiagnostics diagnostics, ChronosixAccessGuard guard, HttpContext http) =>
                guard.IsAllowed(http)
                    ? Results.Json(diagnostics.GetJobExecutions(id), ChronosixJson.Options)
                    : Results.NotFound());

        api.MapPost("/jobs/{id}/trigger",
            (string id, ChronosixDiagnostics diagnostics, ChronosixAccessGuard guard, HttpContext http) =>
                guard.IsAllowed(http)
                    ? Results.Json(diagnostics.TriggerJob(id), ChronosixJson.Options)
                    : Results.NotFound());

        api.MapPost("/jobs/{id}/enabled",
            (string id, bool value, ChronosixDiagnostics diagnostics, ChronosixAccessGuard guard, HttpContext http) =>
                guard.IsAllowed(http)
                    ? Results.Json(new { id, enabled = value, changed = diagnostics.SetJobEnabled(id, value) },
                        ChronosixJson.Options)
                    : Results.NotFound());

        api.MapPost("/failures/{id:guid}/acknowledge",
            (Guid id, ChronosixDiagnostics diagnostics, ChronosixAccessGuard guard, HttpContext http) =>
                guard.IsAllowed(http)
                    ? Results.Json(new { id, acknowledged = diagnostics.AcknowledgeFailure(id) }, ChronosixJson.Options)
                    : Results.NotFound());

        api.MapPost("/failures/clear",
            (ChronosixDiagnostics diagnostics, ChronosixAccessGuard guard, HttpContext http) =>
                guard.IsAllowed(http)
                    ? Results.Json(new { cleared = diagnostics.ClearFailures() }, ChronosixJson.Options)
                    : Results.NotFound());

        return endpoints;
    }
}
