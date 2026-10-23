using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The Case-filing replay calls the filer directly, records its own history
/// and never writes the receipt's work item: it has no work store at all
/// (R190, R367, R378).
/// </summary>
public sealed class ReplayCaseFilingsTests
{
    private static readonly DateTimeOffset Now = new(2031, 6, 1, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("transient-1", 0, nameof(TimeoutException), CaseFilingReplayEvent.Failed, "intake_processing_failure")]
    [InlineData("transient-5", 4, nameof(TimeoutException), CaseFilingReplayEvent.Exhausted, "intake_processing_failure")]
    [InlineData("terminal", 0, nameof(InvalidOperationException), CaseFilingReplayEvent.Exhausted, "unexpected_intake_processing_failure")]
    [InlineData("relinked", 0, nameof(IntakeOperationConflictException), CaseFilingReplayEvent.Failed, "intake_processing_failure")]
    [InlineData("removed-version", 0, nameof(FileNotFoundException), CaseFilingReplayEvent.Failed, "intake_processing_failure")]
    [InlineData("bad-input", 0, nameof(InvalidDataException), CaseFilingReplayEvent.Exhausted, "invalid_intake_data")]
    public async Task AFailedAttemptIsRecordedAndBoundedByTheLivePathsAttempts(
        string row,
        int priorFailures,
        string fault,
        CaseFilingReplayEvent expected,
        string failureCode)
    {
        Assert.NotEmpty(row);
        var harness = new Harness();
        var candidate = harness.Add(failedAttempts: priorFailures,
            lastFailedAtUtc: priorFailures == 0 ? null : Now.AddHours(-3));
        harness.Promotion.Fault = fault switch
        {
            nameof(TimeoutException) => new TimeoutException("The filing connection timed out."),
            nameof(IntakeOperationConflictException) => new IntakeOperationConflictException(),
            nameof(FileNotFoundException) => new FileNotFoundException("The Case artifact version was removed."),
            nameof(InvalidDataException) => new InvalidDataException("The automatic Case evidence plan is invalid."),
            _ => new InvalidOperationException("The promotion plan cannot be read.")
        };

        var result = await harness.Sweep.ExecuteAsync(5);

        var record = Assert.Single(harness.Store.Records);
        Assert.Equal(expected, record.Kind);
        Assert.Equal(priorFailures + 1, record.Attempt);
        Assert.Equal(failureCode, record.FailureCode);
        Assert.Equal(Now, record.OccurredAtUtc);
        Assert.Equal(
            expected == CaseFilingReplayEvent.Exhausted
                ? $"intake-replay-exhausted:{candidate.ReceiptId:N}:{candidate.CaseId:N}:{candidate.AssociationVersion}"
                : $"intake-replay-failed:{candidate.ReceiptId:N}:{candidate.CaseId:N}:{candidate.AssociationVersion}:{priorFailures + 1}",
            record.OperationKey);
        Assert.True(record.OperationKey.Length <= 100);
        Assert.Equal(new ReplayCaseFilingsResult(1, 0, 0, 1, expected == CaseFilingReplayEvent.Exhausted ? 1 : 0), result);
    }

    [Fact]
    public void TheBoundIsTheLivePathsAttemptsAndTheWindowIsTheExistingTwoHours()
    {
        Assert.Equal(5, ReplayCaseFilings.MaximumAttempts);
        Assert.Equal(TimeSpan.FromHours(2), ReplayCaseFilings.SystemAssociationWindow);
    }

    [Theory]
    [InlineData("after-1-early", 1, 29, false)]
    [InlineData("after-1-due", 1, 30, true)]
    [InlineData("after-2-early", 2, 119, false)]
    [InlineData("after-2-due", 2, 120, true)]
    [InlineData("after-4-early", 4, 1799, false)]
    [InlineData("after-4-due", 4, 1800, true)]
    public void AFailedCandidateIsDueOnlyAfterTheLivePathsDelay(
        string row,
        int failedAttempts,
        int secondsSinceLastFailure,
        bool due)
    {
        Assert.NotEmpty(row);
        var candidate = Candidate(failedAttempts, Now.AddSeconds(-secondsSinceLastFailure));

        Assert.Equal(due, ReplayCaseFilings.IsDue(candidate, Now));
    }

