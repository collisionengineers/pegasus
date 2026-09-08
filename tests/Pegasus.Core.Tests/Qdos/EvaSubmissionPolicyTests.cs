using System.Net;
using Pegasus.Core.Cases;
using Pegasus.Core.Eva;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Qdos;

public sealed class EvaSubmissionPolicyTests
{
    [Theory]
    [InlineData("22 Park Avenue")]
    [InlineData("22 Park Avenue, Watford")]
    [InlineData("22-24 Park Avenue")]
    [InlineData("22 Park Avenue, St John's")]
    public void AcceptedClaimantAddressRetainsOrdinaryAddressText(string value)
    {
        // Structural punctuation variants of the supplied vendor address,
        // not additional instruction evidence or a postal-validity claim.
        Assert.Equal(value, EvaSubmissionPolicy.AcceptedClaimantAddress(
            new(AddressValue(value, CaseDataValueKind.Fact), null, null)));
    }

    [Fact]
    public void ConfirmedClaimantAddressWinsAndSuggestionsAreNotAccepted()
    {
        var fact = AddressValue("22 Park Avenue", CaseDataValueKind.Fact);
        var confirmed = AddressValue("15 High Street", CaseDataValueKind.Confirmed);
        var suggestion = AddressValue("22 Park Avenue", CaseDataValueKind.Suggestion);

        Assert.Equal(confirmed.Value, EvaSubmissionPolicy.AcceptedClaimantAddress(
            new(fact, suggestion, confirmed)));
        Assert.Equal(fact.Value, EvaSubmissionPolicy.AcceptedClaimantAddress(
            new(fact, suggestion, null)));
        Assert.Null(EvaSubmissionPolicy.AcceptedClaimantAddress(new(null, suggestion, null)));
        Assert.Null(EvaSubmissionPolicy.AcceptedClaimantAddress(new(null, null, null)));
        Assert.Null(EvaSubmissionPolicy.AcceptedClaimantAddress(
            new(fact, null, confirmed with { Value = " " })));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t\r\n\u00a0")]
    [InlineData("22\0 Park Avenue")]
    [InlineData("22 Park\nAvenue")]
    [InlineData("22\u200b Park Avenue")]
    [InlineData("22\U000e0001 Park Avenue")]
    public void UnusableClaimantAddressIsNotSent(string value) =>
        Assert.Null(EvaSubmissionPolicy.AcceptedClaimantAddress(
            new(AddressValue(value, CaseDataValueKind.Fact), null, null)));

    [Fact]
    public void ClaimantAddressLengthIsEnforcedWithoutTruncation()
    {
        // Length probes, not fabricated postal-address evidence.
        var boundary = "22 Park Avenue".PadRight(40, 'x');
        Assert.Equal(boundary, EvaSubmissionPolicy.AcceptedClaimantAddress(
            new(AddressValue(boundary, CaseDataValueKind.Fact), null, null)));
        Assert.Null(EvaSubmissionPolicy.AcceptedClaimantAddress(
            new(AddressValue(boundary + "x", CaseDataValueKind.Fact), null, null)));
    }

    private static CaseDataValue<string> AddressValue(string value, CaseDataValueKind kind) =>
        new(value, kind, new(CaseDataSourceKind.IntakeEvidence,
            "eva-request-model", "supplied EVA address example", "case-031-fixture", 1));

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
