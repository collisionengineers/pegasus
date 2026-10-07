using Pegasus.Core.Intake;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Tests.Triage;

/// <summary>
/// Issue 1047: Reply with finding is offered on a Completed Triage until its
/// outcome reply is sent; only Reopen and a fresh completion offer it again.
/// Issue 1044: the active states the Triage queue lists.
/// </summary>
public sealed class TriageOutcomeReplySentTests
{
    private static readonly DateTimeOffset NowUtc = new(2031, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheActiveStatesAreTheThreeTheTriageQueueAndMetricRead()
    {
        Assert.Equal(
            new[] { TriageState.Open, TriageState.AwaitingInformation, TriageState.FindingRecorded },
            TriageLifecycleRules.ActiveStates);
    }

    [Fact]
    public void ACompletedTriageWithNoSentReplyStillOffersIt()
    {
        var detail = Detail(TriageState.Completed, sentOutcomeReplyVersion: null,
            (TriageState.FindingRecorded, 2), (TriageState.Completed, 3));

        Assert.False(TriageLifecycleRules.OutcomeReplySent(detail));
    }

    [Fact]
    public void AReplySentAfterCompletionIsSent()
    {
        var detail = Detail(TriageState.Completed, sentOutcomeReplyVersion: 3,
            (TriageState.FindingRecorded, 2), (TriageState.Completed, 3));

        Assert.True(TriageLifecycleRules.OutcomeReplySent(detail));
    }

    [Fact]
    public void ALinkedSentItemDoesNotStartANewCompletion()
    {
        // The Sent-item link advances the version but leaves the Triage Completed.
        var detail = Detail(TriageState.Completed, sentOutcomeReplyVersion: 3,
            (TriageState.FindingRecorded, 2), (TriageState.Completed, 3), (TriageState.Completed, 4));

        Assert.True(TriageLifecycleRules.OutcomeReplySent(detail));
    }

    [Fact]
    public void AReplySentBeforeReopenAndAFreshCompletionIsOfferedAgain()
    {
        var detail = Detail(TriageState.Completed, sentOutcomeReplyVersion: 3,
            (TriageState.FindingRecorded, 2),
            (TriageState.Completed, 3),
            (TriageState.Open, 4),
            (TriageState.FindingRecorded, 5),
            (TriageState.Completed, 6));

        Assert.False(TriageLifecycleRules.OutcomeReplySent(detail));
    }

    [Theory]
    [InlineData(TriageState.Open)]
    [InlineData(TriageState.FindingRecorded)]
    [InlineData(TriageState.Cancelled)]
    public void OnlyACompletedTriageHasASentOutcomeReply(TriageState state)
    {
        var detail = Detail(state, sentOutcomeReplyVersion: 3,
            (TriageState.FindingRecorded, 2), (TriageState.Completed, 3), (state, 4));

        Assert.False(TriageLifecycleRules.OutcomeReplySent(detail));
    }

    private static TriageDetail Detail(
        TriageState state,
        long? sentOutcomeReplyVersion,
        params (TriageState AfterState, long AfterVersion)[] history)
    {
        var caseId = Guid.NewGuid();
        var record = new TriageRecord(
            caseId,
            new TriageOrigin(
                Guid.NewGuid(),
                new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, "receipt-token"),
                "source-hash",
                Guid.NewGuid()),
            "AB12CDE",
            state,
            null,
            null,
            history[^1].AfterVersion,
            "t.QDOS31001",
            Guid.NewGuid());
        return new TriageDetail(
            record,
            NowUtc.AddDays(-1),
            [],
            [],
            history.Select(entry => new TriageHistoryEntry(
                    Guid.NewGuid(),
                    caseId,
                    "triage_event",
                    "worker",
                    "System",
                    "Recorded.",
                    $"op-{entry.AfterVersion}",
                    NowUtc,
                    entry.AfterVersion - 1,
                    entry.AfterVersion,
                    entry.AfterState,
                    null,
                    null))
                .ToArray(),
            [])
        {
            SentOutcomeReplyVersion = sentOutcomeReplyVersion
        };
    }
}
