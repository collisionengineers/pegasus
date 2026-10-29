using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Assessment;

public sealed class FetchGuideValuationTests
{
    private static readonly ActionActor Engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

    [Fact]
    public async Task ASourceWithoutAConnectedProviderIsRefusedBeforeTheCaseIsRead()
    {
        var caseData = new RecordingCaseData();
        var fetch = new FetchGuideValuation([], caseData, new RecordingSchedule(), new RecordingVins());

        await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            fetch.ExecuteAsync(Request(ValuationSource.Glasses), default));

        Assert.Equal(0, caseData.Reads);
    }

    [Theory]
    [InlineData(ValuationSource.EngineersValue)]
    [InlineData(ValuationSource.AiMarketResearch)]
    public async Task OnlyAGuideSourceCanBeFetched(ValuationSource source)
    {
        var fetch = new FetchGuideValuation([], new RecordingCaseData(), new RecordingSchedule(), new RecordingVins());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            fetch.ExecuteAsync(Request(source), default));
    }

    /// <summary>
    /// The Case says which sources are connected before Get valuation is
    /// pressed (operator, 28 September 2026): only a guide source with a
    /// registered provider is.
    /// </summary>
    [Fact]
    public void OnlyAGuideSourceWithAProviderIsConnected()
    {
        var fetch = new FetchGuideValuation(
            [new StubProvider(ValuationSource.Brego)], new RecordingCaseData(), new RecordingSchedule(), new RecordingVins());

        Assert.True(fetch.IsConnected(ValuationSource.Brego));
        Assert.False(fetch.IsConnected(ValuationSource.Glasses));
        Assert.False(fetch.IsConnected(ValuationSource.EngineersValue));
        Assert.False(new FetchGuideValuation([], new RecordingCaseData(), new RecordingSchedule(), new RecordingVins())
            .IsConnected(ValuationSource.Brego));
    }

    [Fact]
    public async Task AnActorWithoutCaseworkOrAnEmptyKeyIsRefused()
    {
        var fetch = new FetchGuideValuation([], new RecordingCaseData(), new RecordingSchedule(), new RecordingVins());
        var reader = ActionActor.SystemWorker("valuation-worker");

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            fetch.ExecuteAsync(Request(ValuationSource.Brego) with { Actor = reader }, default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            fetch.ExecuteAsync(Request(ValuationSource.Brego) with { OperationKey = " " }, default));
    }

    /// <summary>
    /// Every valuation's own report is filed on the Case (operator, 1 October
    /// 2026): the figures are answered and the report's filing is handed on
    /// with the registration and month it was valued for.
    /// </summary>
    [Fact]
    public async Task AValuationWithAReportAnswersTheFiguresAndSchedulesTheReport()
    {
        var report = new StubReport("glass-stock:33636950");
        var schedule = new RecordingSchedule();
        var request = Request(ValuationSource.Glasses);
        var fetch = new FetchGuideValuation(
            [new AnsweringProvider(ValuationSource.Glasses, report)],
            new RecordingCaseData(Valued(request.CaseId, "KY12CAB", 69000)),
            schedule,
            new RecordingVins());

        var quote = await fetch.ExecuteAsync(request, default);

        Assert.Equal(17717m, quote.RetailValue);
        var scheduled = Assert.Single(schedule.Requests);
        Assert.Equal(request.CaseId, scheduled.CaseId);
        Assert.Equal(ValuationSource.Glasses, scheduled.Source);
        Assert.Equal("KY12CAB", scheduled.Registration);
        Assert.Equal(new DateOnly(2026, 9, 1), scheduled.GuideMonth);
        Assert.Same(report, scheduled.Report);
        Assert.Same(Engineer, scheduled.Actor);
    }

    [Fact]
    public async Task AValuationWithoutAReportSchedulesNothing()
    {
        var schedule = new RecordingSchedule();
        var request = Request(ValuationSource.Glasses);
        var fetch = new FetchGuideValuation(
            [new AnsweringProvider(ValuationSource.Glasses, report: null)],
            new RecordingCaseData(Valued(request.CaseId, "KY12CAB", 69000)),
            schedule,
            new RecordingVins());

        await fetch.ExecuteAsync(request, default);

        Assert.Empty(schedule.Requests);
    }

    /// <summary>
    /// The VIN the source names for the valued vehicle is handed on to fill
    /// the valued work's empty VIN (operator, 7 October 2026).
    /// </summary>
    [Fact]
    public async Task AValuationThatNamesAVinHandsItOnForTheValuedWork()
    {
        var vins = new RecordingVins();
        var request = Request(ValuationSource.Glasses) with { Work = CaseWorkSelector.Primary };
        var fetch = new FetchGuideValuation(
            [new AnsweringProvider(ValuationSource.Glasses, report: null, vin: "TESTVEH0A1B2C3D45")],
            new RecordingCaseData(Valued(request.CaseId, "KY12CAB", 69000)),
            new RecordingSchedule(),
            vins);

        var quote = await fetch.ExecuteAsync(request, default);

        Assert.Equal(17717m, quote.RetailValue);
        var filled = Assert.Single(vins.Fills);
        Assert.Equal((Engineer, request.CaseId, CaseWorkSelector.Primary, "TESTVEH0A1B2C3D45"), filled);
    }

    [Fact]
    public async Task AValuationThatNamesNoVinFillsNothing()
    {
        var vins = new RecordingVins();
        var request = Request(ValuationSource.Glasses);
        var fetch = new FetchGuideValuation(
            [new AnsweringProvider(ValuationSource.Glasses, report: null)],
            new RecordingCaseData(Valued(request.CaseId, "KY12CAB", 69000)),
            new RecordingSchedule(),
            vins);

        await fetch.ExecuteAsync(request, default);

        Assert.Empty(vins.Fills);
    }

    /// <summary>
    /// R465 (28 October 2026), R545 (29 October 2026): a Case's one automatic
    /// request is Get valuation run by the system for the current work and the
    /// current London month, made as the Glass's recorder; its figures are
    /// recorded as the card a Save records, with no mileage, and the request is
    /// marked asked before Glass's is asked and settled after.
    /// </summary>
    [Fact]
    public async Task AnAutomaticRequestRecordsTheGlassCardForTheCurrentMonth()
    {
        var caseId = Guid.NewGuid();
        var schedule = new RecordingSchedule();
        var store = new AutomaticStore(Open());
        var run = Automatic(
            new AnsweringProvider(ValuationSource.Glasses, new StubReport("glass-stock:1")),
            new RecordingCaseData(Valued(caseId, "KY12CAB", 69000)),
            store,
            schedule: schedule);

        var outcome = await run.ExecuteAsync(new(WorkItem, caseId), default);

        Assert.Equal(AutomaticGuideValuationOutcome.Recorded, outcome);
        Assert.Equal(["begin", "record", "complete:Recorded"], store.Steps);
        var (actor, card) = Assert.Single(store.Recorded);
        Assert.Same(AutomaticGuideValuationPolicy.Actor, actor);
        Assert.Equal(ActorKind.Automation, actor.Kind);
        Assert.Equal(ValuationSource.Glasses, card.Source);
        Assert.Equal(new DateOnly(2031, 5, 1), card.GuideMonth);
        Assert.Equal(new DateOnly(2031, 5, 14), card.Date);
        Assert.Equal(17717m, card.RetailValue);
        Assert.Equal(15600m, card.TradeValue);
        Assert.Null(card.Mileage);
        Assert.Same(actor, Assert.Single(schedule.Requests).Actor);
    }

    /// <summary>
    /// The request waits, asking nothing of Glass's, while the registration or
    /// mileage is not yet accepted (as the vehicle lookup waits for a
    /// registration, and so a mileage the lookup fills is the one valued), and
    /// while anyone holds the Case's edit lease, because system work yields to
    /// a held lease.
    /// </summary>
    [Theory]
    [InlineData("registration")]
    [InlineData("lease")]
    public async Task AnAutomaticRequestWaitsWithoutAskingGlass(string reason)
    {
        var caseId = Guid.NewGuid();
        var provider = new CountingProvider();
        var store = new AutomaticStore(reason == "lease" ? Open() with { EditLeaseExpiresAtUtc = Now.AddMinutes(5) } : Open());
        var registration = reason == "registration" ? " " : "KY12CAB";

        var outcome = await Automatic(provider, new RecordingCaseData(Valued(caseId, registration, 69000)), store)
            .ExecuteAsync(new(WorkItem, caseId), default);

        Assert.Equal(AutomaticGuideValuationOutcome.Waiting, outcome);
        Assert.Equal(0, provider.Calls);
        Assert.Equal([$"defer:{Now + AutomaticGuideValuationPolicy.Wait:O}"], store.Steps);
    }

    /// <summary>A lapsed lease is nobody's, so it does not hold the request.</summary>
    [Fact]
    public void ALapsedLeaseDoesNotHoldTheAutomaticRequest() =>
        Assert.False(AutomaticGuideValuationPolicy.Waits(
            Open() with { EditLeaseExpiresAtUtc = Now.AddSeconds(-1) }, hasVehicleFacts: true, Now));

    /// <summary>
    /// A request is settled unasked when the switch is off (R546), when this
    /// host has no Glass's provider, and when the Case is closed or its current
    /// work already holds a Glass's card.
    /// </summary>
    [Theory]
    [InlineData("off", AutomaticGuideValuationOutcome.SwitchedOff)]
    [InlineData("offline", AutomaticGuideValuationOutcome.Unavailable)]
    [InlineData("closed", AutomaticGuideValuationOutcome.NotNeeded)]
    [InlineData("card", AutomaticGuideValuationOutcome.NotNeeded)]
    public async Task AnAutomaticRequestIsSettledUnasked(string reason, AutomaticGuideValuationOutcome expected)
    {
        var caseId = Guid.NewGuid();
        var provider = new CountingProvider();
        var store = new AutomaticStore(reason switch
        {
            "closed" => Open() with { IsOpen = false },
            "card" => Open() with { HoldsSourceCard = true },
            _ => Open(),
        });
        IGuideValuationProvider connected = reason == "offline" ? new StubProvider(ValuationSource.Brego) : provider;

        var outcome = await Automatic(
                connected,
                new RecordingCaseData(Valued(caseId, "KY12CAB", 69000)),
                store,
                enabled: reason != "off")
            .ExecuteAsync(new(WorkItem, caseId), default);

        Assert.Equal(expected, outcome);
        Assert.Equal(0, provider.Calls);
        Assert.Equal([$"complete:{expected}"], store.Steps);
    }

    /// <summary>
    /// A vehicle Glass's cannot value, or Glass's not answering, settles the
    /// request with nothing recorded: there is one automatic request ever, and
    /// any later valuation is staff-started (R545).
    /// </summary>
    [Theory]
    [InlineData("age", AutomaticGuideValuationOutcome.NotValued)]
    [InlineData("down", AutomaticGuideValuationOutcome.Unavailable)]
    public async Task AnAutomaticRequestGlassCannotAnswerIsSettledOnce(
        string answer, AutomaticGuideValuationOutcome expected)
    {
        var caseId = Guid.NewGuid();
        var store = new AutomaticStore(Open());
        var provider = new CountingProvider(answer == "age"
            ? new GuideValuationNotValuedException(ValuationSource.Glasses, GuideValuationNotValuedReason.VehicleAge)
            : new GuideValuationProviderUnavailableException(ValuationSource.Glasses));

        var outcome = await Automatic(provider, new RecordingCaseData(Valued(caseId, "KY12CAB", 69000)), store)
            .ExecuteAsync(new(WorkItem, caseId), default);

        Assert.Equal(expected, outcome);
        Assert.Equal(1, provider.Calls);
        Assert.Empty(store.Recorded);
        Assert.Equal(["begin", $"complete:{expected}"], store.Steps);
    }

    /// <summary>A request another pass has already started is not asked again.</summary>
    [Fact]
    public async Task AnAutomaticRequestAlreadyStartedIsNotAskedAgain()
    {
        var caseId = Guid.NewGuid();
        var provider = new CountingProvider();
        var store = new AutomaticStore(Open(), begins: false);

        var outcome = await Automatic(provider, new RecordingCaseData(Valued(caseId, "KY12CAB", 69000)), store)
            .ExecuteAsync(new(WorkItem, caseId), default);

        Assert.Equal(AutomaticGuideValuationOutcome.NotRecorded, outcome);
        Assert.Equal(0, provider.Calls);
        Assert.Equal(["begin"], store.Steps);
    }

    private static readonly DateTimeOffset Now = new(2031, 5, 14, 9, 30, 0, TimeSpan.Zero);

    private static readonly Guid WorkItem = Guid.NewGuid();

    private static AutomaticGuideValuationTarget Open() =>
        new(IsOpen: true, HoldsSourceCard: false, EditLeaseExpiresAtUtc: null);

    private static RunAutomaticGuideValuation Automatic(
        IGuideValuationProvider provider,
        RecordingCaseData caseData,
        AutomaticStore store,
        bool enabled = true,
        RecordingSchedule? schedule = null) =>
        new(
            new FetchGuideValuation([provider], caseData, schedule ?? new RecordingSchedule(), new RecordingVins()),
            caseData,
            store,
            new FixedSwitch(enabled),
            new FixedClock(Now));

    private static FetchGuideValuationRequest Request(ValuationSource source) => new(
        Guid.NewGuid(),
        3,
        Engineer,
        "get-valuation-test",
        "opaque-live-case-lease",
        source,
        new DateOnly(2026, 9, 14));

    private static CaseDataProjection Valued(Guid caseId, string registration, long mileage)
    {
        var text = new CaseField<string>(null, null, null);
        var date = new CaseField<DateOnly>(null, null, null);
        var source = new CaseDataSource(CaseDataSourceKind.StaffCorrection, "test", "Test", "test", 1);
        return new(
            new CaseIdentity(caseId, "QDOS", 2031, 1, "QDOS3100001"),
            new(null, null, null, null, null, null, null, null, null),
            DateTimeOffset.UnixEpoch,
            1,
            CaseLifecycleState.Review,
            new(new(false, false), new(false, "test", 1)),
            new(text),
            new(text, text, text),
            new(text),
            new(
                new(new(registration, CaseDataValueKind.Fact, source), null, null),
                text,
                text,
                text,
                new(new(mileage, CaseDataValueKind.Fact, source), null, null),
                text),
            new(date, text),
            new(text, text, text),
            new(new DateOnly(1970, 1, 1), text),
            new(date, date, text, new(null, null, null)));
    }

    private sealed class StubProvider(ValuationSource source) : IGuideValuationProvider
    {
        public ValuationSource Source => source;

        public Task<GuideValuationQuote> GetAsync(GuideValuationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class AnsweringProvider(
        ValuationSource source, IGuideValuationReport? report, string? vin = null) : IGuideValuationProvider
    {
        public ValuationSource Source => source;

        public Task<GuideValuationQuote> GetAsync(GuideValuationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new GuideValuationQuote(17717m, 15600m, request.GuideMonth, request.Mileage)
            {
                Report = report,
                Vin = vin,
            });
    }

    private sealed class CountingProvider(Exception? failure = null) : IGuideValuationProvider
    {
        public int Calls { get; private set; }

        public ValuationSource Source => ValuationSource.Glasses;

        public Task<GuideValuationQuote> GetAsync(GuideValuationRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return failure is null
                ? Task.FromResult(new GuideValuationQuote(17717m, 15600m, request.GuideMonth, request.Mileage))
                : Task.FromException<GuideValuationQuote>(failure);
        }
    }

    private sealed class AutomaticStore(AutomaticGuideValuationTarget target, bool begins = true)
        : IAutomaticGuideValuationStore
    {
        public List<string> Steps { get; } = [];

        public List<(ActionActor Actor, ValuationDetails Card)> Recorded { get; } = [];

        public Task<IReadOnlyList<AutomaticGuideValuationRequest>> ListDueAutomaticAsync(
            DateTimeOffset nowUtc, int maximumItems, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AutomaticGuideValuationTarget?> ReadAutomaticTargetAsync(
            Guid caseId, ValuationSource source, CancellationToken cancellationToken) =>
            Task.FromResult<AutomaticGuideValuationTarget?>(target);

        public Task DeferAutomaticAsync(Guid workItemId, DateTimeOffset dueAtUtc, CancellationToken cancellationToken)
        {
            Steps.Add($"defer:{dueAtUtc:O}");
            return Task.CompletedTask;
        }

        public Task<bool> BeginAutomaticAsync(Guid workItemId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            Steps.Add("begin");
            return Task.FromResult(begins);
        }

        public Task CompleteAutomaticAsync(
            Guid workItemId,
            AutomaticGuideValuationOutcome outcome,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            Steps.Add($"complete:{outcome}");
            return Task.CompletedTask;
        }

        public Task<bool> RecordAutomaticAsync(
            Guid caseId,
            ActionActor actor,
            string operationKey,
            ValuationDetails card,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
        {
            Steps.Add("record");
            Recorded.Add((actor, card));
            return Task.FromResult(true);
        }
    }

    private sealed class FixedSwitch(bool on) : IAutomaticGuideValuationControl
    {
        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) => Task.FromResult(on);

        public Task<bool> SetEnabledAsync(
            bool enabled, ActionActor actor, string? reason, string operationKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingVins : IFillGlassVin
    {
        public List<(ActionActor Actor, Guid CaseId, CaseWorkSelector Work, string Vin)> Fills { get; } = [];

        public Task FillAsync(
            ActionActor actor, Guid caseId, CaseWorkSelector work, string vin, CancellationToken cancellationToken)
        {
            Fills.Add((actor, caseId, work, vin));
            return Task.CompletedTask;
        }
    }

    private sealed class StubReport(string identity) : IGuideValuationReport
    {
        public string Identity => identity;

        public Task<byte[]> FetchPdfAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingSchedule : IScheduleGuideValuationReport
    {
        public List<FileGuideValuationReportRequest> Requests { get; } = [];

        public Task ScheduleAsync(FileGuideValuationReportRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCaseData(CaseDataProjection? data = null) : ICaseDataQueries
    {
        public int Reads { get; private set; }

        public Task<CaseDataProjection?> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(data);
        }
    }
}
