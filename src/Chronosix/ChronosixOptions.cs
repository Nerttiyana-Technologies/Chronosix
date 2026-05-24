using Microsoft.AspNetCore.Http;

namespace Chronosix;

/// <summary>
/// Configuration for Chronosix, supplied through <c>services.AddChronosix(options =&gt; ...)</c>.
/// </summary>
public sealed class ChronosixOptions
{
    /// <summary>
    /// Master switch. When false the dashboard, API and SignalR hub all return 404 and
    /// no job instrumentation is surfaced. Default <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of executions retained in the in-memory timeline buffer.
    /// Default <c>250</c>.
    /// </summary>
    public int MaxExecutionHistory { get; set; } = 250;

    /// <summary>
    /// Maximum number of captured failures retained by the failure tracer.
    /// Default <c>100</c>.
    /// </summary>
    public int MaxFailureHistory { get; set; } = 100;

    /// <summary>
    /// Maximum number of log lines retained per execution. Default <c>200</c>.
    /// </summary>
    public int MaxLogLinesPerExecution { get; set; } = 200;

    /// <summary>
    /// Time zone used to evaluate cron expressions. Default <see cref="TimeZoneInfo.Utc"/>.
    /// </summary>
    public TimeZoneInfo SchedulerTimeZone { get; set; } = TimeZoneInfo.Utc;

    /// <summary>
    /// When true (the default) the dashboard is only reachable while the host is running
    /// in the Development environment. Set false to expose it elsewhere — in which case
    /// supplying <see cref="Authorize"/> is strongly recommended.
    /// </summary>
    public bool RestrictToDevelopment { get; set; } = true;

    /// <summary>
    /// Optional custom authorization gate. When set, it fully governs access to every
    /// Chronosix endpoint and <see cref="RestrictToDevelopment"/> is ignored.
    /// </summary>
    public Func<HttpContext, bool>? Authorize { get; set; }

    /// <summary>Title shown in the dashboard header. Default <c>"Chronosix"</c>.</summary>
    public string DashboardTitle { get; set; } = "Chronosix";

    /// <summary>
    /// Default dashboard theme: <c>"dark"</c> or <c>"light"</c>. Individual visitors can
    /// toggle and their choice is remembered in the browser. Default <c>"dark"</c>.
    /// </summary>
    public string DefaultTheme { get; set; } = "dark";
}
