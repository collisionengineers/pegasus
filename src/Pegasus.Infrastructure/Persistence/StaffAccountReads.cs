using Microsoft.Extensions.Caching.Memory;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The account read the staff cookie check and the Pegasus Desktop token check
/// share (<c>EfStaffAccountQueries.GetAccountReadAsync</c>), the staff display names, the eligible
/// sign-off Engineer list and each sign-off Engineer's signature, each kept in
/// <see cref="IMemoryCache"/> through <see cref="EvictableCacheEntry"/> for
/// <see cref="StaffSessionPolicy.AccountReadLifetime"/>. Every account writer in
/// this process evicts the account after its commit, and with it the sign-off
/// list (one settings save can change two accounts: the new default and the
/// previous one), so a change made in Pegasus is seen on the next request; a
/// change made outside it (direct SQL, the intake wipe) is seen once the read
/// expires. The Web runs as one instance (ADR-0058), which is what makes
/// in-process eviction exact. An id with no account, and an account with no
/// eligible signature, is never kept. The reads a store makes inside its own
/// transaction (<c>EfStaffAccountQueries</c>'s static methods) never come here.
/// </summary>
public sealed class StaffAccountReads(IMemoryCache cache)
{
    // Private key types, so no other IMemoryCache user can collide with an entry.
    private readonly record struct ReadKey(Guid StaffId);

    private readonly record struct NameKey(Guid StaffId);

    private readonly record struct SignatureKey(Guid StaffId);

    // One list for the process: eligibility, printed name and default flag all
    // live on the accounts, so any account write evicts it (#1950).
    private static readonly object SignOffEngineersKey = new();

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

    /// <summary>
    /// The kept eligible sign-off Engineer list, or <paramref name="read"/>'s
    /// answer, kept (an empty list included) until any account write evicts it.
    /// </summary>
    public Task<IReadOnlyList<SignOffEngineerProfile>> GetOrReadSignOffEngineersAsync(
        Func<CancellationToken, Task<IReadOnlyList<SignOffEngineerProfile>>> read,
        CancellationToken cancellationToken) =>
        EvictableCacheEntry.GetOrReadAsync(
            cache,
            SignOffEngineersKey,
            StaffSessionPolicy.AccountReadLifetime,
            slidingWindow: null,
            read,
            cancellationToken);

    /// <summary>
    /// The kept signature of one sign-off Engineer (up to 1 MiB), or
    /// <paramref name="read"/>'s answer, kept unless it is null (not an eligible
    /// sign-off Engineer) or an eviction of the account landed while it ran.
    /// </summary>
    public Task<SignOffSignature?> GetOrReadSignOffSignatureAsync(
        Guid staffId,
        Func<CancellationToken, Task<SignOffSignature?>> read,
        CancellationToken cancellationToken) =>
        GetOrReadFoundAsync(new SignatureKey(staffId), read, cancellationToken);

    public void Evict(Guid staffId)
    {
        EvictableCacheEntry.Evict(cache, new ReadKey(staffId));
        EvictableCacheEntry.Evict(cache, new NameKey(staffId));
        EvictableCacheEntry.Evict(cache, new SignatureKey(staffId));
        EvictableCacheEntry.Evict(cache, SignOffEngineersKey);
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

/// <summary>
/// The account fields the staff cookie check and the Pegasus Desktop token
/// check need (ADR-0092). <paramref name="Role"/> is null for an account that
/// does not hold exactly one staff role, which is no member of staff.
/// </summary>
public sealed record StaffAccountRead(
    string? SecurityStamp,
    bool IsEnabled,
    bool MustChangePassword,
    StaffRole? Role);
