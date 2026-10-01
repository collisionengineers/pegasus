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
        var fetch = new FetchGuideValuation([], caseData, new RecordingSchedule());

        await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            fetch.ExecuteAsync(Request(ValuationSource.Glasses), default));

        Assert.Equal(0, caseData.Reads);
    }

    [Theory]
    [InlineData(ValuationSource.EngineersValue)]
    [InlineData(ValuationSource.AiMarketResearch)]
    public async Task OnlyAGuideSourceCanBeFetched(ValuationSource source)
    {
        var fetch = new FetchGuideValuation([], new RecordingCaseData(), new RecordingSchedule());

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
            [new StubProvider(ValuationSource.Brego)], new RecordingCaseData(), new RecordingSchedule());

        Assert.True(fetch.IsConnected(ValuationSource.Brego));
        Assert.False(fetch.IsConnected(ValuationSource.Glasses));
        Assert.False(fetch.IsConnected(ValuationSource.EngineersValue));
        Assert.False(new FetchGuideValuation([], new RecordingCaseData(), new RecordingSchedule())
            .IsConnected(ValuationSource.Brego));
    }

    [Fact]
    public async Task AnActorWithoutCaseworkOrAnEmptyKeyIsRefused()
    {
        var fetch = new FetchGuideValuation([], new RecordingCaseData(), new RecordingSchedule());
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
            schedule);

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
            schedule);

        await fetch.ExecuteAsync(request, default);

        Assert.Empty(schedule.Requests);
    }

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

    private sealed class AnsweringProvider(ValuationSource source, IGuideValuationReport? report) : IGuideValuationProvider
    {
        public ValuationSource Source => source;

        public Task<GuideValuationQuote> GetAsync(GuideValuationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new GuideValuationQuote(17717m, 15600m, request.GuideMonth, request.Mileage) { Report = report });
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
