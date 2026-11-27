using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

/// <summary>
/// The classifier's record of its runs and the Administrator's page over it
/// (FRD-08, FRD-17; R1318, R1319): only the Automation Actor records a run;
/// a run's outcome is read from what the classifier reports; the health row
/// counts failures in a row past a run still running; and the test instance
/// pages its own runs and the live ones together, saying why the live ones
/// are missing when they are.
/// </summary>
public sealed class ClassifierRunsTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly ClassifierRunFacts Facts = new(false, "claude-sonnet-5-5", 20, 10, 2m, "1.0.0");

    [Fact]
    public async Task AMemberOfStaffIsRefusedTheClassifiersRecord()
    {
        var store = new Store();

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => Record(store).ExecuteAsync(
            new(Guid.NewGuid(), ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]), "mcp:run-open:1", Facts, null),
            CancellationToken.None));

        Assert.Null(store.Opened);
        Assert.Null(store.Closed);
    }

    [Fact]
    public async Task AnOpenTakesPegasussClockAndTheActorThatRecordedIt()
    {
        var store = new Store();
        var runId = Guid.NewGuid();

        await Record(store).ExecuteAsync(
            new(runId, ActionActor.Automation("pegasus-automation"), "mcp:run-open:1", Facts with { Model = "  model  " }, null),
            CancellationToken.None);

        Assert.Equal(new ClassifierRunOpening(runId, Facts with { Model = "model" }, "pegasus-automation", Now), store.Opened);
        Assert.Null(store.Closed);
    }

    [Fact]
    public async Task ACloseReadsHowTheRunEndedCountsItsActsAndCutsLongText()
    {
        var store = new Store();
        ClassifierRunItem[] items =
        [
            Item(ClassifierRunAction.Classified) with { Reason = new string('r', 600) },
            Item(ClassifierRunAction.Linked),
            Item(ClassifierRunAction.Left),
            Item(ClassifierRunAction.NotReached)
        ];

        await Record(store).ExecuteAsync(
            new(Guid.NewGuid(), ActionActor.Automation("pegasus-automation"), "mcp:run-close:1", Facts,
                Result("error_max_turns", items) with { Summary = new string('s', 5000) }),
            CancellationToken.None);

        var closed = Assert.IsType<ClassifierRunClosing>(store.Closed);
        Assert.Equal(ClassifierRunOutcome.CutShort, closed.Outcome);
        Assert.Equal(2, closed.Acted);
        Assert.Equal(ClassifierRunPolicy.SummaryLength, closed.Result.Summary!.Length);
        Assert.Equal(ClassifierRunPolicy.ReasonLength, closed.Result.Items[0].Reason!.Length);
    }

    [Fact]
    public async Task ARunWithNegativeFiguresOrTooManyMessagesIsRefused()
    {
        var store = new Store();
        var automation = ActionActor.Automation("pegasus-automation");

        await Assert.ThrowsAsync<ArgumentException>(() => Record(store).ExecuteAsync(
            new(Guid.NewGuid(), automation, "mcp:k", Facts, Result("success", []) with { CostUsd = -1m }),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => Record(store).ExecuteAsync(
            new(Guid.NewGuid(), automation, "mcp:k", Facts,
                Result("success", [.. Enumerable.Range(0, ClassifierRunPolicy.MaximumItems + 1).Select(_ => Item(ClassifierRunAction.Left))])),
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => Record(store).ExecuteAsync(
            new(Guid.Empty, automation, "mcp:k", Facts, null),
            CancellationToken.None));

        Assert.Null(store.Closed);
        Assert.Null(store.Opened);
    }

    [Theory]
    [InlineData("success", ClassifierRunOutcome.Completed)]
    [InlineData("nothing eligible", ClassifierRunOutcome.NothingToDo)]
    [InlineData("nothing eligible: candidates hidden", ClassifierRunOutcome.CandidatesHidden)]
    [InlineData("error_max_turns", ClassifierRunOutcome.CutShort)]
    [InlineData("error_max_budget_usd", ClassifierRunOutcome.CutShort)]
    [InlineData("failed: candidates hidden", ClassifierRunOutcome.Failed)]
    [InlineData("failed: no verdicts", ClassifierRunOutcome.Failed)]
    [InlineData("error_during_execution", ClassifierRunOutcome.Failed)]
    [InlineData("something new", ClassifierRunOutcome.Failed)]
    public void AnOutcomeIsReadFromWhatTheClassifierReportsAndAnUnknownOneIsAFailure(string outcome, ClassifierRunOutcome expected) =>
        Assert.Equal(expected, ClassifierRunPolicy.OutcomeOf(outcome));

    [Fact]
    public void ARunWithNoCloseIsRunningForFifteenMinutesAndThenDidNotFinish()
    {
        Assert.Equal(ClassifierRunOutcome.Running, ClassifierRunPolicy.OutcomeAt(Run(Now.AddMinutes(-14), null), Now));
        Assert.Equal(ClassifierRunOutcome.DidNotFinish, ClassifierRunPolicy.OutcomeAt(Run(Now.AddMinutes(-16), null), Now));
        Assert.Equal(ClassifierRunOutcome.Completed, ClassifierRunPolicy.OutcomeAt(Run(Now.AddMinutes(-16), ClassifierRunOutcome.Completed), Now));
    }

    [Fact]
    public void FailuresInARowPassOverARunStillRunningAndStopAtOneThatDidNot()
    {
        var hidden = Run(Now.AddMinutes(-15), ClassifierRunOutcome.Failed) with
        {
            CandidatesHidden = true,
            AwaitingDecision = 3,
            HiddenRuns = 4
        };
        ClassifierRunSummary[] recent =
        [
            Run(Now.AddMinutes(-1), null),
            hidden,
            Run(Now.AddMinutes(-30), null),
            Run(Now.AddMinutes(-45), ClassifierRunOutcome.Completed),
            Run(Now.AddMinutes(-60), ClassifierRunOutcome.Failed)
        ];

        var health = ClassifierRunPolicy.Health(
            ClassifierRunEnvironment.Live,
            new(recent, 5, 0.5m, 7.25m),
            Now);

        Assert.Equal(recent[0], health.LastRun);
        Assert.Equal(ClassifierRunOutcome.Running, health.LastOutcome);
        Assert.Equal(2, health.FailuresInARow);
        Assert.False(health.FailuresAtLeast);
        Assert.Equal(3, health.HiddenAwaiting);
        Assert.Equal(4, health.HiddenRuns);
        Assert.Equal(7.25m, health.SpendThisMonth);
    }

    [Fact]
    public void TheLiveRunsHealthIsReadFromTheRunsThemselves()
    {
        var dayStart = new DateTimeOffset(2031, 5, 6, 0, 0, 0, TimeSpan.FromHours(1));
        var monthStart = ClassifierRunPolicy.MonthStartAt(Now);
        ClassifierRunSummary[] runs =
        [
            Run(Now.AddHours(-1), ClassifierRunOutcome.Completed) with { CostUsd = 0.25m },
            Run(Now.AddHours(-20), ClassifierRunOutcome.Completed) with { CostUsd = 0.5m },
            Run(Now.AddDays(-3), ClassifierRunOutcome.Completed) with { CostUsd = 1m }
        ];

        var facts = ClassifierRunPolicy.FactsFrom(runs, Now, dayStart, monthStart);

        Assert.Equal(2, facts.RunsLast24Hours);
        Assert.Equal(0.25m, facts.SpendToday);
        Assert.Equal(1.75m, facts.SpendThisMonth);
        Assert.Equal(new DateTimeOffset(2031, 4, 30, 23, 0, 0, TimeSpan.Zero), monthStart);
    }

    [Fact]
    public async Task ProductionReadsOnlyItsOwnRunsAndThePageItAsksFor()
    {
        var store = new Store();
        var live = new Live();

        var view = await List(store, live).ExecuteAsync(Query(ClassifierRunEnvironment.Live, page: 2), CancellationToken.None);

        Assert.Equal(ClassifierLiveRunsState.ThisDeployment, view.LiveRuns);
        Assert.Equal(0, live.Reads);
        Assert.Equal(ClassifierRunPolicy.PageSize, store.Filter!.Skip);
        Assert.Equal(ClassifierRunPolicy.PageSize + 1, store.Filter.Take);
        Assert.Equal(ClassifierRunEnvironment.Live, Assert.Single(view.Health).Environment);
    }

    [Fact]
    public async Task TheInstanceWithoutTheLiveReadSaysSoAndShowsItsOwn()
    {
        var store = new Store { Rows = [Run(Now.AddMinutes(-5), ClassifierRunOutcome.Completed)] };

        var view = await List(store, live: null).ExecuteAsync(Query(ClassifierRunEnvironment.Test), CancellationToken.None);

        Assert.Equal(ClassifierLiveRunsState.Off, view.LiveRuns);
        Assert.Equal(ClassifierRunEnvironment.Test, Assert.Single(view.Runs).Environment);
        Assert.Equal(ClassifierRunEnvironment.Test, Assert.Single(view.Health).Environment);
    }

    [Fact]
    public async Task TheInstancePagesItsOwnRunsAndTheLiveOnesTogetherNewestFirst()
    {
        var store = new Store
        {
            Rows = [Run(Now.AddMinutes(-10), ClassifierRunOutcome.Completed), Run(Now.AddMinutes(-40), ClassifierRunOutcome.Failed)]
        };
        var live = new Live
        {
            Rows =
            [
                Run(Now.AddMinutes(-5), ClassifierRunOutcome.Completed) with { Environment = ClassifierRunEnvironment.Live },
                Run(Now.AddMinutes(-20), ClassifierRunOutcome.NothingToDo) with { Environment = ClassifierRunEnvironment.Live },
                // Read for the month's spend, but outside the list's period.
                Run(Now.AddDays(-3), ClassifierRunOutcome.Completed) with { Environment = ClassifierRunEnvironment.Live, CostUsd = 3m }
            ]
        };

        var view = await List(store, live).ExecuteAsync(
            Query(ClassifierRunEnvironment.Test) with { FromUtc = Now.AddHours(-1) },
            CancellationToken.None);

        Assert.Equal(ClassifierLiveRunsState.Shown, view.LiveRuns);
        Assert.Equal("Live Test Live Test", string.Join(' ', view.Runs.Select(run => run.Environment)));
        Assert.Equal(0, store.Filter!.Skip);
        Assert.Equal("Test Live", string.Join(' ', view.Health.Select(health => health.Environment)));
        Assert.Equal(3.2m, view.Health[1].SpendThisMonth);
        Assert.False(view.HasMore);
    }

    [Fact]
    public async Task TheInstanceFiltersTheLiveRunsAsItsOwnAndCanShowThemAlone()
    {
        var store = new Store { Rows = [Run(Now.AddMinutes(-10), ClassifierRunOutcome.Completed)] };
        var acted = Run(Now.AddMinutes(-5), ClassifierRunOutcome.Completed) with
        {
            Environment = ClassifierRunEnvironment.Live,
            Actions = new Dictionary<ClassifierRunAction, int> { [ClassifierRunAction.Classified] = 1 }
        };
        var idle = Run(Now.AddMinutes(-20), ClassifierRunOutcome.Completed) with
        {
            Environment = ClassifierRunEnvironment.Live,
            Actions = new Dictionary<ClassifierRunAction, int> { [ClassifierRunAction.Left] = 2 }
        };
        var live = new Live { Rows = [acted, idle] };

        var view = await List(store, live).ExecuteAsync(
            Query(ClassifierRunEnvironment.Test) with { Environment = ClassifierRunEnvironment.Live, OnlyActed = true },
            CancellationToken.None);

        Assert.Equal(acted.Id, Assert.Single(view.Runs).Id);
        Assert.Null(store.Filter);
    }

    [Theory]
    [InlineData(true, ClassifierLiveRunsState.Refused)]
    [InlineData(false, ClassifierLiveRunsState.Unavailable)]
    public async Task LiveRunsThatCannotBeReadSayWhyAndTheInstancesOwnStillShow(bool refused, ClassifierLiveRunsState expected)
    {
        var store = new Store { Rows = [Run(Now.AddMinutes(-10), ClassifierRunOutcome.Completed)] };
        var live = new Live { Failure = new ProductionClassifierRunsUnavailableException(refused, "no") };

        var view = await List(store, live).ExecuteAsync(Query(ClassifierRunEnvironment.Test), CancellationToken.None);

        Assert.Equal(expected, view.LiveRuns);
        Assert.Single(view.Runs);
        Assert.Equal(ClassifierRunEnvironment.Test, Assert.Single(view.Health).Environment);
    }

    [Fact]
    public async Task APeriodLongerThanAMonthOrAPageOutOfRangeIsRefused()
    {
        var store = new Store();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => List(store, null).ExecuteAsync(
            Query(ClassifierRunEnvironment.Live) with { FromUtc = Now.AddDays(-32) },
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => List(store, null).ExecuteAsync(
            Query(ClassifierRunEnvironment.Live, page: ClassifierRunPolicy.MaximumPage + 1),
            CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => List(store, null).ExecuteAsync(
            Query(ClassifierRunEnvironment.Live) with { Actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]) },
            CancellationToken.None));
        Assert.Null(store.Filter);
    }

    [Fact]
    public async Task ALiveRunIsReadOnlyOnTheInstanceAndByTheDateItStarted()
    {
        var store = new Store();
        var live = new Live();
        var runId = Guid.NewGuid();
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

        Assert.Null(await List(store, live).GetAsync(
            administrator, ClassifierRunEnvironment.Live, ClassifierRunEnvironment.Test, runId, new DateOnly(2031, 5, 6), CancellationToken.None));
        Assert.Equal(0, live.Reads);
        Assert.Null(await List(store, live).GetAsync(
            administrator, ClassifierRunEnvironment.Test, ClassifierRunEnvironment.Live, runId, null, CancellationToken.None));
        Assert.Equal(0, live.Reads);
        await List(store, live).GetAsync(
            administrator, ClassifierRunEnvironment.Test, ClassifierRunEnvironment.Live, runId, new DateOnly(2031, 5, 6), CancellationToken.None);
        Assert.Equal((runId, new DateOnly(2031, 5, 6)), live.Detail);
    }

    [Fact]
    public void EveryActHasOneWireNameAndReadsBackFromIt()
    {
        foreach (var action in Enum.GetValues<ClassifierRunAction>())
        {
            Assert.Equal(action, ClassifierRunPolicy.ActionFromWire(ClassifierRunPolicy.WireName(action)));
        }

        Assert.Equal("case_created", ClassifierRunPolicy.WireName(ClassifierRunAction.CaseCreated));
        Assert.Equal("left_for_staff", ClassifierRunPolicy.WireName(ClassifierRunAction.LeftForStaff));
        Assert.Null(ClassifierRunPolicy.ActionFromWire("CaseCreated"));
        Assert.Null(ClassifierRunPolicy.ActionFromWire(null));
    }

    private static RecordClassifierRun Record(Store store) => new(store, new FixedClock(Now));

    private static ListClassifierRuns List(Store store, Live? live) => new(store, new FixedClock(Now), live);

    private static ClassifierRunsQuery Query(ClassifierRunEnvironment local, int page = 1) =>
        new(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
            local,
            Now.AddDays(-7),
            Now.AddHours(1),
            null,
            false,
            null,
            false,
            page);

    private static ClassifierRunItem Item(ClassifierRunAction action) =>
        new(Guid.NewGuid(), action, null, null, null, null, null, Now);

    private static ClassifierRunResult Result(string outcome, IReadOnlyList<ClassifierRunItem> items) =>
        new(outcome, items.Count, 0, false, 0, 0, 0, 0, 4, 0.1m, 1000, null, null, items);

    private static ClassifierRunSummary Run(DateTimeOffset startedAtUtc, ClassifierRunOutcome? outcome) =>
        new(
            Guid.NewGuid(),
            ClassifierRunEnvironment.Live,
            startedAtUtc,
            outcome is null ? null : startedAtUtc.AddMinutes(2),
            false,
            outcome?.ToString(),
            outcome,
            1,
            0,
            false,
            0,
            outcome is null ? null : 0.1m,
            outcome is null ? null : 120_000,
            new Dictionary<ClassifierRunAction, int>());

    private sealed class Store : IClassifierRunStore
    {
        public ClassifierRunOpening? Opened { get; private set; }
        public ClassifierRunClosing? Closed { get; private set; }
        public ClassifierRunFilter? Filter { get; private set; }
        public IReadOnlyList<ClassifierRunSummary> Rows { get; init; } = [];

        public Task<ClassifierRunRecorded> OpenAsync(ClassifierRunOpening opening, CancellationToken cancellationToken)
        {
            Opened = opening;
            return Task.FromResult(new ClassifierRunRecorded(opening.RunId, opening.AtUtc, null, false));
        }

        public Task<ClassifierRunRecorded> CloseAsync(ClassifierRunClosing closing, CancellationToken cancellationToken)
        {
            Closed = closing;
            return Task.FromResult(new ClassifierRunRecorded(closing.RunId, closing.AtUtc, closing.AtUtc, false));
        }

        public Task<IReadOnlyList<ClassifierRunSummary>> ListAsync(ClassifierRunFilter filter, CancellationToken cancellationToken)
        {
            Filter = filter;
            return Task.FromResult<IReadOnlyList<ClassifierRunSummary>>(
            [
                .. Rows
                    .Where(run => ClassifierRunPolicy.Matches(run, filter.Outcome, filter.OnlyActed, filter.NowUtc))
                    .OrderByDescending(run => run.StartedAtUtc)
                    .Skip(filter.Skip)
                    .Take(filter.Take)
            ]);
        }

        public Task<ClassifierRunDetail?> GetAsync(Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult<ClassifierRunDetail?>(null);

        public Task<ClassifierRunHealthFacts> HealthAsync(
            DateTimeOffset nowUtc,
            DateTimeOffset dayStartUtc,
            DateTimeOffset monthStartUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(ClassifierRunPolicy.FactsFrom(
                [.. Rows.OrderByDescending(run => run.StartedAtUtc)], nowUtc, dayStartUtc, monthStartUtc));
    }

    private sealed class Live : IProductionClassifierRuns
    {
        public IReadOnlyList<ClassifierRunSummary> Rows { get; init; } = [];
        public ProductionClassifierRunsUnavailableException? Failure { get; init; }
        public int Reads { get; private set; }
        public (Guid, DateOnly)? Detail { get; private set; }

        public Task<IReadOnlyList<ClassifierRunSummary>> ListAsync(
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            CancellationToken cancellationToken)
        {
            Reads++;
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult<IReadOnlyList<ClassifierRunSummary>>(
                [.. Rows.Where(run => run.StartedAtUtc >= fromUtc && run.StartedAtUtc < toUtc)]);
        }

        public Task<ClassifierRunDetail?> GetAsync(Guid runId, DateOnly startedOnUtc, CancellationToken cancellationToken)
        {
            Reads++;
            Detail = (runId, startedOnUtc);
            return Task.FromResult<ClassifierRunDetail?>(null);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
