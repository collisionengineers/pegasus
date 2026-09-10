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

    /// <summary>
    /// The staleness rule the store applies to a holder's own live scope.
    /// Three missed renewals are the proof that the window which claimed it is
    /// gone; anything sooner is a second live window and the holder is asked.
    /// A hidden tab throttles its heartbeat timer, so one or even two missed
    /// renewals must not be read as staleness.
    /// </summary>
    [Fact]
    public void AScopeIsStaleOnlyAfterThreeMissedRenewals()
    {
        var claimedAt = new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);
        var expiresAt = claimedAt.Add(EditScopeAuthority.Duration);

        Assert.Equal(claimedAt, EditScopeAuthority.LastHeartbeatUtc(expiresAt));
        Assert.Equal(
            EditScopeAuthority.HeartbeatInterval + EditScopeAuthority.HeartbeatInterval
                + EditScopeAuthority.HeartbeatInterval,
            EditScopeAuthority.StaleAfter);
        Assert.False(EditScopeAuthority.IsStale(expiresAt, claimedAt));
        Assert.False(EditScopeAuthority.IsStale(
            expiresAt, claimedAt.Add(EditScopeAuthority.HeartbeatInterval)));
        Assert.False(EditScopeAuthority.IsStale(
            expiresAt, claimedAt.Add(EditScopeAuthority.HeartbeatInterval + EditScopeAuthority.HeartbeatInterval)));
        Assert.False(EditScopeAuthority.IsStale(
            expiresAt, claimedAt.Add(EditScopeAuthority.StaleAfter)));
        Assert.True(EditScopeAuthority.IsStale(
            expiresAt, claimedAt.Add(EditScopeAuthority.StaleAfter) + TimeSpan.FromSeconds(1)));
    }

    /// <summary>
    /// A scope stays claimable by its own holder as it ages: staleness is a
    /// window inside the lease, not a second expiry, so the rule still applies
    /// while <see cref="EditScopeAuthority.IsHeld"/> is true.
    /// </summary>
    [Fact]
    public void StalenessIsReachedWellBeforeTheScopeExpires()
    {
        var claimedAt = new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);
        var expiresAt = claimedAt.Add(EditScopeAuthority.Duration);
        var stale = claimedAt.Add(EditScopeAuthority.StaleAfter) + TimeSpan.FromSeconds(1);

        Assert.True(EditScopeAuthority.StaleAfter < EditScopeAuthority.Duration);
        Assert.True(EditScopeAuthority.IsHeld(expiresAt, stale));
        Assert.True(EditScopeAuthority.IsStale(expiresAt, stale));
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
