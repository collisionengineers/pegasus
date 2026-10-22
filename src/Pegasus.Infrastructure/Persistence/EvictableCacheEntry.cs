using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One <see cref="IMemoryCache"/> entry that its writers evict after they
/// commit, as <c>EfReleaseNoteStore</c> keeps each person's release note. A read
/// takes the entry's signal before it reads and stores its value with that
/// signal as an expiration token, so an eviction that lands during the read
/// cancels the signal and the value is stored already expired: a row read
/// before a commit never outlives the commit's eviction.
/// </summary>
internal static class EvictableCacheEntry
{
    private static readonly Lock SignalGate = new();

    // The signal's own key, so no other IMemoryCache user can collide with it.
    private readonly record struct SignalKey(object Key);

    public static async Task<TValue> GetOrReadAsync<TValue>(
        IMemoryCache cache,
        object key,
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
        var signal = TakeSignal(cache, key);
        var value = await read(cancellationToken);
        cache.Set(
            key,
            value,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absoluteLifetime,
                SlidingExpiration = slidingWindow
            }.AddExpirationToken(new CancellationChangeToken(signal)));
        return value;
    }

    public static void Evict(IMemoryCache cache, object key)
    {
        lock (SignalGate)
        {
            var signalKey = new SignalKey(key);
            if (cache.Get(signalKey) is CancellationTokenSource signal)
            {
                cache.Remove(signalKey);
                signal.Cancel();
            }
        }

        cache.Remove(key);
    }

    private static CancellationToken TakeSignal(IMemoryCache cache, object key)
    {
        lock (SignalGate)
        {
            var signalKey = new SignalKey(key);
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
}
