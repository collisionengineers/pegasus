using Pegasus.Core.Address;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Address;

/// <summary>
/// Which inspection-address states let a case be created.
/// </summary>
/// <remarks>
/// This test was written out twice — in the staff screen and in the case-data
/// snapshot factory — and each copy had to be found when a state was added.
/// Adding <see cref="InspectionAddressResolutionState.Supplied"/> is exactly
/// the change that would have been missed.
/// </remarks>
public sealed class InspectionAddressResolutionPolicyTests
{
    [Theory]
    [InlineData(InspectionAddressResolutionState.Unresolved)]
    [InlineData(InspectionAddressResolutionState.Suggested)]
    public void AnUnsettledAddressDoesNotSatisfyCaseCreation(
        InspectionAddressResolutionState state)
    {
        Assert.False(InspectionAddressResolutionPolicy.IsSettled(state));
        Assert.False(
            InspectionAddressResolutionPolicy.SatisfiesCaseCreation(
                state,
                principalIsImageBased: false));
    }

    [Theory]
    [InlineData(InspectionAddressResolutionState.Accepted)]
    [InlineData(InspectionAddressResolutionState.Corrected)]
    [InlineData(InspectionAddressResolutionState.Supplied)]
    public void AnAddressAPersonSettledSatisfiesCaseCreation(
        InspectionAddressResolutionState state)
    {
        // Three routes, one meaning: a person looked at the evidence and said
        // what the address is. Supplying it where nothing was extracted is not
        // inference — the prohibition is on Pegasus deriving an address, not on
        // a member of staff stating one.
        Assert.True(InspectionAddressResolutionPolicy.IsSettled(state));
        Assert.True(
            InspectionAddressResolutionPolicy.SatisfiesCaseCreation(
                state,
                principalIsImageBased: false));
    }

    [Fact]
    public void AnImageBasedPrincipalNeedsNothingSettledFirst()
    {
        // The mode is the address for these providers, and the case records it
        // on creation, so there is nothing for a person to confirm beforehand.
        Assert.All(
            Enum.GetValues<InspectionAddressResolutionState>(),
            state => Assert.True(
                InspectionAddressResolutionPolicy.SatisfiesCaseCreation(
                    state,
                    principalIsImageBased: true)));
    }

    [Fact]
    public void EveryStateIsClassifiedDeliberately() =>
        Assert.All(
            Enum.GetValues<InspectionAddressResolutionState>(),
            state => _ = InspectionAddressResolutionPolicy.IsSettled(state));

    [Fact]
    public void AnUndeclaredStateFailsClosed() =>
        Assert.Throws<InvalidOperationException>(
            () => InspectionAddressResolutionPolicy.IsSettled(
                (InspectionAddressResolutionState)99));

    [Fact]
    public void AMemberOfStaffSettlesAnAddressAsStaff()
    {
        var staffId = Guid.NewGuid();
        var settler = InspectionAddressResolutionPolicy.RequireSettler(
            ActionActor.Staff(staffId, [StaffRole.Engineer]));
        Assert.Equal(ActorKind.Staff, settler.Kind);
        Assert.Equal(staffId.ToString("D"), settler.Subject);
        Assert.Equal("staff", InspectionAddressResolutionPolicy.SettlerWord(settler.Kind));
    }

    [Fact]
    public void TheAutomationActorSettlesAnAddressAsItselfAndNeverAsStaff()
    {
        // ADR-0064: the Automation actor does the casework staff do, settling
        // an inspection address included, and its settlement is its own.
        var settler = InspectionAddressResolutionPolicy.RequireSettler(
            ActionActor.Automation("claude-desktop"));
        Assert.Equal(ActorKind.Automation, settler.Kind);
        Assert.Equal("claude-desktop", settler.Subject);
        Assert.Equal("Automation", InspectionAddressResolutionPolicy.SettlerWord(settler.Kind));
    }

    [Fact]
    public void NoOtherActorSettlesAnAddress()
    {
        Assert.Throws<ArgumentException>(() => InspectionAddressResolutionPolicy.RequireSettler(
            ActionActor.SystemWorker("pegasus-worker")));
        Assert.Throws<ArgumentException>(() => InspectionAddressResolutionPolicy.RequireSettler(
            ActionActor.Principal(Guid.NewGuid())));
        Assert.Throws<InvalidOperationException>(
            () => InspectionAddressResolutionPolicy.SettlerWord(ActorKind.SystemWorker));
        Assert.Throws<InvalidOperationException>(
            () => InspectionAddressResolutionPolicy.SettlerWord(ActorKind.Principal));
    }
}
