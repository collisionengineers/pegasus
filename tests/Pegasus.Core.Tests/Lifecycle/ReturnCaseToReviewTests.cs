using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

/// <summary>
/// Return to Review runs only from With Engineer and Report sent (FRD-13). A
/// Not ready Case reaches Review by itself when every required item is present.
/// </summary>
public sealed class ReturnCaseToReviewTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly ActionActor Actor =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Theory]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    [InlineData(CaseLifecycleState.ReportSent)]
    public async Task ReturnToReviewIsAcceptedFromWithEngineerAndReportSent(CaseLifecycleState state)
    {
        var store = new ReviewStore(state);

        var returned = await new ReturnCaseToReview(store).ExecuteAsync(Request(), default);

        Assert.NotNull(store.Returned);
        Assert.Equal(CaseLifecycleState.Review, returned.State);
    }

    [Theory]
    [InlineData(CaseLifecycleState.NotReady)]
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.Held)]
    [InlineData(CaseLifecycleState.PrincipalCancelled)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected)]
    [InlineData(CaseLifecycleState.CreatedInError)]
    [InlineData(CaseLifecycleState.SourceEmailUnlinked)]
    public async Task ReturnToReviewIsRefusedElsewhere(CaseLifecycleState state)
    {
        var store = new ReviewStore(state);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ReturnCaseToReview(store).ExecuteAsync(Request(), default));

        Assert.Null(store.Returned);
    }

    [Fact]
    public async Task ReplayOfAnAppliedReturnIsNotRefused()
    {
        var store = new ReviewStore(CaseLifecycleState.Review, replay: true);

        await new ReturnCaseToReview(store).ExecuteAsync(Request(), default);

        Assert.NotNull(store.Returned);
    }

    private static ReturnCaseToReviewRequest Request() =>
        new(CaseId, 4, Actor, "return-to-review", "Back to Review", "lease-token");

    private sealed class ReviewStore(CaseLifecycleState state, bool replay = false) : ICaseWorkflowStore
    {
        private CaseWorkflowRecord _current = CaseQueryRulesTests.Workflow(state, hasOpenQuery: false);

        public ReturnCaseToReviewRequest? Returned { get; private set; }

        public Task<CaseWorkflowRecord?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseWorkflowRecord?>(caseId == CaseId ? _current : null);

        public Task<bool> HasOperationAsync(Guid caseId, string operationKey, CancellationToken cancellationToken) =>
            Task.FromResult(replay);

        public Task<CaseWorkflowRecord> ReturnToReviewAsync(ReturnCaseToReviewRequest request, CancellationToken cancellationToken)
        {
            Returned = request;
            _current = _current with { State = CaseLifecycleState.Review, Version = _current.Version + 1 };
            return Task.FromResult(_current);
        }

        public Task<IReadOnlyDictionary<Guid, Guid?>> GetAssignedEngineersAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ChangeStateAsync(CaseMutationRequest request, CaseLifecycleState targetState, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> HoldAsync(PutCaseOnHoldRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ReleaseHoldAsync(CaseMutationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> AssignEngineerAsync(AssignCaseEngineerRequest request, Guid? signOffEngineerId, CaseLifecycleState targetState, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> RecordReportApprovalAsync(RecordCaseReportApprovalRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LinkReportEvidenceResult> LinkReportEvidenceAsync(LinkReportEvidenceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> UnlinkReportEvidenceAsync(UnlinkReportEvidenceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> CloseAsync(CloseCaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ReopenAsync(ReopenCaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ReturnToEngineerAsync(ReturnCaseToEngineerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> RaiseQueryAsync(RaiseCaseQueryRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ResolveQueriesAsync(ResolveCaseQueriesRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease> ClaimAsync(ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease> RenewAsync(RenewCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease> HeartbeatAsync(HeartbeatCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease?> ResumeAsync(ResumeCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ReleaseAsync(ReleaseCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
