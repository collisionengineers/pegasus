using Pegasus.Core.Actors;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Identity;

public sealed class StaffActorFactoryTests
{
    [Fact]
    public void StaffActorsRequireExactlyOneRecognizedRole()
    {
        var staffId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => ActionActor.Staff(staffId, []));
        Assert.Throws<ArgumentException>(() =>
            ActionActor.Staff(staffId, [StaffRole.Administrator, StaffRole.Engineer]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActionActor.Staff(staffId, [(StaffRole)99]));
    }

    [Fact]
    public void ClaimTranslationRejectsZeroMultipleAndUnknownRoles()
    {
        var staffId = Guid.NewGuid().ToString("D");

        Assert.False(StaffActorFactory.TryCreate(staffId, [], out var none));
        Assert.Null(none);
        Assert.False(StaffActorFactory.TryCreate(
            staffId,
            [StaffRole.Administrator.ToString(), StaffRole.Engineer.ToString()],
            out var multiple));
        Assert.Null(multiple);
        Assert.False(StaffActorFactory.TryCreate(staffId, ["Unknown"], out var unknown));
        Assert.Null(unknown);
    }

    [Fact]
    public void AScalarAdministratorClaimRetainsOneRoleAndInheritsEngineerCapability()
    {
        var staffId = Guid.NewGuid();

        Assert.True(StaffActorFactory.TryCreate(
            staffId.ToString("D"),
            [StaffRole.Administrator.ToString()],
            out var actor));

        Assert.Equal([StaffRole.Administrator], actor!.Roles);
        Assert.True(actor.IsInRole(StaffRole.Engineer));
    }
}
