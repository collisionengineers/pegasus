using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Workflow;

public sealed class EditScopeAuthorityTests
{
    [Fact]
    public void AdministratorMayClaimEveryApplicationEditScope()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        foreach (var kind in Enum.GetValues<EditScopeKind>())
        {
            EditScopeAuthority.RequireActor(kind, actor);
        }
    }

    [Theory]
    [InlineData(StaffRole.User)]
    [InlineData(StaffRole.Engineer)]
    public void OrdinaryStaffMayClaimCaseworkButNotAdministrationScopes(StaffRole role)
    {
        AssertCaseworkOnly(ActionActor.Staff(Guid.NewGuid(), [role]));
    }

    [Fact]
    public void AutomationMayClaimCaseworkButNotAdministrationScopes()
    {
        AssertCaseworkOnly(ActionActor.Automation("automation"));
    }

    private static void AssertCaseworkOnly(ActionActor actor)
    {
        foreach (var kind in Enum.GetValues<EditScopeKind>())
        {
            if (kind is EditScopeKind.Triage or EditScopeKind.ImageIntake)
            {
                EditScopeAuthority.RequireActor(kind, actor);
            }
            else
            {
                Assert.Throws<StaffAuthorizationException>(() => EditScopeAuthority.RequireActor(kind, actor));
            }
        }
    }
}
