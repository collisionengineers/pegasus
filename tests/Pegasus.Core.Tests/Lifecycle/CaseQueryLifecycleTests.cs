using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

/// <summary>
/// Query acts take no lease, expected version or reason. Open returns the
/// query id. Exactly one of a message and a channel.
/// </summary>
public sealed class CaseQueryLifecycleTests
{
    private static readonly Guid CaseId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly ActionActor Actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

    [Fact]
    public async Task OpenReturnsTheQueryIdTheStoreRecorded()
    {
        var store = new QueryStore();
        var request = new OpenCaseQueryRequest(CaseId, Actor, "open-phone", null, CaseQueryChannel.Phone, "Called.");
        var id = await new OpenCaseQuery(store).ExecuteAsync(request, default);
        Assert.Equal(store.OpenedId, id);
        Assert.Equal(request, store.Opened);
    }

    [Fact]
    public async Task MarkDoneReopenAndAssignReachTheStoreWithoutALease()
    {
        var store = new QueryStore();
        var queryId = Guid.NewGuid();
        var act = new CaseQueryActRequest(queryId, Actor, "act");
        await new MarkCaseQueryDone(store).ExecuteAsync(act, default);
        await new ReopenCaseQuery(store).ExecuteAsync(act, default);
        var assign = new AssignCaseQueryRequest(queryId, Actor, "assign", null);
        await new AssignCaseQuery(store).ExecuteAsync(assign, default);
        Assert.Equal(act, store.Marked);
        Assert.Equal(act, store.Reopened);
        Assert.Equal(assign, store.Assigned);
    }

    [Theory]
    [InlineData("archived", true)]
    [InlineData("open", false)]
    public void RequireCanOpenRefusesUntilTheReportIsSentAndTheCaseIsNotArchived(string name, bool archived)
    {
        Assert.NotEmpty(name);
        Assert.Throws<InvalidOperationException>(() => CaseQueryRules.RequireCanOpen(reportIsSent: false, archived));
        if (archived)
        {
            Assert.Throws<InvalidOperationException>(() => CaseQueryRules.RequireCanOpen(reportIsSent: true, archived: true));
        }
        else
        {
            CaseQueryRules.RequireCanOpen(reportIsSent: true, archived: false);
        }
    }

    [Fact]
    public void AnUnknownChannelIsRefused()
    {
        var store = new QueryStore();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OpenCaseQuery(store).ExecuteAsync(
                new(CaseId, Actor, "bad-channel", null, (CaseQueryChannel)99, "A query."),
                default).GetAwaiter().GetResult());
        Assert.Null(store.Opened);
    }

    private sealed class QueryStore : ICaseWorkflowStore
    {
        public Guid OpenedId { get; } = Guid.NewGuid();
        public OpenCaseQueryRequest? Opened { get; private set; }
        public CaseQueryActRequest? Marked { get; private set; }
        public CaseQueryActRequest? Reopened { get; private set; }
        public AssignCaseQueryRequest? Assigned { get; private set; }

        public Task<Guid> OpenQueryAsync(OpenCaseQueryRequest request, CancellationToken cancellationToken)
        {
            Opened = request;
            return Task.FromResult(OpenedId);
        }

        public Task<CaseWorkflowRecord> MarkQueryDoneAsync(CaseQueryActRequest request, CancellationToken cancellationToken)
        {
            Marked = request;
            return Task.FromResult(Record());
        }

        public Task<CaseWorkflowRecord> ReopenQueryAsync(CaseQueryActRequest request, CancellationToken cancellationToken)
        {
            Reopened = request;
            return Task.FromResult(Record());
        }

        public Task<CaseWorkflowRecord> AssignQueryAsync(AssignCaseQueryRequest request, CancellationToken cancellationToken)
        {
            Assigned = request;
            return Task.FromResult(Record());
        }

        private static CaseWorkflowRecord Record() => new(
            CaseId, new(CaseId, "QDOS", 2026, 1, "QDOS260001"), CaseLifecycleState.ReportSent,
            null, null, null, null, null, null, null, 1);

        public Task<CaseWorkflowRecord?> GetAsync(Guid caseId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, Guid?>> GetAssignedEngineersAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> HasOperationAsync(Guid caseId, string operationKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease> ClaimAsync(ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease> HeartbeatAsync(HeartbeatCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease?> ResumeAsync(ResumeCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ReleaseAsync(ReleaseCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
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
    }
}
