using System.Threading.Channels;

namespace Pegasus.Web.Background;

/// <summary>
/// One piece of provider work the Web host runs after the request that asked
/// for it has answered. <see cref="Key"/> names the record the work acts on.
/// </summary>
public sealed record ProviderWork(
    Guid Key, string Kind, Func<IServiceProvider, CancellationToken, Task> RunAsync)
{
    public override string ToString() => $"{Kind} {Key:D}";
}

/// <summary>What became of work offered to the queue.</summary>
public enum ProviderWorkAdmission
{
    /// <summary>Queued; it runs after any work already running for its record.</summary>
    Queued,

    /// <summary>The queue is full and nothing was queued; the caller runs the work itself.</summary>
    Full
}

/// <summary>
/// The Web host's bounded queue of provider work and the registry of what is
/// reserved, queued or running for each record.
/// </summary>
/// <remarks>
/// <para>
/// A caller reserves a record before it writes the claim that owes work, and
/// keeps the reservation until that work is queued or has run. So a record is
/// never seen idle between its claim and its work, and a second caller that
/// finds it reserved waits on the same work instead of settling it.
/// </para>
/// <para>
/// Work for one record runs one piece at a time, in the order it was queued.
/// Nothing offered is dropped: a second piece for a busy record queues behind
/// the first.
/// </para>
/// <para>
/// The registry is this process's own memory. It answers "is anything running
/// for this record" only for a host with one instance: a record found waiting
/// with nothing registered here is treated as interrupted.
/// </para>
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

    private readonly Lock sync = new();
    private readonly Dictionary<Guid, Record> records = [];

    /// <summary>
    /// Marks the record busy until the reservation is released, or until the
    /// work it admits has run.
    /// </summary>
    public ProviderWorkReservation Reserve(Guid key)
    {
        lock (sync)
        {
            if (!records.TryGetValue(key, out var record))
            {
                records[key] = record = new();
            }
            record.Holders++;
        }

        return new(this, key);
    }

    /// <summary>Whether anything is reserved, queued or running for this record in this process.</summary>
    public bool IsInFlight(Guid key)
    {
        lock (sync)
        {
            return records.ContainsKey(key);
        }
    }

    internal ChannelReader<ProviderWork> Reader => channel.Reader;

    internal bool TryWrite(ProviderWork work) => channel.Writer.TryWrite(work);

    /// <summary>
    /// Waits until no other work runs for the record, and holds it until the
    /// answer is disposed. The caller must hold a reservation for the record.
    /// </summary>
    internal async Task<IDisposable> EnterAsync(Guid key, CancellationToken cancellationToken)
    {
        SemaphoreSlim gate;
        lock (sync)
        {
            gate = records.TryGetValue(key, out var record)
                ? record.Gate
                : throw new InvalidOperationException($"Nothing is reserved for {key:D}.");
        }

        await gate.WaitAsync(cancellationToken);
        return new Entered(gate);
    }

    /// <summary>Gives back one holder of the record; the last one frees it.</summary>
    internal void Release(Guid key)
    {
        lock (sync)
        {
            if (records.TryGetValue(key, out var record) && --record.Holders == 0)
            {
                records.Remove(key);
                record.Dispose();
            }
        }
    }

    private sealed class Record : IDisposable
    {
        public int Holders { get; set; }

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public void Dispose() => Gate.Dispose();
    }

    private sealed class Entered(SemaphoreSlim gate) : IDisposable
    {
        private int released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}

/// <summary>
/// A record held busy for a caller. Disposing it releases the record unless
/// the work it admitted now holds it; queued work releases it when it has run.
/// </summary>
public sealed class ProviderWorkReservation : IDisposable
{
    private readonly ProviderWorkQueue queue;
    private bool handedOver;
    private bool released;

    internal ProviderWorkReservation(ProviderWorkQueue queue, Guid key)
    {
        this.queue = queue;
        Key = key;
    }

    public Guid Key { get; }

    /// <summary>
    /// Queues the work under this reservation, which the work then holds until
    /// it has run. A full queue leaves the reservation with the caller.
    /// </summary>
    public ProviderWorkAdmission Admit(ProviderWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        ObjectDisposedException.ThrowIf(released || handedOver, this);
        if (work.Key != Key)
        {
            throw new ArgumentException("The work names another record than the reservation.", nameof(work));
        }
        if (!queue.TryWrite(work))
        {
            return ProviderWorkAdmission.Full;
        }

        handedOver = true;
        return ProviderWorkAdmission.Queued;
    }

    /// <summary>
    /// Runs the work here, under this reservation, after any work already
    /// running for the record: what a caller does when the queue is full.
    /// </summary>
    public async Task RunHereAsync(ProviderWork work, IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(services);
        ObjectDisposedException.ThrowIf(released || handedOver, this);
        using (await queue.EnterAsync(Key, cancellationToken))
        {
            await work.RunAsync(services, cancellationToken);
        }
    }

    public void Dispose()
    {
        if (handedOver || released)
        {
            return;
        }

        released = true;
        queue.Release(Key);
    }
}
