using Microsoft.Extensions.Caching.Memory;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Core.Tests;

/// <summary>
/// A read that is kept until its writer evicts it, and never kept past an
/// eviction that landed while the read was running.
/// </summary>
public sealed class EvictableCacheEntryTests
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private static readonly object Key = new();

    [Fact]
    public async Task ARepeatReadIsServedFromTheCacheUntilTheEntryIsEvicted()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var reads = 0;
        Task<int> Read(CancellationToken _) => Task.FromResult(++reads);

        Assert.Equal(1, await Get(cache, Read));
        Assert.Equal(1, await Get(cache, Read));
        Assert.Equal(1, reads);

        EvictableCacheEntry.Evict(cache, Key);

        Assert.Equal(2, await Get(cache, Read));
        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task AReadThatWasRunningWhenTheEntryWasEvictedIsNeverKept()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var reads = 0;

        var stale = await Get(cache, _ =>
        {
            // The writer commits and evicts while this read is in flight, so
            // the value it returns was read before the commit.
            var value = ++reads;
            EvictableCacheEntry.Evict(cache, Key);
            return Task.FromResult(value);
        });

        Assert.Equal(1, stale);
        Assert.Equal(2, await Get(cache, _ => Task.FromResult(++reads)));
    }

    [Fact]
    public async Task AReadThatThrowsIsNotKept()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Get(cache, _ => throw new InvalidOperationException("read failed")));

        Assert.Equal(7, await Get(cache, _ => Task.FromResult(7)));
    }

    private static Task<int> Get(IMemoryCache cache, Func<CancellationToken, Task<int>> read) =>
        EvictableCacheEntry.GetOrReadAsync(
            cache, Key, Lifetime, slidingWindow: null, read, CancellationToken.None);
}
