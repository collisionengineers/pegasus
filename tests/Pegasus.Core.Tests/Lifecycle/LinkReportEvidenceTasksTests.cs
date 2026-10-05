using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

/// <summary>
/// Staff Mark report sent reports the tasks the link created from the delivery's frozen
/// after-send list (CASE-20), alongside the Case's workflow.
/// </summary>
public sealed class LinkReportEvidenceTasksTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly ActionActor Staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Fact]
    public async Task TheResultCarriesTheWorkflowAndTheTasksTheLinkCreated()
    {
        var task = new CaseTaskRecord(
            Guid.NewGuid(), CaseId, "Send the figures to the garage", null, CaseTaskState.Open, 0, 1);
        var store = new LinkStore([task]);

        var result = await new LinkReportEvidence(store).ExecuteAsync(Request(), default);

        Assert.Equal(CaseLifecycleState.PostReport, result.Workflow.State);
        Assert.Equal(task, Assert.Single(result.TasksCreated));
        Assert.Equal(Request(), store.Linked);
    }

    [Fact]
    public async Task ALinkThatCreatedNoTasksReportsNone()
    {
        var result = await new LinkReportEvidence(new LinkStore([])).ExecuteAsync(Request(), default);

        Assert.Empty(result.TasksCreated);
    }

    [Fact]
    public async Task TasksOfAnotherCaseAreRefused()
    {
        var foreign = new CaseTaskRecord(
            Guid.NewGuid(), Guid.NewGuid(), "Another Case's task", null, CaseTaskState.Open, 0, 1);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new LinkReportEvidence(new LinkStore([foreign])).ExecuteAsync(Request(), default));
    }

    private static readonly Guid EvidenceId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static LinkReportEvidenceRequest Request() =>
        new(
            CaseId,
            3,
            Staff,
            "link-sent-report",
            "Exact approved-mailbox Sent item linked",
            new string('a', CaseEditAuthority.LeaseTokenLength),
            EvidenceId);

    private static CaseWorkflowRecord Workflow(CaseLifecycleState state) =>
        new(
            CaseId,
            new(CaseId, "QDOS", 2026, 1, "QDOS260001"),
            state,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            4);

    private sealed class LinkStore(IReadOnlyList<CaseTaskRecord> tasks) : ICaseWorkflowStore
    {
        public LinkReportEvidenceRequest? Linked { get; private set; }

        public Task<CaseWorkflowRecord?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseWorkflowRecord?>(caseId == CaseId ? Workflow(CaseLifecycleState.ReportPreparation) : null);

        public Task<bool> HasOperationAsync(Guid caseId, string operationKey, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<LinkReportEvidenceResult> LinkReportEvidenceAsync(
            LinkReportEvidenceRequest request,
            CancellationToken cancellationToken)
        {
            Linked = request;
            return Task.FromResult(new LinkReportEvidenceResult(Workflow(CaseLifecycleState.PostReport), tasks));
        }

        public Task<IReadOnlyDictionary<Guid, Guid?>> GetAssignedEngineersAsync(
            IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseEditLease> ClaimAsync(ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseEditLease> RenewAsync(RenewCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseEditLease> HeartbeatAsync(HeartbeatCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseEditLease?> ResumeAsync(ResumeCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReleaseAsync(ReleaseCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ChangeStateAsync(
            CaseMutationRequest request, CaseLifecycleState targetState, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> HoldAsync(PutCaseOnHoldRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ReleaseHoldAsync(CaseMutationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ReturnToReviewAsync(ReturnCaseToReviewRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> AssignEngineerAsync(
            AssignCaseEngineerRequest request,
            Guid? signOffEngineerId,
            CaseLifecycleState targetState,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> SetSignOffEngineerAsync(
            SetCaseSignOffEngineerRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> RecordReportApprovalAsync(
            RecordCaseReportApprovalRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> UnlinkReportEvidenceAsync(
            UnlinkReportEvidenceRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> CloseAsync(CloseCaseRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ReopenAsync(ReopenCaseRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ReturnToEngineerAsync(
            ReturnCaseToEngineerRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
