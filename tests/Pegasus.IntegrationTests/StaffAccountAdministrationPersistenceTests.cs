using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class StaffAccountAdministrationPersistenceTests
{
    [Fact]
    public async Task StaffAccountQueryProjectsOneRoleAndItsVersion()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var user = new PegasusIdentityUser { Id = Guid.NewGuid(), UserName = "single-role" };

        Assert.True((await userManager.CreateAsync(user, "Password-1")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, StaffRoleNames.Engineer)).Succeeded);

        var account = await services.GetRequiredService<IStaffAccountQueries>().GetAsync(user.Id, default);

        Assert.NotNull(account);
        Assert.Equal(StaffRole.Engineer, account!.Role);
        Assert.Equal(0, account.Version);
    }

    [Fact]
    public async Task SignOffChoicesExcludeDisabledAndUnsignedAccounts()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var valid = await CreateSignOffAccountAsync(userManager, "valid-sign-off", enabled: true, signed: true);
        _ = await CreateSignOffAccountAsync(userManager, "disabled-sign-off", enabled: false, signed: true);
        _ = await CreateSignOffAccountAsync(userManager, "unsigned-sign-off", enabled: true, signed: false);

        var choices = await services.GetRequiredService<IStaffAccountQueries>()
            .ListSignOffEngineersAsync(default);

        Assert.Equal(valid.Id, Assert.Single(choices).StaffId);
    }

    [Fact]
    public async Task AdministratorAccountsAreEligibleForEngineerAssignment()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var administrator = await CreateStaffAccountAsync(
            userManager, "administrator-engineer-choice", StaffRole.Administrator);
        var engineer = await CreateStaffAccountAsync(
            userManager, "engineer-choice", StaffRole.Engineer);
        _ = await CreateStaffAccountAsync(userManager, "user-choice", StaffRole.User);

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var choices = await services.GetRequiredService<ICaseEngineerChoices>().GetAsync(actor, default);
        var administratorEligibility = await services.GetRequiredService<ICaseEngineerEligibility>()
            .GetAsync(administrator.Id, default);

        Assert.Equal([administrator.Id, engineer.Id], choices.Select(choice => choice.StaffId));
        Assert.True(administratorEligibility.HasEngineerRole);
    }

    private static async Task<PegasusIdentityUser> CreateSignOffAccountAsync(
        UserManager<PegasusIdentityUser> userManager,
        string userName,
        bool enabled,
        bool signed)
    {
        var user = new PegasusIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            IsEnabled = enabled,
            IsSignOffEngineer = true,
            SignOffPrintedName = userName,
            SignOffSignature = signed ? [0x89, 0x50, 0x4e, 0x47] : null
        };
        Assert.True((await userManager.CreateAsync(user, "Password-1")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, StaffRoleNames.Engineer)).Succeeded);
        return user;
    }

    private static async Task<PegasusIdentityUser> CreateStaffAccountAsync(
        UserManager<PegasusIdentityUser> userManager,
        string userName,
        StaffRole role)
    {
        var user = new PegasusIdentityUser { Id = Guid.NewGuid(), UserName = userName };
        Assert.True((await userManager.CreateAsync(user, "Password-1")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role.ToString())).Succeeded);
        return user;
    }
}
