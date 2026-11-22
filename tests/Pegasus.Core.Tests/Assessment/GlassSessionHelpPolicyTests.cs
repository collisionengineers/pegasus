using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// Which Glass's session the Automation's tools act in on a Case (design 08
/// section 5; R1170, R1214): its own live session, or an Engineer's Active
/// session; refused with the glass.help.* code otherwise.
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
    public void AnEngineersActiveSessionIsTheOne()
    {
        var engineers = Staff(GlassRepairEstimateSessionState.Active);
        var ended = Automation(GlassRepairEstimateSessionState.Completed);

        var target = GlassSessionHelpPolicy.Find(new(ended, [engineers]), null);

        Assert.Same(engineers, target.Session);
        Assert.Null(target.Code);
        Assert.True(target.IsStaff);
        Assert.Same(engineers, GlassSessionHelpPolicy.Find(new(ended, [engineers]), engineers.Id).Session);
        Assert.Null(GlassSessionHelpPolicy.Find(new(ended, [engineers]), engineers.Id).Code);
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
        var first = Staff(GlassRepairEstimateSessionState.Active);
        var second = Staff(GlassRepairEstimateSessionState.Active, userId: Guid.NewGuid());
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
    public void AnEngineersSessionThatHasLeftActiveIsNotActive(GlassRepairEstimateSessionState state)
    {
        var left = Staff(state);

        Assert.Equal(GlassSessionHelpPolicy.NotActiveCode, GlassSessionHelpPolicy.RefusalOf(left));
        Assert.Equal(GlassSessionHelpPolicy.NotActiveCode, GlassSessionHelpPolicy.Find(new(null, [left]), left.Id).Code);
        Assert.Empty(GlassSessionHelpPolicy.Open(new(null, [left])));
    }

    private static GlassRepairEstimateSession Staff(
        GlassRepairEstimateSessionState state, Guid? userId = null) =>
        new(
            Guid.NewGuid(), Guid.Empty, userId ?? EngineerId, 1, "engineer-account", state, 3, "launch-" + Guid.NewGuid().ToString("N"),
            Start, "33584499", "1954488", null);

    private static GlassRepairEstimateSession Automation(
        GlassRepairEstimateSessionState state) =>
        new(
            Guid.NewGuid(), Guid.Empty, Guid.Empty, 0, "automation-account", state, 1, "mcp:start-" + Guid.NewGuid().ToString("N"),
            Start, null, null, null)
        {
            OwnerKind = ActorKind.Automation,
        };
}
