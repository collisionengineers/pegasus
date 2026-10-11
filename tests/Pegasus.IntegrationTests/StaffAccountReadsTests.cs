using Pegasus.Core.Actors;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

public sealed class StaffAccountReadsTests
{
    private static readonly StaffAccountRead Read = new("stamp", IsEnabled: true, MustChangePassword: false);

    [Fact]
    public void AReadIsKeptUntilItsLifetimeEnds()
    {
        var clock = new MutableClock(new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero));
        var reads = new StaffAccountReads(clock);
        var staffId = Guid.NewGuid();
        reads.Set(staffId, Read, reads.Generation);

        clock.Now += StaffSessionPolicy.AccountReadLifetime - TimeSpan.FromTicks(1);
        Assert.Same(Read, reads.TryGet(staffId));
        clock.Now += TimeSpan.FromTicks(1);
        Assert.Null(reads.TryGet(staffId));
    }

    [Fact]
    public void AnEvictionRemovesTheReadAndRefusesOneTakenBeforeIt()
    {
        var reads = new StaffAccountReads(TimeProvider.System);
        var staffId = Guid.NewGuid();
        reads.Set(staffId, Read, reads.Generation);
        reads.Evict(staffId);
        Assert.Null(reads.TryGet(staffId));

        // A request read the old row, then an account change committed and evicted.
        var generation = reads.Generation;
        reads.Evict(staffId);
        reads.Set(staffId, Read, generation);
        Assert.Null(reads.TryGet(staffId));
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
