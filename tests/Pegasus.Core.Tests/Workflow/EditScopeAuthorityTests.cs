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

    private static readonly Guid RecordId = Guid.Parse("7d2c4a1e-5b3f-4e8a-9c6d-1f2e3a4b5c6d");
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly ActionActor Holder = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
    private static readonly string Token = new('a', CaseEditAuthority.LeaseTokenLength);

    [Fact]
    public void AnyVersionDifferenceIsRefused()
    {
        EditScopeAuthority.RequireVersion(EditScopeKind.Triage, RecordId, 4, 4);

        var exception = Assert.Throws<EditScopeVersionConflictException>(() =>
            EditScopeAuthority.RequireVersion(EditScopeKind.Triage, RecordId, currentVersion: 3, expectedVersion: 4));
        Assert.Equal(4, exception.ExpectedVersion);
        Assert.Equal(3, exception.ActualVersion);
    }

    /// <summary>
    /// No live scope, or a missing or malformed token, is expired before the holder is asked;
    /// another holder or a non-matching token is a conflict, and the token is compared only
    /// once every other check has passed.
    /// </summary>
    [Fact]
    public void AScopeIsRefusedInItsOneOrder()
    {
        var compared = 0;
        bool Matches(string _) { compared++; return true; }
        void Require(ActionActor actor, string? token, ActorKind? kind, DateTimeOffset? expires, Func<string, bool> matches) =>
            EditScopeAuthority.RequireScope(
                EditScopeKind.ImageIntake, RecordId, actor, token, kind, Holder.SubjectId, expires, matches, Now);
        var live = Now.AddMinutes(1);

        Require(Holder, Token, ActorKind.Staff, live, Matches);
        Assert.Equal(1, compared);

        Assert.Throws<EditScopeExpiredException>(() => Require(Holder, Token, ActorKind.Staff, null, Matches));
        Assert.Throws<EditScopeExpiredException>(() => Require(Holder, Token, ActorKind.Staff, Now, Matches));
        Assert.Throws<EditScopeExpiredException>(() => Require(Holder, null, ActorKind.Staff, live, Matches));
        Assert.Throws<EditScopeExpiredException>(() => Require(Holder, "short", ActorKind.Staff, live, Matches));
        Assert.Throws<EditScopeConflictException>(() =>
            Require(ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), Token, ActorKind.Staff, live, Matches));
        Assert.Throws<EditScopeConflictException>(() => Require(Holder, Token, null, live, Matches));
        Assert.Throws<EditScopeConflictException>(() => Require(Holder, Token, ActorKind.Automation, live, Matches));
        Assert.Equal(1, compared);

        Assert.Throws<EditScopeConflictException>(() => Require(Holder, Token, ActorKind.Staff, live, _ => false));
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
