using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class GetIntakeTests
{
    private static readonly DateTimeOffset NowUtc = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ManyReceiptsNeedCaseworkAndAnIdentifierEach()
    {
        var getIntake = new GetIntake(new Receipts());

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            getIntake.ExecuteManyAsync(
                [Guid.NewGuid()],
                ActionActor.Principal(Guid.NewGuid()),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            getIntake.ExecuteManyAsync(
                [Guid.NewGuid(), Guid.Empty],
                Caseworker(),
                CancellationToken.None));
    }

    [Fact]
    public async Task ManyReceiptsAreOneReadOfTheStoreNotOneEach()
    {
        var first = Receipt();
        var second = Receipt();
        var receipts = new BatchReceipts(first, second);

        var found = await new GetIntake(receipts).ExecuteManyAsync(
            [second.Id, first.Id, Guid.NewGuid()],
            Caseworker(),
            CancellationToken.None);

        Assert.Equal(1, receipts.ManyReads);
        Assert.Equal(0, receipts.SingleReads);
        Assert.Equal(new[] { second.Id, first.Id }, found.Select(receipt => receipt.Id));
    }

    [Fact]
    public async Task AStoreWithoutABatchReadAsksForEachReceiptOnceInTheOrderGiven()
    {
        var first = Receipt();
        var second = Receipt();
        var receipts = new Receipts(first, second);

        var found = await new GetIntake(receipts).ExecuteManyAsync(
            [second.Id, first.Id, Guid.NewGuid(), second.Id],
            Caseworker(),
            CancellationToken.None);

        Assert.Equal(3, receipts.SingleReads);
        Assert.Equal(new[] { second.Id, first.Id }, found.Select(receipt => receipt.Id));
    }

    private static ActionActor Caseworker() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    private static IntakeReceipt Receipt() => new(
        Guid.NewGuid(),
        "instruction.pdf",
        "application/pdf",
        1,
        new string('a', 64),
        new(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N")),
        NowUtc,
        NowUtc,
        IntakeDecision.NeedsSorting,
        "Needs sorting.",
        [],
        [],
        null,
        [],
        null,
        null,
        false,
        "test-reader",
        "1",
        null,
        null);

    /// <summary>A receipt store with no batch read of its own, so the interface's default applies.</summary>
    private class Receipts(params IntakeReceipt[] known) : IIntakeReceiptQueries
    {
        private int singleReads;

        public int SingleReads => Volatile.Read(ref singleReads);

        protected IntakeReceipt[] Known { get; } = known;

        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref singleReads);
            return Task.FromResult(Known.SingleOrDefault(receipt => receipt.Id == id));
        }
    }

    /// <summary>A store that answers a batch in one call, as the SQL store does.</summary>
    private sealed class BatchReceipts(params IntakeReceipt[] known) : Receipts(known), IIntakeReceiptQueries
    {
        private int manyReads;

        public int ManyReads => Volatile.Read(ref manyReads);

        public Task<IReadOnlyList<IntakeReceipt>> GetManyAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref manyReads);
            IReadOnlyList<IntakeReceipt> found =
                [.. ids.Distinct().Select(id => Known.SingleOrDefault(receipt => receipt.Id == id)).OfType<IntakeReceipt>()];
            return Task.FromResult(found);
        }
    }
}
