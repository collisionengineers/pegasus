using System.Net;
using Pegasus.Core.Eva;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Qdos;

public sealed class EvaSubmissionPolicyTests
{
    [Fact]
    public void FirstManualSendMovesReviewToReportPreparation() =>
        Assert.Equal(
            CaseLifecycleState.ReportPreparation,
            EvaSubmissionPolicy.StateAfterSend(CaseLifecycleState.Review));

    [Theory]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    [InlineData(CaseLifecycleState.PostReport)]
    public void ManualResendDoesNotChangeLaterState(CaseLifecycleState state) =>
        Assert.Equal(state, EvaSubmissionPolicy.StateAfterSend(state));

    [Theory]
    [InlineData(EvaSubmissionOutcome.Rejected)]
    [InlineData(EvaSubmissionOutcome.Unknown)]
    public void UndeliveredManualSendFromReviewDoesNotMoveTheCase(EvaSubmissionOutcome outcome)
    {
        var result = new EvaSubmissionResult(outcome, null, null, "code", "detail", 0);

        Assert.False(result.IsDelivered);
        Assert.Equal(
            CaseLifecycleState.Review,
            EvaSubmissionPolicy.StateAfterSend(CaseLifecycleState.Review, result.IsDelivered));
    }

    [Fact]
    public void PartialManualSendFromReviewStillMovesTheCase()
    {
        var result = new EvaSubmissionResult(EvaSubmissionOutcome.Partial, null, null, null, null, 1);

        Assert.True(result.IsDelivered);
        Assert.Equal(
            CaseLifecycleState.ReportPreparation,
            EvaSubmissionPolicy.StateAfterSend(CaseLifecycleState.Review, result.IsDelivered));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManualSettingControlsSubmission(bool enabled)
    {
        var modes = new EvaSubmissionModes(enabled);

        Assert.Equal(enabled, EvaSubmissionPolicy.Allows(modes));
        Assert.Equal(enabled, modes.IsEnabled);
    }

    [Fact]
    public void ManualSubmissionRequiresCaseworkRight() =>
        Assert.Equal(Pegasus.Core.Identity.StaffAccessRight.PerformCasework, EvaSubmissionPolicy.RequiredRight);

    [Theory]
    [InlineData(HttpStatusCode.OK, 200, true, EvaSubmissionOutcome.Succeeded)]
    [InlineData(HttpStatusCode.OK, 200, false, EvaSubmissionOutcome.Partial)]
    [InlineData(HttpStatusCode.OK, 400, false, EvaSubmissionOutcome.Rejected)]
    [InlineData(HttpStatusCode.Unauthorized, null, false, EvaSubmissionOutcome.Rejected)]
    [InlineData(HttpStatusCode.InternalServerError, null, false, EvaSubmissionOutcome.Unknown)]
    [InlineData(null, null, false, EvaSubmissionOutcome.Unknown)]
    public void ProviderOutcomesRemainDistinct(
        HttpStatusCode? status,
        int? envelopeStatus,
        bool hasIdentifier,
        EvaSubmissionOutcome expected) =>
        Assert.Equal(expected, EvaSubmissionPolicy.Classify(status, envelopeStatus, hasIdentifier));

    [Fact]
    public void AllFourOutcomesAreReachable()
    {
        var reached = new[]
        {
            EvaSubmissionPolicy.Classify(HttpStatusCode.OK, 200, true),
            EvaSubmissionPolicy.Classify(HttpStatusCode.OK, 200, false),
            EvaSubmissionPolicy.Classify(HttpStatusCode.OK, 400, false),
            EvaSubmissionPolicy.Classify(null, null, false)
        };

        Assert.Equal(
            Enum.GetValues<EvaSubmissionOutcome>().Order(),
            reached.Distinct().Order());
    }

    [Fact]
    public void OnlyASuccessHasNoFailureCode()
    {
        Assert.Null(EvaSubmissionPolicy.FailureCode(EvaSubmissionOutcome.Succeeded, HttpStatusCode.OK));

        foreach (var outcome in Enum.GetValues<EvaSubmissionOutcome>()
            .Where(item => item != EvaSubmissionOutcome.Succeeded))
        {
            Assert.False(string.IsNullOrWhiteSpace(
                EvaSubmissionPolicy.FailureCode(outcome, HttpStatusCode.BadRequest)));
        }
    }
}
