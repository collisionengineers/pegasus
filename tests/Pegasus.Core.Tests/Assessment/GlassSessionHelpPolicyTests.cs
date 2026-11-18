using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// Which Glass's session the Automation's tools act in on a Case (design 08
/// section 5; R1170): its own live session, or an Engineer's Active session
/// with help on; refused with the glass.help.* code otherwise.
/// </summary>
public sealed class GlassSessionHelpPolicyTests
{
    private static readonly DateTimeOffset Start = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    private static readonly Guid EngineerId = Guid.NewGuid();

    [Fact]
    public void ACaseWithNoSessionHasNone()
    {
        var target = GlassSessionHelpPolicy.Find(new(null, []), null);

        Assert.Null(target.Session);
        Assert.Equal(GlassSessionHelpPolicy.NoSessionCode, target.Code);
    }

    [Fact]
    public void AnEngineersActiveSessionWithHelpOffIsOff()
    {
        var engineers = Staff(GlassRepairEstimateSessionState.Active, helpOn: false);

        var target = GlassSessionHelpPolicy.Find(new(null, [engineers]), null);

        Assert.Null(target.Session);
        Assert.Equal(GlassSessionHelpPolicy.OffCode, target.Code);
        Assert.Equal(GlassSessionHelpPolicy.OffCode, GlassSessionHelpPolicy.Find(new(null, [engineers]), engineers.Id).Code);
    }

    [Fact]
    public void AnEngineersActiveSessionWithHelpOnIsTheOne()
    {
        var engineers = Staff(GlassRepairEstimateSessionState.Active, helpOn: true);
        var ended = Automation(GlassRepairEstimateSessionState.Completed);

        var target = GlassSessionHelpPolicy.Find(new(ended, [engineers]), null);

        Assert.Same(engineers, target.Session);
        Assert.Null(target.Code);
        Assert.True(target.IsStaff);
    }

    [Fact]
    public void TheAutomationsOwnLiveSessionIsTheOne()
    {
        var own = Automation(GlassRepairEstimateSessionState.Active);

        var target = GlassSessionHelpPolicy.Find(new(own, []), null);

        Assert.Same(own, target.Session);
        Assert.Null(target.Code);
        Assert.False(target.IsStaff);
    }

    [Fact]
    public void TheAutomationsOwnEndedSessionIsShownAsItStands()
    {
        var own = Automation(GlassRepairEstimateSessionState.Cancelled);

        var target = GlassSessionHelpPolicy.Find(new(own, []), null);

        Assert.Same(own, target.Session);
        Assert.Null(target.Code);
    }

    [Fact]
    public void SeveralAreRefusedUntilOneIsNamed()
    {
        var first = Staff(GlassRepairEstimateSessionState.Active, helpOn: true);
        var second = Staff(GlassRepairEstimateSessionState.Active, helpOn: true, userId: Guid.NewGuid());
        var own = Automation(GlassRepairEstimateSessionState.Active);
        var candidates = new GlassSessionHelpCandidates(own, [first, second]);

        var target = GlassSessionHelpPolicy.Find(candidates, null);

        Assert.Null(target.Session);
        Assert.Equal(GlassSessionHelpPolicy.SeveralCode, target.Code);
        Assert.Equal(3, GlassSessionHelpPolicy.Open(candidates).Count);
        Assert.Same(second, GlassSessionHelpPolicy.Find(candidates, second.Id).Session);
        Assert.Null(GlassSessionHelpPolicy.Find(candidates, second.Id).Code);
        Assert.Same(own, GlassSessionHelpPolicy.Find(candidates, own.Id).Session);
        Assert.Equal(GlassSessionHelpPolicy.NoSessionCode, GlassSessionHelpPolicy.Find(candidates, Guid.NewGuid()).Code);
    }

    [Theory]
    [InlineData(GlassRepairEstimateSessionState.Importing)]
    [InlineData(GlassRepairEstimateSessionState.Completed)]
    [InlineData(GlassRepairEstimateSessionState.Cancelled)]
    [InlineData(GlassRepairEstimateSessionState.Unknown)]
    public void AHelpedSessionThatHasLeftActiveIsNotActive(GlassRepairEstimateSessionState state)
    {
        var helped = Staff(state, helpOn: true);

        var target = GlassSessionHelpPolicy.Find(new(null, [helped]), null);

        Assert.Same(helped, target.Session);
        Assert.Equal(GlassSessionHelpPolicy.NotActiveCode, target.Code);
        Assert.Equal(GlassSessionHelpPolicy.NotActiveCode, GlassSessionHelpPolicy.Find(new(null, [helped]), helped.Id).Code);
        Assert.Equal(GlassSessionHelpPolicy.NotActiveCode, GlassSessionHelpPolicy.RefusalOf(helped));
    }

    [Fact]
    public void TheNewerOfAnEndedHelpedSessionAndTheAutomationsEndedOneIsShown()
    {
        var older = Automation(GlassRepairEstimateSessionState.Cancelled, Start);
        var helped = Staff(GlassRepairEstimateSessionState.Completed, helpOn: true, createdAt: Start.AddMinutes(5));

        Assert.Same(helped, GlassSessionHelpPolicy.Find(new(older, [helped]), null).Session);

        var newer = Automation(GlassRepairEstimateSessionState.Cancelled, Start.AddMinutes(10));
        var target = GlassSessionHelpPolicy.Find(new(newer, [helped]), null);
        Assert.Same(newer, target.Session);
        Assert.Null(target.Code);
    }

    [Fact]
    public void OnlyTheOwnerTurnsHelpOnAnActiveStaffSession()
    {
        var owner = ActionActor.Staff(EngineerId, [StaffRole.Engineer]);
        var active = Staff(GlassRepairEstimateSessionState.Active, helpOn: false);

        Assert.True(GlassSessionHelpPolicy.MayTurnHelp(owner, active));
        Assert.False(GlassSessionHelpPolicy.MayTurnHelp(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), active));
        Assert.False(GlassSessionHelpPolicy.MayTurnHelp(owner, active with { State = GlassRepairEstimateSessionState.Importing }));
        Assert.False(GlassSessionHelpPolicy.MayTurnHelp(owner, Automation(GlassRepairEstimateSessionState.Active)));
        Assert.False(GlassSessionHelpPolicy.HelpOn(Automation(GlassRepairEstimateSessionState.Active) with { HelpOnAtUtc = Start }));
    }

    private static GlassRepairEstimateSession Staff(
        GlassRepairEstimateSessionState state, bool helpOn, Guid? userId = null, DateTimeOffset? createdAt = null) =>
        new(
            Guid.NewGuid(), Guid.Empty, userId ?? EngineerId, 1, "engineer-account", state, 3, "launch-" + Guid.NewGuid().ToString("N"),
            createdAt ?? Start, "33584499", "1954488", null)
        {
            HelpOnAtUtc = helpOn ? Start : null,
        };

    private static GlassRepairEstimateSession Automation(
        GlassRepairEstimateSessionState state, DateTimeOffset? createdAt = null) =>
        new(
            Guid.NewGuid(), Guid.Empty, Guid.Empty, 0, "automation-account", state, 1, "mcp:start-" + Guid.NewGuid().ToString("N"),
            createdAt ?? Start, null, null, null)
        {
            OwnerKind = ActorKind.Automation,
        };
}
