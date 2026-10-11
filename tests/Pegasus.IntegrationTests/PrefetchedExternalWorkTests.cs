using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

public sealed class PrefetchedExternalWorkTests
{
    [Fact]
    public void TheOfferedRowIsHandedOverOnceAndOnlyToItsOwnIdentifier()
    {
        var row = new ExternalWorkItemEntity { Id = Guid.NewGuid(), Kind = "create_case_custody", OperationKey = "k", State = "Queued" };
        var slot = new PrefetchedExternalWork();

        slot.Offer(row);
        Assert.Null(slot.Take(Guid.NewGuid()));

        slot.Offer(row);
        Assert.Same(row, slot.Take(row.Id));
        Assert.Null(slot.Take(row.Id));
    }
}
