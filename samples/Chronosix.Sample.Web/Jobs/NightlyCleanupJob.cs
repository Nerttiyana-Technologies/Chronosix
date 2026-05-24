using Chronosix.Abstractions;

namespace Chronosix.Sample.Web.Jobs;

/// <summary>
/// A job whose real schedule is far in the future (03:00 daily). It exists to show off
/// the "Trigger Now" button: you should not have to wait until 3am to test it.
/// </summary>
public sealed class NightlyCleanupJob : IChronosixJob
{
    public async Task ExecuteAsync(ChronosixJobContext context, CancellationToken cancellationToken)
    {
        context.Log($"Cleanup started via {context.Trigger} trigger.");
        context.Log("Scanning temp directory…");
        await Task.Delay(Random.Shared.Next(600, 2_200), cancellationToken);

        var purged = Random.Shared.Next(0, 320);
        context.Log($"Purged {purged} stale files.");
    }
}
