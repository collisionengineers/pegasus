using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// Every reason yields exactly one primary next step, and a grouped origin —
/// image material by construction — registers images whatever its reason.
/// The policy is the one owner of the mapping the page and the automation
/// surface both read.
/// </summary>
public sealed class UnidentifiedNextStepPolicyTests
{
    [Theory]
    [InlineData(UnidentifiedReasonCode.AuditOriginalReportMissing, UnidentifiedNextStep.AddOriginalReport)]
    [InlineData(UnidentifiedReasonCode.NoUsableIdentification, UnidentifiedNextStep.LinkToCase)]
    [InlineData(UnidentifiedReasonCode.ConflictingIdentification, UnidentifiedNextStep.LinkToCase)]
    [InlineData(UnidentifiedReasonCode.AmbiguousOwnershipOrDestination, UnidentifiedNextStep.LinkToCase)]
    [InlineData(UnidentifiedReasonCode.UnreadableOrCorruptContent, UnidentifiedNextStep.LinkToCase)]
    [InlineData(UnidentifiedReasonCode.UnsupportedContent, UnidentifiedNextStep.LinkToCase)]
    [InlineData(UnidentifiedReasonCode.TechnicalProcessingFailure, UnidentifiedNextStep.ProcessAgain)]
    public void ReceiptOriginsFollowTheReason(
        UnidentifiedReasonCode reason,
        UnidentifiedNextStep expected) =>
        Assert.Equal(expected, UnidentifiedNextStepPolicy.Primary(reason, UnidentifiedOriginKind.Receipt));

    [Theory]
    [InlineData(UnidentifiedReasonCode.NoUsableIdentification)]
    [InlineData(UnidentifiedReasonCode.ConflictingIdentification)]
    [InlineData(UnidentifiedReasonCode.TechnicalProcessingFailure)]
    public void GroupedOriginsRegisterImages(UnidentifiedReasonCode reason) =>
        Assert.Equal(
            UnidentifiedNextStep.RegisterImages,
            UnidentifiedNextStepPolicy.Primary(reason, UnidentifiedOriginKind.SubmissionGroup));

    [Fact]
    public void EveryDefinedReasonResolvesToOneStep()
    {
        foreach (var reason in Enum.GetValues<UnidentifiedReasonCode>())
        {
            foreach (var originKind in Enum.GetValues<UnidentifiedOriginKind>())
            {
                Assert.True(
                    Enum.IsDefined(UnidentifiedNextStepPolicy.Primary(reason, originKind)),
                    $"{reason}/{originKind} produced an undefined next step.");
            }
        }
    }
}
