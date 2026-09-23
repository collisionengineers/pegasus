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

    private static ProblemReportRequest Request(
        ActionActor actor, string? description = "The Save button did nothing.", string? operationKey = null) => new(
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
        new ProblemReportClientFacts("1580x1000", "Mozilla/5.0", true, ["2026-09-20T14:59:00Z TypeError: x is undefined"]),
        operationKey ?? Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AReportIssueCarriesTheReporterAndCapturedContext()
    {
        var store = new FakeStore();
        var sink = new FakeSink();
        var logs = new FakeLogs([new ActionLogRow(Guid.NewGuid(), "Cases", "Save", "QDOS26001", UserId.ToString("D"), Now.AddMinutes(-2), "Succeeded", "corr-1", "Staff")]);
        var report = await new ReportProblem(store, sink, logs, new FixedClock(Now)).ExecuteAsync(Request(User()), default);

        Assert.Equal(ProblemReportStatus.Sent, report.Status);
        Assert.Equal(42, report.IssueNumber);
        Assert.Equal("https://github.com/collisionengineers/pegasus/issues/42", report.IssueUrl);
        Assert.Equal(Now, report.SentAtUtc);
        Assert.Equal(UserId, report.StaffId);
        var sent = Assert.Single(sink.Sent);
        Assert.Equal($"Pegasus: The Save button did nothing. [{sent.Id:D}]", ProblemReportPolicy.Title(sent));
        var body = ProblemReportPolicy.Body(sent);
        Assert.Contains(sent.Id.ToString("D"), body, StringComparison.Ordinal);
        Assert.Contains(sent.Description, body, StringComparison.Ordinal);
        Assert.Contains("QDOS26001", body, StringComparison.Ordinal);
        Assert.Contains("integration-user", body, StringComparison.Ordinal);
        Assert.Contains("TypeError: x is undefined", body, StringComparison.Ordinal);
        Assert.Contains("No server exception was captured", body, StringComparison.Ordinal);
        Assert.Equal("QDOS26001", sent.Snapshot.CaseReference);
        Assert.Null(logs.LastFilter!.Actor);
        Assert.Equal(UserId.ToString("D"), logs.LastFilter.ActingActor);
        Assert.Equal(ProblemReportPolicy.RecentActionCount, logs.LastFilter.PageSize);
    }

    [Fact]
    public void IssueBodyIncludesCapturedInnerExceptionAndStack()
    {
        var report = RetainedReport(0);
        var details = "System.InvalidOperationException: deletion failed\n ---> Microsoft.Data.SqlClient.SqlException: DELETE permission denied\n   at Pegasus.Delete()";
        report = report with { Snapshot = report.Snapshot with { ExceptionDetails = details } };

        var body = ProblemReportPolicy.Body(report);

        Assert.Contains("DELETE permission denied", body, StringComparison.Ordinal);
        Assert.Contains("at Pegasus.Delete()", body, StringComparison.Ordinal);
        Assert.DoesNotContain("No server exception was captured", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASlowAcceptedPostRecordsSentWhileItsOriginalClaimIsStillOwned()
    {
        var store = new FakeStore();
        var sink = new BlockingSink();
        var clock = new MutableClock(Now);
        var sending = new ReportProblem(store, sink, new FakeLogs([]), clock)
            .ExecuteAsync(Request(User()), default);
        await sink.Entered.Task;

        clock.Now = Now.Add(ProblemReportPolicy.DispatchClaimLease).AddSeconds(1);
        sink.Release.TrySetResult(true);
        var report = await sending;

        Assert.Equal(ProblemReportStatus.Sent, report.Status);
        Assert.Equal(42, report.IssueNumber);
        Assert.Single(sink.Sent);
    }

    [Fact]
    public async Task AFailureRecordingSentDeliveryDoesNotCallTheSendFailureHandler()
    {
        var store = new FakeStore
        {
            MarkSentFailure = new InvalidOperationException("database unavailable")
        };
        var sink = new FakeSink();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ReportProblem(store, sink, new FakeLogs([]), new FixedClock(Now))
                .ExecuteAsync(Request(User()), default));

        Assert.Equal("database unavailable", exception.Message);
        Assert.Single(sink.Sent);
        Assert.Equal(0, store.MarkNotSentCalls);
        Assert.Equal(ProblemReportStatus.NotSent, Assert.Single(store.Reports).Status);
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
    public async Task SameOperationReturnsTheFirstReportWithoutPostingTwiceAndChangedDetailsConflict()
    {
        var store = new FakeStore();
        var sink = new FakeSink();
        var action = new ReportProblem(store, sink, new FakeLogs([]), new FixedClock(Now));
        var key = Guid.NewGuid().ToString("N");

        var first = await action.ExecuteAsync(Request(User(), operationKey: key), default);
        var replay = await action.ExecuteAsync(Request(User(), operationKey: key), default);

        Assert.Equal(first.Id, replay.Id);
        Assert.Single(store.Reports);
        Assert.Single(sink.Sent);
        await Assert.ThrowsAsync<ProblemReportOperationConflictException>(() =>
            action.ExecuteAsync(Request(User(), "Different details", key), default));
        Assert.Single(sink.Sent);
    }

    [Fact]
    public async Task AmbiguousDeliveryIsHeldUntilAnAdministratorConfirmsTheIssue()
    {
        var store = new FakeStore();
        var sink = new FakeSink
        {
            Failure = new ProblemReportDeliveryUnknownException("The GitHub issue POST outcome is unknown.")
        };
        var clock = new FixedClock(Now);
        var report = await new ReportProblem(store, sink, new FakeLogs([]), clock)
            .ExecuteAsync(Request(User()), default);

        Assert.Equal(ProblemReportStatus.Unknown, report.Status);
        sink.Failure = null;
        var retry = new RetryProblemReport(store, sink, clock);
        Assert.Equal(ProblemReportStatus.Unknown,
            (await retry.ExecuteAsync(Administrator(), report.Id, default))!.Status);
        Assert.Empty(sink.Sent);
        var delivery = new ProblemReportDelivery(123, "https://github.com/collisionengineers/pegasus/issues/123");
        var reconciled = await new ReconcileProblemReport(store, clock)
            .ConfirmIssueAsync(Administrator(), report.Id, delivery, default);
        Assert.Equal(123, reconciled.IssueNumber);
        Assert.Equal(ProblemReportStatus.Sent, reconciled.Status);
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
    public async Task AnExpiredClaimRequiresReconciliationBeforeAnotherPost()
    {
        var store = new FakeStore();
        var sink = new FakeSink { Failure = new HttpRequestException("temporary failure") };
        var failed = await new ReportProblem(store, sink, new FakeLogs([]), new FixedClock(Now)).ExecuteAsync(Request(User()), default);

        var lease = ProblemReportPolicy.DispatchClaimLease;
        var abandoned = await store.TryClaimAsync(failed.Id, "abandoned", Now - lease, lease, default);
        Assert.Equal("abandoned", abandoned!.DispatchClaimToken);

        sink.Failure = null;
        var unresolved = await new RetryProblemReport(store, sink, new FixedClock(Now)).ExecuteAsync(Administrator(), failed.Id, default);

        Assert.Equal(ProblemReportStatus.Unknown, unresolved!.Status);
        Assert.Empty(sink.Sent);
        await new ReconcileProblemReport(store, new FixedClock(Now))
            .ConfirmNoIssueAsync(Administrator(), failed.Id, default);
        var recovered = await new RetryProblemReport(store, sink, new FixedClock(Now)).ExecuteAsync(Administrator(), failed.Id, default);
        Assert.Equal(ProblemReportStatus.Sent, recovered!.Status);
        Assert.Single(sink.Sent);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
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
            return Task.FromResult(new ProblemReportDelivery(42, "https://github.com/collisionengineers/pegasus/issues/42"));
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
            return new ProblemReportDelivery(42, "https://github.com/collisionengineers/pegasus/issues/42");
        }
    }

    private sealed class FakeStore : IProblemReportStore
    {
        public List<ProblemReport> Reports { get; } = [];
        public Exception? MarkSentFailure { get; init; }
        public int MarkNotSentCalls { get; private set; }

        public Task<ProblemReportAddResult> AddAsync(NewProblemReport report, CancellationToken cancellationToken)
        {
            var existing = Reports.SingleOrDefault(item => item.OperationKey == report.OperationKey);
            if (existing is not null)
            {
                if (existing.StaffId != report.StaffId || existing.RequestHash != report.RequestHash)
                    throw new ProblemReportOperationConflictException();
                return Task.FromResult(new ProblemReportAddResult(existing, true));
            }
            var added = new ProblemReport(Guid.NewGuid(), report.StaffId, report.Description, report.Snapshot, report.CreatedAtUtc,
                ProblemReportStatus.NotSent, null, null, null, null, null, null, report.OperationKey, report.RequestHash);
            Reports.Add(added);
            return Task.FromResult(new ProblemReportAddResult(added, false));
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
                    && report.Status == ProblemReportStatus.NotSent);
                if (index < 0)
                {
                    return Task.FromResult<ProblemReport?>(null);
                }
                if (Reports[index].DispatchClaimToken is not null)
                {
                    if (Reports[index].DispatchClaimExpiresAtUtc is null
                        || Reports[index].DispatchClaimExpiresAtUtc <= nowUtc)
                        Reports[index] = Reports[index] with { Status = ProblemReportStatus.Unknown,
                            Failure = "Previous outcome unknown.", DispatchClaimToken = null,
                            DispatchClaimExpiresAtUtc = null };
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
            MarkSentFailure is { } failure
                ? Task.FromException<ProblemReport>(failure)
                : Task.FromResult(Replace(id, report => report.DispatchClaimToken == claimToken
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
                : throw new ProblemReportClaimConflictException()));

        public Task<ProblemReport> MarkNotSentAsync(Guid id, string claimToken, string failure, CancellationToken cancellationToken)
        {
            MarkNotSentCalls++;
            return Task.FromResult(Replace(id, report => report.DispatchClaimToken == claimToken
                ? report with
                {
                    Status = ProblemReportStatus.NotSent,
                    Failure = failure,
                    DispatchClaimExpiresAtUtc = null,
                    DispatchClaimToken = null
                }
                : throw new ProblemReportClaimConflictException()));
        }

        public Task<ProblemReport> MarkUnknownAsync(Guid id, string claimToken, string reason, CancellationToken cancellationToken) =>
            Task.FromResult(Replace(id, report => report.DispatchClaimToken == claimToken
                ? report with { Status = ProblemReportStatus.Unknown, Failure = reason,
                    DispatchClaimToken = null, DispatchClaimExpiresAtUtc = null }
                : throw new ProblemReportClaimConflictException()));

        public Task<ProblemReport> ConfirmIssueAsync(Guid id, ProblemReportDelivery delivery, DateTimeOffset atUtc, CancellationToken cancellationToken) =>
            Task.FromResult(Replace(id, report => report.Status == ProblemReportStatus.Unknown
                ? report with { Status = ProblemReportStatus.Sent, IssueNumber = delivery.IssueNumber,
                    IssueUrl = delivery.IssueUrl, SentAtUtc = atUtc, Failure = null }
                : throw new ProblemReportClaimConflictException()));

        public Task<ProblemReport> ConfirmNoIssueAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Replace(id, report => report.Status == ProblemReportStatus.Unknown
                ? report with { Status = ProblemReportStatus.NotSent, Failure = null }
                : throw new ProblemReportClaimConflictException()));

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
