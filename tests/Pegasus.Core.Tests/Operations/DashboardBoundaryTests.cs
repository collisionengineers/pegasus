using System.Collections.Immutable;
using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
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
                        ReceivedAtUtc: NowUtc, UnidentifiedReasonCode.NoUsableIdentification,
                        ResolutionReason: null)
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
        var owners = snapshot.NeedsAttention.ToDictionary(item => item.Kind, item => item.Owner);
        Assert.Equal("No owner", owners[NeedsAttentionKind.CaseChase]);
        Assert.Equal("Unassigned", owners[NeedsAttentionKind.HeldDecision]);
        Assert.Equal("No owner", owners[NeedsAttentionKind.Unidentified]);
        Assert.Equal("Unassigned", owners[NeedsAttentionKind.Triage]);
    }

    /// <summary>
    /// FRD-19 (INT-32): early vehicle images paired with their Case put the
    /// Case in Needs attention, due at the pairing, so staff see it is ready.
    /// </summary>
    [Fact]
    public async Task NeedsAttentionListsACaseItsVehicleImagesPairedIntoDueAtThePairing()
    {
        var caseId = Guid.NewGuid();
        var pairedAt = NowUtc.AddMinutes(-20);
        var registeredAt = NowUtc.AddHours(-2);
        var snapshot = await ExecuteAsync(
            new RecordingDashboardQueries
            {
                Paired = [new(caseId, "a.QDOS26028", "GJ13EVC-01", "QDOS", null, pairedAt, registeredAt)]
            },
            NowUtc);

        var item = Assert.Single(snapshot.NeedsAttention);
        Assert.Equal(NeedsAttentionKind.VehicleImagesPaired, item.Kind);
        Assert.Equal(caseId, item.Id);
        Assert.Equal("a.QDOS26028", item.Reference);
        Assert.Equal("GJ13EVC-01", item.Title);
        Assert.Equal("QDOS", item.Detail);
        Assert.Equal(pairedAt, item.Due);
        Assert.Equal(registeredAt, item.Received);
        Assert.Equal(NeedsAttentionPriority.Overdue, item.Priority);
        Assert.Null(item.OwnerStaffId);
        Assert.Equal("Unassigned", item.Owner);
        Assert.Equal($"/Cases/{caseId:D}", item.Route);
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
            && item.Detail == "QDOS"
            && item.Owner == "Unassigned");
        Assert.Contains(snapshot.NeedsAttention, item =>
            item.Kind == NeedsAttentionKind.UnassignedEngineer
            && item.Id == unassignedId
            && item.Owner == "Unassigned");
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

    /// <summary>
    /// Each no-finding state is read whole in one call, however long it is,
    /// and the Triage total is the rows read rather than a separate count.
    /// </summary>
    [Fact]
    public async Task EachNoFindingTriageStateIsReadOnceWholeAndCountedFromItsRows()
    {
        var triageItems = Enumerable.Range(1, 250)
            .Select(index => NewTriage(Guid.NewGuid(), $"O{index:000}", TriageState.Open))
            .Concat(Enumerable.Range(1, 3)
                .Select(index => NewTriage(Guid.NewGuid(), $"A{index:000}", TriageState.AwaitingInformation)))
            .Append(NewTriage(Guid.NewGuid(), "F001", TriageState.FindingRecorded))
            .ToList();
        var triage = new StubListTriage { Items = triageItems };

        var snapshot = await ExecuteAsync(new RecordingDashboardQueries(), NowUtc, triage: triage);

        Assert.Equal(
            new TriageState?[] { TriageState.Open, TriageState.AwaitingInformation },
            triage.ListAllStates.Order());
        Assert.Equal(0, triage.PageReads);
        Assert.Equal(253, snapshot.TriageCount);
        Assert.Equal(254, snapshot.Metrics.Triages);
        Assert.Equal(253, snapshot.Attention.KindCounts[NeedsAttentionKind.Triage]);
    }

    /// <summary>
    /// A draft on a Case belongs to that Case's engineer, read for every draft
    /// in one call; a draft whose Case has no workflow, or that is not on a
    /// Case, has no owner.
    /// </summary>
    [Fact]
    public async Task DraftOwnersAreTheirCasesEngineersReadInOneCall()
    {
        var engineerId = Guid.NewGuid();
        var assignedCase = Guid.NewGuid();
        var unknownCase = Guid.NewGuid();
        var workflows = new RecordingAssignedEngineers(new Dictionary<Guid, Guid?> { [assignedCase] = engineerId });
        var jobs = new StubAiJobs(
        [
            NewDraft(AiJobSubjectKind.Case, assignedCase, "D1").Job,
            NewDraft(AiJobSubjectKind.Case, assignedCase, "D2").Job,
            NewDraft(AiJobSubjectKind.Case, unknownCase, "D3").Job,
            NewDraft(AiJobSubjectKind.Unidentified, Guid.NewGuid(), "D4").Job
        ]);

        var snapshot = await new GetOperationsSnapshot(
            new StubListTriage(),
            new StubDueWorkQueries(),
            new RecordingDashboardQueries(),
            new StubSearchCases(),
            new StubUnidentifiedQueue(),
            new UnknownStaffAccounts(),
            new FixedWorkflowConfiguration(new("case-workflow", 1)),
            new InMemoryWorkCentreDismissals(),
            new FixedTimeProvider(NowUtc),
            jobs,
            workflows).ExecuteAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]));

        var owners = snapshot.Attention.Items
            .Where(item => item.Kind == NeedsAttentionKind.AiDraft)
            .ToDictionary(item => item.Reference, item => item.OwnerStaffId);
        Assert.Equal(engineerId, owners["D1"]);
        Assert.Equal(engineerId, owners["D2"]);
        Assert.Null(owners["D3"]);
        Assert.Null(owners["D4"]);
        var read = Assert.Single(workflows.Calls);
        Assert.Equal(new[] { assignedCase, unknownCase }.Order(), read.Order());
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
            new StubListTriage { Items = [NewTriage(Guid.NewGuid(), "AB12CDE", TriageState.Open)] },
            new StubDueWorkQueries(),
            new RecordingDashboardQueries(),
            new StubSearchCases { Items = [mine, theirs] },
            new StubUnidentifiedQueue { Rows = [NewUnidentified(Guid.NewGuid(), "U1000")] },
            new UnknownStaffAccounts(),
            new FixedWorkflowConfiguration(new("case-workflow", 1)),
            new InMemoryWorkCentreDismissals(),
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
    public async Task AnAssignedReviewRowNamesItsEngineer()
    {
        var engineerId = Guid.NewGuid();
        var review = ReviewCase(Guid.NewGuid(), "C/2026/OWNED", enteredAtUtc: NowUtc.AddDays(-1)) with
        {
            EngineerId = engineerId
        };
        var snapshot = await new GetOperationsSnapshot(
            new StubListTriage(),
            new StubDueWorkQueries(),
            new RecordingDashboardQueries(),
            new StubSearchCases { Items = [review] },
            new StubUnidentifiedQueue(),
            new NamedStaffAccounts(engineerId, "Alex"),
            new FixedWorkflowConfiguration(new("case-workflow", 1)),
            new InMemoryWorkCentreDismissals(),
            new FixedTimeProvider(NowUtc)).ExecuteAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]));

        var row = Assert.Single(snapshot.NeedsAttention);
        Assert.Equal(NeedsAttentionKind.ReviewCase, row.Kind);
        Assert.Equal("Alex", row.Owner);
    }

    [Fact]
    public async Task KindChipsCountTheScopeBeforeTheKindFilter()
    {
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var snapshot = await new GetOperationsSnapshot(
            new StubListTriage { Items = [NewTriage(Guid.NewGuid(), "AB12CDE", TriageState.Open)] },
            new StubDueWorkQueries(),
            new RecordingDashboardQueries(),
            new StubSearchCases(),
            new StubUnidentifiedQueue { Rows = [NewUnidentified(Guid.NewGuid(), "U3001"), NewUnidentified(Guid.NewGuid(), "U3002")] },
            new UnknownStaffAccounts(),
            new FixedWorkflowConfiguration(new("case-workflow", 1)),
            new InMemoryWorkCentreDismissals(),
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
    /// Find within Needs attention (v30 WB): the term narrows the scoped list
    /// before paging, while every chip still counts the scope before it.
    /// </summary>
    [Fact]
    public async Task FindNarrowsTheScopedListBeforePagingAndLeavesTheChipCountsAlone()
    {
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var snapshot = await new GetOperationsSnapshot(
            new StubListTriage { Items = [NewTriage(Guid.NewGuid(), "AB12CDE", TriageState.Open)] },
            new StubDueWorkQueries(),
            new RecordingDashboardQueries(),
            new StubSearchCases(),
            new StubUnidentifiedQueue { Rows = [NewUnidentified(Guid.NewGuid(), "U3001"), NewUnidentified(Guid.NewGuid(), "U3002")] },
            new UnknownStaffAccounts(),
            new FixedWorkflowConfiguration(new("case-workflow", 1)),
            new InMemoryWorkCentreDismissals(),
            new FixedTimeProvider(NowUtc)).ExecuteAsync(
                new NeedsAttentionQuery(administrator, Search: " u3002 "));

        Assert.Equal(["U3002"], snapshot.Attention.Items.Select(item => item.Reference).ToArray());
        Assert.Equal(1, snapshot.Attention.TotalCount);
        Assert.Equal(2, snapshot.Attention.KindCounts[NeedsAttentionKind.Unidentified]);
        Assert.Equal(1, snapshot.Attention.KindCounts[NeedsAttentionKind.Triage]);
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

    /// <summary>
    /// Dismiss (FRD-15) hides every row of the record that began at or before
    /// it, from the rows, the chips and the counts alike.
    /// </summary>
    [Fact]
    public async Task ADismissedRecordLeavesTheRowsTheChipsAndTheCounts()
    {
        var dismissedId = Guid.NewGuid();
        var keptId = Guid.NewGuid();
        var dismissals = new InMemoryWorkCentreDismissals();
        await dismissals.DismissAsync(
            dismissedId, NowUtc.AddMinutes(-10), ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), CancellationToken.None);
        var searchCases = new StubSearchCases
        {
            Items =
            [
                ReviewCase(dismissedId, "C/2026/GONE", enteredAtUtc: NowUtc.AddDays(-2)),
                ReviewCase(keptId, "C/2026/KEPT", enteredAtUtc: NowUtc.AddDays(-2))
            ]
        };
        var dueWork = new StubDueWorkQueries { Due = [NewDueWork(dismissedId, "C/2026/GONE", NowUtc.AddHours(-1))] };

        var snapshot = await ExecuteAsync(
            new RecordingDashboardQueries(), NowUtc, searchCases: searchCases, dueWork: dueWork, dismissals: dismissals);
        var attentionRows = await ExecuteAttentionRowsAsync(
            new RecordingDashboardQueries(), NowUtc, searchCases: searchCases, dueWork: dueWork, dismissals: dismissals);

        Assert.Equal([keptId], snapshot.NeedsAttention.Select(item => item.Id).ToArray());
        Assert.Equal(1, snapshot.Attention.TotalCount);
        Assert.Equal(0, snapshot.Attention.KindCounts[NeedsAttentionKind.CaseChase]);
        Assert.Equal(1, snapshot.Attention.KindCounts[NeedsAttentionKind.ReviewCase]);
        Assert.Equal([keptId], attentionRows.Select(item => item.Id).ToArray());
        // The metric strip counts the Cases, not the rows.
        Assert.Equal(snapshot.CaseStages.Review, snapshot.Metrics.Review);
    }

    /// <summary>
    /// A row whose occurrence began after the dismissal (the next chase falls
    /// due, the Case re-enters Review) shows again; a changed target moves only
    /// the due date, so it never brings a dismissed row back.
    /// </summary>
    [Fact]
    public async Task ARowThatBeginsAfterTheDismissalShowsAgainAndATargetChangeDoesNot()
    {
        var chasedId = Guid.NewGuid();
        var reenteredId = Guid.NewGuid();
        var stillDismissedId = Guid.NewGuid();
        var dismissedAt = NowUtc.AddHours(-3);
        var dismissals = new InMemoryWorkCentreDismissals();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        foreach (var id in new[] { chasedId, reenteredId, stillDismissedId })
        {
            await dismissals.DismissAsync(id, dismissedAt, staff, CancellationToken.None);
        }

        var snapshot = await ExecuteAsync(
            new RecordingDashboardQueries(),
            NowUtc,
            searchCases: new StubSearchCases
            {
                Items =
                [
                    ReviewCase(reenteredId, "C/2026/BACK", enteredAtUtc: NowUtc.AddHours(-1)),
                    ReviewCase(stillDismissedId, "C/2026/GONE", enteredAtUtc: NowUtc.AddDays(-2))
                ]
            },
            dueWork: new StubDueWorkQueries { Due = [NewDueWork(chasedId, "C/2026/CHASE", NowUtc.AddHours(-1))] },
            workflowConfiguration: new("case-workflow", 2) { ReviewTargetDays = 5 },
            dismissals: dismissals);

        Assert.Equal(
            new HashSet<Guid> { chasedId, reenteredId },
            snapshot.NeedsAttention.Select(item => item.Id).ToHashSet());
    }

    [Fact]
    public async Task NothingNeedingAttentionReadsNoDismissals()
    {
        var dismissals = new InMemoryWorkCentreDismissals();

        var snapshot = await ExecuteAsync(new RecordingDashboardQueries(), NowUtc, dismissals: dismissals);

        Assert.Empty(snapshot.NeedsAttention);
        Assert.Equal(0, dismissals.Reads);
    }

    private static CaseSearchItem ReviewCase(Guid caseId, string reference, DateTimeOffset enteredAtUtc) =>
        NewHeldCase(caseId, reference) with
        {
            State = CaseLifecycleState.Review,
            EngineerId = Guid.NewGuid(),
            StateEnteredAtUtc = enteredAtUtc
        };

    [Fact]
    public async Task APageBeyondTheEndLandsOnTheLastPageFromTheSameReads()
    {
        var triage = new StubListTriage();
        var rows = Enumerable.Range(1, GetOperationsSnapshot.PageSize + 10)
            .Select(index => NewUnidentified(Guid.NewGuid(), $"U{3000 + index}"))
            .ToArray();
        var snapshot = await BuildSnapshot(
                new RecordingDashboardQueries(), NowUtc, null,
                new StubUnidentifiedQueue { Rows = rows }, triage, null, null, null)
            .ExecuteAsync(new NeedsAttentionQuery(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]), Page: 9));

        // Page 9 does not exist; the last page is 2, with the ten rows past the first fifty.
        Assert.Equal(2, snapshot.Attention.Page);
        Assert.Equal(10, snapshot.Attention.Items.Count);
        Assert.Equal(rows.Length, snapshot.Attention.TotalCount);
        // Both no-finding Triage states were read once each: the clamp re-cuts the
        // list already read and never runs the snapshot again.
        Assert.Equal(2, triage.ListAllStates.Count);
    }

    [Fact]
    public async Task HandedOpenJobsAndConfigurationAreNotReadAgain()
    {
        var draft = NewDraft(AiJobSubjectKind.Case, Guid.NewGuid(), "D1");
        var jobs = new StubAiJobs([draft.Job]);
        var configuration = new FixedWorkflowConfiguration(new("case-workflow", 1));
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        GetOperationsSnapshot Snapshot() => new(
            new StubListTriage(),
            new StubDueWorkQueries(),
            new RecordingDashboardQueries(),
            new StubSearchCases(),
            new StubUnidentifiedQueue(),
            new UnknownStaffAccounts(),
            configuration,
            new InMemoryWorkCentreDismissals(),
            new FixedTimeProvider(NowUtc),
            jobs);

        var read = await Snapshot().ExecuteAsync(new NeedsAttentionQuery(actor));
        var afterOwnRead = (jobs.OpenReads, configuration.Reads);
        var handed = await Snapshot().ExecuteAsync(new NeedsAttentionQuery(
            actor, OpenAiJobs: [draft.Job], Configuration: new("case-workflow", 1)));

        // Read for itself, the snapshot asks once for each and derives the drafts
        // from that one read; handed both, it asks for neither.
        Assert.Equal((1, 1), afterOwnRead);
        Assert.Equal((1, 1), (jobs.OpenReads, configuration.Reads));
        Assert.Equal(NeedsAttentionKind.AiDraft, Assert.Single(read.Attention.Items).Kind);
        Assert.Equal(NeedsAttentionKind.AiDraft, Assert.Single(handed.Attention.Items).Kind);
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
        CaseWorkflowConfiguration? workflowConfiguration = null,
        InMemoryWorkCentreDismissals? dismissals = null)
    {
        var snapshot = BuildSnapshot(
            recorder, nowUtc, searchCases, unidentified, triage, dueWork, requestStore, workflowConfiguration, dismissals);
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
        CaseWorkflowConfiguration? workflowConfiguration = null,
        InMemoryWorkCentreDismissals? dismissals = null)
    {
        IGetAttentionRows attentionRows = BuildSnapshot(
            recorder, nowUtc, searchCases, unidentified, triage, dueWork, requestStore, workflowConfiguration, dismissals);
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
        CaseWorkflowConfiguration? workflowConfiguration,
        InMemoryWorkCentreDismissals? dismissals = null)
    {
        var timeProvider = new FixedTimeProvider(nowUtc);
        return new GetOperationsSnapshot(
            triage ?? new StubListTriage(),
            dueWork ?? new StubDueWorkQueries(),
            recorder,
            searchCases ?? new StubSearchCases(),
            unidentified ?? new StubUnidentifiedQueue(),
            new UnknownStaffAccounts(),
            new FixedWorkflowConfiguration(workflowConfiguration ?? new("case-workflow", 1)),
            dismissals ?? new InMemoryWorkCentreDismissals(),
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
        PrincipalCode: "QDOS");

    private static UnidentifiedQueueRow NewUnidentified(Guid id, string reference) => new(
        id,
        reference,
        UnidentifiedMediaKind.Image,
        FileName: "IMG_4418.jpg",
        EmailSubject: null,
        EmailSender: null,
        ReceivedAtUtc: NowUtc,
        UnidentifiedReasonCode.NoUsableIdentification,
        ResolutionReason: null);

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
        public IReadOnlyList<PairedVehicleImagesCase> Paired { get; init; } = [];

        public Task<CaseStageCounts> GetCaseStageCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CaseStageCounts(0, 0, 0, 0));

        public Task<IReadOnlyList<PairedVehicleImagesCase>> ListPairedVehicleImagesAwaitingStaffAsync(
            CancellationToken cancellationToken) => Task.FromResult(Paired);
    }

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }

    private sealed class FixedWorkflowConfiguration(CaseWorkflowConfiguration configuration)
        : ICaseWorkflowConfiguration
    {
        private int reads;

        public int Reads => Volatile.Read(ref reads);

        public Task<CaseWorkflowConfiguration> GetCurrentAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref reads);
            return Task.FromResult(configuration);
        }
    }

    /// <summary>
    /// Pages the way <see cref="ListTriage"/> does — state filter, then a
    /// Skip/Take window with the full match count — so a read that asks for
    /// one unfiltered page is truncated exactly as the real store truncates
    /// it. Rows keep insertion order; callers that need a record to survive
    /// paging place it inside the window. The whole-state read records each
    /// state it was asked for.
    /// </summary>
    private sealed class StubListTriage : IListTriage
    {
        public IReadOnlyList<TriageSummary> Items { get; init; } = [];

        public List<TriageState?> ListAllStates { get; } = [];

        public int PageReads { get; private set; }

        public Task<TriageListPage> ExecuteAsync(
            ListTriageQuery query,
            CancellationToken cancellationToken = default)
        {
            PageReads++;
            var matches = Items
                .Where(item => query.States is null || query.States.Contains(item.State))
                .ToArray();
            var page = matches
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToArray();
            return Task.FromResult(new TriageListPage(page, query.Page, query.PageSize, matches.Length));
        }

        public Task<int> CountAsync(
            ActionActor actor,
            IReadOnlyCollection<TriageState>? states,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Count(item => states is null || states.Contains(item.State)));

        public Task<IReadOnlyList<TriageSummary>> ListAllAsync(
            ActionActor actor,
            IReadOnlyCollection<TriageState>? states,
            CancellationToken cancellationToken = default)
        {
            if (states is null)
            {
                ListAllStates.Add(null);
            }
            else
            {
                ListAllStates.AddRange(states.Select(state => (TriageState?)state));
            }

            return Task.FromResult<IReadOnlyList<TriageSummary>>(
                Items.Where(item => states is null || states.Contains(item.State)).ToArray());
        }
    }

    private sealed class StubAiJobs(IReadOnlyList<AiJobRecord> open) : IAiJobQueries
    {
        private int openReads;

        public int OpenReads => Volatile.Read(ref openReads);

        public Task<IReadOnlyList<AiJobRecord>> ListOpenAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref openReads);
            return Task.FromResult(open);
        }

        public Task<AiJobQueryPage> ListOpenPageAsync(AiJobKind? kind, string grantId, DateTimeOffset? afterCreatedAtUtc, Guid? afterJobId, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AiJobRecord>> ListForSubjectAsync(Guid subjectId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<AiJobRecord>> ListRecentAsync(int max, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AiJobCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>Answers only the batch engineer read, and records each set of Cases it was asked for.</summary>
    private sealed class RecordingAssignedEngineers(IReadOnlyDictionary<Guid, Guid?> engineers) : ICaseWorkflowQueries
    {
        public List<IReadOnlyCollection<Guid>> Calls { get; } = [];

        public Task<CaseWorkflowRecord?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, Guid?>> GetAssignedEngineersAsync(
            IReadOnlyCollection<Guid> caseIds,
            CancellationToken cancellationToken)
        {
            Calls.Add(caseIds);
            return Task.FromResult<IReadOnlyDictionary<Guid, Guid?>>(
                engineers.Where(pair => caseIds.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value));
        }

        public Task<bool> HasOperationAsync(Guid caseId, string operationKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static AiDraft NewDraft(AiJobSubjectKind subjectKind, Guid subjectId, string reference) => new(
        new AiJobRecord(
            Guid.NewGuid(),
            AiJobKind.Estimate,
            subjectKind,
            subjectId,
            reference,
            "Draft the estimate.",
            null,
            null,
            AiJobState.DraftReady,
            ActorKind.Staff,
            "staff",
            NowUtc.AddHours(-2),
            NowUtc.AddDays(1),
            null, null, null, null, null, null, null, null, null,
            1),
        AiDraftAction.Review,
        $"/Cases/{subjectId:D}",
        NowUtc.AddHours(-1),
        NowUtc.AddHours(1));

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
        public List<CaseLifecycleState> RequestedStates { get; } = [];

        public Task<SearchCasesResult> ExecuteAsync(
            SearchCasesQuery query,
            CancellationToken cancellationToken)
        {
            RequestedStates.AddRange(query.Filters.States ?? []);
            var matching = Items.Where(item => query.Filters.States is not { Count: > 0 } states || states.Contains(item.State))
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

        public Task<RequestOperationsProjection> GetAsync(
            int maximumItems,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RequestOperationsProjection([.. Items], LimitReached: false));
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

    /// <summary>Resolves the one named staff member it holds.</summary>
    private sealed class NamedStaffAccounts(Guid staffId, string userName) : IStaffAccountQueries
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
            Task.FromResult<IReadOnlyList<StaffAccountSummary>>(
                staffIds.Contains(staffId)
                    ? [new StaffAccountSummary(staffId, userName, IsEnabled: true, MustChangePassword: false, StaffRole.Engineer)]
                    : []);

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");
    }
}
