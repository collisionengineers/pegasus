using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Reports;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// A signatory's prepared signature is kept by the hash of the stored bytes
/// (efficiency register A3-17, R330): prepared once, bounded, and never kept
/// when its preparation fails.
/// </summary>
public sealed class PreparedSignatureCacheTests
{
    [Fact]
    public void TheSameStoredSignatureIsPreparedOnce()
    {
        var cache = new PreparedSignatureCache(4);
        var prepared = 0;
        byte[] Prepare(byte[] stored)
        {
            prepared++;
            return [.. stored, 0xFF];
        }

        var first = cache.GetOrPrepare([1, 2, 3], Prepare);
        var second = cache.GetOrPrepare([1, 2, 3], Prepare);

        Assert.Equal(1, prepared);
        Assert.Same(first, second);
        Assert.Equal<byte>([1, 2, 3, 0xFF], first);
    }

    [Fact]
    public void AChangedSignatureIsPreparedAgain()
    {
        var cache = new PreparedSignatureCache(4);
        var prepared = 0;
        byte[] Prepare(byte[] stored)
        {
            prepared++;
            return stored;
        }

        cache.GetOrPrepare([1], Prepare);
        cache.GetOrPrepare([2], Prepare);

        Assert.Equal(2, prepared);
    }

    [Fact]
    public void AFailedPreparationIsNotKept()
    {
        var cache = new PreparedSignatureCache(4);
        var attempts = 0;
        byte[] Prepare(byte[] stored)
        {
            attempts++;
            return attempts == 1
                ? throw new ReportRenderRejectedException("The report signature image could not be decoded.")
                : stored;
        }

        Assert.Throws<ReportRenderRejectedException>(() => cache.GetOrPrepare([9], Prepare));
        Assert.Equal(0, cache.Count);
        Assert.Equal<byte>([9], cache.GetOrPrepare([9], Prepare));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void TheCacheKeepsNoMoreThanItsCapacityAndDropsTheOldestFirst()
    {
        var cache = new PreparedSignatureCache(2);
        var prepared = 0;
        byte[] Prepare(byte[] stored)
        {
            prepared++;
            return stored;
        }

        cache.GetOrPrepare([1], Prepare);
        cache.GetOrPrepare([2], Prepare);
        cache.GetOrPrepare([3], Prepare);
        Assert.Equal(2, cache.Count);
        Assert.Equal(3, prepared);

        cache.GetOrPrepare([3], Prepare);
        cache.GetOrPrepare([2], Prepare);
        Assert.Equal(3, prepared);
        cache.GetOrPrepare([1], Prepare);
        Assert.Equal(4, prepared);
    }

    [Fact]
    public async Task ConcurrentRendersOfOneSignatureAllGetOneKeptResult()
    {
        var cache = new PreparedSignatureCache(4);
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ =>
            Task.Run(() => cache.GetOrPrepare([7, 7], stored => [.. stored]))));

        Assert.Single(results.Distinct());
        Assert.Equal(1, cache.Count);
    }
}
