using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Identity;

public sealed class IdentityUseCaseTests
{
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.Parse("ed03353a-bcb8-48d9-aa1b-37061b114af1"), [StaffRole.Administrator]);

    [Fact]
    public async Task NamedStaffQueriesAreBoundedAndRequireCurrentAdministrator()
    {
        var account = Account(Guid.NewGuid());
        var queries = new RecordingQueries(new([account], HasMoreAccounts: true));

        var list = await new ListStaffAccounts(queries).ExecuteAsync(
            new(Administrator, PageNumber: 2, PageSize: 25),
            default);
        var roles = await new GetRoleAssignments(queries).ExecuteAsync(
            new(Administrator, MaximumResults: 12),
            default);
        var detail = await new GetStaffAccount(queries).ExecuteAsync(
            new(Administrator, account.Id),
            default);

        Assert.Equal((25, 25), queries.ListCalls[0]);
        Assert.Equal((0, 12), queries.ListCalls[1]);
        Assert.True(list.HasPreviousPage);
        Assert.True(list.HasMoreAccounts);
        Assert.Equal(account.Roles, Assert.Single(roles.Accounts).CurrentRoles);
        Assert.Equal(account, detail?.Account);

        var engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new ListStaffAccounts(queries).ExecuteAsync(new(engineer), default));
    }

    [Fact]
    public async Task HeldLeaseQueryRequiresAdministratorAndReturnsOnlyStoreProjection()
    {
        var staffId = Guid.NewGuid();
        var expected = new StaffHeldCaseEditLease(
            Guid.NewGuid(),
            "ABC-2026-00001",
            7,
            DateTimeOffset.UtcNow.AddMinutes(5));
        var queries = new RecordingQueries(new([], HasMoreAccounts: false), [expected]);

        var result = await new GetStaffHeldCaseEditLeases(queries).ExecuteAsync(
            new(Administrator, staffId),
            default);

        Assert.Equal(staffId, result.StaffId);
        Assert.Equal(expected, Assert.Single(result.Leases));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new GetStaffHeldCaseEditLeases(queries).ExecuteAsync(
                new(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), staffId),
                default));
    }

    [Fact]
    public void ResetPasswordResultNeverFormatsItsTemporarySecret()
    {
        var result = new ResetStaffPasswordResult(
            Guid.NewGuid(),
            "temporary-secret",
            1,
            2,
            false);

        Assert.Equal(nameof(ResetStaffPasswordResult), result.ToString());
        Assert.False(result.ToString().Contains(
            result.TemporaryPassword,
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task StaffCommandsNormalizeThroughTheirNarrowStores()
    {
        var store = new RecordingStaffStore();
        var staffId = Guid.NewGuid();

        await new CreateStaffAccount(store).ExecuteAsync(
            new(Administrator, "  new.user  ", "password", "  Approved starter access  ", "  create-1  "),
            default);
        await new DisableStaffAccount(store).ExecuteAsync(
            new(Administrator, staffId, "  Access removed  ", "  disable-1  "),
            default);
        await new AssignStaffRoles(store).ExecuteAsync(
            new(
                Administrator,
                staffId,
                [StaffRole.User, StaffRole.Administrator, StaffRole.User],
                "  Approved duties  ",
                "  roles-1  "),
            default);
        await new EnableStaffAccount(store).ExecuteAsync(
            new(Administrator, staffId, "  Access restored  ", "  enable-1  "),
            default);
        await new ForceStaffLogout(store).ExecuteAsync(
            new(Administrator, staffId, "  End sessions  ", "  logout-1  "),
            default);
        await new ResetStaffPassword(store).ExecuteAsync(
            new(Administrator, staffId, "  Recovery  ", "  reset-1  "),
            default);
        await new DeleteStaffAccount(store).ExecuteAsync(
            new(Administrator, staffId, "  Access removed permanently  ", "  delete-1  "),
            default);
        await new UpdateStaffAccountSignOff(store).ExecuteAsync(
            new(
                Administrator,
                staffId,
                true,
                "  A Engineer  ",
                "  M.Inst.IAEA  ",
                Png(),
                true,
                "  Sign-off approved  ",
                "  sign-off-1  "),
            default);

        Assert.Equal("new.user", store.CreateRequest?.UserName);
        Assert.Equal("create-1", store.CreateRequest?.OperationKey);
        Assert.Equal("Approved starter access", store.CreateRequest?.Reason);
        Assert.Equal("Access removed", store.DisableRequest?.Reason);
        Assert.Equal(
            [StaffRole.Administrator, StaffRole.User],
            store.AssignRequest?.Roles);
        Assert.Equal("Approved duties", store.AssignRequest?.Reason);
        Assert.Equal("Access restored", store.EnableRequest?.Reason);
        Assert.Equal("End sessions", store.LogoutRequest?.Reason);
        Assert.Equal("Recovery", store.ResetRequest?.Reason);
        Assert.Equal("Access removed permanently", store.DeleteRequest?.Reason);
        Assert.Equal("A Engineer", store.SignOffRequest?.PrintedName);
        Assert.Equal("M.Inst.IAEA", store.SignOffRequest?.Qualifications);
        Assert.Equal("Sign-off approved", store.SignOffRequest?.Reason);
        Assert.Equal("sign-off-1", store.SignOffRequest?.OperationKey);
    }

    [Fact]
    public async Task SignOffUpdateRequiresAdministratorAndPrintedName()
    {
        var store = new RecordingStaffStore();
        var staffId = Guid.NewGuid();
        var engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new UpdateStaffAccountSignOff(store).ExecuteAsync(
                SignOffRequest(staffId) with { Actor = engineer },
                default));
        var missingName = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            new UpdateStaffAccountSignOff(store).ExecuteAsync(
                SignOffRequest(staffId) with { PrintedName = " " },
                default));
        var ineligibleDefault = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            new UpdateStaffAccountSignOff(store).ExecuteAsync(
                SignOffRequest(staffId) with { IsSignOffEngineer = false },
                default));

        Assert.Equal(
            StaffAccountAdministrationError.SignOffPrintedNameRequired,
            missingName.Error);
        Assert.Equal(
            StaffAccountAdministrationError.IneligibleSignOffEngineer,
            ineligibleDefault.Error);
        Assert.Null(store.SignOffRequest);
    }

    [Fact]
    public async Task SignOffSignaturePolicyRejectsInvalidUploads()
    {
        foreach (var signature in new[]
        {
            Array.Empty<byte>(),
            new byte[] { 1, 2, 3 },
            OversizedPngSignature()
        })
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                new UpdateStaffAccountSignOff(new RecordingStaffStore()).ExecuteAsync(
                    SignOffRequest(Guid.NewGuid()) with { Signature = signature },
                    default));
        }
    }

    [Fact]
    public async Task SignOffQualificationsAreOptionalAndEligibilityHasOneRule()
    {
        var store = new RecordingStaffStore();
        await new UpdateStaffAccountSignOff(store).ExecuteAsync(
            SignOffRequest(Guid.NewGuid()) with { Qualifications = " " },
            default);

        Assert.Null(store.SignOffRequest?.Qualifications);
        Assert.True(SignOffEngineerEligibility.IsEligible(
            true,
            [StaffRole.Engineer],
            true,
            Png()));
        Assert.False(SignOffEngineerEligibility.IsEligible(
            false,
            [StaffRole.Engineer],
            true,
            Png()));
        Assert.False(SignOffEngineerEligibility.IsEligible(
            true,
            [StaffRole.User],
            true,
            Png()));
        Assert.False(SignOffEngineerEligibility.IsEligible(
            true,
            [StaffRole.Engineer],
            false,
            Png()));
        Assert.False(SignOffEngineerEligibility.IsEligible(
            true,
            [StaffRole.Engineer],
            true,
            null));
    }

    [Fact]
    public async Task StaffAccountCannotRunDestructiveAdministrativeActionsOnItself()
    {
        var staffId = Guid.Parse("b0a0e70a-1b8c-4ee4-bc21-1de58b73cf0c");
        var actor = ActionActor.Staff(staffId, [StaffRole.Administrator]);
        var store = new RecordingStaffStore();

        var disable = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            new DisableStaffAccount(store).ExecuteAsync(
                new(actor, staffId, "Disable self", "disable-self"),
                default));
        var logout = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            new ForceStaffLogout(store).ExecuteAsync(
                new(actor, staffId, "Logout self", "logout-self"),
                default));
        var reset = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            new ResetStaffPassword(store).ExecuteAsync(
                new(actor, staffId, "Reset self", "reset-self"),
                default));
        var delete = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            new DeleteStaffAccount(store).ExecuteAsync(
                new(actor, staffId, "Delete self", "delete-self"),
                default));

        Assert.Equal(StaffAccountAdministrationError.SelfAction, disable.Error);
        Assert.Equal(StaffAccountAdministrationError.SelfAction, logout.Error);
        Assert.Equal(StaffAccountAdministrationError.SelfAction, reset.Error);
        Assert.Equal(StaffAccountAdministrationError.SelfAction, delete.Error);
        Assert.Null(store.DisableRequest);
        Assert.Null(store.LogoutRequest);
        Assert.Null(store.ResetRequest);
        Assert.Null(store.DeleteRequest);
    }

    private static StaffAccountSummary Account(Guid id) =>
        new(id, "staff", true, false, [StaffRole.User]);

    private static UpdateStaffAccountSignOffRequest SignOffRequest(Guid staffId) =>
        new(
            Administrator,
            staffId,
            true,
            "A Engineer",
            null,
            Png(),
            true,
            "Approved",
            "sign-off-operation");

    private static byte[] Png() =>
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    private static byte[] OversizedPngSignature()
    {
        var signature = new byte[SignOffSignaturePolicy.MaximumBytes + 1];
        Png().CopyTo(signature, 0);
        return signature;
    }

    private sealed class RecordingQueries(
        StaffAccountQuerySlice slice,
        IReadOnlyList<StaffHeldCaseEditLease>? leases = null)
        : IStaffAccountQueries,
          IStaffHeldCaseEditLeaseQueries
    {
        public List<(int Offset, int Limit)> ListCalls { get; } = [];

        public Task<StaffAccountQuerySlice> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken)
        {
            ListCalls.Add((offset, limit));
            return Task.FromResult(slice);
        }

        public Task<StaffAccountSummary?> GetAsync(
            Guid staffId,
            CancellationToken cancellationToken) =>
            Task.FromResult(slice.Accounts.SingleOrDefault(account => account.Id == staffId));

        public Task<IReadOnlyList<StaffHeldCaseEditLease>> ListHeldCaseEditLeasesAsync(
            Guid staffId,
            CancellationToken cancellationToken) =>
            Task.FromResult(leases ?? (IReadOnlyList<StaffHeldCaseEditLease>)[]);

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SignOffEngineerProfile>>([]);

        public Task<SignOffEngineerProfile?> GetSignOffEngineerAsync(
            Guid staffId,
            CancellationToken cancellationToken) =>
            Task.FromResult<SignOffEngineerProfile?>(null);
    }

    private sealed class RecordingStaffStore :
        ICreateStaffAccountStore,
        IDisableStaffAccountStore,
        IAssignStaffRolesStore,
        IEnableStaffAccountStore,
        IForceStaffLogoutStore,
        IResetStaffPasswordStore,
        IDeleteStaffAccountStore,
        IUpdateStaffAccountSignOffStore
    {
        public CreateStaffAccountRequest? CreateRequest { get; private set; }
        public DisableStaffAccountRequest? DisableRequest { get; private set; }
        public AssignStaffRolesRequest? AssignRequest { get; private set; }
        public EnableStaffAccountRequest? EnableRequest { get; private set; }
        public ForceStaffLogoutRequest? LogoutRequest { get; private set; }
        public ResetStaffPasswordRequest? ResetRequest { get; private set; }
        public DeleteStaffAccountRequest? DeleteRequest { get; private set; }
        public UpdateStaffAccountSignOffRequest? SignOffRequest { get; private set; }

        public Task<CreateStaffAccountResult> CreateAsync(
            CreateStaffAccountRequest request,
            CancellationToken cancellationToken)
        {
            CreateRequest = request;
            return Task.FromResult(new CreateStaffAccountResult(Account(Guid.NewGuid()), false));
        }

        public Task<DisableStaffAccountResult> DisableAsync(
            DisableStaffAccountRequest request,
            CancellationToken cancellationToken)
        {
            DisableRequest = request;
            return Task.FromResult(new DisableStaffAccountResult(Account(request.StaffId), 1, 2, false));
        }

        public Task<AssignStaffRolesResult> AssignAsync(
            AssignStaffRolesRequest request,
            CancellationToken cancellationToken)
        {
            AssignRequest = request;
            return Task.FromResult(new AssignStaffRolesResult(Account(request.StaffId), 1, 2, false));
        }

        public Task<EnableStaffAccountResult> EnableAsync(
            EnableStaffAccountRequest request,
            CancellationToken cancellationToken)
        {
            EnableRequest = request;
            return Task.FromResult(new EnableStaffAccountResult(Account(request.StaffId), false));
        }

        public Task<ForceStaffLogoutResult> ForceLogoutAsync(
            ForceStaffLogoutRequest request,
            CancellationToken cancellationToken)
        {
            LogoutRequest = request;
            return Task.FromResult(new ForceStaffLogoutResult(request.StaffId, 1, 2, false));
        }

        public Task<ResetStaffPasswordResult> ResetPasswordAsync(
            ResetStaffPasswordRequest request,
            CancellationToken cancellationToken)
        {
            ResetRequest = request;
            return Task.FromResult(new ResetStaffPasswordResult(request.StaffId, "temporary", 1, 2, false));
        }

        public Task<DeleteStaffAccountResult> DeleteAsync(
            DeleteStaffAccountRequest request,
            CancellationToken cancellationToken)
        {
            DeleteRequest = request;
            return Task.FromResult(new DeleteStaffAccountResult(request.StaffId, 1, 2, true, false));
        }

        public Task<UpdateStaffAccountSignOffResult> UpdateAsync(
            UpdateStaffAccountSignOffRequest request,
            CancellationToken cancellationToken)
        {
            SignOffRequest = request;
            return Task.FromResult(new UpdateStaffAccountSignOffResult(
                Account(request.StaffId),
                false));
        }
    }

}
