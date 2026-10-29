using Microsoft.Extensions.Caching.Memory;
using Pegasus.Core.Actors;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The staff cookie check's account read and the staff display names, each
/// kept in <see cref="IMemoryCache"/> through <see cref="EvictableCacheEntry"/>
/// for <see cref="StaffSessionPolicy.AccountReadLifetime"/>. Every account
/// writer in this process evicts the account after its commit, so a change made
/// in Pegasus is seen on the next request; a change made outside it (direct
/// SQL, the intake wipe) is seen once the read expires. The Web runs as one
/// instance (ADR-0058), which is what makes in-process eviction exact. An id
/// with no account is never kept.
/// </summary>
public sealed class StaffAccountReads(IMemoryCache cache)
{
    // Private key types, so no other IMemoryCache user can collide with an entry.
    private readonly record struct ReadKey(Guid StaffId);

    private readonly record struct NameKey(Guid StaffId);

    /// <summary>
    /// The kept account read, or <paramref name="read"/>'s answer, kept unless
    /// it is null or an eviction landed while it ran.
    /// </summary>
    public Task<StaffAccountRead?> GetOrReadAsync(
        Guid staffId,
        Func<CancellationToken, Task<StaffAccountRead?>> read,
        CancellationToken cancellationToken) =>
        GetOrReadFoundAsync(new ReadKey(staffId), read, cancellationToken);

    /// <summary>
    /// The usernames of <paramref name="staffIds"/>: the kept ones, and the
    /// rest from one call of <paramref name="readNames"/>, each kept unless an
    /// eviction of its account landed while the call ran. An id
    /// <paramref name="readNames"/> does not answer is left out and not kept.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, string>> GetOrReadNamesAsync(
        IReadOnlyCollection<Guid> staffIds,
        Func<IReadOnlyCollection<Guid>, CancellationToken, Task<IReadOnlyDictionary<Guid, string>>> readNames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(staffIds);
        ArgumentNullException.ThrowIfNull(readNames);
        var names = new Dictionary<Guid, string>();
        var unkept = new List<Guid>();
        foreach (var staffId in staffIds.Distinct())
        {
            if (cache.TryGetValue(new NameKey(staffId), out string? kept) && kept is not null)
            {
                names[staffId] = kept;
            }
            else
            {
                unkept.Add(staffId);
            }
        }

        if (unkept.Count == 0)
        {
            return names;
        }

        // One read answers every unkept name, but each name takes its own
        // eviction signal when its entry starts reading. The read waits until
        // every entry has started, so all the signals are taken before it runs
        // and an eviction that lands during it refuses that account's name. It
        // starts only if an entry still misses when it begins reading.
        var signalsTaken = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oneRead = new Lazy<Task<IReadOnlyDictionary<Guid, string>>>(() =>
            ReadWhenSignalsAreTakenAsync(signalsTaken.Task, unkept, readNames, cancellationToken));
        var entries = unkept
            .Select(staffId => GetOrReadFoundAsync(
                new NameKey(staffId),
                async _ => (await oneRead.Value).GetValueOrDefault(staffId),
                cancellationToken))
            .ToArray();
        signalsTaken.SetResult();
        var read = await Task.WhenAll(entries);
        for (var index = 0; index < unkept.Count; index++)
        {
            if (read[index] is { } name)
            {
                names[unkept[index]] = name;
            }
        }

        return names;
    }

    public void Evict(Guid staffId)
    {
        EvictableCacheEntry.Evict(cache, new ReadKey(staffId));
        EvictableCacheEntry.Evict(cache, new NameKey(staffId));
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> ReadWhenSignalsAreTakenAsync(
        Task signalsTaken,
        IReadOnlyCollection<Guid> staffIds,
        Func<IReadOnlyCollection<Guid>, CancellationToken, Task<IReadOnlyDictionary<Guid, string>>> readNames,
        CancellationToken cancellationToken)
    {
        await signalsTaken;
        return await readNames(staffIds, cancellationToken);
    }

    // EvictableCacheEntry keeps whatever the read returns, so a null answer
    // (no such account) is dropped again and read afresh on the next call.
    private async Task<TValue?> GetOrReadFoundAsync<TValue>(
        object key,
        Func<CancellationToken, Task<TValue?>> read,
        CancellationToken cancellationToken)
        where TValue : class
    {
        var value = await EvictableCacheEntry.GetOrReadAsync(
            cache,
            key,
            StaffSessionPolicy.AccountReadLifetime,
            slidingWindow: null,
            read,
            cancellationToken);
        if (value is null)
        {
            cache.Remove(key);
        }

        return value;
    }
}

/// <summary>The account fields the staff cookie check needs.</summary>
public sealed record StaffAccountRead(string? SecurityStamp, bool IsEnabled, bool MustChangePassword);
