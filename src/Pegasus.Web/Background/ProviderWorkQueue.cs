using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Pegasus.Web.Background;

/// <summary>
/// One piece of provider work the Web host runs after the request that asked
/// for it has answered. <see cref="Key"/> names the record the work acts on;
/// one key has at most one piece of work queued or running.
/// </summary>
public sealed record ProviderWork(
    Guid Key, string Kind, Func<IServiceProvider, CancellationToken, Task> RunAsync)
{
    public override string ToString() => $"{Kind} {Key:D}";
}

/// <summary>
/// The Web host's bounded queue of provider work and the registry of what is
/// queued or running.
/// </summary>
/// <remarks>
/// The registry is this process's own memory. It answers "is anything running
/// for this record" only for a host with one instance: a record found waiting
/// with nothing registered here is treated as interrupted.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "A queue of work to run, not a collection type.")]
public sealed class ProviderWorkQueue
{
    /// <summary>More waiting work than this is refused rather than held.</summary>
    public const int Capacity = 64;

    private readonly Channel<ProviderWork> channel = Channel.CreateBounded<ProviderWork>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });

    private readonly ConcurrentDictionary<Guid, byte> inFlight = new();

    /// <summary>
    /// Queues the work, or answers true without queuing when work for the same
    /// key is already queued or running: that work's outcome is the answer.
    /// False only when the queue is full.
    /// </summary>
    public bool TryEnqueue(ProviderWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!inFlight.TryAdd(work.Key, 0))
        {
            return true;
        }
        if (channel.Writer.TryWrite(work))
        {
            return true;
        }

        inFlight.TryRemove(work.Key, out _);
        return false;
    }

    /// <summary>Whether work for this key is queued or running in this process.</summary>
    public bool IsInFlight(Guid key) => inFlight.ContainsKey(key);

    internal ChannelReader<ProviderWork> Reader => channel.Reader;

    internal void Complete(Guid key) => inFlight.TryRemove(key, out _);
}
