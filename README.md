# Chronosix

**In-process scheduled task & background worker dashboard for ASP.NET Core.**

Chronosix is a middleware library that intercepts the state of your background jobs
*in-process* and mounts an embedded dashboard at `/chronosix` — no database, no separate
service, no extra infrastructure. Add one line, press F5, and you can see exactly what
your schedulers are doing.

```csharp
builder.Services.AddChronosix();
// ...
app.MapChronosix();
```

---

## The problem

Most enterprise .NET apps schedule background work with Quartz.NET, Hangfire, or native
`IHostedService` / `BackgroundService` loops driven by cron expressions. Checking whether
those jobs are actually running correctly usually means **staring at rolling console
logs** or **writing throwaway API endpoints** just to trigger a job for testing.
The scheduler dashboards that do exist usually need a database back-end to function.

Chronosix removes that friction. It keeps a live, in-memory picture of every job and
serves it from your own process.

## What you get

- **The "Trigger Now" button** — fire any automated job manually, on demand, regardless
  of its cron schedule. No more waiting until 3am to test the nightly cleanup.
- **Timeline visualizer** — a Gantt-style execution graph showing how long each run took
  and where bottlenecks or overlapping executions occurred, updated in real time over
  SignalR.
- **Failure tracer** — unhandled exceptions thrown inside background worker loops are
  captured into an in-memory queue with full stack traces, so a failure is something you
  *inspect* rather than something you *scroll past*.
- **Executive dashboard** — a refined UI with first-class **dark and light themes**
  (toggle in the header, remembered per browser).
- **Zero dependencies** — the core library needs nothing beyond the ASP.NET Core shared
  framework. No database, no Redis, no NuGet dependency tree.

## Install

```bash
dotnet add package Chronosix
```

Optional scheduler adapters:

```bash
dotnet add package Chronosix.Quartz     # surface Quartz.NET jobs
dotnet add package Chronosix.Hangfire   # surface Hangfire jobs
```

Targets `net8.0`, `net9.0` and `net10.0`.

## Quick start

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddChronosix();

// A job on a 6-field cron schedule (sec min hour day month day-of-week):
builder.Services.AddChronosixJob<ReportJob>("0 */5 * * * *");

// An instrumented background worker:
builder.Services.AddChronosixWorker<ImportWorker>();

var app = builder.Build();

app.MapChronosix();   // mounts /chronosix, /chronosix/hub and /chronosix/api

app.Run();
```

Browse to **`/chronosix`**.

## Authoring jobs

### `IChronosixJob` — scheduler-driven jobs

Implement `IChronosixJob` and let the built-in scheduler run it. The job is resolved from
a fresh DI scope for every execution, so scoped dependencies are safe to use.

```csharp
public sealed class ReportJob : IChronosixJob
{
    private readonly IReportService _reports;

    public ReportJob(IReportService reports) => _reports = reports;

    public async Task ExecuteAsync(ChronosixJobContext context, CancellationToken ct)
    {
        context.Log("Generating the daily report…");
        await _reports.GenerateAsync(ct);
        context.Log("Done.");
    }
}
```

Register it on a cron schedule, a fixed interval, or as manual-only:

```csharp
builder.Services.AddChronosixJob<ReportJob>("0 0 6 * * *");          // cron: 06:00 daily
builder.Services.AddChronosixJob<PollJob>(TimeSpan.FromSeconds(30)); // every 30 seconds
builder.Services.AddChronosixJob<ReindexJob>();                      // manual-only (Trigger Now)
```

### `ChronosixBackgroundService` — instrumented workers

For a long-lived worker, derive from `ChronosixBackgroundService` instead of
`BackgroundService`. Chronosix then owns the scheduling loop, times every iteration,
captures exceptions and gives the worker a working "Trigger Now" button:

```csharp
[ChronosixJob(Name = "Import Worker", IntervalSeconds = 25)]
public sealed class ImportWorker : ChronosixBackgroundService
{
    public ImportWorker(IServiceProvider services) : base(services) { }

    protected override async Task ExecuteJobAsync(ChronosixJobContext context, CancellationToken ct)
    {
        context.Log("Importing batch…");
        await DoImportAsync(ct);
    }
}
```

```csharp
builder.Services.AddChronosixWorker<ImportWorker>();
```

If Chronosix is ever not registered, the worker still runs — just uninstrumented — so it
is always safe to ship.

### Existing `BackgroundService` workers — no restructuring

Already have native `IHostedService` / `BackgroundService` workers — perhaps driven by
Cronos expressions — that you would rather not rewrite? Leave the base class and the loop
exactly as they are. Inject `ChronosixDiagnostics` and wrap each iteration in
`TrackAsync`:

```csharp
public sealed class InventorySyncWorker : BackgroundService
{
    private readonly ChronosixDiagnostics _chronosix;

