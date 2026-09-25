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
                    Origin: "Instruction-initiated", CreatedAtUtc: NowUtc)
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
                NeedsAttentionKind.CaseChase,
                NeedsAttentionKind.HeldDecision,
                NeedsAttentionKind.Unidentified,
                NeedsAttentionKind.Triage
            },
            snapshot.NeedsAttention.Select(item => item.Kind).ToArray());
    }

    [Fact]
    public async Task NeedsAttentionPartitionsReviewCasesUsingTheCurrentCompletenessConfiguration()
    {
        var reviewId = Guid.NewGuid();
        var unassignedId = Guid.NewGuid();
        var searchCases = new StubSearchCases
        {
            Items =
            [
                NewHeldCase(reviewId, "C/2026/REVIEW") with
                {
                    State = CaseLifecycleState.Review,
                    InstructionComplete = true,
                    ImagesComplete = false,
                },
                NewHeldCase(unassignedId, "C/2026/ASSIGN") with
                {
                    State = CaseLifecycleState.Review,
                    InstructionComplete = false,
                    ImagesComplete = true,
                }
            ]
        };
        var snapshot = await ExecuteAsync(
            new RecordingDashboardQueries(),
            NowUtc,
            workflowConfiguration: new("case-workflow", 1)
            {
                RequireInstructions = false,
                RequireImages = true,
            },
            searchCases: searchCases);

        Assert.Contains(snapshot.NeedsAttention, item =>
            item.Kind == NeedsAttentionKind.ReviewCase
            && item.Id == reviewId
            && item.Title == "KP68 ABC"
            && item.Detail == "QDOS");
        Assert.Contains(snapshot.NeedsAttention, item =>
            item.Kind == NeedsAttentionKind.UnassignedEngineer && item.Id == unassignedId);
        Assert.DoesNotContain(snapshot.NeedsAttention, item =>
            item.Kind == NeedsAttentionKind.ReviewCase && item.Id == unassignedId);
        Assert.Equal(
            1,
            searchCases.RequestedStates.Count(state => state == CaseLifecycleState.Review));
        Assert.DoesNotContain(
            searchCases.RequestedStates,
            state => state == CaseLifecycleState.ReportPreparation);
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
        // Failed external work is never a Needs attention row (Work Centre D1).
        Assert.DoesNotContain(NeedsAttentionKind.AiDraft, kinds);
        Assert.DoesNotContain("ExternalWork", Enum.GetNames<NeedsAttentionKind>());
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
        var triageItems = Enumerable.Range(1, GetOperationsSnapshot.PageSize)
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

        // Due instant first (D2): the overdue chase, then the Unidentified item due
        // by tonight's midnight (target 0), then the Triage due tomorrow (target 1);
        // the hold has no held-at and no review date, so it is undated and last.
        Assert.Equal(
            [
                NeedsAttentionKind.CaseChase,
                NeedsAttentionKind.Unidentified,
                NeedsAttentionKind.Triage,
                NeedsAttentionKind.HeldDecision
            ],
            snapshot.NeedsAttention.Select(item => item.Kind).ToArray());
        Assert.Equal(NeedsAttentionPriority.Overdue, snapshot.NeedsAttention[0].Priority);
        Assert.Equal(NeedsAttentionPriority.Today, snapshot.NeedsAttention[1].Priority);
        Assert.Equal(NeedsAttentionPriority.Normal, snapshot.NeedsAttention[2].Priority);
        Assert.Null(snapshot.NeedsAttention[3].Due);
        Assert.Equal(1, snapshot.Attention.OverdueCount);
        Assert.Equal(1, snapshot.Attention.TodayCount);
        Assert.Equal(2, snapshot.Attention.LaterCount);
    }

    [Fact]
    public async Task TheOperationsBadgeCountsRetryableExternalFailuresOnly()
    {
        var store = new StubRequestOperationStore
        {
            RetryableFailureCount = 137
        };
        var badge = new GetOperationsBadge(store, new FixedTimeProvider(NowUtc));

        Assert.Equal(137, await badge.ExecuteAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.User])));
    }

    [Theory]
    [InlineData(StaffRole.Administrator)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public async Task MineShowsMyRowsAndTheUnownedRowsEveryStaffRoleCanTake(StaffRole role)
    {
        var staffId = Guid.NewGuid();
        var actor = ActionActor.Staff(staffId, [role]);
        var mine = NewHeldCase(Guid.NewGuid(), "H-MINE") with { EngineerId = staffId };
        var theirs = NewHeldCase(Guid.NewGuid(), "H-THEIRS") with { EngineerId = Guid.NewGuid() };
        var snapshotFor = (ActionActor actor) => new GetOperationsSnapshot(
            new StubIntakeReceiptQueries(),
            new StubListTriage { Items = [NewTriage(Guid.NewGuid(), "AB12CDE", TriageState.Open)] },
            new StubDueWorkQueries(),
            new RecordingDashboardQueries(),
            new StubSearchCases { Items = [mine, theirs] },
            new StubUnidentifiedQueue { Rows = [NewUnidentified(Guid.NewGuid(), "U1000")] },
            new UnknownStaffAccounts(),
            new FixedWorkflowConfiguration(new("case-workflow", 1)),
            new FixedTimeProvider(NowUtc)).ExecuteAsync(new NeedsAttentionQuery(actor, NeedsAttentionScope.Mine));

        var view = await snapshotFor(actor);

        Assert.Equal(
            [NeedsAttentionKind.Triage, NeedsAttentionKind.HeldDecision],
            view.NeedsAttention.Select(item => item.Kind).ToArray());
        Assert.Equal("H-MINE", view.NeedsAttention[1].Reference);
        Assert.Equal(NeedsAttentionScope.Mine, view.Scope);
        Assert.Equal(2, view.Attention.TotalCount);
    }

    [Fact]
    public async Task KindChipsCountTheScopeBeforeTheKindFilter()
    {
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var snapshot = await new GetOperationsSnapshot(
            new StubIntakeReceiptQueries(),
            new StubListTriage { Items = [NewTriage(Guid.NewGuid(), "AB12CDE", TriageState.Open)] },
            new StubDueWorkQueries(),
            new RecordingDashboardQueries(),
            new StubSearchCases(),
            new StubUnidentifiedQueue { Rows = [NewUnidentified(Guid.NewGuid(), "U3001"), NewUnidentified(Guid.NewGuid(), "U3002")] },
            new UnknownStaffAccounts(),
            new FixedWorkflowConfiguration(new("case-workflow", 1)),
            new FixedTimeProvider(NowUtc)).ExecuteAsync(
                new NeedsAttentionQuery(administrator, NeedsAttentionScope.Office, 1, [NeedsAttentionKind.Triage]));

        // The page and its totals are the filtered list...
        Assert.Equal([NeedsAttentionKind.Triage], snapshot.Attention.Items.Select(item => item.Kind).ToArray());
        Assert.Equal(1, snapshot.Attention.TotalCount);
        // ...while every chip still counts the whole scope.
        Assert.Equal(1, snapshot.Attention.KindCounts[NeedsAttentionKind.Triage]);
        Assert.Equal(2, snapshot.Attention.KindCounts[NeedsAttentionKind.Unidentified]);
    }

    /// <summary>
    /// The Triages metric (last in the strip) counts every active Triage Case:
    /// Open, Awaiting information and Finding recorded; Completed and
    /// Cancelled are not work.
    /// </summary>
    [Fact]
    public async Task TriagesMetricCountsTheThreeActiveTriageStates()
    {
        var snapshot = await ExecuteAsync(
            new RecordingDashboardQueries(),
            NowUtc,
            triage: new StubListTriage
            {
                Items =
                [
                    NewTriage(Guid.NewGuid(), "AB12CDE", TriageState.Open),
                    NewTriage(Guid.NewGuid(), "AB12CDF", TriageState.AwaitingInformation),
                    NewTriage(Guid.NewGuid(), "AB12CDG", TriageState.FindingRecorded),
                    NewTriage(Guid.NewGuid(), "AB12CDH", TriageState.Completed),
                    NewTriage(Guid.NewGuid(), "AB12CDI", TriageState.Cancelled)
                ]
            });

        Assert.Equal(3, snapshot.Metrics.Triages);
        var triageRoutes = snapshot.NeedsAttention
            .Where(item => item.Kind == NeedsAttentionKind.Triage)
            .Select(item => item.Route)
            .ToArray();
        Assert.Equal(2, triageRoutes.Length);
        Assert.All(triageRoutes, route => Assert.StartsWith("/Cases/", route, StringComparison.Ordinal));
    }

    [Fact]
    public async Task NeedsAttentionIsBoundedAtFiftyRows()
    {
        var recorder = new RecordingDashboardQueries();
        var rows = Enumerable.Range(1, GetOperationsSnapshot.PageSize + 10)
            .Select(index => NewUnidentified(Guid.NewGuid(), $"U{1000 + index}"))
            .ToArray();
        var snapshot = await ExecuteAsync(
            recorder,
            NowUtc,
            unidentified: new StubUnidentifiedQueue { Rows = rows });

        Assert.Equal(rows.Length, snapshot.UnidentifiedCount);
        Assert.Equal(GetOperationsSnapshot.PageSize, snapshot.NeedsAttention.Count);
        // Paged, never cut (D4): the counts are of the whole list.
        Assert.Equal(rows.Length, snapshot.Attention.TotalCount);
        Assert.Equal(2, snapshot.Attention.TotalPages);
        Assert.Equal(rows.Length, snapshot.Attention.KindCounts[NeedsAttentionKind.Unidentified]);
        Assert.Equal(rows.Length, snapshot.Metrics.Unidentified);
    }

    [Fact]
    public async Task NeedsAttentionReadsPastFiveHundredForEveryPagedSource()
    {
        var count = 501;
        var due = Enumerable.Range(1, count)
            .Select(index => NewDueWork(Guid.NewGuid(), $"D{index:000}", NowUtc.AddHours(-1)))
            .ToArray();
        var held = Enumerable.Range(1, count)
            .Select(index => NewHeldCase(Guid.NewGuid(), $"H{index:000}"))
            .ToArray();
        var review = Enumerable.Range(1, count)
            .Select(index => NewHeldCase(Guid.NewGuid(), $"R{index:000}") with
            {
                State = CaseLifecycleState.Review,
                EngineerId = Guid.NewGuid()
            })
            .ToArray();
        var triage = Enumerable.Range(1, count)
            .Select(index => NewTriage(Guid.NewGuid(), $"T{index:000}", TriageState.Open))
            .ToArray();
        var snapshot = BuildSnapshot(
            new RecordingDashboardQueries(), NowUtc,
            new StubSearchCases { Items = [.. held, .. review] },
            null,
            new StubListTriage { Items = triage },
            new StubDueWorkQueries { Due = due },
            null,
            null);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

        foreach (var kind in new[]
        {
            NeedsAttentionKind.CaseChase,
            NeedsAttentionKind.HeldDecision,
            NeedsAttentionKind.ReviewCase,
            NeedsAttentionKind.Triage
        })
        {
            var page = await snapshot.ExecuteAsync(new NeedsAttentionQuery(
                actor, Page: 11, Kinds: [kind]));
            Assert.Equal(count, page.Attention.TotalCount);
            Assert.Equal(count, page.Attention.KindCounts[kind]);
            Assert.Single(page.Attention.Items);
            Assert.Equal(kind, page.Attention.Items[0].Kind);
        }
    }

    [Theory]
    [InlineData(GetOperationsSnapshot.MaximumAttentionRows)]
    [InlineData(GetOperationsSnapshot.MaximumAttentionRows + 1)]
    public async Task NotificationAttentionRowsStopAtTheShellLimit(int available)
    {
        var recorder = new RecordingDashboardQueries();
        var rows = Enumerable.Range(1, available)
            .Select(index => NewUnidentified(Guid.NewGuid(), $"U{2000 + index}"))
            .ToArray();

        var attention = await ExecuteAttentionRowsAsync(
            recorder,
            NowUtc,
            unidentified: new StubUnidentifiedQueue { Rows = rows });

        Assert.Equal(Math.Min(available, GetOperationsSnapshot.MaximumAttentionRows), attention.Count);
        Assert.Equal(
            rows.Take(GetOperationsSnapshot.MaximumAttentionRows).Select(row => row.Id),
            attention.Select(row => row.Id));
    }

    private static async Task<OperationsSnapshot> ExecuteAsync(
        RecordingDashboardQueries recorder,
        DateTimeOffset nowUtc,
        StubSearchCases? searchCases = null,
        StubUnidentifiedQueue? unidentified = null,
        StubListTriage? triage = null,
        StubDueWorkQueries? dueWork = null,
        StubRequestOperationStore? requestStore = null,
        CaseWorkflowConfiguration? workflowConfiguration = null)
    {
        var snapshot = BuildSnapshot(
            recorder, nowUtc, searchCases, unidentified, triage, dueWork, requestStore, workflowConfiguration);
        return await snapshot.ExecuteAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]));
    }

    /// <summary>
    /// The notifications menu's own narrow query (<see cref="IGetAttentionRows"/>),
    /// exercised directly rather than through the full snapshot — the two
    /// share <c>FetchAttentionInputsAsync</c> but only this interface applies
    /// <see cref="GetOperationsSnapshot.MaximumAttentionRows"/>.
    /// </summary>
    private static async Task<IReadOnlyList<NeedsAttentionItem>> ExecuteAttentionRowsAsync(
        RecordingDashboardQueries recorder,
        DateTimeOffset nowUtc,
        StubSearchCases? searchCases = null,
        StubUnidentifiedQueue? unidentified = null,
        StubListTriage? triage = null,
        StubDueWorkQueries? dueWork = null,
        StubRequestOperationStore? requestStore = null,
        CaseWorkflowConfiguration? workflowConfiguration = null)
    {
        IGetAttentionRows attentionRows = BuildSnapshot(
            recorder, nowUtc, searchCases, unidentified, triage, dueWork, requestStore, workflowConfiguration);
        return await attentionRows.ExecuteAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]));
    }

    private static GetOperationsSnapshot BuildSnapshot(
        RecordingDashboardQueries recorder,
        DateTimeOffset nowUtc,
        StubSearchCases? searchCases,
        StubUnidentifiedQueue? unidentified,
        StubListTriage? triage,
        StubDueWorkQueries? dueWork,
        StubRequestOperationStore? requestStore,
        CaseWorkflowConfiguration? workflowConfiguration)
    {
        var timeProvider = new FixedTimeProvider(nowUtc);
        return new GetOperationsSnapshot(
            new StubIntakeReceiptQueries(),
            triage ?? new StubListTriage(),
            dueWork ?? new StubDueWorkQueries(),
            recorder,
            searchCases ?? new StubSearchCases(),
            unidentified ?? new StubUnidentifiedQueue(),
            new NoStaffAccounts(),
            new FixedWorkflowConfiguration(workflowConfiguration ?? new("case-workflow", 1)),
            timeProvider);
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
        Origin: "Instruction-initiated",
        CreatedAtUtc: NowUtc);

    private static TriageSummary NewTriage(Guid id, string registration, TriageState state) => new(
        id,
        registration,
        state,
        AssigneeId: null,
        LinkedInstructionCaseId: null,
        CreatedAtUtc: NowUtc,
        Version: 1,
        Reference: "t.QDOS26001",
        Provider: "QDOS");

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
        RequestOperationState.Failed,
        Guid.NewGuid(),
        "C/2026/009",
        "QDOS",
        NowUtc,
        ExternalKind: "document_custody",
        AttemptCount: 2,
        FailureCode: "custody_failed",
        FailureReason: "The document could not be placed in accepted Case custody.",
        canRetry);

    private sealed class RecordingDashboardQueries : IDashboardQueries
    {
        public Task<CaseStageCounts> GetCaseStageCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CaseStageCounts(0, 0, 0, 0));
    }

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }

    private sealed class FixedWorkflowConfiguration(CaseWorkflowConfiguration configuration)
        : ICaseWorkflowConfiguration
    {
        public Task<CaseWorkflowConfiguration> GetCurrentAsync(
            CancellationToken cancellationToken) => Task.FromResult(configuration);
    }

    private sealed class StubIntakeReceiptQueries : IIntakeReceiptQueries
    {
        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeQueueCounts(0));

        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IntakeReceipt?>(null);
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

        public Task<int> CountAsync(
            ActionActor actor,
            TriageState? state,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Count(item => state is null || item.State == state));
    }

    private sealed class StubDueWorkQueries : ICaseDueWorkQueries
    {
        public IReadOnlyList<CaseDueWork> Due { get; init; } = [];

        public Task<CaseDueWork?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult(Due.FirstOrDefault(work => work.CaseId == caseId));

        public Task<IReadOnlyList<CaseDueWork>> GetDueAsync(
            DateTimeOffset asOfUtc,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseDueWork>>(Due.Skip((page - 1) * pageSize).Take(pageSize).ToArray());
    }

    private sealed class StubSearchCases : ISearchCases
    {
        public IReadOnlyList<CaseSearchItem> Items { get; init; } = [];
        public List<CaseLifecycleState?> RequestedStates { get; } = [];

        public Task<SearchCasesResult> ExecuteAsync(
            SearchCasesQuery query,
            CancellationToken cancellationToken)
        {
            RequestedStates.Add(query.Filters.State);
            var matching = Items.Where(item => query.Filters.State is null || item.State == query.Filters.State)
                .ToArray();
            return Task.FromResult(new SearchCasesResult(
                matching.Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToArray(),
                query.Page,
                query.PageSize,
                query.Page > 1,
                query.Page * query.PageSize < matching.Length));
        }
    }

    private sealed class StubUnidentifiedQueue : IUnidentifiedStore
    {
        public UnidentifiedQueueRow[] Rows { get; init; } = [];

        public Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(
            UnidentifiedMediaKind? mediaKind,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnidentifiedQueueRow>>(
                Rows.Where(row => mediaKind is null || row.MediaKind == mediaKind).ToArray());

        public Task<int> CountOpenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.Length);

        public Task<UnidentifiedRegisterResult> RegisterAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<UnidentifiedResolveResult> ResolveAsync(
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

        public int RetryableFailureCount { get; init; }

        public Task<RequestOperationsProjection> GetAsync(
            int maximumItems,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RequestOperationsProjection([.. Items], LimitReached: false));

        public Task<int> CountRetryableExternalFailuresAsync(
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) => Task.FromResult(RetryableFailureCount);
    }

    /// <summary>Resolves nobody: every owner reads as former staff, and nothing throws.</summary>
    private sealed class UnknownStaffAccounts : IStaffAccountQueries
    {
        public Task<StaffAccountQuerySlice> ListAsync(int offset, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<StaffAccountSummary?> GetAsync(Guid staffId, CancellationToken cancellationToken) =>
            Task.FromResult<StaffAccountSummary?>(null);

        public Task<IReadOnlyList<StaffAccountSummary>> GetManyAsync(
            IReadOnlyCollection<Guid> staffIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffAccountSummary>>([]);

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");
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

        public Task<IReadOnlyList<StaffAccountSummary>> GetManyAsync(
            IReadOnlyCollection<Guid> staffIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");
    }
}
