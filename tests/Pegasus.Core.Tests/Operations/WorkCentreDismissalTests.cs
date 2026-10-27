using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

/// <summary>
/// Dismiss on the Work Centre (FRD-15): a record's rows that began at or
/// before its dismissal are hidden for everyone; a row that began after shows.
/// </summary>
public sealed class WorkCentreDismissalTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 10, 2, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ARowIsDismissedOnlyWhenItBeganAtOrBeforeTheDismissal()
    {
        Assert.True(WorkCentreDismissalPolicy.IsDismissed(NowUtc.AddMinutes(-1), NowUtc));
        Assert.True(WorkCentreDismissalPolicy.IsDismissed(NowUtc, NowUtc));
        Assert.False(WorkCentreDismissalPolicy.IsDismissed(NowUtc.AddMinutes(1), NowUtc));
        Assert.False(WorkCentreDismissalPolicy.IsDismissed(NowUtc, null));
    }

    [Fact]
    public void AnAiJobRowBeginsWhenTheJobEnteredTheStateItShows()
    {
        var created = NowUtc.AddHours(-3);
        var queued = NewJob(AiJobState.Queued, created);
        Assert.Equal(created, WorkCentreDismissalPolicy.AiJobQualifiedAt(queued));
        // A lapsed lease returned the job to the queue when the lease ended.
        Assert.Equal(NowUtc.AddHours(-1), WorkCentreDismissalPolicy.AiJobQualifiedAt(
            queued with { LeaseExpiresAtUtc = NowUtc.AddHours(-1) }));
        Assert.Equal(NowUtc.AddHours(-2), WorkCentreDismissalPolicy.AiJobQualifiedAt(
            NewJob(AiJobState.Taken, created) with { TakenAtUtc = NowUtc.AddHours(-2) }));
        Assert.Equal(NowUtc.AddMinutes(-30), WorkCentreDismissalPolicy.AiJobQualifiedAt(
            NewJob(AiJobState.DraftReady, created) with { DraftReadyAtUtc = NowUtc.AddMinutes(-30) }));
        Assert.Equal(NowUtc.AddMinutes(-10), WorkCentreDismissalPolicy.AiJobQualifiedAt(
            NewJob(AiJobState.Failed, created) with { ClosedAtUtc = NowUtc.AddMinutes(-10) }));
    }

    [Fact]
    public async Task DismissRecordsTheRecordAtNowForTheStaffMember()
    {
        var store = new InMemoryWorkCentreDismissals();
        var recordId = Guid.NewGuid();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await new DismissWorkCentreItem(store, new FixedTimeProvider(NowUtc))
            .ExecuteAsync(new DismissWorkCentreItemRequest(recordId, actor), CancellationToken.None);

        var dismissed = await store.ListAsync([recordId], CancellationToken.None);
        Assert.Equal(NowUtc, dismissed[recordId]);
        Assert.Equal(actor.SubjectId, store.DismissedBy[recordId]);
    }

    [Fact]
    public async Task TheAutomationActorDismissesInItsOwnName()
    {
        var store = new InMemoryWorkCentreDismissals();
        var recordId = Guid.NewGuid();

        await new DismissWorkCentreItem(store, new FixedTimeProvider(NowUtc)).ExecuteAsync(
            new DismissWorkCentreItemRequest(recordId, ActionActor.Automation("grant-1")),
            CancellationToken.None);

        Assert.Equal("grant-1", store.DismissedBy[recordId]);
    }

    [Fact]
    public async Task OnlyACaseworkActorCanDismiss()
    {
        var store = new InMemoryWorkCentreDismissals();
        var dismiss = new DismissWorkCentreItem(store, new FixedTimeProvider(NowUtc));

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => dismiss.ExecuteAsync(
            new DismissWorkCentreItemRequest(Guid.NewGuid(), ActionActor.SystemWorker("worker")),
            CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => dismiss.ExecuteAsync(
            new DismissWorkCentreItemRequest(Guid.NewGuid(), ActionActor.Principal(Guid.NewGuid())),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => dismiss.ExecuteAsync(
            new DismissWorkCentreItemRequest(Guid.Empty, ActionActor.Staff(Guid.NewGuid(), [StaffRole.User])),
            CancellationToken.None));
        Assert.Empty(store.DismissedBy);
    }

    [Fact]
    public async Task TheAiJobsSectionHidesADismissedJobUntilItChangesState()
    {
        var queued = NewJob(AiJobState.Queued, NowUtc.AddHours(-3));
        var other = NewJob(AiJobState.Queued, NowUtc.AddHours(-2));
        var store = new InMemoryWorkCentreDismissals();
        await store.DismissAsync(queued.JobId, NowUtc.AddHours(-1), Staff(), CancellationToken.None);
        var list = new ListWorkCentreAiJobs(new StubAiJobs([]), new StubAdoptions(), store);

        var whileQueued = await list.ExecuteAsync(Staff(), [queued, other], 2, NowUtc, CancellationToken.None);
        Assert.Equal([other.JobId], whileQueued.Select(row => row.Job.JobId).ToArray());

        // Taken after the dismissal: a new occurrence, so the job shows again.
        var taken = queued with { State = AiJobState.Taken, TakenAtUtc = NowUtc.AddMinutes(-5) };
        var afterTaken = await list.ExecuteAsync(Staff(), [taken, other], 2, NowUtc, CancellationToken.None);
        Assert.Equal([taken.JobId, other.JobId], afterTaken.Select(row => row.Job.JobId).ToArray());
    }

    [Fact]
    public async Task TheAiJobsSectionKeepsItsSelectionAndOrder()
    {
        var queued = NewJob(AiJobState.Queued, NowUtc.AddHours(-1));
        var taken = NewJob(AiJobState.Taken, NowUtc.AddHours(-2)) with { TakenAtUtc = NowUtc.AddHours(-1) };
        var draft = NewJob(AiJobState.DraftReady, NowUtc.AddHours(-3)) with { DraftReadyAtUtc = NowUtc.AddHours(-1) };
        var research = NewJob(AiJobState.Queued, NowUtc) with { Kind = AiJobKind.MarketResearch };
        var failedRecently = NewJob(AiJobState.Failed, NowUtc.AddDays(-2)) with { ClosedAtUtc = NowUtc.AddDays(-1) };
        var failedLongAgo = NewJob(AiJobState.Failed, NowUtc.AddDays(-30)) with { ClosedAtUtc = NowUtc.AddDays(-20) };
        var completedRecently = NewJob(AiJobState.Completed, NowUtc.AddDays(-2)) with { ClosedAtUtc = NowUtc.AddHours(-2) };
        var completedLongAgo = NewJob(AiJobState.Completed, NowUtc.AddDays(-30)) with { ClosedAtUtc = NowUtc.AddDays(-20) };
        var list = new ListWorkCentreAiJobs(
            new StubAiJobs([failedRecently, failedLongAgo, completedRecently, completedLongAgo]),
            new StubAdoptions(),
            new InMemoryWorkCentreDismissals());

        var rows = await list.ExecuteAsync(
            Staff(), [queued, taken, draft, research], 2, NowUtc, CancellationToken.None);

        // A job that completed or failed inside the window shows below the
        // live work; one closed before it does not show at all.
        Assert.Equal(
            [draft.JobId, taken.JobId, queued.JobId, completedRecently.JobId, failedRecently.JobId],
            rows.Select(row => row.Job.JobId).ToArray());
        Assert.NotNull(rows[0].Draft);
        Assert.All(rows.Skip(1), row => Assert.Null(row.Draft));
    }

    [Fact]
    public async Task TheAdoptingEngineerIsCarriedOnTheEstimateRowOnly()
    {
        var estimate = NewJob(AiJobState.Completed, NowUtc.AddDays(-1)) with { ClosedAtUtc = NowUtc.AddHours(-2) };
        var query = NewJob(AiJobState.Completed, NowUtc.AddDays(-1)) with
        {
            Kind = AiJobKind.QueryResponse,
            ClosedAtUtc = NowUtc.AddHours(-2)
        };
        var engineer = Guid.NewGuid();
        var adoptions = new StubAdoptions { [estimate.JobId] = engineer };
        var list = new ListWorkCentreAiJobs(
            new StubAiJobs([estimate, query]), adoptions, new InMemoryWorkCentreDismissals());

        var rows = await list.ExecuteAsync(Staff(), [], 2, NowUtc, CancellationToken.None);

        Assert.Equal(engineer, rows.Single(row => row.Job.JobId == estimate.JobId).AdoptedByStaffId);
        Assert.Null(rows.Single(row => row.Job.JobId == query.JobId).AdoptedByStaffId);
        // Only the estimate job is asked about: a query draft is never adopted
        // into a Repair specification.
        Assert.Equal([estimate.JobId], adoptions.Asked.ToArray());
    }

    [Theory]
    [InlineData(WorkCentreAiActionFilter.Estimate, AiJobKind.Estimate, AiJobState.DraftReady, true)]
    [InlineData(WorkCentreAiActionFilter.Estimate, AiJobKind.QueryResponse, AiJobState.DraftReady, false)]
    [InlineData(WorkCentreAiActionFilter.QueryDraft, AiJobKind.QueryResponse, AiJobState.Queued, true)]
    [InlineData(WorkCentreAiActionFilter.QueryDraft, AiJobKind.Estimate, AiJobState.Queued, false)]
    [InlineData(WorkCentreAiActionFilter.Failed, AiJobKind.Estimate, AiJobState.Failed, true)]
    [InlineData(WorkCentreAiActionFilter.Failed, AiJobKind.Estimate, AiJobState.Completed, false)]
    [InlineData(WorkCentreAiActionFilter.All, AiJobKind.UnidentifiedResolution, AiJobState.Taken, true)]
    public void TheSubFilterNamesTheJobKindOrItsFailure(
        WorkCentreAiActionFilter filter, AiJobKind kind, AiJobState state, bool expected)
    {
        var job = NewJob(state, NowUtc) with { Kind = kind };

        Assert.Equal(expected, WorkCentreAiActionPolicy.Matches(filter, job));
    }

    [Fact]
    public async Task TwoSectionsShareOneStoreLookupForBothTheirRecords()
    {
        var store = new InMemoryWorkCentreDismissals();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await store.DismissAsync(first, NowUtc, Staff(), CancellationToken.None);
        var batch = new WorkCentreDismissalBatch(store, sections: 2, CancellationToken.None);
        using var attention = batch.Join();
        using var aiJobs = batch.Join();

        var attentionRead = attention.ListAsync([first, second], CancellationToken.None);
        Assert.False(attentionRead.IsCompleted);
        var aiJobsRead = await aiJobs.ListAsync([first], CancellationToken.None);

        Assert.Equal(1, store.Reads);
        Assert.Equal(NowUtc, aiJobsRead[first]);
        Assert.Equal(NowUtc, (await attentionRead)[first]);
    }

    [Fact]
    public async Task ASectionThatWithdrawsNeverHoldsTheOtherBack()
    {
        var store = new InMemoryWorkCentreDismissals();
        var recordId = Guid.NewGuid();
        await store.DismissAsync(recordId, NowUtc, Staff(), CancellationToken.None);
        var batch = new WorkCentreDismissalBatch(store, sections: 2, CancellationToken.None);
        var failed = batch.Join();
        using var shown = batch.Join();

        var read = shown.ListAsync([recordId], CancellationToken.None);
        Assert.False(read.IsCompleted);
        failed.Dispose();

        Assert.Equal(NowUtc, (await read)[recordId]);
        Assert.Equal(1, store.Reads);
    }

    [Fact]
    public async Task NoStoreLookupIsSentWhenNoSectionHasARecord()
    {
        var store = new InMemoryWorkCentreDismissals();
        var batch = new WorkCentreDismissalBatch(store, sections: 2, CancellationToken.None);
        using var attention = batch.Join();
        using var aiJobs = batch.Join();

        var read = attention.ListAsync([], CancellationToken.None);
        aiJobs.Dispose();

        Assert.Empty(await read);
        Assert.Equal(0, store.Reads);
    }

    private static ActionActor Staff() => ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private static AiJobRecord NewJob(AiJobState state, DateTimeOffset createdAtUtc) => new(
        Guid.NewGuid(),
        AiJobKind.Estimate,
        AiJobSubjectKind.Case,
        Guid.NewGuid(),
        "QDOS26001",
        "Draft the estimate.",
        null,
        null,
        state,
        ActorKind.Staff,
        "staff",
        createdAtUtc,
        NowUtc.AddDays(1),
        null, null, null, null, null, null, null, null, null,
        1);

    private sealed class StubAiJobs(IReadOnlyList<AiJobRecord> recent) : IAiJobQueries
    {
        public Task<IReadOnlyList<AiJobRecord>> ListOpenAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AiJobQueryPage> ListOpenPageAsync(AiJobKind? kind, string grantId, DateTimeOffset? afterCreatedAtUtc, Guid? afterJobId, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AiJobRecord>> ListOpenForSubjectAsync(Guid subjectId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AiJobRecord?> GetPendingMarketResearchAsync(Guid subjectId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AiJobRecord>> ListRecentAsync(int max, CancellationToken cancellationToken) =>
            Task.FromResult(recent);

        public Task<AiJobCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubAdoptions : IAiDraftAdoptionQueries
    {
        private readonly Dictionary<Guid, Guid> adopted = [];

        public List<Guid> Asked { get; } = [];

        public Guid this[Guid jobId]
        {
            set => adopted[jobId] = value;
        }

        public Task<IReadOnlyDictionary<Guid, Guid>> ReadAsync(
            IReadOnlyCollection<Guid> jobIds,
            CancellationToken cancellationToken)
        {
            Asked.AddRange(jobIds);
            return Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(
                adopted.Where(pair => jobIds.Contains(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

/// <summary>The dismissal store in memory: a later dismissal moves the instant on, an earlier one never back.</summary>
internal sealed class InMemoryWorkCentreDismissals : IWorkCentreDismissalStore
{
    private readonly Dictionary<Guid, DateTimeOffset> dismissed = [];

    public Dictionary<Guid, string> DismissedBy { get; } = [];

    public int Reads { get; private set; }

    public Task DismissAsync(
        Guid recordId,
        DateTimeOffset dismissedAtUtc,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        if (!dismissed.TryGetValue(recordId, out var existing) || existing < dismissedAtUtc)
        {
            dismissed[recordId] = dismissedAtUtc;
            DismissedBy[recordId] = actor.SubjectId;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ListAsync(
        IReadOnlyCollection<Guid> recordIds,
        CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult<IReadOnlyDictionary<Guid, DateTimeOffset>>(
            dismissed.Where(pair => recordIds.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value));
    }
}
