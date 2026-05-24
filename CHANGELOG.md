# Changelog

All notable changes to Chronosix are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-05-24

The first release of Chronosix.

### Added

- **Core library (`Chronosix`)** — an in-process dashboard for ASP.NET Core background
  jobs, mounted at `/chronosix` with `services.AddChronosix()` and `app.MapChronosix()`.
- **`IChronosixJob`** plus `AddChronosixJob` registration for cron, fixed-interval and
  manual-only scheduled jobs, each executed in its own dependency-injection scope.
- **`ChronosixBackgroundService`** — an instrumented, drop-in replacement for
  `BackgroundService`, registered with `AddChronosixWorker`.
- **`ChronosixDiagnostics.TrackAsync`** — instruments an existing native
  `IHostedService` / `BackgroundService` worker per loop iteration, with no base-class
  change, for apps that prefer not to restructure.
- **Trigger Now** — fire any job on demand from the dashboard or REST API, regardless of
  its schedule.
- **Timeline visualizer** — a Gantt-style, lane-packed execution graph with live updates
  over SignalR, surfacing run durations, bottlenecks and overlapping executions.
- **Failure tracer** — a bounded in-memory queue that captures unhandled exceptions
  thrown inside job loops, with full stack traces and acknowledgement.
- **Executive dashboard UI** with first-class dark and light themes and a remembered
  per-browser preference.
- **Dependency-free cron parser** supporting 5- and 6-field expressions, ranges, steps,
  lists, wrap-around ranges and named months/days.
- **REST API** under `/chronosix/api` and a **SignalR hub** at `/chronosix/hub`.
- **`Chronosix.Quartz`** — adapter that surfaces Quartz.NET 3.x jobs on the dashboard.
- **`Chronosix.Hangfire`** — adapter that surfaces Hangfire 1.8.x jobs on the dashboard.
- Multi-targeting for `net8.0`, `net9.0` and `net10.0`.
- A runnable sample web application and GitHub Actions workflows for CI and releases.

[Unreleased]: https://github.com/sureshsubramanian/Chronosix/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/sureshsubramanian/Chronosix/releases/tag/v0.1.0
