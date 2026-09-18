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
}
