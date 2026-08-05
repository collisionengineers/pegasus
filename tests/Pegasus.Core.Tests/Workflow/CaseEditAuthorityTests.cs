using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Workflow;

public sealed class CaseEditAuthorityTests
{
    private static readonly Guid CaseId = Guid.Parse("2f1c1d9e-1f4a-4f22-9a5c-2c8d3f6b7a10");
    private static readonly DateTimeOffset Now =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private const string Holder = "6f0c2c9a-33b1-4f5d-8a2e-9b7c1d4e5f60";

    [Fact]
    public void StaleVersionIsRefusedBeforeAnyLeaseQuestionIsAsked()
    {
        var exception = Assert.Throws<CaseVersionConflictException>(() =>
            CaseEditAuthority.RequireVersion(CaseId, caseVersion: 8, expectedVersion: 7));

        Assert.Equal(CaseId, exception.CaseId);
        Assert.Equal(7, exception.ExpectedVersion);
        Assert.Equal(8, exception.ActualVersion);
    }

    [Fact]
    public void ExpiredLeaseIsRefusedAsHavingNoEditAuthority()
    {
        var exception = Assert.Throws<CaseEditLeaseExpiredException>(() =>
            Require(leaseExpiresAtUtc: Now));

        Assert.Equal(CaseId, exception.CaseId);
        Assert.Equal(4, exception.CaseVersion);
    }

    [Fact]
    public void MissingPresentedTokenRetainedHashOrHolderIsRefusedAsHavingNoEditAuthority()
    {
        Assert.Throws<CaseEditLeaseExpiredException>(() =>
            Require(presentedLeaseToken: "   "));
        Assert.Throws<CaseEditLeaseExpiredException>(() =>
            Require(hasRetainedLeaseTokenHash: false));
        Assert.Throws<CaseEditLeaseExpiredException>(() =>
            Require(retainedLeaseHolder: null));
        Assert.Throws<CaseEditLeaseExpiredException>(() =>
            CaseEditAuthority.RequireLease(
                CaseId,
                caseVersion: 4,
                actorSubjectId: Holder,
                presentedLeaseToken: "a-live-token",
                retainedLeaseHolder: Holder,
                hasRetainedLeaseTokenHash: true,
                leaseExpiresAtUtc: null,
                presentedTokenMatchesRetainedHash: true,
                Now));
    }

    [Fact]
    public void WrongHolderIsRefusedEvenWhenTheTokenMatches()
    {
        var exception = Assert.Throws<CaseEditLeaseConflictException>(() =>
            Require(retainedLeaseHolder: Guid.NewGuid().ToString("D")));

        Assert.Equal(CaseId, exception.CaseId);
        Assert.Equal(4, exception.CaseVersion);
    }

    [Fact]
    public void UnprovableTokenIsRefusedEvenForTheHolder()
    {
        var exception = Assert.Throws<CaseEditLeaseConflictException>(() =>
            Require(presentedTokenMatchesRetainedHash: false));

        Assert.Equal(CaseId, exception.CaseId);
    }

    [Fact]
    public void HolderPresentingTheLiveTokenAtTheLoadedVersionIsAllowed()
    {
        CaseEditAuthority.RequireVersion(CaseId, caseVersion: 4, expectedVersion: 4);
        Require();
    }

    [Fact]
    public void AnExpiryInThePastIsNotHeldAndAnExpiryInTheFutureIs()
    {
        Assert.False(CaseEditAuthority.IsHeld(null, Now));
        Assert.False(CaseEditAuthority.IsHeld(Now.AddMinutes(-1), Now));
        Assert.False(CaseEditAuthority.IsHeld(Now, Now));
        Assert.True(CaseEditAuthority.IsHeld(Now.AddMinutes(1), Now));
    }

    [Fact]
    public void TheIssuedTokenLengthIsTheOneContractEveryValidatorShares() =>
        Assert.Equal(64, CaseEditAuthority.LeaseTokenLength);

    [Fact]
    public async Task TheHolderIsDisclosedByStaffAccountNameAndNeverByItsIdentifier()
    {
        var staffId = Guid.NewGuid();
        var accounts = new StubStaffAccounts(staffId, "r.hughes");
        var viewer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        var holder = await new DescribeCaseEditAuthorityHolder(accounts).ExecuteAsync(
            staffId.ToString("D"),
            viewer,
            default);

        Assert.Equal("r.hughes", holder.DisplayName);
        Assert.Equal(staffId, accounts.Requested);
    }

    [Fact]
    public async Task AnUnresolvableHolderIsDescribedWithoutAnIdentifier()
    {
        var accounts = new StubStaffAccounts(Guid.NewGuid(), "r.hughes");
        var viewer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var describe = new DescribeCaseEditAuthorityHolder(accounts);

        Assert.Null((await describe.ExecuteAsync(
            Guid.NewGuid().ToString("D"),
            viewer,
            default)).DisplayName);
        Assert.Null((await describe.ExecuteAsync("automation", viewer, default)).DisplayName);
        Assert.Null((await describe.ExecuteAsync(
            Guid.Empty.ToString("D"),
            viewer,
            default)).DisplayName);
    }

    [Fact]
    public async Task DisclosingTheHolderRequiresCaseworkPermissionBeforeAnyAccountIsRead()
    {
        var accounts = new StubStaffAccounts(Guid.NewGuid(), "r.hughes");

        var exception = await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new DescribeCaseEditAuthorityHolder(accounts).ExecuteAsync(
                Guid.NewGuid().ToString("D"),
                ActionActor.SystemWorker("case-worker"),
                default));

        Assert.Equal(StaffAccessRight.PerformCasework, exception.Permission);
        Assert.Null(accounts.Requested);
    }

    private sealed class StubStaffAccounts(Guid staffId, string userName) : IStaffAccountQueries
    {
        public Guid? Requested { get; private set; }

        public Task<StaffAccountSummary?> GetAsync(
            Guid requestedStaffId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requested = requestedStaffId;
            return Task.FromResult<StaffAccountSummary?>(
                requestedStaffId == staffId
                    ? new(staffId, userName, true, false, [StaffRole.User], null)
                    : null);
        }

        public Task<StaffAccountQuerySlice> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException(
                "Disclosing one holder must not enumerate the staff directory.");
    }

    private static void Require(
        string? presentedLeaseToken = "a-live-token",
        string? retainedLeaseHolder = Holder,
        bool hasRetainedLeaseTokenHash = true,
        DateTimeOffset? leaseExpiresAtUtc = null,
        bool presentedTokenMatchesRetainedHash = true) =>
        CaseEditAuthority.RequireLease(
            CaseId,
            caseVersion: 4,
            actorSubjectId: Holder,
            presentedLeaseToken,
            retainedLeaseHolder,
            hasRetainedLeaseTokenHash,
            leaseExpiresAtUtc ?? Now.AddMinutes(5),
            presentedTokenMatchesRetainedHash,
            Now);
}
