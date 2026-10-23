using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

/// <summary>
/// Log query and Resolve query (operator, 11 October 2026): the use cases
/// hand the store the request as it is. The store asks the Core rule of the
/// workflow it read under its lock and skips it for a replay of the same
/// operation key (eff-07-l, register A3-06).
/// </summary>
public sealed class CaseQueryLifecycleTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly ActionActor Actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Fact]
    public async Task LoggingAQueryOnAReportSentCaseReachesTheStoreWithItsChannelAndDetails()
    {
        var store = new QueryStore(CaseLifecycleState.ReportSent, hasOpenQuery: false);
        var request = Raise(CaseQueryChannel.Phone, "The claimant disputes the pre-accident value.");

        var raised = await new RaiseCaseQuery(store).ExecuteAsync(request, default);

        Assert.Same(request, store.Raised);
        Assert.True(raised.HasOpenQuery);
    }

    [Fact]
    public async Task ADetailLessQueryIsStillLogged()
    {
        var store = new QueryStore(CaseLifecycleState.ReportSent, hasOpenQuery: false);

        await new RaiseCaseQuery(store).ExecuteAsync(Raise(CaseQueryChannel.WhatsApp, null), default);

        Assert.Equal(CaseQueryChannel.WhatsApp, store.Raised!.Channel);
        Assert.Null(store.Raised.Details);
    }

    [Theory]
    [InlineData("not sent", false, false)]
    [InlineData("archived", true, true)]
    [InlineData("not sent and archived", false, true)]
    public void AQueryIsRefusedBeforeTheReportIsSentOrOnAnArchivedCase(string label, bool reportIsSent, bool archived)
    {
        Assert.NotEmpty(label);
        var exception = Assert.Throws<InvalidOperationException>(
            () => CaseQueryRules.RequireCanLog(reportIsSent, archived));

        Assert.Equal("A query can be logged only once the report is sent.", exception.Message);
    }

    [Fact]
    public void AQueryIsLoggedOnceTheReportIsSentOnAnArchivableCase() =>
        CaseQueryRules.RequireCanLog(reportIsSent: true, archived: false);

    [Fact]
    public async Task AnUnknownChannelIsRefused()
    {
        var store = new QueryStore(CaseLifecycleState.ReportSent, hasOpenQuery: false);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new RaiseCaseQuery(store).ExecuteAsync(Raise((CaseQueryChannel)99, "A query."), default));
    }

    [Fact]
    public async Task ResolvingReachesTheStoreWhileAQueryIsOpen()
    {
        var store = new QueryStore(CaseLifecycleState.ReportSent, hasOpenQuery: true);
        var request = Resolve();

        var resolved = await new ResolveCaseQueries(store).ExecuteAsync(request, default);

        Assert.Same(request, store.Resolved);
        Assert.False(resolved.HasOpenQuery);
    }

    [Fact]
    public async Task ResolvingAnOpenQueryIsNotTiedToReportSent()
    {
        // The query outlives Return to Engineer (operator, 11 October 2026).
        var store = new QueryStore(CaseLifecycleState.ReportPreparation, hasOpenQuery: true);

        await new ResolveCaseQueries(store).ExecuteAsync(Resolve(), default);

        Assert.NotNull(store.Resolved);
    }

    [Theory]
    [InlineData("nothing open", false, false)]
    [InlineData("archived", true, true)]
    [InlineData("nothing open and archived", false, true)]
    public void ResolvingWithNothingOpenOrOnAnArchivedCaseIsRefused(string label, bool hasOpenQuery, bool archived)
    {
        Assert.NotEmpty(label);
        var exception = Assert.Throws<InvalidOperationException>(
            () => CaseQueryRules.RequireCanResolve(hasOpenQuery, archived));

        Assert.Equal("The case has no open query to resolve.", exception.Message);
    }

    [Fact]
    public async Task NeitherActionReadsTheCaseBeforeTheStore()
    {
        var store = new QueryStore(CaseLifecycleState.ReportSent, hasOpenQuery: true);

        await new RaiseCaseQuery(store).ExecuteAsync(Raise(CaseQueryChannel.Other, "A query."), default);
        await new ResolveCaseQueries(store).ExecuteAsync(Resolve(), default);

        Assert.NotNull(store.Raised);
        Assert.NotNull(store.Resolved);
        Assert.Equal(0, store.Reads);
    }

    [Fact]
    public async Task NeitherActionNeedsAReasonButBothNeedTheLease()
    {
        var store = new QueryStore(CaseLifecycleState.ReportSent, hasOpenQuery: true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new RaiseCaseQuery(store).ExecuteAsync(
                new(CaseId, 4, Actor, "log", string.Empty, string.Empty, CaseQueryChannel.Email, null), default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ResolveCaseQueries(store).ExecuteAsync(
                new(CaseId, 4, Actor, "resolve", string.Empty, string.Empty), default));
    }

    private static RaiseCaseQueryRequest Raise(CaseQueryChannel channel, string? details) =>
        new(CaseId, 4, Actor, "log-query", string.Empty, "lease-token", channel, details);

    private static ResolveCaseQueriesRequest Resolve() =>
        new(CaseId, 4, Actor, "resolve-query", string.Empty, "lease-token");

    private sealed class QueryStore(CaseLifecycleState state, bool hasOpenQuery) : ICaseWorkflowStore
    {
        private CaseWorkflowRecord _current = CaseQueryRulesTests.Workflow(state, hasOpenQuery);

        public RaiseCaseQueryRequest? Raised { get; private set; }
        public ResolveCaseQueriesRequest? Resolved { get; private set; }

        /// <summary>Every read of the Case or of its operations a command made before the write.</summary>
        public int Reads { get; private set; }

        public Task<CaseWorkflowRecord?> GetAsync(Guid caseId, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult<CaseWorkflowRecord?>(caseId == CaseId ? _current : null);
        }

        public Task<IReadOnlyDictionary<Guid, Guid?>> GetAssignedEngineersAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasOperationAsync(Guid caseId, string operationKey, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(false);
        }

        public Task<CaseWorkflowRecord> RaiseQueryAsync(RaiseCaseQueryRequest request, CancellationToken cancellationToken)
        {
            Raised = request;
            _current = _current with { HasOpenQuery = true, Version = _current.Version + 1 };
            return Task.FromResult(_current);
        }

        public Task<CaseWorkflowRecord> ResolveQueriesAsync(ResolveCaseQueriesRequest request, CancellationToken cancellationToken)
        {
            Resolved = request;
            _current = _current with { HasOpenQuery = false, Version = _current.Version + 1 };
            return Task.FromResult(_current);
        }

        public Task<CaseWorkflowRecord> StartWorkAsync(CaseMutationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> HoldAsync(PutCaseOnHoldRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ReleaseHoldAsync(CaseMutationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ReturnToReviewAsync(ReturnCaseToReviewRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEngineerAssignment> AssignEngineerAsync(AssignCaseEngineerRequest request, CaseLifecycleState targetState, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> RecordReportApprovalAsync(RecordCaseReportApprovalRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LinkReportEvidenceResult> LinkReportEvidenceAsync(LinkReportEvidenceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> UnlinkReportEvidenceAsync(UnlinkReportEvidenceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> CloseAsync(CloseCaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ReopenAsync(ReopenCaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ReturnToEngineerAsync(ReturnCaseToEngineerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease> ClaimAsync(ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease> RenewAsync(RenewCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease> HeartbeatAsync(HeartbeatCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease?> ResumeAsync(ResumeCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ReleaseAsync(ReleaseCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
