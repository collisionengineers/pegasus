using Pegasus.Core.Custody;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Custody;

public sealed class CustodyRecoveryPolicyTests
{
    [Fact]
    public async Task StaffRetryRequiresCaseworkActorReasonLeaseRenderedWorkflowVersionAndIdempotency()
    {
        var store = new RecordingStore();
        var useCase = new RetryCaseCustody(store);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var valid = new RetryCaseCustodyRequest(
            Guid.NewGuid(), 7, actor, "retry-1", "Operator verified the provider is available.",
            "lease-1", CustodyTargetKind.CaseSource);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            useCase.ExecuteAsync(valid with { Actor = ActionActor.SystemWorker("worker") }));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            useCase.ExecuteAsync(valid with { Actor = ActionActor.Principal(Guid.NewGuid()) }));
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

    /// <summary>
    /// The Automation Actor holds the casework right and retries custody as a
    /// member of staff does (ADR-0064); its request is hashed with its own kind.
    /// </summary>
    [Fact]
    public async Task AutomationActorRetriesCustodyWithItsOwnAttribution()
    {
        var store = new RecordingStore();
        var useCase = new RetryCaseCustody(store);
        var automation = new RetryCaseCustodyRequest(
            Guid.NewGuid(), 7, ActionActor.Automation("automation"), "retry-1",
            "Box is reachable again.", "lease-1", CustodyTargetKind.AuditReference);

        var result = await useCase.ExecuteAsync(automation);

        Assert.Equal(RetryCaseCustodyOutcome.Pending, result.Outcome);
        Assert.Equal(ActorKind.Automation, store.Request!.Actor.Kind);
        var automationHash = store.RequestHash;
        await useCase.ExecuteAsync(automation with
        {
            Actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
        });
        Assert.NotEqual(automationHash, store.RequestHash);
    }

    private sealed class RecordingStore : ICustodyRecoveryPersistence
    {
        public RetryCaseCustodyRequest? Request { get; private set; }
        public string? Reason { get; private set; }
        public string RequestHash { get; private set; } = string.Empty;

        public Task<RetryCaseCustodyResult> RetryAsync(
            RetryCaseCustodyRequest request,
            string normalizedReason,
            string requestHash,
            CustodyRetryPolicyAuthority policy,
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
