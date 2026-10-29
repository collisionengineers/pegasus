using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

public sealed class CaseEditLeaseTests
{
    [Fact]
    public async Task ClaimNormalizesOperationKeyWithoutChangingClaimFingerprintMaterial()
    {
        var actor = ActionActor.Staff(
            Guid.NewGuid(),
            [StaffRole.Administrator]);
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

        var result = await new CaseEditLeases(store).ClaimAsync(request, default);

        Assert.Equal(expected, result);
        var forwarded = Assert.IsType<ClaimCaseEditLeaseRequest>(store.ClaimRequest);
        Assert.Equal(request.CaseId, forwarded.CaseId);
        Assert.Equal(request.ExpectedVersion, forwarded.ExpectedVersion);
        Assert.Same(actor, forwarded.Actor);
        Assert.Equal("exact-lease-claim", forwarded.OperationKey);
    }

    [Fact]
    public async Task ReleaseNormalizesItsOperationKey()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = new CaseEditLease(
            Guid.NewGuid(),
            "opaque-token",
            actor.SubjectId,
            3,
            new DateTimeOffset(2031, 2, 3, 4, 5, 0, TimeSpan.Zero));
        var store = new RecordingLeaseStore(lease);
        var releaseRequest = new ReleaseCaseEditLeaseRequest(
            lease.CaseId,
            actor,
            "  release-lease  ",
            lease.Token);

        await new CaseEditLeases(store).ReleaseAsync(
            releaseRequest,
            default);

