using Chronosix.Abstractions;

namespace Chronosix.Sample.Web.Jobs;

/// <summary>
/// An instrumented background worker that fails intermittently. It demonstrates the
/// Failure Tracer: roughly one run in three throws, and the exception is captured for
/// inspection on the dashboard instead of vanishing into the console.
/// </summary>
[ChronosixJob(
    Name = "Flaky Import Worker",
    Description = "Imports records every 25 seconds and fails intermittently — watch the Failure Tracer.",
    IntervalSeconds = 25)]
public sealed class FlakyImportWorker : ChronosixBackgroundService
{
    public FlakyImportWorker(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
    }

    protected override async Task ExecuteJobAsync(ChronosixJobContext context, CancellationToken cancellationToken)
    {
        context.Log("Connecting to upstream feed…");
        await Task.Delay(Random.Shared.Next(300, 1_100), cancellationToken);

        var batch = Random.Shared.Next(20, 200);
        context.Log($"Importing a batch of {batch} records…");
        await Task.Delay(Random.Shared.Next(400, 1_900), cancellationToken);

        if (Random.Shared.NextDouble() < 0.35)
        {
            throw new InvalidOperationException(
                $"Upstream feed returned a malformed payload while importing a batch of {batch} records.");
        }

        context.Log($"Imported {batch} records successfully.");
    }
}
