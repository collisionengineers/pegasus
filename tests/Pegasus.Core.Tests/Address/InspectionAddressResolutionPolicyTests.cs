using Pegasus.Core.Address;

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
        Assert.False(InspectionAddressResolutionPolicy.IsStaffResolved(state));
        Assert.False(
            InspectionAddressResolutionPolicy.SatisfiesCaseCreation(
                state,
                providerIsImageBased: false));
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
        Assert.True(InspectionAddressResolutionPolicy.IsStaffResolved(state));
        Assert.True(
            InspectionAddressResolutionPolicy.SatisfiesCaseCreation(
                state,
                providerIsImageBased: false));
    }

    [Fact]
    public void AnImageBasedProviderNeedsNothingSettledFirst()
    {
        // The mode is the address for these providers, and the case records it
        // on creation, so there is nothing for a person to confirm beforehand.
        Assert.All(
            Enum.GetValues<InspectionAddressResolutionState>(),
            state => Assert.True(
                InspectionAddressResolutionPolicy.SatisfiesCaseCreation(
                    state,
                    providerIsImageBased: true)));
    }

    [Fact]
    public void EveryStateIsClassifiedDeliberately() =>
        Assert.All(
            Enum.GetValues<InspectionAddressResolutionState>(),
            state => _ = InspectionAddressResolutionPolicy.IsStaffResolved(state));

    [Fact]
    public void AnUndeclaredStateFailsClosed() =>
        Assert.Throws<InvalidOperationException>(
            () => InspectionAddressResolutionPolicy.IsStaffResolved(
                (InspectionAddressResolutionState)99));
}
