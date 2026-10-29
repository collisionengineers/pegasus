using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Workflow;

/// <summary>
/// One command's edit authority (FRD-14): a Triage Case's is its Triage edit scope, any other
/// Case's is its edit lease, and the release goes back through the port that issued it.
/// </summary>
public sealed class OneCommandEditAuthorityTests
{
    private static readonly ActionActor Actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Fact]
    public async Task ATriageCaseClaimsAndReleasesItsTriageEditScope()
    {
        var caseId = Guid.NewGuid();
        var leases = new RecordingLeases();
        var scopes = new RecordingScopes(new(EditScopeKind.Triage, caseId, "scope-token", "holder", 4, DateTimeOffset.MaxValue));
        var authority = new OneCommandEditAuthority(new CaseEditLeases(leases), scopes);

        var claim = await authority.ClaimAsync(caseId, isTriageCase: true, 4, Actor, "triage-write-claim:k", default);
        await authority.ReleaseAsync(claim, Actor, "triage-write-claim:k:release", default);

        Assert.Equal(new OneCommandEditClaim(caseId, true, 4, "scope-token", "triage-write-claim:k"), claim);
        var claimed = Assert.Single(scopes.Claims);
        Assert.Equal((EditScopeKind.Triage, caseId, 4L), (claimed.ScopeKind, claimed.RecordId, claimed.ExpectedVersion));
        var released = Assert.Single(scopes.Releases);
        Assert.Equal(("scope-token", "triage-write-claim:k:release"), (released.LeaseToken, released.OperationKey));
        Assert.Empty(leases.Claims);
        Assert.Empty(leases.Releases);
    }

    [Fact]
    public async Task AnyOtherCaseClaimsAndReleasesItsEditLeaseAtTheVersionTheClaimAnswers()
    {
        var caseId = Guid.NewGuid();
        var leases = new RecordingLeases(new CaseEditLease(caseId, "lease-token", "holder", 9, DateTimeOffset.MaxValue));
        var scopes = new RecordingScopes(null);
        var authority = new OneCommandEditAuthority(new CaseEditLeases(leases), scopes);

        var claim = await authority.ClaimAsync(caseId, isTriageCase: false, 7, Actor, "claim-key", default);
        await authority.ReleaseAsync(claim, Actor, "release-key", default);

        Assert.Equal(new OneCommandEditClaim(caseId, false, 9, "lease-token", "claim-key"), claim);
        Assert.Equal(7, Assert.Single(leases.Claims).ExpectedVersion);
        var released = Assert.Single(leases.Releases);
        Assert.Equal(("lease-token", "release-key"), (released.LeaseToken, released.OperationKey));
        Assert.Empty(scopes.Claims);
        Assert.Empty(scopes.Releases);
    }

    [Fact]
    public void ATriageSaveIsRefusedAsChangedWhenTheRecordMovedPastThePostedVersion()
    {
        var caseId = Guid.NewGuid();

        var refused = Assert.Throws<EditScopeVersionConflictException>(() =>
            OneCommandEditAuthority.RequireTriageSaveAtPostedVersion(
                new OneCommandEditClaim(caseId, true, 5, "scope-token", "k"), 4));

        Assert.Equal((4L, 5L), (refused.ExpectedVersion, refused.ActualVersion));
        OneCommandEditAuthority.RequireTriageSaveAtPostedVersion(
            new OneCommandEditClaim(caseId, true, 4, "scope-token", "k"), 4);
        OneCommandEditAuthority.RequireTriageSaveAtPostedVersion(
            new OneCommandEditClaim(caseId, false, 5, "lease-token", "k"), 4);
    }

    [Fact]
    public void AReleaseRefusedBecauseAStaffWriteEndedTheAuthorityIsNotAFailure()
    {
        var caseId = Guid.NewGuid();
        var automation = ActionActor.Automation(Guid.NewGuid().ToString("N"));

        Assert.True(OneCommandEditAuthority.WriteEndedIt(Actor, new CaseEditLeaseExpiredException(caseId, 3)));
        Assert.True(OneCommandEditAuthority.WriteEndedIt(Actor, new EditScopeExpiredException(EditScopeKind.Triage, caseId)));
        Assert.True(OneCommandEditAuthority.WriteEndedIt(Actor, new CaseEditLeaseConflictException(caseId, 3)));
        Assert.False(OneCommandEditAuthority.WriteEndedIt(Actor, new InvalidOperationException("store fault")));
        // An Automation write keeps its lease, so its release was due.
        Assert.False(OneCommandEditAuthority.WriteEndedIt(automation, new CaseEditLeaseExpiredException(caseId, 3)));
    }

    [Fact]
    public void ADerivedOperationKeyKeepsItsSuffixWithinCoresKeyLength()
    {
        var derived = OneCommandEditAuthority.DeriveOperationKey(new string('k', 120), ":release");

        Assert.Equal(OneCommandEditAuthority.MaximumOperationKeyLength, derived.Length);
        Assert.EndsWith(":release", derived, StringComparison.Ordinal);
        Assert.Equal("key:lease", OneCommandEditAuthority.DeriveOperationKey("key", ":lease"));
    }

    private sealed class RecordingLeases(CaseEditLease? claimResult = null) : ILeaseCaseForEdit
    {
        public List<ClaimCaseEditLeaseRequest> Claims { get; } = [];
        public List<ReleaseCaseEditLeaseRequest> Releases { get; } = [];

        public Task<CaseEditLease> ClaimAsync(ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken)
        {
            Claims.Add(request);
            return Task.FromResult(claimResult ?? throw new InvalidOperationException("No claim expected."));
        }

        public Task<CaseEditLease> HeartbeatAsync(HeartbeatCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CaseEditLease?> ResumeAsync(ResumeCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReleaseAsync(ReleaseCaseEditLeaseRequest request, CancellationToken cancellationToken)
        {
            Releases.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingScopes(EditScopeLease? claimResult) : IEditScopeLeases
    {
        public List<ClaimEditScopeRequest> Claims { get; } = [];
        public List<ReleaseEditScopeRequest> Releases { get; } = [];

        public Task<EditScopeSnapshot?> GetActiveAsync(
            EditScopeKind scopeKind, Guid recordId, ActionActor actor, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<EditScopeLease> ClaimAsync(ClaimEditScopeRequest request, CancellationToken cancellationToken)
        {
            Claims.Add(request);
            return Task.FromResult(claimResult ?? throw new InvalidOperationException("No claim expected."));
        }

        public Task<EditScopeLease> HeartbeatAsync(HeartbeatEditScopeRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReleaseAsync(ReleaseEditScopeRequest request, CancellationToken cancellationToken)
        {
            Releases.Add(request);
            return Task.CompletedTask;
        }
    }
}
