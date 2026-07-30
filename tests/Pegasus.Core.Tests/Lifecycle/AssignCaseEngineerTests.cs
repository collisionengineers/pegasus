using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

public sealed class AssignCaseEngineerTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly Guid EngineerId = Guid.NewGuid();
    private static readonly ActionActor Actor =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    [Theory]
    [InlineData(false, false, false, "does not exist")]
    [InlineData(true, false, true, "is disabled")]
    [InlineData(true, true, false, "does not hold the Engineer role")]
    public async Task IneligibleStaffCannotBeAssignedOrUnlockWork(
        bool accountExists,
        bool isEnabled,
        bool hasEngineerRole,
        string expectedMessage)
    {
        var store = new RecordingWorkflowStore();
        var eligibility = new StubEligibility(
            new(accountExists, isEnabled, hasEngineerRole));
        var sut = new AssignCaseEngineer(
            store,
            new DefaultCaseWorkflowConfiguration(),
            eligibility);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ExecuteAsync(CreateAssignmentRequest(), default));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, eligibility.CallCount);
        Assert.Equal(0, store.AssignmentCount);
        Assert.Null(store.Current.AssignedEngineerId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new StartCaseWork(store, eligibility).ExecuteAsync(
                new ChangeCaseStateRequest(
                    CaseId,
                    0,
                    Actor,
                    "start-after-rejected-assignment",
                    "Attempt to start after rejected assignment",
                    "lease-token"),
                default));
    }

    [Fact]
    public async Task EnabledEngineerCanBeAssignedAndExactReplayDoesNotRecheckEligibility()
    {
        var store = new RecordingWorkflowStore();
        var eligibility = new StubEligibility(new(true, true, true));
        var sut = new AssignCaseEngineer(
            store,
            new DefaultCaseWorkflowConfiguration(),
            eligibility);
        var request = CreateAssignmentRequest();

        var assigned = await sut.ExecuteAsync(request, default);
        eligibility.Current = new(true, false, true);
        var replay = await sut.ExecuteAsync(request, default);

        Assert.Equal(EngineerId, assigned.AssignedEngineerId);
        Assert.Equal(1L, assigned.Version);
        Assert.Equal(assigned, replay);
        Assert.Equal(1, eligibility.CallCount);
        Assert.Equal(2, store.AssignmentCount);
    }

    private static AssignCaseEngineerRequest CreateAssignmentRequest() =>
        new(
            CaseId,
            0,
            Actor,
            "assign-engineer",
            "Assign eligible Engineer",
            "lease-token",
            EngineerId,
            new(true, true, true, true, "accepted-readiness"));

    private sealed class StubEligibility(CaseEngineerEligibility current)
        : ICaseEngineerEligibility
    {
        public CaseEngineerEligibility Current { get; set; } = current;

        public int CallCount { get; private set; }

        public Task<CaseEngineerEligibility> GetAsync(
            Guid staffId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(EngineerId, staffId);
            CallCount++;
            return Task.FromResult(Current);
        }
    }

    private sealed class RecordingWorkflowStore : ICaseWorkflowStore
    {
        private string? _appliedOperationKey;
        private AssignCaseEngineerRequest? _appliedRequest;

        public CaseWorkflowRecord Current { get; private set; } = new(
            CaseId,
            new(CaseId, "QDOS", 2026, 1, "QDOS260001"),
            CaseLifecycleState.Review,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0);

        public int AssignmentCount { get; private set; }

        public Task<CaseWorkflowRecord?> GetAsync(
            Guid caseId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<CaseWorkflowRecord?>(caseId == CaseId ? Current : null);
        }

        public Task<bool> HasOperationAsync(
            Guid caseId,
            string operationKey,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                caseId == CaseId
                && string.Equals(_appliedOperationKey, operationKey, StringComparison.Ordinal));
        }

        public Task<CaseWorkflowRecord> AssignEngineerAsync(
            AssignCaseEngineerRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AssignmentCount++;
            if (_appliedOperationKey is not null)
            {
                if (_appliedRequest != request)
                {
                    throw new InvalidOperationException("The operation key was reused for different input.");
                }

                return Task.FromResult(Current);
            }

            _appliedOperationKey = request.OperationKey;
            _appliedRequest = request;
            Current = Current with
            {
                AssignedEngineerId = request.EngineerId,
                Version = Current.Version + 1
            };
            return Task.FromResult(Current);
        }

        public Task<CaseEditLease> ClaimAsync(
            ClaimCaseEditLeaseRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseEditLease> RenewAsync(
            RenewCaseEditLeaseRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReleaseAsync(
            ReleaseCaseEditLeaseRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ChangeStateAsync(
            CaseMutationRequest request,
            CaseLifecycleState targetState,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> HoldAsync(
            PutCaseOnHoldRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ReleaseHoldAsync(
            CaseMutationRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ReturnToReviewAsync(
            ReturnCaseToReviewRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> RecordReportApprovalAsync(
            RecordCaseReportApprovalRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> LinkReportEvidenceAsync(
            LinkReportEvidenceRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> UnlinkReportEvidenceAsync(
            UnlinkReportEvidenceRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> CloseAsync(
            CloseCaseRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseWorkflowRecord> ReopenAsync(
            ReopenCaseRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
