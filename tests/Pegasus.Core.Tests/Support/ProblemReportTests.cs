using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Support;

namespace Pegasus.Core.Tests.Support;

public sealed class ProblemReportTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AdministratorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 15, 0, 0, TimeSpan.Zero);

    private static ActionActor User() => ActionActor.Staff(UserId, [StaffRole.User]);
    private static ActionActor Administrator() => ActionActor.Staff(AdministratorId, [StaffRole.Administrator]);

    private static ProblemReportRequest Request(ActionActor actor, string? description = "The Save button did nothing.") => new(
        actor,
        description,
        "0.1.0-alpha.1",
        new string('c', 40),
        "/Cases/1f2e?section=estimate",
        "GET",
        "00-trace-01",
        "integration-user",
        "User",
        "QDOS26001",
        null,
        null,
        new ProblemReportClientFacts("1580x1000", "Mozilla/5.0", true, ["2026-09-20T14:59:00Z TypeError: x is undefined"]));

    [Fact]
    public async Task AReportIsStoredThenRaisedWithThePersonsWordsFirst()
    {
        var store = new FakeStore();
        var sink = new FakeSink();
        var logs = new FakeLogs([new ActionLogRow(Guid.NewGuid(), "Cases", "Save", "QDOS26001", UserId.ToString("D"), Now.AddMinutes(-2), "Succeeded", "corr-1", "Staff")]);
        var report = await new ReportProblem(store, sink, logs, new FixedClock(Now)).ExecuteAsync(Request(User()), default);

        Assert.Equal(ProblemReportStatus.Sent, report.Status);
        Assert.Equal(42, report.IssueNumber);
        Assert.Equal("https://github.com/example/pegasus/issues/42", report.IssueUrl);
        Assert.Equal(Now, report.SentAtUtc);
        Assert.Equal(UserId, report.StaffId);
        var sent = Assert.Single(sink.Sent);
        Assert.Equal("Problem report: /Cases/1f2e?section=estimate (QDOS26001)", ProblemReportPolicy.Title(sent));
        var body = ProblemReportPolicy.Body(sent);
        Assert.StartsWith("The Save button did nothing.", body, StringComparison.Ordinal);
        Assert.Contains("source     " + new string('c', 40), body, StringComparison.Ordinal);
        Assert.Contains("Cases / Save  QDOS26001  Succeeded", body, StringComparison.Ordinal);
        Assert.Contains("TypeError: x is undefined", body, StringComparison.Ordinal);
        Assert.Equal(UserId.ToString("D"), logs.LastFilter!.Actor);
        Assert.Equal(ProblemReportPolicy.RecentActionCount, logs.LastFilter.PageSize);
    }

    [Fact]
    public async Task AFailedRaiseKeepsTheReportAsNotSentWithTheReasonAndAnAdministratorRetries()
    {
        var store = new FakeStore();
        var sink = new FakeSink { Failure = new HttpRequestException("GitHub refused the issue with 401 Unauthorized.") };
        var clock = new FixedClock(Now);
        var report = await new ReportProblem(store, sink, new FakeLogs([]), clock).ExecuteAsync(Request(User()), default);

        Assert.Equal(ProblemReportStatus.NotSent, report.Status);
        Assert.Null(report.IssueNumber);
        Assert.Equal("GitHub refused the issue with 401 Unauthorized.", report.Failure);
        Assert.Equal("The Save button did nothing.", report.Description);
        Assert.Null(report.DispatchClaimToken);
        Assert.Null(report.DispatchClaimExpiresAtUtc);

        var retry = new RetryProblemReport(store, sink, clock);
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => retry.ExecuteAsync(User(), report.Id, default));

        sink.Failure = null;
        var retried = await retry.ExecuteAsync(Administrator(), report.Id, default);
        Assert.Equal(ProblemReportStatus.Sent, retried!.Status);
        Assert.Equal(42, retried.IssueNumber);
        Assert.Null(retried.Failure);

        // A sent report is not raised twice.
        var again = await retry.ExecuteAsync(Administrator(), report.Id, default);
        Assert.Single(sink.Sent);
        Assert.Equal(ProblemReportStatus.Sent, again!.Status);
    }

    [Fact]
    public async Task AnEmptyDescriptionOrANonStaffActorIsRefusedBeforeAnythingIsStored()
    {
        var store = new FakeStore();
        var report = new ReportProblem(store, new FakeSink(), new FakeLogs([]), new FixedClock(Now));

        await Assert.ThrowsAsync<ArgumentException>(() => report.ExecuteAsync(Request(User(), "   "), default));
        await Assert.ThrowsAsync<ArgumentException>(() => report.ExecuteAsync(Request(User(), new string('x', ProblemReportPolicy.MaximumDescriptionLength + 1)), default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => report.ExecuteAsync(Request(ActionActor.Automation("automation-client")), default));
        Assert.Empty(store.Reports);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => new ListProblemReports(store).ExecuteAsync(User(), default));
    }

    [Fact]
    public async Task AnUnavailableActionLogDoesNotStopTheReport()
    {
        var store = new FakeStore();
        var logs = new FakeLogs([]) { Failure = new InvalidOperationException("log down") };
        var report = await new ReportProblem(store, new FakeSink(), logs, new FixedClock(Now)).ExecuteAsync(Request(User()), default);

        Assert.Equal(ProblemReportStatus.Sent, report.Status);
        Assert.Empty(report.Snapshot.RecentActions);
    }

    [Fact]
    public async Task ListReturnsEveryRetainedReportInNewestFirstOrder()
    {
        var store = new FakeStore();
        for (var index = 0; index < 201; index++)
        {
            store.Reports.Add(RetainedReport(index));
        }

        var reports = await new ListProblemReports(store).ExecuteAsync(Administrator(), default);

        Assert.Equal(201, reports.Count);
        Assert.Equal("Report 200", reports[0].Description);
        Assert.Equal("Report 0", reports[^1].Description);
    }

    private static ProblemReport RetainedReport(int index)
    {
        var request = Request(User());
        var occurredAt = Now.AddMinutes(index);
        var snapshot = new ProblemReportSnapshot(
            request.Version,
            request.SourceSha,
            occurredAt,
            request.Route,
            request.Method,
            request.TraceId,
            request.ActorName,
            request.ActorRole,
            request.CaseReference,
            request.ExceptionType,
            request.ExceptionMessage,
            [],
            request.Client);
        return new ProblemReport(
            Guid.NewGuid(),
            UserId,
            $"Report {index}",
            snapshot,
            occurredAt,
            ProblemReportStatus.NotSent,
            null,
            null,
            "not sent",
            null,
            null,
            null);
    }

    [Fact]
    public async Task ConcurrentRetriesGiveTheSinkToOnlyOneClaimant()
    {
        var store = new FakeStore();
        var failedSink = new FakeSink { Failure = new HttpRequestException("temporary failure") };
        var failed = await new ReportProblem(store, failedSink, new FakeLogs([]), new FixedClock(Now)).ExecuteAsync(Request(User()), default);
        var sink = new BlockingSink();
        var retry = new RetryProblemReport(store, sink, new FixedClock(Now));

        var first = retry.ExecuteAsync(Administrator(), failed.Id, default);
        await sink.Entered.Task;

        var second = await retry.ExecuteAsync(Administrator(), failed.Id, default);
        Assert.Equal(ProblemReportStatus.NotSent, second!.Status);
        Assert.Equal(1, sink.Calls);

        sink.Release.TrySetResult(true);
        var sent = await first;
        Assert.Equal(ProblemReportStatus.Sent, sent!.Status);
        Assert.Single(sink.Sent);
    }

    [Fact]
    public async Task AnExpiredClaimCanBeReclaimedAfterAnAbandonedAttempt()
    {
        var store = new FakeStore();
        var sink = new FakeSink { Failure = new HttpRequestException("temporary failure") };
        var failed = await new ReportProblem(store, sink, new FakeLogs([]), new FixedClock(Now)).ExecuteAsync(Request(User()), default);

        var lease = ProblemReportPolicy.DispatchClaimLease;
        var abandoned = await store.TryClaimAsync(failed.Id, "abandoned", Now - lease, lease, default);
        Assert.Equal("abandoned", abandoned!.DispatchClaimToken);

        sink.Failure = null;
        var recovered = await new RetryProblemReport(store, sink, new FixedClock(Now)).ExecuteAsync(Administrator(), failed.Id, default);

        Assert.Equal(ProblemReportStatus.Sent, recovered!.Status);
        Assert.Single(sink.Sent);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeLogs(IReadOnlyList<ActionLogRow> rows) : IActionLogQueries
    {
        public ActionLogFilter? LastFilter { get; private set; }
        public Exception? Failure { get; init; }

        public Task<ActionLogPage> ListAsync(ActionLogFilter filter, CancellationToken cancellationToken)
        {
            LastFilter = filter;
            return Failure is null ? Task.FromResult(new ActionLogPage(rows, false)) : Task.FromException<ActionLogPage>(Failure);
        }
    }

    private sealed class FakeSink : IProblemReportSink
    {
        public List<ProblemReport> Sent { get; } = [];
        public Exception? Failure { get; set; }

        public Task<ProblemReportDelivery> SendAsync(ProblemReport report, CancellationToken cancellationToken)
        {
            if (Failure is not null)
            {
                return Task.FromException<ProblemReportDelivery>(Failure);
            }

            Sent.Add(report);
            return Task.FromResult(new ProblemReportDelivery(42, "https://github.com/example/pegasus/issues/42"));
        }
    }

    private sealed class BlockingSink : IProblemReportSink
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<ProblemReport> Sent { get; } = [];
        public int Calls;

        public async Task<ProblemReportDelivery> SendAsync(ProblemReport report, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            Sent.Add(report);
            return new ProblemReportDelivery(42, "https://github.com/example/pegasus/issues/42");
        }
    }

    private sealed class FakeStore : IProblemReportStore
    {
        public List<ProblemReport> Reports { get; } = [];

        public Task<ProblemReport> AddAsync(NewProblemReport report, CancellationToken cancellationToken)
        {
            var added = new ProblemReport(Guid.NewGuid(), report.StaffId, report.Description, report.Snapshot, report.CreatedAtUtc,
                ProblemReportStatus.NotSent, null, null, null, null, null, null);
            Reports.Add(added);
            return Task.FromResult(added);
        }

        public Task<ProblemReport?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Reports.SingleOrDefault(report => report.Id == id));

        public Task<IReadOnlyList<ProblemReport>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProblemReport>>(Reports.OrderByDescending(report => report.CreatedAtUtc).ToArray());

        public Task<ProblemReport?> TryClaimAsync(
            Guid id,
            string claimToken,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            var claimExpiresAtUtc = nowUtc.Add(leaseDuration);
            lock (Reports)
            {
                var index = Reports.FindIndex(report => report.Id == id
                    && report.Status == ProblemReportStatus.NotSent
                    && (report.DispatchClaimToken is null
                        || report.DispatchClaimExpiresAtUtc is null
                        || report.DispatchClaimExpiresAtUtc <= nowUtc));
                if (index < 0)
                {
                    return Task.FromResult<ProblemReport?>(null);
                }

                Reports[index] = Reports[index] with
                {
                    DispatchClaimToken = claimToken,
                    DispatchClaimExpiresAtUtc = claimExpiresAtUtc
                };
                return Task.FromResult<ProblemReport?>(Reports[index]);
            }
        }

        public Task<ProblemReport> MarkSentAsync(Guid id, string claimToken, ProblemReportDelivery delivery, DateTimeOffset atUtc, CancellationToken cancellationToken) =>
            Task.FromResult(Replace(id, report => report.DispatchClaimToken == claimToken
                ? report with
                {
                    Status = ProblemReportStatus.Sent,
                    IssueNumber = delivery.IssueNumber,
                    IssueUrl = delivery.IssueUrl,
                    Failure = null,
                    SentAtUtc = atUtc,
                    DispatchClaimExpiresAtUtc = null,
                    DispatchClaimToken = null
                }
                : report));

        public Task<ProblemReport> MarkNotSentAsync(Guid id, string claimToken, string failure, CancellationToken cancellationToken) =>
            Task.FromResult(Replace(id, report => report.DispatchClaimToken == claimToken
                ? report with
                {
                    Status = ProblemReportStatus.NotSent,
                    Failure = failure,
                    DispatchClaimExpiresAtUtc = null,
                    DispatchClaimToken = null
                }
                : report));

        private ProblemReport Replace(Guid id, Func<ProblemReport, ProblemReport> change)
        {
            lock (Reports)
            {
                var index = Reports.FindIndex(report => report.Id == id);
                Reports[index] = change(Reports[index]);
                return Reports[index];
            }
        }
    }
}
