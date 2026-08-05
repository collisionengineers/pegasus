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
