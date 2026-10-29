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
    public void ReturnToReviewIsAcceptedFromWithEngineerAndReportSent(CaseLifecycleState state) =>
        CaseLifecycleRules.RequireCanReturnToReview(state);

    [Theory]
    [InlineData(CaseLifecycleState.NotReady)]
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.Held)]
    [InlineData(CaseLifecycleState.PrincipalCancelled)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected)]
    [InlineData(CaseLifecycleState.CreatedInError)]
    [InlineData(CaseLifecycleState.SourceEmailUnlinked)]
    public void ReturnToReviewIsRefusedElsewhere(CaseLifecycleState state)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CaseLifecycleRules.RequireCanReturnToReview(state));

        Assert.Equal("A case can enter Review only from With Engineer or Report sent.", exception.Message);
    }

    /// <summary>
    /// The state refusal and the replay exemption belong to the transition store, which asks
    /// them of the workflow it read under its lock: the command reads no Case of its own
    /// (eff-07-l, register A3-06).
    /// </summary>
    [Fact]
    public async Task ReturnToReviewReachesTheStoreWithoutReadingTheCaseFirst()
    {
        var store = new ReviewStore();

        var returned = await new ReturnCaseToReview(store).ExecuteAsync(Request(), default);

        Assert.NotNull(store.Returned);
        Assert.Equal(CaseLifecycleState.Review, returned.State);
        Assert.Equal(0, store.Reads);
    }

    private static ReturnCaseToReviewRequest Request() =>
        new(CaseId, 4, Actor, "return-to-review", "Back to Review", "lease-token");

    private sealed class ReviewStore : ICaseWorkflowStore
    {
        private CaseWorkflowRecord _current =
            CaseQueryRulesTests.Workflow(CaseLifecycleState.ReportPreparation, hasOpenQuery: false);

        public ReturnCaseToReviewRequest? Returned { get; private set; }

        /// <summary>Every read of the Case or of its operations a command made before the write.</summary>
        public int Reads { get; private set; }

        public Task<CaseWorkflowRecord?> GetAsync(Guid caseId, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult<CaseWorkflowRecord?>(caseId == CaseId ? _current : null);
        }

        public Task<bool> HasOperationAsync(Guid caseId, string operationKey, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(false);
        }

        public Task<CaseWorkflowRecord> ReturnToReviewAsync(ReturnCaseToReviewRequest request, CancellationToken cancellationToken)
        {
            Returned = request;
            _current = _current with { State = CaseLifecycleState.Review, Version = _current.Version + 1 };
            return Task.FromResult(_current);
        }

        public Task<IReadOnlyDictionary<Guid, Guid?>> GetAssignedEngineersAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> StartWorkAsync(CaseMutationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> HoldAsync(PutCaseOnHoldRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ReleaseHoldAsync(CaseMutationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEngineerAssignment> AssignEngineerAsync(AssignCaseEngineerRequest request, CaseLifecycleState targetState, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> RecordReportApprovalAsync(RecordCaseReportApprovalRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LinkReportEvidenceResult> LinkReportEvidenceAsync(LinkReportEvidenceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> UnlinkReportEvidenceAsync(UnlinkReportEvidenceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> CloseAsync(CloseCaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ReopenAsync(ReopenCaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ReturnToEngineerAsync(ReturnCaseToEngineerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid> OpenQueryAsync(OpenCaseQueryRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> MarkQueryDoneAsync(CaseQueryActRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ReopenQueryAsync(CaseQueryActRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> AssignQueryAsync(AssignCaseQueryRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease> ClaimAsync(ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease> HeartbeatAsync(HeartbeatCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease?> ResumeAsync(ResumeCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ReleaseAsync(ReleaseCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
