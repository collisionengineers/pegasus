using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Identity;

public sealed class IdentityUseCaseTests
{
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.Parse("ed03353a-bcb8-48d9-aa1b-37061b114af1"), [StaffRole.Administrator]);

    [Fact]
    public async Task StaffAccountSettingsNormalizeRoleAndSignOffAsOneCommand()
    {
        var store = new RecordingStore();
        await new UpdateStaffAccountSettings(store).ExecuteAsync(
            SettingsRequest(Guid.NewGuid()) with
            {
                PrintedName = "  A. Engineer  ",
                Qualifications = "  M.Inst.IAEA  ",
                Reason = "  Approved sign-off  ",
                OperationKey = "  settings-1  "
            }, default);

        Assert.Equal(StaffRole.Engineer, store.SettingsRequest?.Role);
        Assert.Equal("A. Engineer", store.SettingsRequest?.PrintedName);
        Assert.Equal("M.Inst.IAEA", store.SettingsRequest?.Qualifications);
        Assert.Equal("Approved sign-off", store.SettingsRequest?.Reason);
        Assert.Equal("settings-1", store.SettingsRequest?.OperationKey);
    }

    [Fact]
    public async Task UserRoleClearsInvalidSignOffSettingsAtomically()
    {
        var store = new RecordingStore();
        await new UpdateStaffAccountSettings(store).ExecuteAsync(
            SettingsRequest(Guid.NewGuid()) with
            {
                Role = StaffRole.User,
                IsSignOffEngineer = true,
                IsDefaultSignOffEngineer = true
            }, default);

        Assert.False(store.SettingsRequest!.IsSignOffEngineer);
        Assert.False(store.SettingsRequest.IsDefaultSignOffEngineer);
        Assert.Null(store.SettingsRequest.PrintedName);
        Assert.Null(store.SettingsRequest.Qualifications);
        Assert.Null(store.SettingsRequest.Signature);
    }

    [Fact]
    public async Task SettingsRequireAdministratorAndPrintedNameForSignOff()
    {
        var store = new RecordingStore();
        var staffId = Guid.NewGuid();
        var engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new UpdateStaffAccountSettings(store).ExecuteAsync(
                SettingsRequest(staffId) with { Actor = engineer }, default));
        var missingName = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            new UpdateStaffAccountSettings(store).ExecuteAsync(
                SettingsRequest(staffId) with { PrintedName = " " }, default));

        Assert.Equal(StaffAccountAdministrationError.SignOffPrintedNameRequired, missingName.Error);
        Assert.Null(store.SettingsRequest);
    }

    [Fact]
    public async Task SettingsRejectInvalidSignatureUpload()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new UpdateStaffAccountSettings(new RecordingStore()).ExecuteAsync(
                SettingsRequest(Guid.NewGuid()) with { Signature = new byte[] { 1, 2, 3 } }, default));
    }

    [Fact]
    public void SignOffEligibilityAllowsAdministratorsAndEngineersOnly()
    {
        Assert.True(SignOffEngineerEligibility.IsEligible(true, StaffRole.Administrator, true, Png()));
        Assert.True(SignOffEngineerEligibility.IsEligible(true, StaffRole.Engineer, true, Png()));
        Assert.False(SignOffEngineerEligibility.IsEligible(true, StaffRole.User, true, Png()));
        Assert.False(SignOffEngineerEligibility.IsEligible(false, StaffRole.Engineer, true, Png()));
    }

    [Fact]
    public void AdministratorInheritsEveryStaffRoleCapability()
    {
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

        Assert.True(administrator.IsInRole(StaffRole.Administrator));
        Assert.True(administrator.IsInRole(StaffRole.Engineer));
        Assert.True(administrator.IsInRole(StaffRole.User));
        Assert.False(engineer.IsInRole(StaffRole.Administrator));
    }

    [Fact]
    public async Task DestructiveActionsRejectSelfBeforeTheirStores()
    {
        var staffId = Guid.Parse("b0a0e70a-1b8c-4ee4-bc21-1de58b73cf0c");
        var actor = ActionActor.Staff(staffId, [StaffRole.Administrator]);
        var store = new RecordingStore();
        await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            new DisableStaffAccount(store).ExecuteAsync(
                new(actor, staffId, "Disable self", "disable-self", 2, LeaseToken), default));
        await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            new ForceStaffLogout(store).ExecuteAsync(
                new(actor, staffId, "Logout self", "logout-self", 2, LeaseToken), default));

        Assert.Null(store.DisableRequest);
        Assert.Null(store.LogoutRequest);
    }

    [Fact]
    public void ResetPasswordResultNeverFormatsItsTemporarySecret()
    {
        var result = new ResetStaffPasswordResult(Guid.NewGuid(), "temporary-secret", 1, 2, false);
        Assert.Equal(nameof(ResetStaffPasswordResult), result.ToString());
    }

    private static readonly string LeaseToken =
        new('a', CaseEditAuthority.LeaseTokenLength);

    private static UpdateStaffAccountSettingsRequest SettingsRequest(Guid staffId) =>
        new(Administrator, staffId, StaffRole.Engineer, true, "A Engineer", null, Png(), true,
            "Approved", "settings-operation", 2, LeaseToken);

    private static byte[] Png() => [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    private static StaffAccountSummary Account(Guid id) =>
        new(id, "staff", true, false, StaffRole.User);

    private sealed class RecordingStore :
        ICreateStaffAccountStore,
        IDisableStaffAccountStore,
        IUpdateStaffAccountSettingsStore,
        IEnableStaffAccountStore,
        IForceStaffLogoutStore,
        IResetStaffPasswordStore,
        IDeleteStaffAccountStore
    {
        public DisableStaffAccountRequest? DisableRequest { get; private set; }
        public ForceStaffLogoutRequest? LogoutRequest { get; private set; }
        public UpdateStaffAccountSettingsRequest? SettingsRequest { get; private set; }

        public Task<CreateStaffAccountResult> CreateAsync(CreateStaffAccountRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new CreateStaffAccountResult(Account(Guid.NewGuid()), false));
        public Task<DisableStaffAccountResult> DisableAsync(DisableStaffAccountRequest request, CancellationToken cancellationToken)
        { DisableRequest = request; return Task.FromResult(new DisableStaffAccountResult(Account(request.StaffId), 0, 0, false)); }
        public Task<UpdateStaffAccountSettingsResult> UpdateAsync(UpdateStaffAccountSettingsRequest request, CancellationToken cancellationToken)
        { SettingsRequest = request; return Task.FromResult(new UpdateStaffAccountSettingsResult(Account(request.StaffId), 0, 0, false)); }
        public Task<EnableStaffAccountResult> EnableAsync(EnableStaffAccountRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new EnableStaffAccountResult(Account(request.StaffId), false));
        public Task<ForceStaffLogoutResult> ForceLogoutAsync(ForceStaffLogoutRequest request, CancellationToken cancellationToken)
        { LogoutRequest = request; return Task.FromResult(new ForceStaffLogoutResult(request.StaffId, 0, 0, false)); }
        public Task<ResetStaffPasswordResult> ResetPasswordAsync(ResetStaffPasswordRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ResetStaffPasswordResult(request.StaffId, "temporary", 0, 0, false));
        public Task<DeleteStaffAccountResult> DeleteAsync(DeleteStaffAccountRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new DeleteStaffAccountResult(request.StaffId, 0, 0, true, false));
    }
}