    public InventorySyncWorker(ChronosixDiagnostics chronosix) => _chronosix = chronosix;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _chronosix.TrackAsync("InventorySync", SyncAsync, stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task SyncAsync(CancellationToken ct) { /* your existing work */ }
}
```

`TrackAsync` times the iteration, places it on the timeline, and routes any unhandled
exception to the failure tracer **without rethrowing it**, so the loop keeps running.
The worker appears on the dashboard as a background worker. This path gives you the
timeline and the failure tracer; it does not give a working "Trigger Now" button —
that requires Chronosix to own the loop, so use `ChronosixBackgroundService` for any
worker you want to fire on demand.

Because Chronosix's cron parser accepts the same standard 5- and 6-field syntax as
Cronos, cron expression *strings* you already use with Cronos drop straight into
`AddChronosixJob<T>("…")` unchanged.

## Configuration

```csharp
builder.Services.AddChronosix(options =>
{
    options.DashboardTitle          = "Acme Jobs";
    options.DefaultTheme            = "dark";           // "dark" or "light"
    options.RestrictToDevelopment   = true;            // 404 the dashboard outside Development
    options.SchedulerTimeZone       = TimeZoneInfo.Utc;
    options.MaxExecutionHistory     = 250;             // timeline ring-buffer size
    options.MaxFailureHistory       = 100;             // failure tracer queue size
    options.MaxLogLinesPerExecution = 200;

    // Full custom gate (overrides RestrictToDevelopment):
    options.Authorize = http => http.User.IsInRole("Operations");
});
```

By default the dashboard is reachable **only in the Development environment**. To expose
it elsewhere, supply an `Authorize` delegate.

## Cron syntax

Chronosix ships its own dependency-free cron parser supporting 5-field
(`min hour day month day-of-week`) and 6-field (leading `second`) expressions, with
`*`, `?`, ranges, steps, lists, wrap-around ranges, and three-letter month/day names.
Quartz-only tokens (`L`, `W`, `#`) are not supported — use the Quartz adapter for those.

## REST API

The dashboard is driven by a small JSON API under `/chronosix/api`, which you can also
call yourself:

| Method & route                                | Purpose                         |
|------------------------------------------------|---------------------------------|
| `GET  /chronosix/api/snapshot`                 | Full dashboard snapshot         |
| `GET  /chronosix/api/jobs/{id}/executions`     | Recent executions for a job     |
| `POST /chronosix/api/jobs/{id}/trigger`        | Trigger a job now               |
| `POST /chronosix/api/jobs/{id}/enabled?value=` | Enable / disable a schedule     |
| `POST /chronosix/api/failures/{id}/acknowledge`| Acknowledge a captured failure  |
| `POST /chronosix/api/failures/clear`           | Clear the failure queue         |

## Scheduler adapters

Already using Quartz.NET or Hangfire? The adapters surface those jobs on the same
dashboard without rewriting them.

```csharp
// Quartz.NET (call after services.AddQuartz(...)):
builder.Services.AddChronosixQuartz();

// Hangfire (call after services.AddHangfire(...)):
builder.Services.AddChronosixHangfire();
```

Quartz executions and Hangfire server executions then appear on the timeline, and their
exceptions feed the failure tracer. The adapters target Quartz.NET 3.x and Hangfire 1.8.x.

## Architecture

```
┌──────────────────────────────────────────────────────────────┐
│ Your ASP.NET Core process                                      │
│                                                                │
│  IChronosixJob ─┐                                              │
│  Workers ───────┼─▶ ChronosixRunner ─▶ ExecutionTracker ──┐    │
│  Quartz/Hangfire┘        │              FailureTracer ─────┤    │
│   (via adapters)         │              JobRegistry ───────┤    │
│                          ▼                                 ▼    │
│                   DashboardNotifier ─▶ SignalR ─▶ /chronosix    │
└──────────────────────────────────────────────────────────────┘
```

Everything lives in memory inside your process. Nothing is persisted; restart the app
and the dashboard starts fresh.

## Repository layout

| Path                           | Contents                                  |
|--------------------------------|-------------------------------------------|
| `src/Chronosix`                | Core library, dashboard, API, SignalR hub |
| `adapters/Chronosix.Quartz`    | Quartz.NET adapter                        |
| `adapters/Chronosix.Hangfire`  | Hangfire adapter                          |
| `samples/Chronosix.Sample.Web` | Runnable sample web app with demo jobs    |
| `.github/workflows`            | CI and release automation                 |

## Building from source

Requires the .NET SDK **10.0.300** (pinned in `global.json`).

```bash
dotnet build Chronosix.sln -c Release
dotnet run --project samples/Chronosix.Sample.Web
```

Then open `http://localhost:5179/chronosix`.

## Releasing

CI builds and verifies every push. To publish a release, push a semver tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

The release workflow packs `Chronosix`, `Chronosix.Quartz` and `Chronosix.Hangfire`,
pushes them to NuGet.org (using the `NUGET_API_KEY` repository secret) and creates a
GitHub Release with the `.nupkg` files attached.

## License

MIT — see [LICENSE](LICENSE).
