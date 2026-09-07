using System.Collections.Immutable;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Operations;

/// <summary>
/// "Today" and "this week" on the dashboard mean the office's today and week.
/// </summary>
/// <remarks>
/// Counting from a UTC midnight would move the boundary by an hour for half
/// the year and silently reassign work between days, which is exactly the
/// kind of quiet wrongness a count nobody can check invites.
/// </remarks>
public sealed class DashboardBoundaryTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 8, 5, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BritishSummerTimeDayStartsAtTheOfficeMidnightNotTheUtcOne()
    {
        // 00:30 on 5 August in London is 23:30 on 4 August UTC. The office's
        // day has already started; a UTC-midnight boundary would still be
        // counting the previous day.
        var (dayStartUtc, _, _) = LondonCalendar.DayAndWeekBoundariesAt(
            new DateTimeOffset(2026, 8, 4, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 8, 4, 23, 0, 0, TimeSpan.Zero), dayStartUtc);
    }

    [Fact]
    public void WeekStartsOnMondayBecauseThatIsTheWeekTheOfficeWorksTo()
    {
        // Wednesday 5 August 2026, midday London.
        var (_, _, weekStartUtc) = LondonCalendar.DayAndWeekBoundariesAt(NowUtc);

        // Monday 3 August, 00:00 London == 23:00 UTC on Sunday 2 August.
        Assert.Equal(new DateTimeOffset(2026, 8, 2, 23, 0, 0, TimeSpan.Zero), weekStartUtc);
    }

    [Fact]
    public void OnAMondayTheWeekStartsThatMorningRatherThanSevenDaysEarlier()
    {
        // Monday 3 August 2026, 09:00 London.
        var (dayStartUtc, _, weekStartUtc) = LondonCalendar.DayAndWeekBoundariesAt(
            new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(dayStartUtc, weekStartUtc);
    }

    [Fact]
    public async Task NeedsAttentionIncludesTheLastHourOfTheGmtTransitionSundayInToday()
    {
        var afterTheTransition =
            new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        var dueBeforeOfficeMidnight =
            new DateTimeOffset(2026, 10, 25, 23, 30, 0, TimeSpan.Zero);
        var snapshot = await ExecuteAsync(
            new RecordingDashboardQueries(),
            afterTheTransition,
            dueWork: new StubDueWorkQueries
            {
                Due = [NewDueWork(Guid.NewGuid(), "C/2026/009", dueBeforeOfficeMidnight)],
            });

        var item = Assert.Single(snapshot.NeedsAttention);
        Assert.Equal(NeedsAttentionPriority.Today, item.Priority);
    }

    /// <summary>
    /// FRD-12 § Work Centre: a needs-attention item is exactly one of the
    /// five kinds, each read from the query that already backs its Cases tab
    /// or Operations table — never a fixture row.
    /// </summary>
    [Fact]
    public async Task NeedsAttentionListsOneRowPerKindFromItsFiveQueries()
    {
        var recorder = new RecordingDashboardQueries();
        var caseId = Guid.NewGuid();
        var search = new StubSearchCases
        {
            Items =
            [
                new(
                    caseId, "C/2026/004", null, CaseType.Inspection, "QDOS",
                    CaseLifecycleState.Held, EngineerId: null, Registration: "KP68 ABC",
                    Claimant: "Meridian Claims", ClaimNumber: null, ReceivedAtUtc: NowUtc,
                    InstructionDate: null, Origin: "Instruction-initiated", CreatedAtUtc: NowUtc)
            ],
        };
        var unidentifiedId = Guid.NewGuid();
        var snapshot = await ExecuteAsync(
            recorder,
            NowUtc,
            searchCases: search,
            unidentified: new StubUnidentifiedQueue
            {
                Rows =
                [
                    new(
                        unidentifiedId, "U1042", UnidentifiedMediaKind.Image,
                        FileName: "IMG_4418.jpg", EmailSubject: null, EmailSender: null,
                        ReceivedAtUtc: NowUtc, UnidentifiedReasonCode.NoUsableIdentification)
                ],
            },
            triage: new StubListTriage
            {
                Items = [NewTriage(Guid.NewGuid(), "AB12CDE", TriageState.Open)],
            },
            dueWork: new StubDueWorkQueries
            {
                Due = [NewDueWork(caseId, "C/2026/009", NowUtc.AddHours(-1))],
            },
            requestStore: new StubRequestOperationStore
            {
                Items = [NewExternalWork(canRetry: true)],
            });

        Assert.Equivalent(
            new[]
            {
                NeedsAttentionKind.Case,
                NeedsAttentionKind.HeldDecision,
                NeedsAttentionKind.Mail,
                NeedsAttentionKind.Triage,
                NeedsAttentionKind.ExternalWork
            },
            snapshot.NeedsAttention.Select(item => item.Kind).ToArray());
    }

    /// <summary>
    /// The Triage kind is work without a finding, and the External work kind
    /// is failure that can still be retried: rows past those boundaries stay
    /// on their own screens.
    /// </summary>
    /// <remarks>
    /// Only the FindingRecorded record is fed — with the no-finding states
    /// queried directly, an Open record is *expected* to appear, so feeding
    /// one here and asserting absence would contradict the projection. The
    /// FindingRecorded row is the one that must never surface.
    /// </remarks>
    [Fact]
    public async Task NeedsAttentionSkipsTriageWithAFindingAndExternalWorkThatCannotRetry()
    {
        var recorder = new RecordingDashboardQueries();
        var snapshot = await ExecuteAsync(
            recorder,
            NowUtc,
            triage: new StubListTriage
            {
                Items = [NewTriage(Guid.NewGuid(), "XY98Z", TriageState.FindingRecorded)],
            },
            requestStore: new StubRequestOperationStore
            {
                Items = [NewExternalWork(canRetry: false)],
            });

        var kinds = snapshot.NeedsAttention.Select(item => item.Kind).ToArray();
        Assert.DoesNotContain(NeedsAttentionKind.Triage, kinds);
        Assert.DoesNotContain(NeedsAttentionKind.ExternalWork, kinds);
    }

    /// <summary>
    /// The Triage list is newest-first across every state, so one unfiltered
    /// page would bury an open record behind fifty settled ones. The
    /// projection queries the no-finding states directly, so the Open record
    /// is read however deep the settled list is.
    /// </summary>
    /// <remarks>
    /// The Open record sits last, past the page-one cut the stub models: a
    /// revert to a single unfiltered read would truncate it away and fail
    /// here, which is what makes this test a guard rather than a tautology.
    /// </remarks>
    [Fact]
    public async Task NeedsAttentionStillListsOpenTriageBehindFiftySettledRecords()
    {
        var recorder = new RecordingDashboardQueries();
        var triageItems = Enumerable.Range(1, GetOperationsSnapshot.MaximumNeedsAttention)
            .Select(index => NewTriage(Guid.NewGuid(), $"S{index:000}", TriageState.FindingRecorded))
            .ToList();
        triageItems.Add(NewTriage(Guid.NewGuid(), "AB12CDE", TriageState.Open));

        var snapshot = await ExecuteAsync(
            recorder,
            NowUtc,
            triage: new StubListTriage { Items = triageItems });

        Assert.Contains(snapshot.NeedsAttention, item => item.Kind == NeedsAttentionKind.Triage);
    }

    [Fact]
    public async Task NeedsAttentionOrdersByPriorityThenDueThenReference()
    {
        var recorder = new RecordingDashboardQueries();
        // Overdue chase, retryable failure (High), then three no-due rows
        // whose references fix the tail order alphabetically.
        var search = new StubSearchCases
        {
            Items = [NewHeldCase(Guid.NewGuid(), "H2000")],
        };
        var snapshot = await ExecuteAsync(
            recorder,
            NowUtc,
            searchCases: search,
            unidentified: new StubUnidentifiedQueue
            {
                Rows = [NewUnidentified(Guid.NewGuid(), "U1000")],
            },
            triage: new StubListTriage { Items = [NewTriage(Guid.NewGuid(), "AB12CDE", TriageState.Open)] },
            dueWork: new StubDueWorkQueries
            {
                Due = [NewDueWork(Guid.NewGuid(), "C/2026/009", NowUtc.AddHours(-1))],
            },
            requestStore: new StubRequestOperationStore { Items = [NewExternalWork(canRetry: true)] });

        Assert.Equal(
            [
                NeedsAttentionKind.Case,        // Overdue
                NeedsAttentionKind.ExternalWork, // High
                NeedsAttentionKind.Triage,       // Normal, "AB12CDE"
                NeedsAttentionKind.HeldDecision, // Normal, "H2000"
                NeedsAttentionKind.Mail          // Normal, "U1000"
            ],
            snapshot.NeedsAttention.Select(item => item.Kind).ToArray());
    }

    [Fact]
    public async Task NeedsAttentionIsBoundedAtFiftyRows()
    {
        var recorder = new RecordingDashboardQueries();
        var rows = Enumerable.Range(1, GetOperationsSnapshot.MaximumNeedsAttention + 10)
            .Select(index => NewUnidentified(Guid.NewGuid(), $"U{1000 + index}"))
            .ToArray();
        var snapshot = await ExecuteAsync(
            recorder,
            NowUtc,
            unidentified: new StubUnidentifiedQueue { Rows = rows });

        Assert.Equal(rows.Length, snapshot.UnidentifiedCount);
        Assert.Equal(GetOperationsSnapshot.MaximumNeedsAttention, snapshot.NeedsAttention.Count);
    }

    private static async Task<OperationsSnapshot> ExecuteAsync(
        RecordingDashboardQueries recorder,
        DateTimeOffset nowUtc,
        StubSearchCases? searchCases = null,
        StubUnidentifiedQueue? unidentified = null,
        StubListTriage? triage = null,
        StubDueWorkQueries? dueWork = null,
        StubRequestOperationStore? requestStore = null)
    {
        var timeProvider = new FixedTimeProvider(nowUtc);
        var snapshot = new GetOperationsSnapshot(
            new StubIntakeReceiptQueries(),
            triage ?? new StubListTriage(),
            dueWork ?? new StubDueWorkQueries(),
            recorder,
            searchCases ?? new StubSearchCases(),
            unidentified ?? new StubUnidentifiedQueue(),
            new GetRequestOperations(requestStore ?? new StubRequestOperationStore(), timeProvider),
            new NoStaffAccounts(),
            timeProvider);
        return await snapshot.ExecuteAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]));
    }

    private static CaseDueWork NewDueWork(Guid caseId, string reference, DateTimeOffset? nextChaseAtUtc) => new(
        caseId,
        reference,
        "Images missing",
        DueBy: null,
        CaseDueWorkState.Scheduled,
        nextChaseAtUtc,
        HeldAtUtc: null,
        RemainingChaseInterval: null,
        MostRecentChannel: "E-mail",
        MostRecentOutcome: "E-mail sent",
        MostRecentNote: null,
        Version: 1);

    private static CaseSearchItem NewHeldCase(Guid caseId, string reference) => new(
        caseId,
        reference,
        AuditReference: null,
        CaseType.Inspection,
        "QDOS",
        CaseLifecycleState.Held,
        EngineerId: null,
        Registration: "KP68 ABC",
        Claimant: "Meridian Claims",
        ClaimNumber: null,
        ReceivedAtUtc: NowUtc,
        InstructionDate: null,
        Origin: "Instruction-initiated",
        CreatedAtUtc: NowUtc);

    private static TriageSummary NewTriage(Guid id, string registration, TriageState state) => new(
        id,
        registration,
        state,
        AssigneeId: null,
        LinkedCaseId: null,
        CreatedAtUtc: NowUtc,
        Version: 1,
        Reference: null,
        Provider: null);

    private static UnidentifiedQueueRow NewUnidentified(Guid id, string reference) => new(
        id,
        reference,
        UnidentifiedMediaKind.Image,
        FileName: "IMG_4418.jpg",
        EmailSubject: null,
        EmailSender: null,
        ReceivedAtUtc: NowUtc,
        UnidentifiedReasonCode.NoUsableIdentification);

    private static RequestOperationProjection NewExternalWork(bool canRetry) => new(
        Guid.NewGuid(),
        RequestOperationKind.ExternalWork,
        RequestOperationState.Failed,
        Guid.NewGuid(),
        "C/2026/009",
        "QDOS",
        NowUtc,
        ExpiresAtUtc: null,
        Version: 1,
        AcceptedFileCount: null,
        AcceptedByteCount: null,
        MaximumFileCount: null,
        MaximumByteCount: null,
        LimitsVersion: null,
        ExternalKind: "document_custody",
        AttemptCount: 2,
        FailureCode: "custody_failed",
        FailureReason: "The document could not be placed in accepted Case custody.",
        canRetry,
        CanRevoke: false,
        CaseVersion: 1,
        RequestCaseEditLeaseState.Available,
        CaseEditLeaseExpiresAtUtc: null);

    private sealed class RecordingDashboardQueries : IDashboardQueries
    {
        public Task<CaseStageCounts> GetCaseStageCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CaseStageCounts(0, 0, 0, 0));
    }

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }

    private sealed class StubIntakeReceiptQueries : IIntakeReceiptQueries
    {
        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeQueueCounts(0, 0));

        public Task<IntakeListPage> ListAsync(
            IntakeDecision? decision,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeListPage([], page, pageSize, 0));

        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IntakeReceipt?>(null);

        public Task<IntakeAssetRecord?> GetAssetAsync(
            Guid receiptId,
            Guid assetId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IntakeAssetRecord?>(null);
    }

    /// <summary>
    /// Pages the way <see cref="ListTriage"/> does — state filter, then a
    /// Skip/Take window with the full match count — so a read that asks for
    /// one unfiltered page is truncated exactly as the real store truncates
    /// it. Rows keep insertion order; callers that need a record to survive
    /// paging place it inside the window.
    /// </summary>
    private sealed class StubListTriage : IListTriage
    {
        public IReadOnlyList<TriageSummary> Items { get; init; } = [];

        public Task<TriageListPage> ExecuteAsync(
            ListTriageQuery query,
            CancellationToken cancellationToken = default)
        {
            var matches = Items
                .Where(item => query.State is null || item.State == query.State)
                .ToArray();
            var page = matches
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToArray();
            return Task.FromResult(new TriageListPage(page, query.Page, query.PageSize, matches.Length));
        }
    }

    private sealed class StubDueWorkQueries : ICaseDueWorkQueries
    {
        public IReadOnlyList<CaseDueWork> Due { get; init; } = [];

        public Task<CaseDueWork?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult(Due.FirstOrDefault(work => work.CaseId == caseId));

        public Task<IReadOnlyList<CaseDueWork>> GetDueAsync(
            DateTimeOffset asOfUtc,
            int maximumResults,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseDueWork>>(Due.Take(maximumResults).ToArray());
    }

    private sealed class StubSearchCases : ISearchCases
    {
        public IReadOnlyList<CaseSearchItem> Items { get; init; } = [];

        public Task<SearchCasesResult> ExecuteAsync(
            SearchCasesQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SearchCasesResult(Items, query.Page, query.PageSize, false, false));
    }

    private sealed class StubUnidentifiedQueue : IUnidentifiedStore
    {
        public IReadOnlyList<UnidentifiedQueueRow> Rows { get; init; } = [];

        public Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(
            UnidentifiedMediaKind? mediaKind,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnidentifiedQueueRow>>(
                Rows.Where(row => mediaKind is null || row.MediaKind == mediaKind).ToArray());

        public Task<UnidentifiedRegisterResult> RegisterAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedResolveResult> ResolveAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedResolveResult?> ProbeResolveReplayAsync(
            ResolveUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UnidentifiedItem?> GetByReferenceAsync(
            string reference,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedItem?> GetByOriginAsync(
            UnidentifiedOrigin origin,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedItem>> ListAsync(
            UnidentifiedState? state = UnidentifiedState.Open,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(
            Guid unidentifiedItemId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubRequestOperationStore : IRequestOperationsProjectionStore
    {
        public IReadOnlyList<RequestOperationProjection> Items { get; init; } = [];

        public Task<RequestOperationsProjection> GetAsync(
            int maximumItems,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RequestOperationsProjection([.. Items], LimitReached: false));
    }

    private sealed class NoStaffAccounts : IStaffAccountQueries
    {
        public Task<StaffAccountQuerySlice> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<StaffAccountSummary?> GetAsync(Guid staffId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<SignOffEngineerProfile?> GetSignOffEngineerAsync(
            Guid staffId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");
    }
}
