using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Workflow;

public sealed class CaseEditAuthorityTests
{
    private static readonly Guid CaseId = Guid.Parse("2f1c1d9e-1f4a-4f22-9a5c-2c8d3f6b7a10");
    private static readonly DateTimeOffset Now =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private const string Holder = "6f0c2c9a-33b1-4f5d-8a2e-9b7c1d4e5f60";
    private static readonly ActionActor HolderActor =
        ActionActor.Staff(Guid.Parse(Holder), [StaffRole.User]);

    [Fact]
    public async Task AdministrativeClearRequiresAnAdministratorBeforeCallingTheStore()
    {
        var store = new RecordingAdministrativeLeaseStore();
        var request = new ClearCaseEditLeaseRequest(
            CaseId,
            Guid.Parse(Holder),
            3,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            "clear-lease",
            "User cannot close the editor");

        var exception = await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new ClearCaseEditLease(store).ExecuteAsync(request, default));

        Assert.Equal(StaffAccessRight.ManageStaffAccounts, exception.Permission);
        Assert.Null(store.Requested);
    }

    [Fact]
    public async Task AdministrativeClearNormalizesItsReplayKeyAndRequiredReason()
    {
        var store = new RecordingAdministrativeLeaseStore();
        var request = new ClearCaseEditLeaseRequest(
            CaseId,
            Guid.Parse(Holder),
            3,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
            "  clear-lease  ",
            "  User cannot close the editor  ");

        var result = await new ClearCaseEditLease(store).ExecuteAsync(request, default);

        Assert.Equal("clear-lease", store.Requested?.OperationKey);
        Assert.Equal("User cannot close the editor", store.Requested?.Reason);
        Assert.Equal(3, result.LeaseGeneration);
    }

    [Fact]
    public async Task AdministrativeClearRejectsAnInvalidTargetGenerationOrReason()
    {
        var store = new RecordingAdministrativeLeaseStore();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var request = new ClearCaseEditLeaseRequest(
            CaseId,
            Guid.Parse(Holder),
            1,
            actor,
            "clear-lease",
            "Required");
        var command = new ClearCaseEditLease(store);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            command.ExecuteAsync(request with { ExpectedHolderUserId = Guid.Empty }, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            command.ExecuteAsync(request with { ExpectedLeaseGeneration = 0 }, default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            command.ExecuteAsync(request with { Reason = " " }, default));
        Assert.Null(store.Requested);
    }

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
                HolderActor,
                presentedLeaseToken: "a-live-token",
                retainedLeaseHolderKind: ActorKind.Staff,
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

    /// <summary>
    /// KANMER-005: a holder is an actor identity, kind and subject together. The Automation
    /// Actor presenting the live token under the same subject text as the staff holder is a
    /// competitor, and so is a staff account under an Automation holder's subject.
    /// </summary>
    [Fact]
    public void TheSameSubjectUnderADifferentActorKindIsRefusedEvenWhenTheTokenMatches()
    {
        Assert.Throws<CaseEditLeaseConflictException>(() =>
            Require(actor: ActionActor.Automation(Holder)));
        Assert.Throws<CaseEditLeaseConflictException>(() =>
            Require(
                actor: HolderActor,
                retainedLeaseHolderKind: ActorKind.Automation));

        var automation = ActionActor.Automation("pegasus-automation");
        Require(
            actor: automation,
            retainedLeaseHolderKind: ActorKind.Automation,
            retainedLeaseHolder: automation.SubjectId);
    }

    /// <summary>
    /// A lease retained before the holder's kind was recorded belongs to nobody: it is still
    /// held, so it is not expired, but no actor of any kind can use it.
    /// </summary>
    [Fact]
    public void AHolderRetainedWithoutAKindIsNobodysUntilItExpires()
    {
        Assert.Throws<CaseEditLeaseConflictException>(() =>
            Require(retainedLeaseHolderKind: null));
        Assert.False(CaseEditAuthority.IsHolder(null, Holder, HolderActor));
        Assert.False(CaseEditAuthority.IsHolder(null, Holder, ActionActor.Automation(Holder)));
    }

    [Fact]
    public void OnlyTheExactKindAndSubjectIsTheHolder()
    {
        Assert.True(CaseEditAuthority.IsHolder(ActorKind.Staff, Holder, HolderActor));
        Assert.False(CaseEditAuthority.IsHolder(ActorKind.Automation, Holder, HolderActor));
        Assert.False(CaseEditAuthority.IsHolder(
            ActorKind.Staff,
            Guid.NewGuid().ToString("D"),
            HolderActor));
        Assert.False(CaseEditAuthority.IsHolder(ActorKind.Staff, null, HolderActor));
        Assert.False(CaseEditAuthority.IsHolder(ActorKind.Staff, " ", HolderActor));
        Assert.True(CaseEditAuthority.IsHolder(
            ActorKind.Automation,
            "pegasus-automation",
            ActionActor.Automation("pegasus-automation")));
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
            ActorKind.Staff,
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
            ActorKind.Staff,
            Guid.NewGuid().ToString("D"),
            viewer,
            default)).DisplayName);
        Assert.Null((await describe.ExecuteAsync(
            ActorKind.Staff,
            "automation",
            viewer,
            default)).DisplayName);
        Assert.Null((await describe.ExecuteAsync(
            ActorKind.Staff,
            Guid.Empty.ToString("D"),
            viewer,
            default)).DisplayName);
    }

    /// <summary>
    /// ADR-0011 keeps the Automation Actor attributable without impersonating staff, so the two
    /// unresolvable cases must stay apart. KANMER-005: the retained kind decides, never the shape
    /// of the subject — a GUID-shaped Automation subject is still the Automation Actor, and a
    /// staff GUID with no account behind it is still a member of staff.
    /// </summary>
    [Fact]
    public async Task TheAutomationHolderIsNotDescribedAsAMemberOfStaff()
    {
        var accounts = new StubStaffAccounts(Guid.NewGuid(), "r.hughes");
        var viewer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var describe = new DescribeCaseEditAuthorityHolder(accounts);

        var automation = await describe.ExecuteAsync(
            ActorKind.Automation,
            ActionActor.Automation("pegasus-automation").SubjectId,
            viewer,
            default);
        Assert.True(automation.IsAutomation);
        Assert.Null(automation.DisplayName);

        var guidShapedAutomation = await describe.ExecuteAsync(
            ActorKind.Automation,
            accounts.KnownStaffId.ToString("D"),
            viewer,
            default);
        Assert.True(guidShapedAutomation.IsAutomation);
        Assert.Null(guidShapedAutomation.DisplayName);
        Assert.Null(accounts.Requested);

        var unresolvedStaff = await describe.ExecuteAsync(
            ActorKind.Staff,
            Guid.NewGuid().ToString("D"),
            viewer,
            default);
        Assert.False(unresolvedStaff.IsAutomation);
        Assert.Null(unresolvedStaff.DisplayName);

        var namedStaff = await describe.ExecuteAsync(
            ActorKind.Staff,
            accounts.KnownStaffId.ToString("D"),
            viewer,
            default);
        Assert.False(namedStaff.IsAutomation);
        Assert.Equal("r.hughes", namedStaff.DisplayName);
    }

    [Fact]
    public async Task AHolderWithoutARetainedKindIsDescribedWithoutAnIdentifierOrAnAccountRead()
    {
        var accounts = new StubStaffAccounts(Guid.NewGuid(), "r.hughes");
        var viewer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        var holder = await new DescribeCaseEditAuthorityHolder(accounts).ExecuteAsync(
            null,
            accounts.KnownStaffId.ToString("D"),
            viewer,
            default);

        Assert.False(holder.IsAutomation);
        Assert.Null(holder.DisplayName);
        Assert.Null(accounts.Requested);
    }

    [Fact]
    public async Task DisclosingTheHolderRequiresCaseworkPermissionBeforeAnyAccountIsRead()
    {
        var accounts = new StubStaffAccounts(Guid.NewGuid(), "r.hughes");

        var exception = await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new DescribeCaseEditAuthorityHolder(accounts).ExecuteAsync(
                ActorKind.Staff,
                Guid.NewGuid().ToString("D"),
                ActionActor.SystemWorker("case-worker"),
                default));

        Assert.Equal(StaffAccessRight.PerformCasework, exception.Permission);
        Assert.Null(accounts.Requested);
    }

    private sealed class StubStaffAccounts(Guid staffId, string userName) : IStaffAccountQueries
    {
        public Guid KnownStaffId => staffId;

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

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<SignOffEngineerProfile?> GetSignOffEngineerAsync(
            Guid staffId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");
    }

    private sealed class RecordingAdministrativeLeaseStore : IAdministrativeCaseEditLeaseStore
    {
        public ClearCaseEditLeaseRequest? Requested { get; private set; }

        public Task<ClearCaseEditLeaseResult> ClearAsync(
            ClearCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requested = request;
            return Task.FromResult(new ClearCaseEditLeaseResult(
                request.CaseId,
                request.ExpectedHolderUserId,
                request.ExpectedLeaseGeneration,
                4,
                Now));
        }
    }

    private static void Require(
        string? presentedLeaseToken = "a-live-token",
        ActorKind? retainedLeaseHolderKind = ActorKind.Staff,
        string? retainedLeaseHolder = Holder,
        bool hasRetainedLeaseTokenHash = true,
        DateTimeOffset? leaseExpiresAtUtc = null,
        bool presentedTokenMatchesRetainedHash = true,
        ActionActor? actor = null) =>
        CaseEditAuthority.RequireLease(
            CaseId,
            caseVersion: 4,
            actor ?? HolderActor,
            presentedLeaseToken,
            retainedLeaseHolderKind,
            retainedLeaseHolder,
            hasRetainedLeaseTokenHash,
            leaseExpiresAtUtc ?? Now.AddMinutes(5),
            presentedTokenMatchesRetainedHash,
            Now);
}
