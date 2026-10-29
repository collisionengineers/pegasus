using Microsoft.Extensions.Caching.Memory;
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
    private const int Capacity = QuestPdfAssessmentReportRenderer.SignatureCapacity;

    [Fact]
    public void TheSameStoredSignatureIsPreparedOnce()
    {
        using var signatures = new KeptSignatures();
        var prepared = 0;
        byte[] Prepare(byte[] stored)
        {
            prepared++;
            return [.. stored, 0xFF];
        }

        var first = signatures.Renderer.GetOrPrepareSignature([1, 2, 3], Prepare);
        var second = signatures.Renderer.GetOrPrepareSignature([1, 2, 3], Prepare);

        Assert.Equal(1, prepared);
        Assert.Same(first, second);
        Assert.Equal<byte>([1, 2, 3, 0xFF], first);
    }

    [Fact]
    public void AChangedSignatureIsPreparedAgain()
    {
        using var signatures = new KeptSignatures();
        var prepared = 0;
        byte[] Prepare(byte[] stored)
        {
            prepared++;
            return stored;
        }

        signatures.Renderer.GetOrPrepareSignature([1], Prepare);
        signatures.Renderer.GetOrPrepareSignature([2], Prepare);

        Assert.Equal(2, prepared);
    }

    [Fact]
    public void AFailedPreparationIsNotKept()
    {
        using var signatures = new KeptSignatures();
        var attempts = 0;
        byte[] Prepare(byte[] stored)
        {
            attempts++;
            return attempts == 1
                ? throw new ReportRenderRejectedException("The report signature image could not be decoded.")
                : stored;
        }

        Assert.Throws<ReportRenderRejectedException>(() => signatures.Renderer.GetOrPrepareSignature([9], Prepare));
        Assert.Equal(0, signatures.Cache.Count);
        Assert.Equal<byte>([9], signatures.Renderer.GetOrPrepareSignature([9], Prepare));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void TheCacheKeepsNoMoreThanItsCapacityAndDropsTheOldestFirst()
    {
        using var signatures = new KeptSignatures();
        var prepared = 0;
        byte[] Prepare(byte[] stored)
        {
            prepared++;
            return stored;
        }

        for (var stored = 1; stored <= Capacity + 1; stored++)
        {
            signatures.Renderer.GetOrPrepareSignature([(byte)stored], Prepare);
        }
        Assert.Equal(Capacity, signatures.Cache.Count);
        Assert.Equal(Capacity + 1, prepared);

        signatures.Renderer.GetOrPrepareSignature([Capacity + 1], Prepare);
        signatures.Renderer.GetOrPrepareSignature([2], Prepare);
        Assert.Equal(Capacity + 1, prepared);
        signatures.Renderer.GetOrPrepareSignature([1], Prepare);
        Assert.Equal(Capacity + 2, prepared);
    }

    [Fact]
    public async Task ConcurrentRendersOfOneSignatureAllGetOneKeptResult()
    {
        using var signatures = new KeptSignatures();
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ =>
            Task.Run(() => signatures.Renderer.GetOrPrepareSignature([7, 7], stored => [.. stored]))));

        Assert.Single(results.Distinct());
        Assert.Equal(1, signatures.Cache.Count);
    }

    /// <summary>The renderer over its own cache, so a fact can count the kept signatures.</summary>
    private sealed class KeptSignatures : IDisposable
    {
        private readonly ReportRenderGate gate = new();

        public KeptSignatures()
        {
            Renderer = new QuestPdfAssessmentReportRenderer(gate, Cache);
        }

        public MemoryCache Cache { get; } = new(new MemoryCacheOptions());

        public QuestPdfAssessmentReportRenderer Renderer { get; }

        public void Dispose()
        {
            Cache.Dispose();
            gate.Dispose();
        }
    }
}
