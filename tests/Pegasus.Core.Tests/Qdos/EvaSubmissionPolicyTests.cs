using System.Net;
using Pegasus.Core.Eva;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Qdos;

/// <summary>
/// EXT-04. FRD-07 requires that "external success, rejection, partial or
/// unknown outcomes must remain distinct", so the classification is pinned
/// here against the answers EVA actually gives — which are not the answers its
/// documentation describes.
/// </summary>
public sealed class EvaSubmissionPolicyTests
{
    [Fact]
    public void FirstManualSendMovesReviewToWithEngineer() =>
        Assert.Equal(
            CaseLifecycleState.ReportPreparation,
            EvaSubmissionPolicy.StateAfterSend(
                CaseLifecycleState.Review,
                EvaSubmissionTrigger.Manual));

    [Theory]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    [InlineData(CaseLifecycleState.PostReport)]
    public void ManualResendDoesNotChangeWithEngineerState(CaseLifecycleState state) =>
        Assert.Equal(state, EvaSubmissionPolicy.StateAfterSend(state, EvaSubmissionTrigger.Manual));

    [Fact]
    public void AutomaticSubmissionIsReviewOnly()
    {
        Assert.Equal(
            CaseLifecycleState.Review,
            EvaSubmissionPolicy.StateAfterSend(
                CaseLifecycleState.Review,
                EvaSubmissionTrigger.Automatic));
        Assert.Throws<EvaHandoffStateException>(() =>
            EvaSubmissionPolicy.StateAfterSend(
                CaseLifecycleState.ReportPreparation,
                EvaSubmissionTrigger.Automatic));
    }

    [Fact]
    public void AnAcceptedEnvelopeWithAnIdentifierSucceeds() =>
        Assert.Equal(
            EvaSubmissionOutcome.Succeeded,
            EvaSubmissionPolicy.Classify(HttpStatusCode.OK, 200, hasIdentifier: true));

    /// <summary>
    /// The instruction landed and EVA told us nothing we can link to it. That
    /// is not a failure — resubmitting would create a second claim — but it is
    /// not a complete success either, and collapsing the two is exactly what
    /// FRD-07 forbids.
    /// </summary>
    [Fact]
    public void AnAcceptedEnvelopeWithoutAnIdentifierIsPartial() =>
        Assert.Equal(
            EvaSubmissionOutcome.Partial,
            EvaSubmissionPolicy.Classify(HttpStatusCode.OK, 200, hasIdentifier: false));

    /// <summary>
    /// The behaviour that makes reading the HTTP status alone unsafe: EVA
    /// answers 200 OK and puts its refusal in the body. Recorded traffic shows
    /// this for a wrong RequestFrom and an unbound Agent code.
    /// </summary>
    [Fact]
    public void ARejectionInsideAnHttpSuccessIsARejection() =>
        Assert.Equal(
            EvaSubmissionOutcome.Rejected,
            EvaSubmissionPolicy.Classify(HttpStatusCode.OK, 400, hasIdentifier: false));

    /// <summary>
    /// EVA's 500 arrives as text/plain, so there is no envelope to read and no
    /// way to know whether the claim was created.
    /// </summary>
    [Fact]
    public void AnUnreadableServerErrorIsUnknown() =>
        Assert.Equal(
            EvaSubmissionOutcome.Unknown,
            EvaSubmissionPolicy.Classify(
                HttpStatusCode.InternalServerError,
                envelopeStatusCode: null,
                hasIdentifier: false));

    /// <summary>
    /// No response at all — a connect failure or timeout. EVA may have created
    /// the claim before the connection died.
    /// </summary>
    [Fact]
    public void NoResponseIsUnknown() =>
        Assert.Equal(
            EvaSubmissionOutcome.Unknown,
            EvaSubmissionPolicy.Classify(
                httpStatus: null,
                envelopeStatusCode: null,
                hasIdentifier: false));

    [Fact]
    public void AnHttpRejectionWithoutAnEnvelopeIsARejection() =>
        Assert.Equal(
            EvaSubmissionOutcome.Rejected,
            EvaSubmissionPolicy.Classify(
                HttpStatusCode.Unauthorized,
                envelopeStatusCode: null,
                hasIdentifier: false));

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

