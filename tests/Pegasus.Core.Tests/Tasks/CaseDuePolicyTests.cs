using Pegasus.Core.Tasks;

namespace Pegasus.Core.Tests.Tasks;

public sealed class CaseDuePolicyTests
{
    [Fact]
    public void AStaffDueDateTakesPrecedenceUntilItIsCleared()
    {
        var acceptedDeadline = new DateOnly(2031, 5, 20);
        var staffDueBy = new DateOnly(2031, 5, 18);

        Assert.Equal(staffDueBy, CaseDuePolicy.Resolve(staffDueBy, acceptedDeadline));
        Assert.Equal(acceptedDeadline, CaseDuePolicy.Resolve(null, acceptedDeadline));
        Assert.Null(CaseDuePolicy.Resolve(null, null));
    }

    [Fact]
    public void TheDueInstantIsTheNextChaseElseTheEndOfTheDueByDate()
    {
        var chase = new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero);
        var dueBy = new DateOnly(2026, 10, 2);

        // A chase time wins over a Due by date.
        Assert.Equal(chase, CaseDuePolicy.DueAt(chase, dueBy));
        // A Due by date is due at the Europe/London midnight that ends it.
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 23, 0, 0, TimeSpan.Zero), CaseDuePolicy.DueAt(null, dueBy));
        Assert.Null(CaseDuePolicy.DueAt(null, null));
    }
}
