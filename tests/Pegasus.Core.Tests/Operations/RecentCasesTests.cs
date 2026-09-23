using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

public sealed class RecentCasesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid StaffId = Guid.NewGuid();
    private static readonly ActionActor Staff = ActionActor.Staff(StaffId, [StaffRole.User]);

    [Theory]
    [InlineData(IntakeSourceChannel.Mailbox, false, CaseArrival.Email)]
    [InlineData(IntakeSourceChannel.ProviderApi, false, CaseArrival.ProviderApi)]
    [InlineData(IntakeSourceChannel.Automation, false, CaseArrival.Automation)]
    [InlineData(IntakeSourceChannel.ManualUpload, true, CaseArrival.Manual)]
    [InlineData(null, false, CaseArrival.Manual)]
    [InlineData(null, true, CaseArrival.Automation)]
    public void ArrivalComesFromTheReceiptChannelThenTheCreatingActor(
        IntakeSourceChannel? channel, bool createdByAutomation, CaseArrival expected) =>
        Assert.Equal(expected, RecentCasesPolicy.Arrival(channel, createdByAutomation));

    [Fact]
    public void TheWindowIsSevenCalendarDaysFromMidnightLondon()
    {
        // 13 September 11:00 London: the window opens at midnight on 6 September (23:00 UTC on the 5th).
        Assert.Equal(new DateTimeOffset(2026, 9, 5, 23, 0, 0, TimeSpan.Zero), RecentCasesPolicy.WindowStart(Now));
    }

    [Fact]
    public async Task TheFeedReturnsThePreviousLookAndStampsThisOne()
    {
        var queries = new FakeQueries();
        var visits = new FakeVisits { LastSeen = Now.AddDays(-2) };
        var sut = new ListRecentCases(queries, visits, new FixedTime(Now));

        var first = await sut.ExecuteAsync(Staff, 1, markSeen: true, default);

        // The second read runs five minutes later on its own clock. With one
        // fixed clock for both, a markSeen:false read that stamped anyway wrote
        // the same instant and no assertion could tell.
        var later = Now.AddMinutes(5);
        var second = await new ListRecentCases(queries, visits, new FixedTime(later))
            .ExecuteAsync(Staff, 2, markSeen: false, default);

        Assert.Equal(Now.AddDays(-2), first.LastSeenUtc);
        Assert.Equal(RecentCasesPolicy.WindowStart(Now), first.WindowStartUtc);
        Assert.Equal(Now, visits.LastSeen);
        Assert.NotEqual(later, visits.LastSeen);
        Assert.Equal(Now, second.LastSeenUtc);
        Assert.Equal([(RecentCasesPolicy.WindowStart(Now), 1), (RecentCasesPolicy.WindowStart(Now), 2)], queries.Reads);
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => sut.ExecuteAsync(ActionActor.Automation("client"), 1, true, default));
    }

    [Fact]
    public async Task ARequestCrossingLondonMidnightUsesItsCapturedBoundaryForTheFeedAndVisit()
    {
        var requestTime = new DateTimeOffset(2026, 9, 13, 22, 59, 59, TimeSpan.Zero);
        var queries = new FakeQueries();
        var visits = new FakeVisits();
        var sut = new ListRecentCases(queries, visits, new FixedTime(requestTime.AddSeconds(2)));

        var result = await sut.ExecuteAsync(Staff, 1, markSeen: true, default, requestTime);

        var expectedStart = new DateTimeOffset(2026, 9, 5, 23, 0, 0, TimeSpan.Zero);
        Assert.Equal(expectedStart, result.WindowStartUtc);
        Assert.Equal([(expectedStart, 1)], queries.Reads);
        Assert.Equal(requestTime, visits.LastSeen);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeQueries : IRecentCaseQueries
    {
        public List<(DateTimeOffset Since, int Page)> Reads { get; } = [];

        public Task<RecentCasesPage> ListAsync(DateTimeOffset sinceUtc, int page, int pageSize, CancellationToken cancellationToken)
        {
            Reads.Add((sinceUtc, page));
            return Task.FromResult(new RecentCasesPage([], page, pageSize, 0));
        }
    }

    private sealed class FakeVisits : IWorkCentreVisitStore
    {
        public DateTimeOffset? LastSeen { get; set; }

        public Task<DateTimeOffset?> GetLastSeenAsync(Guid staffId, CancellationToken cancellationToken) =>
            Task.FromResult(staffId == StaffId ? LastSeen : null);

        public Task MarkSeenAsync(Guid staffId, DateTimeOffset seenAtUtc, CancellationToken cancellationToken)
        {
            LastSeen = seenAtUtc;
            return Task.CompletedTask;
        }
    }
}
