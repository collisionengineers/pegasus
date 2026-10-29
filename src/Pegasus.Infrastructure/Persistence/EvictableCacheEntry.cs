using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One <see cref="IMemoryCache"/> entry that its writers evict after they
/// commit, as <c>EfReleaseNoteStore</c> keeps each person's release note. A read
/// takes the entry's signal before it reads and stores its value with that
/// signal as an expiration token, so an eviction that lands during the read
/// cancels the signal and the value is stored already expired: a row read
/// before a commit never outlives the commit's eviction. An entry read with a
/// family also takes the family's signal, so one <see cref="EvictFamily"/>
/// expires every entry of that family at once, the same way.
/// </summary>
internal static class EvictableCacheEntry
{
    private static readonly Lock SignalGate = new();

    // The signals' own keys, so no other IMemoryCache user can collide with them.
    private readonly record struct SignalKey(object Key);

    private readonly record struct FamilySignalKey(object Family);

    public static Task<TValue> GetOrReadAsync<TValue>(
        IMemoryCache cache,
        object key,
        TimeSpan absoluteLifetime,
        TimeSpan? slidingWindow,
        Func<CancellationToken, Task<TValue>> read,
        CancellationToken cancellationToken) =>
        GetOrReadAsync(cache, key, family: null, absoluteLifetime, slidingWindow, read, cancellationToken);

    public static async Task<TValue> GetOrReadAsync<TValue>(
        IMemoryCache cache,
        object key,
        object? family,
        TimeSpan absoluteLifetime,
        TimeSpan? slidingWindow,
        Func<CancellationToken, Task<TValue>> read,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(key, out TValue? kept))
        {
            return kept!;
        }

        // Taken before the read: an eviction that lands during it cancels the
        // token, and an entry stored with a cancelled token is already expired.
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteLifetime,
            SlidingExpiration = slidingWindow
        }.AddExpirationToken(new CancellationChangeToken(TakeSignal(cache, new SignalKey(key))));
        if (family is not null)
        {
            // Entries cannot be listed, so each of a family is stored with the
            // token of the one signal EvictFamily cancels.
            options.AddExpirationToken(new CancellationChangeToken(TakeSignal(cache, new FamilySignalKey(family))));
        }

        var value = await read(cancellationToken);
        cache.Set(key, value, options);
        return value;
    }

    public static void Evict(IMemoryCache cache, object key)
    {
        CancelSignal(cache, new SignalKey(key));
        cache.Remove(key);
    }

    public static void EvictFamily(IMemoryCache cache, object family) =>
        CancelSignal(cache, new FamilySignalKey(family));

    private static CancellationToken TakeSignal(IMemoryCache cache, object signalKey)
    {
        lock (SignalGate)
        {
            if (cache.Get(signalKey) is not CancellationTokenSource signal)
            {
                signal = new CancellationTokenSource();
                cache.Set(
                    signalKey,
                    signal,
                    new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
            }

            return signal.Token;
        }
    }

    private static void CancelSignal(IMemoryCache cache, object signalKey)
    {
        lock (SignalGate)
        {
            if (cache.Get(signalKey) is CancellationTokenSource signal)
            {
                cache.Remove(signalKey);
                signal.Cancel();
            }
        }
    }
}
