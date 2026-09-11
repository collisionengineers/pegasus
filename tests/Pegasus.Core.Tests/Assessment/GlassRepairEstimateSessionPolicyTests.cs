using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

public sealed class GlassRepairEstimateSessionPolicyTests
{
    private static readonly Guid EngineerId = Guid.NewGuid();
    private static readonly ActionActor Engineer = ActionActor.Staff(EngineerId, [StaffRole.Engineer]);

    [Theory]
    [InlineData(GlassRepairEstimateSessionState.Prepared, true)]
    [InlineData(GlassRepairEstimateSessionState.Launching, true)]
    [InlineData(GlassRepairEstimateSessionState.Active, true)]
    [InlineData(GlassRepairEstimateSessionState.AwaitingImport, true)]
    [InlineData(GlassRepairEstimateSessionState.Unknown, true)]
    [InlineData(GlassRepairEstimateSessionState.Importing, false)]
    [InlineData(GlassRepairEstimateSessionState.Completed, false)]
    [InlineData(GlassRepairEstimateSessionState.Failed, false)]
    [InlineData(GlassRepairEstimateSessionState.Expired, false)]
    [InlineData(GlassRepairEstimateSessionState.Cancelled, false)]
    public void EverySessionThatHoldsTheAccountCanBeClosedExceptOneMidImport(
        GlassRepairEstimateSessionState state, bool closable)
    {
        Assert.Equal(closable, GlassRepairEstimateSessionPolicy.CanClose(state));

        var closure = () => GlassRepairEstimateSessionPolicy.ValidateClosure(
            new(Engineer, Guid.NewGuid(), 3, ExternalSessionClosed: true, "Closed in Glass's."),
            Session(state));
        if (closable)
        {
            closure();
        }
        else
        {
            Assert.Throws<GlassRepairEstimateRefusalException>(closure);
        }
    }

    [Fact]
    public void ClosureStillNeedsTheConfirmationAndAReasonFromTheOwner()
    {
        Assert.Throws<GlassRepairEstimateRefusalException>(() =>
            GlassRepairEstimateSessionPolicy.ValidateClosure(
                new(Engineer, Guid.NewGuid(), 3, ExternalSessionClosed: false, "Closed in Glass's."),
                Session(GlassRepairEstimateSessionState.Active)));
        Assert.Throws<GlassRepairEstimateRefusalException>(() =>
            GlassRepairEstimateSessionPolicy.ValidateClosure(
                new(Engineer, Guid.NewGuid(), 3, ExternalSessionClosed: true, " "),
                Session(GlassRepairEstimateSessionState.Active)));
        Assert.Throws<GlassRepairEstimateRefusalException>(() =>
            GlassRepairEstimateSessionPolicy.ValidateClosure(
                new(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), Guid.NewGuid(), 3, true, "Closed."),
                Session(GlassRepairEstimateSessionState.Active)));
    }

    private static GlassRepairEstimateSession Session(GlassRepairEstimateSessionState state) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            EngineerId,
            CredentialGeneration: 1,
            "account-key",
            state,
            Version: 3,
            OperationKey: "op",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddHours(8),
            ProviderVehicleId: null,
            ProviderEstimateId: null,
            FailureCode: null);
}
