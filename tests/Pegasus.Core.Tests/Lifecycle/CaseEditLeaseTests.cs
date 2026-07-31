using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

public sealed class CaseEditLeaseTests
{
    [Fact]
    public async Task AcquireNormalizesOperationKeyWithoutChangingClaimFingerprintMaterial()
    {
        var actor = ActionActor.Staff(
            Guid.NewGuid(),
            [StaffRole.Administrator, StaffRole.Engineer]);
        var request = new ClaimCaseEditLeaseRequest(
            Guid.NewGuid(),
            7,
            actor,
            "  exact-lease-claim  ");
        var expected = new CaseEditLease(
            request.CaseId,
            "opaque-token",
            actor.SubjectId,
            request.ExpectedVersion,
            new DateTimeOffset(2031, 2, 3, 4, 5, 0, TimeSpan.Zero));
        var store = new RecordingLeaseStore(expected);

        var result = await new AcquireCaseEditLease(store).ExecuteAsync(request, default);

        Assert.Equal(expected, result);
        var forwarded = Assert.IsType<ClaimCaseEditLeaseRequest>(store.ClaimRequest);
        Assert.Equal(request.CaseId, forwarded.CaseId);
        Assert.Equal(request.ExpectedVersion, forwarded.ExpectedVersion);
        Assert.Same(actor, forwarded.Actor);
        Assert.Equal("exact-lease-claim", forwarded.OperationKey);
    }

    [Fact]
    public async Task RenewAndReleaseNormalizeTheirIndependentOperationKeys()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = new CaseEditLease(
            Guid.NewGuid(),
            "opaque-token",
            actor.SubjectId,
            3,
            new DateTimeOffset(2031, 2, 3, 4, 5, 0, TimeSpan.Zero));
        var store = new RecordingLeaseStore(lease);
        var renewRequest = new RenewCaseEditLeaseRequest(
            lease.CaseId,
            lease.Version,
            actor,
            "  renew-lease  ",
            lease.Token);
        var releaseRequest = new ReleaseCaseEditLeaseRequest(
            lease.CaseId,
            actor,
            "  release-lease  ",
            lease.Token);

        var renewed = await new RenewCaseEditLease(store).ExecuteAsync(
            renewRequest,
            default);
        await new ReleaseCaseEditLease(store).ExecuteAsync(
            releaseRequest,
            default);

        Assert.Equal(lease, renewed);
        Assert.Equal("renew-lease", store.RenewRequest?.OperationKey);
        Assert.Same(actor, store.RenewRequest?.Actor);
        Assert.Equal(lease.Token, store.RenewRequest?.LeaseToken);
        Assert.Equal("release-lease", store.ReleaseRequest?.OperationKey);
        Assert.Same(actor, store.ReleaseRequest?.Actor);
        Assert.Equal(lease.Token, store.ReleaseRequest?.LeaseToken);
    }

    [Fact]
    public async Task AcquireRejectsUnauthorizedPrincipalBeforeStoreAccess()
    {
        var store = new RecordingLeaseStore(
            new(Guid.NewGuid(), "unused", "unused", 0, DateTimeOffset.MaxValue));
        var request = new ClaimCaseEditLeaseRequest(
            Guid.NewGuid(),
            0,
            ActionActor.SystemWorker("case-worker"),
            "claim-lease");

        var exception = await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => new AcquireCaseEditLease(store).ExecuteAsync(request, default));

        Assert.Equal(StaffAccessRight.PerformCasework, exception.Permission);
        Assert.Null(store.ClaimRequest);
    }

    private sealed class RecordingLeaseStore(CaseEditLease claimResult) : ILeaseCaseForEdit
    {
        public ClaimCaseEditLeaseRequest? ClaimRequest { get; private set; }
        public RenewCaseEditLeaseRequest? RenewRequest { get; private set; }
        public ReleaseCaseEditLeaseRequest? ReleaseRequest { get; private set; }

        public Task<CaseEditLease> ClaimAsync(
            ClaimCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClaimRequest = request;
            return Task.FromResult(claimResult);
        }

        public Task<CaseEditLease> RenewAsync(
            RenewCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RenewRequest = request;
            return Task.FromResult(claimResult);
        }

        public Task ReleaseAsync(
            ReleaseCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReleaseRequest = request;
            return Task.CompletedTask;
        }
    }
}