        Assert.Equal("release-lease", store.ReleaseRequest?.OperationKey);
        Assert.Same(actor, store.ReleaseRequest?.Actor);
        Assert.Equal(lease.Token, store.ReleaseRequest?.LeaseToken);
    }

    [Fact]
    public async Task ClaimRejectsUnauthorizedPrincipalBeforeStoreAccess()
    {
        var store = new RecordingLeaseStore(
            new(Guid.NewGuid(), "unused", "unused", 0, DateTimeOffset.MaxValue));
        var request = new ClaimCaseEditLeaseRequest(
            Guid.NewGuid(),
            0,
            ActionActor.SystemWorker("case-worker"),
            "claim-lease");

        var exception = await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => new CaseEditLeases(store).ClaimAsync(request, default));

        Assert.Equal(StaffAccessRight.PerformCasework, exception.Permission);
        Assert.Null(store.ClaimRequest);
    }

    [Fact]
    public async Task ReleaseRejectsATokenLongerThanOneCanEverBeIssued()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var caseId = Guid.NewGuid();
        var overLengthToken = new string('a', CaseEditAuthority.LeaseTokenLength + 1);
        var store = new RecordingLeaseStore(
            new(caseId, "unused", actor.SubjectId, 0, DateTimeOffset.MaxValue));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new CaseEditLeases(store).ReleaseAsync(
                new(caseId, actor, "release-lease", overLengthToken),
                default));

        Assert.Null(store.ReleaseRequest);
    }

    /// <summary>
    /// A heartbeat carries no operation key and no expected version, because it records nothing
    /// and cannot conflict with the holder's own saves. It still proves the token, and still needs
    /// casework authority, exactly as a claim does.
    /// </summary>
    [Fact]
    public async Task HeartbeatForwardsOnlyTheCaseActorAndToken()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var lease = new CaseEditLease(
            Guid.NewGuid(),
            new string('b', CaseEditAuthority.LeaseTokenLength),
            actor.SubjectId,
            9,
            new DateTimeOffset(2031, 2, 3, 4, 5, 0, TimeSpan.Zero));
        var store = new RecordingLeaseStore(lease);

        var result = await new CaseEditLeases(store).HeartbeatAsync(
            new(lease.CaseId, actor, lease.Token),
            default);

        Assert.Equal(lease, result);
        var forwarded = Assert.IsType<HeartbeatCaseEditLeaseRequest>(store.HeartbeatRequest);
        Assert.Equal(lease.CaseId, forwarded.CaseId);
        Assert.Same(actor, forwarded.Actor);
        Assert.Equal(lease.Token, forwarded.LeaseToken);
    }

    [Fact]
    public async Task HeartbeatRejectsAnUnauthorizedPrincipalAndAnUnissuableTokenBeforeStoreAccess()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingLeaseStore(
            new(caseId, "unused", "unused", 0, DateTimeOffset.MaxValue));
        var leases = new CaseEditLeases(store);

        var refusal = await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            leases.HeartbeatAsync(
                new(caseId, ActionActor.SystemWorker("case-worker"), "token"),
                default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            leases.HeartbeatAsync(
                new(
                    caseId,
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
                    new string('a', CaseEditAuthority.LeaseTokenLength + 1)),
                default));

        Assert.Equal(StaffAccessRight.PerformCasework, refusal.Permission);
        Assert.Null(store.HeartbeatRequest);
    }

    [Fact]
    public void MutationValidationRejectsATokenLongerThanOneCanEverBeIssued()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var request = new ChangeCaseStateRequest(
            Guid.NewGuid(),
            0,
            actor,
            "change-state",
            "A settled reason",
            new string('a', CaseEditAuthority.LeaseTokenLength + 1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => CaseLifecycleRules.ValidateMutation(request));

        CaseLifecycleRules.ValidateMutation(
            request with
            {
                EditLeaseToken = new string('a', CaseEditAuthority.LeaseTokenLength)
            });
    }

    [Fact]
    public async Task ResumeHandsBackTheStoresAnswerForTheCallersOwnLease()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = new CaseEditLease(
            Guid.NewGuid(),
            new string('b', CaseEditAuthority.LeaseTokenLength),
            actor.SubjectId,
            5,
            new DateTimeOffset(2031, 2, 3, 4, 5, 0, TimeSpan.Zero));
        var store = new RecordingLeaseStore(lease);
        var request = new ResumeCaseEditLeaseRequest(lease.CaseId, actor);

        var result = await new CaseEditLeases(store).ResumeAsync(request, default);

        Assert.Equal(lease, result);
        Assert.Same(request, store.ResumeRequest);
    }

    [Fact]
    public async Task ResumeRequiresCaseworkPermissionAndACaseBeforeTheStoreIsAsked()
    {
        var store = new RecordingLeaseStore(new CaseEditLease(
            Guid.NewGuid(),
            new string('b', CaseEditAuthority.LeaseTokenLength),
            "holder",
            5,
            new DateTimeOffset(2031, 2, 3, 4, 5, 0, TimeSpan.Zero)));
        var leases = new CaseEditLeases(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => leases.ResumeAsync(
            new ResumeCaseEditLeaseRequest(Guid.NewGuid(), ActionActor.SystemWorker("case-worker")),
            default));
        await Assert.ThrowsAsync<ArgumentException>(() => leases.ResumeAsync(
            new ResumeCaseEditLeaseRequest(
                Guid.Empty,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.User])),
            default));
        Assert.Null(store.ResumeRequest);
    }

    private sealed class RecordingLeaseStore(CaseEditLease claimResult) : ILeaseCaseForEdit
    {
        public ClaimCaseEditLeaseRequest? ClaimRequest { get; private set; }
        public HeartbeatCaseEditLeaseRequest? HeartbeatRequest { get; private set; }
        public ResumeCaseEditLeaseRequest? ResumeRequest { get; private set; }
        public ReleaseCaseEditLeaseRequest? ReleaseRequest { get; private set; }

        public Task<CaseEditLease> ClaimAsync(
            ClaimCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClaimRequest = request;
            return Task.FromResult(claimResult);
        }

        public Task<CaseEditLease> HeartbeatAsync(
            HeartbeatCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HeartbeatRequest = request;
            return Task.FromResult(claimResult);
        }

        public Task<CaseEditLease?> ResumeAsync(
            ResumeCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResumeRequest = request;
            return Task.FromResult<CaseEditLease?>(claimResult);
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