    [Theory]
    [InlineData("pending-set", 2, 2, 1, false)]
    [InlineData("partial-set", 2, 1, 1, true)]
    [InlineData("failed-or-confirmed-set", 2, 2, 0, true)]
    [InlineData("no-plan", 0, 0, 0, true)]
    public void ASetWhosePlannedFilesAreAllRecordedWithOnePendingIsLeftToCustodyReconciliation(
        string row,
        int planned,
        int recorded,
        int pending,
        bool due)
    {
        Assert.NotEmpty(row);
        var candidate = Candidate(0, null) with
        {
            PlannedOccurrences = planned,
            RecordedOccurrences = recorded,
            PendingOccurrences = pending
        };

        Assert.Equal(due, ReplayCaseFilings.IsDue(candidate, Now));
    }

    [Fact]
    public async Task TheStoreIsAskedForTheTwoHourCutoffAndTheSweepsOwnTime()
    {
        var harness = new Harness();
        harness.Add();

        await harness.Sweep.ExecuteAsync(5);

        var page = Assert.Single(harness.Store.Pages);
        Assert.Null(page.After);
        Assert.Equal(Now.AddHours(-2), page.Cutoff);
        Assert.Equal(Now, page.NowUtc);
        Assert.Equal(5, page.MaximumItems);
    }

    [Fact]
    public void ARelinkToTheSameCaseKeysItsAttemptsAfresh()
    {
        var first = Candidate(0, null);
        var relinked = first with { AssociationVersion = first.AssociationVersion + 2 };

        Assert.NotEqual(
            new CaseFilingReplayRecord(first, CaseFilingReplayEvent.Failed, 1, "x", Now).OperationKey,
            new CaseFilingReplayRecord(relinked, CaseFilingReplayEvent.Failed, 1, "x", Now).OperationKey);
        Assert.NotEqual(
            new CaseFilingReplayRecord(first, CaseFilingReplayEvent.Exhausted, 1, "x", Now).OperationKey,
            new CaseFilingReplayRecord(relinked, CaseFilingReplayEvent.Exhausted, 1, "x", Now).OperationKey);
    }

    [Fact]
    public async Task ANotDuePageIsPassedOverFromItsCursorUntilTheLimitIsDue()
    {
        var harness = new Harness();
        var early = harness.Add(failedAttempts: 1, lastFailedAtUtc: Now.AddSeconds(-1));
        var second = harness.Add(failedAttempts: 1, lastFailedAtUtc: Now.AddSeconds(-1));
        var due = harness.Add();

        var result = await harness.Sweep.ExecuteAsync(2);

        Assert.Equal(2, harness.Store.Pages.Count);
        Assert.Equal(new CaseFilingReplayCursor(second.LinkedAtUtc, second.ReceiptId), harness.Store.Pages[1].After);
        Assert.Equal([due.ReceiptId], harness.Promotion.Prepared);
        Assert.DoesNotContain(early.ReceiptId, harness.Promotion.Prepared);
        Assert.Equal(1, result.Candidates);
        Assert.Empty(harness.Store.Records);
    }

    /// <summary>
    /// A filer answer that nothing is left to file filed nothing, so the
    /// result does not count it as filed.
    /// </summary>
    [Fact]
    public async Task NothingLeftToFileIsNotCountedAsFiled()
    {
        var harness = new Harness();
        harness.Add();

        var result = await harness.Sweep.ExecuteAsync(5);

        Assert.Single(harness.Promotion.Prepared);
        Assert.Equal(new ReplayCaseFilingsResult(1, 0, 0, 0, 0), result);
        Assert.Empty(harness.Store.Records);
    }

    [Fact]
    public async Task ADeferredFilingIsCountedAndRecordsNothing()
    {
        var harness = new Harness();
        harness.Add();
        harness.Promotion.Disposition = AutomaticCaseEvidencePromotionPreparationDisposition.Deferred;

        var result = await harness.Sweep.ExecuteAsync(5);

        Assert.Equal(new ReplayCaseFilingsResult(1, 0, 1, 0, 0), result);
        Assert.Empty(harness.Store.Records);
    }

    [Fact]
    public async Task AReceiptRelinkedSinceThePageIsNotAttempted()
    {
        var harness = new Harness();
        var candidate = harness.Add();
        harness.Receipts.Linked[candidate.ReceiptId] = Guid.NewGuid();

        var result = await harness.Sweep.ExecuteAsync(5);

        Assert.Empty(harness.Promotion.Prepared);
        Assert.Empty(harness.Store.Records);
        Assert.Equal(new ReplayCaseFilingsResult(1, 0, 0, 0, 0), result);
    }

