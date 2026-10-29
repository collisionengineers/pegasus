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

    private static readonly ActionActor Colleague = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
    private static readonly ActionActor Automation = ActionActor.Automation("automation");
    private static readonly ActionActor OtherAutomation = ActionActor.Automation("automation-other");

    /// <summary>
    /// A scope refuses only an expectation newer than the version it is checked against, as a
    /// Case lease does; an older one is not the scope's refusal (R516, 29 October 2026).
    /// </summary>
    [Fact]
    public void OnlyANewerExpectedVersionIsRefused()
    {
        EditScopeAuthority.RequireVersion(EditScopeKind.Triage, RecordId, currentVersion: 4, expectedVersion: 4);
        EditScopeAuthority.RequireVersion(EditScopeKind.Triage, RecordId, currentVersion: 4, expectedVersion: 3);

        var exception = Assert.Throws<EditScopeVersionConflictException>(() =>
            EditScopeAuthority.RequireVersion(EditScopeKind.Triage, RecordId, currentVersion: 3, expectedVersion: 4));
        Assert.Equal(4, exception.ExpectedVersion);
        Assert.Equal(3, exception.ActualVersion);
    }

    /// <summary>
    /// No retained scope, or a missing or malformed token, is expired before the holder is asked;
    /// on a live scope another holder or a non-matching token is a conflict. The token is compared
    /// only once the holder has been matched.
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

        Assert.Throws<EditScopeExpiredException>(() => EditScopeAuthority.RequireScope(
            EditScopeKind.ImageIntake, RecordId, Holder, Token, null, null, null, Matches, Now));
        Assert.Throws<EditScopeExpiredException>(() => Require(Holder, null, ActorKind.Staff, live, Matches));
        Assert.Throws<EditScopeExpiredException>(() => Require(Holder, "short", ActorKind.Staff, live, Matches));
        Assert.Throws<EditScopeConflictException>(() => Require(Colleague, Token, ActorKind.Staff, live, Matches));
        Assert.Throws<EditScopeConflictException>(() => Require(Holder, Token, null, live, Matches));
        Assert.Throws<EditScopeConflictException>(() => Require(Holder, Token, ActorKind.Automation, live, Matches));
        Assert.Equal(1, compared);

        Assert.Throws<EditScopeConflictException>(() => Require(Holder, Token, ActorKind.Staff, live, _ => false));
    }

    /// <summary>
    /// A holder whose scope lapsed but whose token still matches carries on, as a Case lease's
    /// does: nobody has claimed the record since (R515, 29 October 2026). A lapsed scope that is
    /// not the presenter's, or whose token does not match, is expired.
    /// </summary>
    [Fact]
    public void ALapsedScopeWhoseTokenStillMatchesCarriesOn()
    {
        void Require(ActionActor actor, DateTimeOffset expires, bool matches) =>
            EditScopeAuthority.RequireScope(
                EditScopeKind.Triage, RecordId, actor, Token, ActorKind.Staff, Holder.SubjectId, expires,
                _ => matches, Now);

        Require(Holder, Now, matches: true);
        Require(Holder, Now.AddMinutes(-10), matches: true);
        EditScopeAuthority.RequireScope(
            EditScopeKind.Triage, RecordId, Automation, Token, ActorKind.Automation, Automation.SubjectId,
            Now.AddMinutes(-1), _ => true, Now);

        Assert.Throws<EditScopeExpiredException>(() => Require(Holder, Now.AddMinutes(-1), matches: false));
        Assert.Throws<EditScopeExpiredException>(() => Require(Colleague, Now.AddMinutes(-1), matches: true));
    }

    /// <summary>
    /// A scope nobody holds is claimed afresh, whoever held it last. A live scope refuses a claim
    /// that does not ask to take it over, the holder's own one-off claim included.
    /// </summary>
    [Fact]
    public void ALiveScopeRefusesAClaimThatDoesNotAskForIt()
    {
        Assert.Equal(EditScopeClaim.New, Claim(Holder, takeOver: false, ActorKind.Staff, Colleague.SubjectId, Now));
        Assert.Equal(EditScopeClaim.New, Claim(Holder, takeOver: true, null, null, null));
        Assert.Equal(
            EditScopeClaim.New,
            Claim(Automation, takeOver: false, ActorKind.Automation, Automation.SubjectId, Now.AddMinutes(-1)));

        Assert.Throws<EditScopeConflictException>(() =>
            Claim(Holder, takeOver: false, ActorKind.Staff, Holder.SubjectId, Now.AddMinutes(1)));
        Assert.Throws<EditScopeConflictException>(() =>
            Claim(Colleague, takeOver: false, ActorKind.Staff, Holder.SubjectId, Now.AddMinutes(1)));
    }

    /// <summary>
    /// Asked, the staff holder's own live scope resumes: the same token comes back and nothing is
    /// recorded, as a Case lease resumes (R518, 29 October 2026). The Automation Actor never
    /// resumes.
    /// </summary>
    [Fact]
    public void ClaimingYourOwnLiveScopeAgainResumesIt()
    {
        Assert.Equal(
            EditScopeClaim.Resume,
            Claim(Holder, takeOver: true, ActorKind.Staff, Holder.SubjectId, Now.AddMinutes(1)));
        Assert.Throws<EditScopeConflictException>(() =>
            Claim(Automation, takeOver: true, ActorKind.Automation, Automation.SubjectId, Now.AddMinutes(1)));
    }

    /// <summary>
    /// Take over follows the Case rule: a staff-held scope is taken over by a colleague or the
    /// Automation Actor, an Automation-held one by staff only, and a holder whose kind was never
    /// recorded by nobody (R517, 29 October 2026).
    /// </summary>
    [Fact]
    public void AutomationNeverTakesOverAnAutomationHolder()
    {
        var live = Now.AddMinutes(1);
        Assert.Equal(EditScopeClaim.TakeOver, Claim(Colleague, takeOver: true, ActorKind.Staff, Holder.SubjectId, live));
        Assert.Equal(EditScopeClaim.TakeOver, Claim(Automation, takeOver: true, ActorKind.Staff, Holder.SubjectId, live));
        Assert.Equal(
            EditScopeClaim.TakeOver,
            Claim(Holder, takeOver: true, ActorKind.Automation, Automation.SubjectId, live));

        Assert.Throws<EditScopeConflictException>(() =>
            Claim(OtherAutomation, takeOver: true, ActorKind.Automation, Automation.SubjectId, live));
        Assert.Throws<EditScopeConflictException>(() =>
            Claim(Colleague, takeOver: true, null, Holder.SubjectId, live));
    }

    /// <summary>
    /// A replayed claim gets the scope its first claim issued while that scope is live; a lapsed
    /// one is expired and another holder's a conflict (R519, 29 October 2026).
    /// </summary>
    [Fact]
    public void AReplayedClaimReturnsOnlyTheLiveScopeItIssued()
    {
        EditScopeAuthority.RequireReplay(
            EditScopeKind.Triage, RecordId, Automation, ActorKind.Automation, Automation.SubjectId,
            Now.AddMinutes(1), Now);

        Assert.Throws<EditScopeExpiredException>(() => EditScopeAuthority.RequireReplay(
            EditScopeKind.Triage, RecordId, Automation, ActorKind.Automation, Automation.SubjectId,
            Now, Now));
        Assert.Throws<EditScopeConflictException>(() => EditScopeAuthority.RequireReplay(
            EditScopeKind.Triage, RecordId, OtherAutomation, ActorKind.Automation, Automation.SubjectId,
            Now.AddMinutes(1), Now));
    }

    private static EditScopeClaim Claim(
        ActionActor actor,
        bool takeOver,
        ActorKind? retainedHolderKind,
        string? retainedHolder,
        DateTimeOffset? expiresAtUtc) =>
        EditScopeAuthority.RequireClaim(
            EditScopeKind.Triage, RecordId, actor, takeOver, retainedHolderKind, retainedHolder, expiresAtUtc, Now);

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
