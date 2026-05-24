using System.Text.Json;
using Chronosix.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Chronosix.Pages;

/// <summary>
/// The page model behind the embedded dashboard. It performs the access check and
/// server-renders an initial snapshot so the dashboard paints instantly; SignalR then
/// keeps it live.
/// </summary>
public sealed class ChronosixDashboardModel : PageModel
{
    private readonly ChronosixDiagnostics _diagnostics;
    private readonly ChronosixAccessGuard _guard;
    private readonly ChronosixOptions _options;

    /// <summary>Constructs the page model. Parameters are supplied by dependency injection.</summary>
    public ChronosixDashboardModel(
        ChronosixDiagnostics diagnostics,
        ChronosixAccessGuard guard,
        IOptions<ChronosixOptions> options)
    {
        _diagnostics = diagnostics;
        _guard = guard;
        _options = options.Value;
    }

    /// <summary>The server-rendered initial snapshot, as a JSON literal for the page script.</summary>
    public string SnapshotJson { get; private set; } = "{}";

    /// <summary>The configured dashboard title.</summary>
    public string DashboardTitle => _options.DashboardTitle;

    /// <summary>The initial theme: <c>"dark"</c> or <c>"light"</c>.</summary>
    public string DefaultTheme =>
        string.Equals(_options.DefaultTheme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";

    /// <summary>Renders the dashboard, or 404 when the caller is not permitted.</summary>
    public IActionResult OnGet()
    {
        if (!_guard.IsAllowed(HttpContext))
        {
            return NotFound();
        }

        var snapshot = _diagnostics.CreateSnapshot();
        SnapshotJson = JsonSerializer.Serialize(snapshot, ChronosixJson.Options);
        return Page();
    }
}