    private static CaseFilingReplayCandidate Candidate(int failedAttempts, DateTimeOffset? lastFailedAtUtc) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(-30), 1, 3, failedAttempts, lastFailedAtUtc, 0, 0, 0);

    private sealed class Harness
    {
        public Harness()
        {
            Sweep = new ReplayCaseFilings(
                Store,
                Receipts,
                new PromoteAssociatedIntakeCaseEvidence(null!, null!, Promotion),
                new FixedTimeProvider(Now));
        }

        public FakeReplayStore Store { get; } = new();
        public FakeReceipts Receipts { get; } = new();
        public FakePromotionStore Promotion { get; } = new();
        public ReplayCaseFilings Sweep { get; }

        public CaseFilingReplayCandidate Add(int failedAttempts = 0, DateTimeOffset? lastFailedAtUtc = null)
        {
            var candidate = Candidate(failedAttempts, lastFailedAtUtc) with
            {
                LinkedAtUtc = Now.AddMinutes(-30).AddSeconds(Store.Candidates.Count)
            };
            Store.Candidates.Add(candidate);
            Receipts.Linked[candidate.ReceiptId] = candidate.CaseId;
            return candidate;
        }
    }

    private sealed record ListedPage(
        CaseFilingReplayCursor? After,
        DateTimeOffset Cutoff,
        DateTimeOffset NowUtc,
        int MaximumItems);

    private sealed class FakeReplayStore : ICaseFilingReplayStore
    {
        public List<CaseFilingReplayCandidate> Candidates { get; } = [];
        public List<ListedPage> Pages { get; } = [];
        public List<CaseFilingReplayRecord> Records { get; } = [];

        public Task<IReadOnlyList<CaseFilingReplayCandidate>> ListDueAsync(
            CaseFilingReplayCursor? after,
            DateTimeOffset systemAssociationCutoff,
            DateTimeOffset nowUtc,
            int maximumItems,
            CancellationToken cancellationToken)
        {
            Pages.Add(new(after, systemAssociationCutoff, nowUtc, maximumItems));
            IReadOnlyList<CaseFilingReplayCandidate> page = Candidates
                .OrderBy(candidate => candidate.LinkedAtUtc)
                .ThenBy(candidate => candidate.ReceiptId)
                .Where(candidate => after is null
                    || candidate.LinkedAtUtc > after.LinkedAtUtc
                    || (candidate.LinkedAtUtc == after.LinkedAtUtc
                        && candidate.ReceiptId.CompareTo(after.ReceiptId) > 0))
                .Take(maximumItems)
                .ToArray();
            return Task.FromResult(page);
        }

        public Task RecordAsync(CaseFilingReplayRecord entry, CancellationToken cancellationToken)
        {
            Records.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReceipts : IIntakeReceiptQueries
    {
        public Dictionary<Guid, Guid> Linked { get; } = [];

        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IntakeReceipt?>(Linked.TryGetValue(id, out var caseId) ? Receipt(id, caseId) : null);

        private static IntakeReceipt Receipt(Guid id, Guid caseId) => new(
            id,
            "follow-up.eml",
            "message/rfc822",
            3,
            new string('d', 64),
            new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, id.ToString("N")),
            Now,
            Now,
            IntakeDecision.NeedsSorting,
            "test",
            [],
            [],
            null,
            [],
            null,
            null,
            false,
            "test",
            "1",
            null,
            null,
            Assets:
            [
                new IntakeAssetRecord(
                    Guid.NewGuid(),
                    "source",
                    "follow-up.eml",
                    "message/rfc822",
                    IntakeAssetKind.Source,
                    IntakeAssetDisposition.Source,
                    3,
                    new string('d', 64),
                    $"retained/{id:N}",
                    null,
                    null,
                    null,
                    null)
            ],
            ManualLinkedCaseId: caseId,
            ManualAssociationVersion: 1,
            ManualAssociationActorKind: ActorKind.SystemWorker);
    }

    /// <summary>
    /// The filer's plan step: it answers or throws as the test sets, so the
    /// filer never reaches content or custody.
    /// </summary>
    private sealed class FakePromotionStore : IAutomaticCaseEvidencePromotionStore
    {
        public Exception? Fault { get; set; }
        public AutomaticCaseEvidencePromotionPreparationDisposition Disposition { get; set; } =
            AutomaticCaseEvidencePromotionPreparationDisposition.NotApplicable;
        public List<Guid> Prepared { get; } = [];

        public Task<AutomaticCaseEvidencePromotionPreparation> PrepareAsync(
            AutomaticCaseEvidencePromotionRequest request,
            CancellationToken cancellationToken)
        {
            Prepared.Add(request.IntakeReceiptId);
            return Fault is { } fault
                ? Task.FromException<AutomaticCaseEvidencePromotionPreparation>(fault)
                : Task.FromResult(new AutomaticCaseEvidencePromotionPreparation(Disposition));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
