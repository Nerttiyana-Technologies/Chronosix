using Chronosix.Abstractions;

namespace Chronosix.Sample.Web.Jobs;

/// <summary>
/// A slower job whose duration varies run to run — useful for seeing how the timeline
/// visualises long bars and the occasional bottleneck.
/// </summary>
public sealed class ReportGenerationJob : IChronosixJob
{
    public async Task ExecuteAsync(ChronosixJobContext context, CancellationToken cancellationToken)
    {
        context.Log("Gathering source data…");
        await Task.Delay(Random.Shared.Next(1_200, 4_500), cancellationToken);

        context.Log("Aggregating metrics…");
        await Task.Delay(Random.Shared.Next(400, 1_600), cancellationToken);

        context.Log("Rendering report document…");
        await Task.Delay(Random.Shared.Next(300, 900), cancellationToken);

        var rows = Random.Shared.Next(2_000, 50_000);
        context.Log($"Report generated with {rows:N0} rows.");
    }
}
