using Pegasus.Core.ImageIntake;

namespace Pegasus.Core.Tests.ImageIntake;

public sealed class ImageIntakeGroupRoutingPolicyTests
{
    [Fact]
    public void PlateOverviewAndUnreadableDamageCloseupShareOneCase()
    {
        var result = ImageIntakeGroupRoutingPolicy.Evaluate(
        [
            Member(VrmRecognitionOutcomeKind.Suggested, "AC04", 0.95),
            Member(VrmRecognitionOutcomeKind.NoReadableResult, null, null)
        ],
        expectedMemberCount: 2,
        eligibleCaseCount: 1);

        Assert.Equal(ImageIntakeGroupRoutingDecision.AssociateExistingCase, result.Decision);
        Assert.Equal("AC04", result.NormalizedRegistration);
    }

    [Fact]
    public void ConflictingReadsRouteToUnidentifiedWithSpecificReason()
    {
        var result = ImageIntakeGroupRoutingPolicy.Evaluate(
        [
            Member(VrmRecognitionOutcomeKind.Suggested, "AC04", 0.95),
            Member(VrmRecognitionOutcomeKind.Suggested, "BD05", 0.95)
        ],
        expectedMemberCount: 2,
        eligibleCaseCount: 1);

        Assert.Equal(ImageIntakeGroupRoutingDecision.RouteToUnidentified, result.Decision);
        Assert.Equal("conflicting_vrms", result.ReasonCode);
    }

    [Fact]
    public void AllUnreadableImagesRouteToUnidentifiedWithSpecificReason()
    {
        var result = ImageIntakeGroupRoutingPolicy.Evaluate(
        [
            Member(VrmRecognitionOutcomeKind.NoReadableResult, null, null),
            Member(VrmRecognitionOutcomeKind.NoReadableResult, null, null)
        ],
        expectedMemberCount: 2,
        eligibleCaseCount: 0);

        Assert.Equal(ImageIntakeGroupRoutingDecision.RouteToUnidentified, result.Decision);
        Assert.Equal("group_no_accepted_vrm", result.ReasonCode);
    }

    [Fact]
    public void IncompleteMemberWaitsWithoutRouting()
    {
        var result = ImageIntakeGroupRoutingPolicy.Evaluate(
            [Member(VrmRecognitionOutcomeKind.Suggested, "AC04", 0.95)],
            expectedMemberCount: 2,
            eligibleCaseCount: 1);

        Assert.Equal(ImageIntakeGroupRoutingDecision.WaitingForMembers, result.Decision);
    }

    [Fact]
    public void OneUsableVrmWithoutUniqueCaseHandsOffToImageIntake()
    {
        var result = ImageIntakeGroupRoutingPolicy.Evaluate(
        [
            Member(VrmRecognitionOutcomeKind.Suggested, "AC04", 0.95),
            Member(VrmRecognitionOutcomeKind.NoReadableResult, null, null)
        ],
        expectedMemberCount: 2,
        eligibleCaseCount: 0);

        Assert.Equal(ImageIntakeGroupRoutingDecision.HandOffToImageIntake, result.Decision);
        Assert.Equal("AC04", result.NormalizedRegistration);
        Assert.Equal("group_vrm_no_eligible_case", result.ReasonCode);
    }

    private static ImageIntakeGroupMemberRecognition Member(
        VrmRecognitionOutcomeKind outcome,
        string? registration,
        double? confidence) =>
        new(Guid.NewGuid(), true, outcome, registration, confidence);
}
