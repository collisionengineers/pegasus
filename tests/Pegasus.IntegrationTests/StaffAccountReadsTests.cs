using Microsoft.Extensions.Caching.Memory;
using Pegasus.Core.Actors;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

public sealed class StaffAccountReadsTests
{
    private static readonly StaffAccountRead Read = new("stamp", IsEnabled: true, MustChangePassword: false);

    [Fact]
    public async Task AReadIsKeptUntilItsLifetimeEnds()
    {
        var clock = new MutableClock(new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        var reads = new StaffAccountReads(TimeProviderMemoryCache.Create(clock));
        var staffId = Guid.NewGuid();
        await reads.GetOrReadAsync(staffId, Found, default);

        clock.Now += StaffSessionPolicy.AccountReadLifetime - TimeSpan.FromTicks(1);
        Assert.Same(Read, await reads.GetOrReadAsync(staffId, Missing, default));
        clock.Now += TimeSpan.FromTicks(1);
        Assert.Null(await reads.GetOrReadAsync(staffId, Missing, default));
    }

    [Fact]
    public async Task AnEvictionRemovesTheReadAndRefusesOneTakenBeforeIt()
    {
        var reads = new StaffAccountReads(new MemoryCache(new MemoryCacheOptions()));
        var staffId = Guid.NewGuid();
        await reads.GetOrReadAsync(staffId, Found, default);
        reads.Evict(staffId);
        Assert.Null(await reads.GetOrReadAsync(staffId, Missing, default));

        // A request read the old row, then an account change committed and evicted.
        var stale = await reads.GetOrReadAsync(
            staffId,
            _ =>
            {
                reads.Evict(staffId);
                return Found(default);
            },
            default);
        Assert.Same(Read, stale);
        Assert.Null(await reads.GetOrReadAsync(staffId, Missing, default));
    }

    [Fact]
    public async Task ANameIsKeptUntilTheLifetimeEnds()
    {
        var clock = new MutableClock(new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        var reads = new StaffAccountReads(TimeProviderMemoryCache.Create(clock));
        var staffId = Guid.NewGuid();
        await reads.GetOrReadNamesAsync([staffId], Alex, default);

        clock.Now += StaffSessionPolicy.AccountReadLifetime - TimeSpan.FromTicks(1);
        Assert.Equal("alex", (await reads.GetOrReadNamesAsync([staffId], NoNames, default))[staffId]);
        clock.Now += TimeSpan.FromTicks(1);
        Assert.Empty(await reads.GetOrReadNamesAsync([staffId], NoNames, default));
    }

    [Fact]
    public async Task AnEvictionRemovesTheNameAndRefusesOneReadBeforeIt()
    {
        var reads = new StaffAccountReads(new MemoryCache(new MemoryCacheOptions()));
        var staffId = Guid.NewGuid();
        await reads.GetOrReadNamesAsync([staffId], Alex, default);
        reads.Evict(staffId);
        Assert.Empty(await reads.GetOrReadNamesAsync([staffId], NoNames, default));

        // A name was read, then an account change committed and evicted.
        var stale = await reads.GetOrReadNamesAsync(
            [staffId],
            (staffIds, cancellationToken) =>
            {
                reads.Evict(staffId);
                return Alex(staffIds, cancellationToken);
            },
            default);
        Assert.Equal("alex", stale[staffId]);
        Assert.Empty(await reads.GetOrReadNamesAsync([staffId], NoNames, default));
    }

    private static Task<StaffAccountRead?> Found(CancellationToken _) => Task.FromResult<StaffAccountRead?>(Read);

    private static Task<StaffAccountRead?> Missing(CancellationToken _) => Task.FromResult<StaffAccountRead?>(null);

    private static Task<IReadOnlyDictionary<Guid, string>> Alex(
        IReadOnlyCollection<Guid> staffIds,
        CancellationToken _) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(staffIds.ToDictionary(staffId => staffId, staffId => "alex"));

    private static Task<IReadOnlyDictionary<Guid, string>> NoNames(
        IReadOnlyCollection<Guid> staffIds,
        CancellationToken _) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
