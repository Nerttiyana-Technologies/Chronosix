using Chronosix.Sample.Web.Jobs;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Chronosix. This wires up the engine, the embedded dashboard,
//    the REST API and the SignalR hub.
builder.Services.AddChronosix(options =>
{
    options.DashboardTitle = "Chronosix — Sample App";

    // The sample exposes the dashboard outside Development so it is easy to try.
    // In a real application, leave this true (the default) or supply options.Authorize.
    options.RestrictToDevelopment = false;
});

// 2. Register a few jobs that implement IChronosixJob, each on a cron schedule.
builder.Services.AddChronosixJob<HeartbeatJob>("*/10 * * * * *", registration =>
{
    registration.Name = "Heartbeat";
    registration.Description = "A fast liveness check that runs every ten seconds.";
});

builder.Services.AddChronosixJob<ReportGenerationJob>("0 */5 * * * *", registration =>
{
    registration.Name = "Report Generation";
    registration.Description = "Builds the rolling analytics report every five minutes.";
    registration.RunOnStartup = true;
});

builder.Services.AddChronosixJob<NightlyCleanupJob>("0 0 3 * * *", registration =>
{
    registration.Name = "Nightly Cleanup";
    registration.Description = "Purges stale temp files at 03:00. Press \"Trigger Now\" to test it on demand.";
});

// 3. Register an instrumented background worker (a ChronosixBackgroundService).
builder.Services.AddChronosixWorker<FlakyImportWorker>();

// 4. A plain BackgroundService — registered the normal way — that instruments itself
//    via ChronosixDiagnostics.TrackAsync without changing its base class.
builder.Services.AddHostedService<InventorySyncWorker>();

var app = builder.Build();

// 5. Mount the dashboard at /chronosix (plus /chronosix/hub and /chronosix/api).
app.MapChronosix();

// Convenience: send the site root straight to the dashboard.
app.MapGet("/", () => Results.Redirect("/chronosix"));

app.Run();
