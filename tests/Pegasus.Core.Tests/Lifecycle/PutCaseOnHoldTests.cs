using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

public sealed class PutCaseOnHoldTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly ActionActor Actor =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    // 23:30 UTC on 13 September is already 00:30 on 14 September in London
    // (British Summer Time), so the office's today is the 14th.
    private static readonly DateTimeOffset Now =
        new(2026, 9, 13, 23, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task HoldWithoutReviewDateReachesTheStoreUnchanged()
    {
        var store = new HoldStore();
        var sut = new PutCaseOnHold(store, new FixedTimeProvider(Now));

        var held = await sut.ExecuteAsync(Request(reviewOn: null), default);

        Assert.Equal(CaseLifecycleState.Held, held.State);
        Assert.Null(store.Held!.ReviewOn);
        Assert.Null(held.HoldReviewOn);
    }

    [Fact]
    public async Task HoldKeepsAReviewDateOnOrAfterTheLondonToday()
    {
        var store = new HoldStore();
        var sut = new PutCaseOnHold(store, new FixedTimeProvider(Now));
        var today = new DateOnly(2026, 9, 14);

        var held = await sut.ExecuteAsync(Request(today), default);

        Assert.Equal(today, store.Held!.ReviewOn);
        Assert.Equal(today, held.HoldReviewOn);
        Assert.Equal(Now, held.HeldAtUtc);
    }

    [Fact]
    public async Task HoldRefusesAReviewDateBeforeTheLondonToday()
    {
        var store = new HoldStore();
        var sut = new PutCaseOnHold(store, new FixedTimeProvider(Now));

        // 13 September is still today in UTC but yesterday in London.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => sut.ExecuteAsync(Request(new DateOnly(2026, 9, 13)), default));

        Assert.Null(store.Held);
    }

    [Fact]
    public async Task ReleasingTheHoldClearsTheReviewDate()
    {
        var store = new HoldStore();
        var hold = new PutCaseOnHold(store, new FixedTimeProvider(Now));
        await hold.ExecuteAsync(Request(new DateOnly(2026, 9, 24)), default);

        var released = await new ReleaseCaseHold(store).ExecuteAsync(
            new ChangeCaseStateRequest(CaseId, 1, Actor, "release", "Back to work", "lease-token"),
            default);

        Assert.Equal(CaseLifecycleState.Review, released.State);
        Assert.Null(released.HoldReviewOn);
        Assert.Null(released.HeldAtUtc);
    }

    private static PutCaseOnHoldRequest Request(DateOnly? reviewOn) =>
        new(CaseId, 0, Actor, "hold", "Awaiting the claimant", "lease-token", reviewOn);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class HoldStore : ICaseWorkflowStore
    {
        public PutCaseOnHoldRequest? Held { get; private set; }

        private CaseWorkflowRecord _current = new(
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

        public Task<CaseWorkflowRecord?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseWorkflowRecord?>(caseId == CaseId ? _current : null);

        public Task<bool> HasOperationAsync(Guid caseId, string operationKey, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<CaseWorkflowRecord> HoldAsync(PutCaseOnHoldRequest request, CancellationToken cancellationToken)
        {
            Held = request;
            _current = _current with
            {
                State = CaseLifecycleState.Held,
                HeldAtUtc = Now,
                HoldReviewOn = request.ReviewOn,
                Version = _current.Version + 1
            };
            return Task.FromResult(_current);
        }

        public Task<CaseWorkflowRecord> ReleaseHoldAsync(CaseMutationRequest request, CancellationToken cancellationToken)
        {
            _current = _current with
            {
                State = CaseLifecycleState.Review,
                HeldAtUtc = null,
                HoldReviewOn = null,
                Version = _current.Version + 1
            };
            return Task.FromResult(_current);
        }

        public Task<CaseWorkflowRecord> AssignEngineerAsync(AssignCaseEngineerRequest request, Guid? signOffEngineerId, CaseLifecycleState targetState, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> SetSignOffEngineerAsync(SetCaseSignOffEngineerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease> ClaimAsync(ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease> RenewAsync(RenewCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseEditLease> HeartbeatAsync(HeartbeatCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ReleaseAsync(ReleaseCaseEditLeaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ChangeStateAsync(CaseMutationRequest request, CaseLifecycleState targetState, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ReturnToReviewAsync(ReturnCaseToReviewRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> RecordReportApprovalAsync(RecordCaseReportApprovalRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> LinkReportEvidenceAsync(LinkReportEvidenceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> UnlinkReportEvidenceAsync(UnlinkReportEvidenceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> CloseAsync(CloseCaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ReopenAsync(ReopenCaseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseWorkflowRecord> ReturnToEngineerAsync(ReturnCaseToEngineerRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
