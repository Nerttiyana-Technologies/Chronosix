using System.Threading.Channels;

namespace Chronosix.Internal;

/// <summary>
/// Carries "Trigger Now" requests from the dashboard to the scheduling loop of each job.
/// Each job gets a single-slot channel: pressing the button while a manual run is already
/// queued simply coalesces, it never piles up.
/// </summary>
internal sealed class TriggerCoordinator
{
    private readonly object _sync = new();
    private readonly Dictionary<string, Channel<byte>> _channels = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the reader a job's scheduling loop awaits for manual triggers.</summary>
    public ChannelReader<byte> GetReader(string jobId) => GetChannel(jobId).Reader;

    /// <summary>
    /// Requests an immediate run of the job. Returns true if the request was accepted
    /// (it is dropped only when a manual run is already pending for the same job).
    /// </summary>
    public bool RequestTrigger(string jobId) => GetChannel(jobId).Writer.TryWrite(1);

    private Channel<byte> GetChannel(string jobId)
    {
        lock (_sync)
        {
            if (!_channels.TryGetValue(jobId, out var channel))
            {
                channel = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
                {
                    FullMode = BoundedChannelFullMode.DropWrite,
                    SingleReader = true,
                    SingleWriter = false
                });
                _channels[jobId] = channel;
            }

            return channel;
        }
    }
}