    /// <summary>
    /// The load-bearing rule of the whole integration. EVA has no idempotency,
    /// so anything we already know the answer to is never sent twice — only an
    /// outcome that leaves delivery genuinely unknown may be retried.
    /// </summary>
    [Theory]
    [InlineData(EvaSubmissionOutcome.Succeeded, false)]
    [InlineData(EvaSubmissionOutcome.Rejected, false)]
    [InlineData(EvaSubmissionOutcome.Partial, false)]
    [InlineData(EvaSubmissionOutcome.Unknown, true)]
    public void OnlyAnUnknownOutcomeIsRetryable(EvaSubmissionOutcome outcome, bool retryable) =>
        Assert.Equal(retryable, EvaSubmissionPolicy.IsRetryable(outcome));

    [Fact]
    public void RetryDelaysBackOffAndThenStop()
    {
        var delays = Enumerable.Range(1, EvaSubmissionRetryPolicy.MaximumAttempts)
            .Select(attempt => EvaSubmissionRetryPolicy.NextAttemptDelay(
                attempt,
                EvaSubmissionOutcome.Unknown))
            .ToArray();

        Assert.All(delays[..^1], delay => Assert.NotNull(delay));
        Assert.Null(delays[^1]);
        Assert.Equal(
            delays[..^1].Select(delay => delay!.Value).OrderBy(delay => delay),
            delays[..^1].Select(delay => delay!.Value));
    }

    [Fact]
    public void ATerminalOutcomeIsNeverRescheduled() =>
        Assert.Null(EvaSubmissionRetryPolicy.NextAttemptDelay(
            attemptCount: 1,
            EvaSubmissionOutcome.Rejected));

    /// <summary>
    /// The two settings are independent by operator decision, so each act
    /// consults only its own. An automatic-only principal has no button, and a
    /// manual-only one never submits by itself.
    /// </summary>
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, true)]
    public void EachTriggerConsultsOnlyItsOwnSetting(
        bool manual,
        bool automatic,
        bool allowsManual,
        bool allowsAutomatic)
    {
        var modes = new EvaSubmissionModes(manual, automatic);

        Assert.Equal(
            allowsManual,
            EvaSubmissionPolicy.Allows(modes, EvaSubmissionTrigger.Manual));
        Assert.Equal(
            allowsAutomatic,
            EvaSubmissionPolicy.Allows(modes, EvaSubmissionTrigger.Automatic));
    }

    [Fact]
    public void ADisabledPrincipalAllowsNeitherAct()
    {
        Assert.False(EvaSubmissionModes.Disabled.IsEnabled);
        Assert.False(EvaSubmissionPolicy.Allows(
            EvaSubmissionModes.Disabled,
            EvaSubmissionTrigger.Manual));
        Assert.False(EvaSubmissionPolicy.Allows(
            EvaSubmissionModes.Disabled,
            EvaSubmissionTrigger.Automatic));
    }

    /// <summary>
    /// Each attempt of a queued submission is its own operation. If they shared
    /// the work row's key, the second attempt would replay the first attempt's
    /// unknown outcome from action history and never reach EVA, so the retry
    /// ladder would spend every attempt sending nothing.
    /// </summary>
    [Fact]
    public void EachAttemptOfOneWorkItemGetsItsOwnOperationKey()
    {
        const string row = "0f3d5b8c9a2e4f118d7c6b5a4938271e";

        var keys = Enumerable.Range(1, EvaSubmissionRetryPolicy.MaximumAttempts)
            .Select(attempt => EvaSubmissionPolicy.AttemptOperationKey(row, attempt))
            .ToArray();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(row, keys, StringComparer.Ordinal);
        Assert.All(keys, key => Assert.True(Guid.TryParseExact(key, "N", out _)));

        // Derived, not generated: a queue message delivered twice for the same
        // attempt must replay rather than submit a second time.
        Assert.Equal(keys[0], EvaSubmissionPolicy.AttemptOperationKey(row, 1));
    }

    [Fact]
    public void OnlyASuccessHasNoFailureCode()
    {
        Assert.Null(EvaSubmissionPolicy.FailureCode(
            EvaSubmissionOutcome.Succeeded,
            HttpStatusCode.OK));

        foreach (var outcome in Enum.GetValues<EvaSubmissionOutcome>()
            .Where(item => item != EvaSubmissionOutcome.Succeeded))
        {
            Assert.False(string.IsNullOrWhiteSpace(
                EvaSubmissionPolicy.FailureCode(outcome, HttpStatusCode.BadRequest)));
        }
    }
}
