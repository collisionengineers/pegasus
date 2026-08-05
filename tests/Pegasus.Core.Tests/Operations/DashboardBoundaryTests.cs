using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Tests.Operations;

/// <summary>
/// "Today" and "this week" on the dashboard mean the office's today and week.
/// </summary>
/// <remarks>
/// Counting from a UTC midnight would move the boundary by an hour for half the
/// year and silently reassign work between days, which is exactly the kind of
/// quiet wrongness a count nobody can check invites.
/// </remarks>
public sealed class DashboardBoundaryTests
{
    [Fact]
    public async Task BritishSummerTimeDayStartsAtTheOfficeMidnightNotTheUtcOne()
    {
        // 00:30 on 5 August in London is 23:30 on 4 August UTC. The office's
        // day has already started; a UTC-midnight boundary would still be
        // counting the previous day.
        var recorder = new RecordingDashboardQueries();
        var snapshot = await ExecuteAsync(recorder, new DateTimeOffset(2026, 8, 4, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 8, 4, 23, 0, 0, TimeSpan.Zero), recorder.DayStartUtc);
        Assert.NotNull(snapshot);
    }

    [Fact]
    public async Task WeekStartsOnMondayBecauseThatIsTheWeekTheOfficeWorksTo()
    {
        // Wednesday 5 August 2026, midday London.
        var recorder = new RecordingDashboardQueries();
        await ExecuteAsync(recorder, new DateTimeOffset(2026, 8, 5, 11, 0, 0, TimeSpan.Zero));

        // Monday 3 August, 00:00 London == 23:00 UTC on Sunday 2 August.
        Assert.Equal(new DateTimeOffset(2026, 8, 2, 23, 0, 0, TimeSpan.Zero), recorder.WeekStartUtc);
    }

    [Fact]
    public async Task OnAMondayTheWeekStartsThatMorningRatherThanSevenDaysEarlier()
    {
        // Monday 3 August 2026, 09:00 London.
        var recorder = new RecordingDashboardQueries();
        await ExecuteAsync(recorder, new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(recorder.DayStartUtc, recorder.WeekStartUtc);
    }

    private static async Task<OperationsSnapshot> ExecuteAsync(
        RecordingDashboardQueries recorder,
        DateTimeOffset nowUtc)
    {
        var timeProvider = new FixedTimeProvider(nowUtc);
        var snapshot = new GetOperationsSnapshot(
            new StubIntakeReceiptQueries(),
            new StubListTriage(),
            new StubDueWorkQueries(),
            recorder,
            timeProvider);
        return await snapshot.ExecuteAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]));
    }

    private sealed class RecordingDashboardQueries : IDashboardQueries
    {
        public DateTimeOffset DayStartUtc { get; private set; }

        public DateTimeOffset WeekStartUtc { get; private set; }

        public Task<CaseStageCounts> GetCaseStageCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CaseStageCounts(0, 0, 0));

        public Task<CaseActivityCounts> GetCaseActivityCountsAsync(
            DateTimeOffset dayStartUtc,
            DateTimeOffset weekStartUtc,
            CancellationToken cancellationToken)
        {
            DayStartUtc = dayStartUtc;
            WeekStartUtc = weekStartUtc;
            return Task.FromResult(new CaseActivityCounts(0, 0, 0, 0, 0));
        }

        public Task<MailActivityCounts> GetMailActivityCountsAsync(
            DateTimeOffset dayStartUtc,
            CancellationToken cancellationToken)
        {
            DayStartUtc = dayStartUtc;
            return Task.FromResult(new MailActivityCounts(0, 0));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }

    private sealed class StubIntakeReceiptQueries : IIntakeReceiptQueries
    {
        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeQueueCounts(0, 0));

        public Task<IReadOnlyList<IntakeReceiptSummary>> ListAsync(
            IntakeDecision? decision,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IntakeReceiptSummary>>([]);

        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IntakeReceipt?>(null);

        public Task<IntakeAssetRecord?> GetAssetAsync(
            Guid receiptId,
            Guid assetId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IntakeAssetRecord?>(null);
    }

    private sealed class StubListTriage : IListTriage
    {
        public Task<TriageListPage> ExecuteAsync(
            ListTriageQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TriageListPage([], query.Page, query.PageSize, 0));
    }

    private sealed class StubDueWorkQueries : ICaseDueWorkQueries
    {
        public Task<CaseDueWork?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseDueWork?>(null);

        public Task<IReadOnlyList<CaseDueWork>> GetDueAsync(
            DateTimeOffset asOfUtc,
            int maximumResults,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseDueWork>>([]);
    }
}
