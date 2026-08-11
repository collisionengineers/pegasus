using Pegasus.Core.Custody;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Custody;

public sealed class CustodyRecoveryPolicyTests
{
    [Fact]
    public async Task StaffRetryRequiresHumanActorReasonLeaseRenderedWorkflowVersionAndIdempotency()
    {
        var store = new RecordingStore();
        var useCase = new RetryCaseCustody(store);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var valid = new RetryCaseCustodyRequest(
            Guid.NewGuid(), 7, actor, "retry-1", "Operator verified the provider is available.",
            "lease-1", CustodyTargetKind.CaseSource);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            useCase.ExecuteAsync(valid with { Actor = ActionActor.Automation("automation") }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(valid with { Reason = " " }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(valid with { EditLeaseToken = " " }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(valid with { OperationKey = " " }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(valid with { ExpectedCaseVersion = -1 }));

        var result = await useCase.ExecuteAsync(valid);

        Assert.Equal(RetryCaseCustodyOutcome.Pending, result.Outcome);
        Assert.Equal(valid, store.Request);
        Assert.Equal(valid.Reason, store.Reason);
        Assert.Matches("^[0-9a-f]{64}$", store.RequestHash);
    }

    private sealed class RecordingStore : ICustodyRecoveryStore
    {
        public RetryCaseCustodyRequest? Request { get; private set; }
        public string? Reason { get; private set; }
        public string RequestHash { get; private set; } = string.Empty;

        public Task<RetryCaseCustodyResult> RetryAsync(
            RetryCaseCustodyRequest request,
            string normalizedReason,
            string requestHash,
            CancellationToken cancellationToken)
        {
            Request = request;
            Reason = normalizedReason;
            RequestHash = requestHash;
            return Task.FromResult(new RetryCaseCustodyResult(
                RetryCaseCustodyOutcome.Pending, request.ExpectedCaseVersion + 1, "Custody retry queued."));
        }
    }
}
