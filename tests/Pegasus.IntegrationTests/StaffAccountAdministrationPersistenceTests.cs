using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class StaffAccountAdministrationPersistenceTests
{
    [Fact]
    public async Task EngineerChoicesIncludeEnabledEngineerWithoutRequiringSignOffProfile()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<PegasusIdentityUser>>();
        var eligible = await CreateUserAsync(userManager, "eligible-engineer", "password");
        var disabled = await CreateUserAsync(
            userManager,
            "disabled-engineer",
            "password",
            isEnabled: false);
        _ = await CreateUserAsync(userManager, "ordinary-user", "password");
        Assert.True((await userManager.AddToRoleAsync(
            eligible,
            StaffRoleNames.Engineer)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(
            disabled,
            StaffRoleNames.Engineer)).Succeeded);

        var choices = await new EfStaffAccountQueries(context).GetAsync(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            default);

        var choice = Assert.Single(choices);
        Assert.Equal(eligible.Id, choice.StaffId);
        Assert.Equal("eligible-engineer", choice.DisplayName);
        Assert.False(eligible.IsSignOffEngineer);
        Assert.Null(eligible.SignOffSignature);
    }

    [Fact]
    public async Task AdministratorRoleRemovalRevokesExistingAuthorizationAndToken()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<PegasusIdentityUser>>();
        var target = await CreateUserAsync(userManager, "role-target", "password");
        var remainingAdministrator = await CreateUserAsync(
            userManager,
            "remaining-administrator",
            "password");
        Assert.True((await userManager.AddToRoleAsync(
            target,
            StaffRoleNames.Administrator)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(
            remainingAdministrator,
            StaffRoleNames.Administrator)).Succeeded);
        AddValidAuthorizationAndToken(context, target.Id);
        await context.SaveChangesAsync();

        var result = await new EfStaffAccountAdministration(
                context,
                userManager,
                TimeProvider.System)
            .AssignAsync(
                new(
                    ActionActor.Staff(remainingAdministrator.Id, [StaffRole.Administrator]),
                    target.Id,
                    [StaffRole.User],
                    "Duties changed",
                    "demote-administrator"),
                default);

        Assert.Equal(1, result.RevokedAuthorizations);
        Assert.Equal(1, result.RevokedTokens);
        Assert.Equal(
            OpenIddictConstants.Statuses.Revoked,
            (await context.Set<OpenIddictEntityFrameworkCoreAuthorization>().SingleAsync()).Status);
        Assert.Equal(
            OpenIddictConstants.Statuses.Revoked,
            (await context.Set<OpenIddictEntityFrameworkCoreToken>().SingleAsync()).Status);
    }

    [Fact]
    public async Task ResetReturnsTemporaryPasswordOnceAndRequiresItsChange()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<PegasusIdentityUser>>();
        var target = await CreateUserAsync(userManager, "reset-target", "old-password");
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var store = new EfStaffAccountAdministration(context, userManager, TimeProvider.System);
        var request = new ResetStaffPasswordRequest(
            administrator,
            target.Id,
            "Staff recovery",
            "reset-target-1");

        var result = await store.ResetPasswordAsync(request, default);

        Assert.True(await userManager.CheckPasswordAsync(target, result.TemporaryPassword));
        Assert.True(target.MustChangePassword);
        Assert.Equal(nameof(ResetStaffPasswordResult), result.ToString());
        var replay = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            store.ResetPasswordAsync(request, default));
        Assert.Equal(StaffAccountAdministrationError.OperationConflict, replay.Error);
    }

    [Fact]
    public async Task DeleteRetainsActorTombstoneAndClearsActiveSecurityMaterial()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<PegasusIdentityUser>>();
        var target = await CreateUserAsync(userManager, "delete-target", "old-password");
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var credentialStore = new EfPerUserExternalCredentialStore(
            context,
            new EphemeralDataProtectionProvider(),
            TimeProvider.System);
        await credentialStore.ReplaceAsync(
            administrator,
            target.Id,
            ExternalCredentialProvider.GlassRepairEstimate,
            0,
            "alex.glass",
            "provider-password",
            true,
            default);
        AddValidAuthorizationAndToken(context, target.Id);
        await context.SaveChangesAsync();

        var result = await new EfStaffAccountAdministration(
                context,
                userManager,
                TimeProvider.System)
            .DeleteAsync(
                new(administrator, target.Id, "Employment ended", "delete-target-1"),
                default);

        context.ChangeTracker.Clear();
        var tombstone = await context.Users.SingleAsync(item => item.Id == target.Id);
        Assert.False(tombstone.IsEnabled);
        Assert.Null(tombstone.PasswordHash);
        Assert.Equal("delete-target", tombstone.UserName);
        Assert.Empty(await userManager.GetRolesAsync(tombstone));
        Assert.True(result.CredentialsCleared);
        Assert.Null(await credentialStore.GetEnabledAsync(
            ActionActor.Staff(target.Id, [StaffRole.User]),
            ExternalCredentialProvider.GlassRepairEstimate,
            default));
        var token = await context.Set<OpenIddictEntityFrameworkCoreToken>().SingleAsync();
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, token.Status);
        Assert.Null(token.Payload);
    }

    [Fact]
    public async Task OnlyEnabledAdministratorCannotBeDisabledOrLoseAdministratorRole()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<PegasusIdentityUser>>();
        var target = await CreateUserAsync(userManager, "last-administrator", "password");
        Assert.True((await userManager.AddToRoleAsync(target, StaffRoleNames.Administrator)).Succeeded);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var store = new EfStaffAccountAdministration(context, userManager, TimeProvider.System);

        var disable = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            store.DisableAsync(
                new(actor, target.Id, "Remove access", "disable-last-administrator"),
                default));
        var roles = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            store.AssignAsync(
                new(actor, target.Id, [StaffRole.User], "Change duties", "roles-last-administrator"),
                default));

        Assert.Equal(StaffAccountAdministrationError.LastAdministrator, disable.Error);
        Assert.Equal(StaffAccountAdministrationError.LastAdministrator, roles.Error);
    }

    [Fact]
    public async Task ConcurrentRemovalOfTwoEnabledAdministratorsHasOneSafeOutcome()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        Guid firstId;
        Guid secondId;
        await using (var seedScope = database.CreateAsyncScope())
        {
            var userManager = seedScope.ServiceProvider
                .GetRequiredService<UserManager<PegasusIdentityUser>>();
            var first = await CreateUserAsync(userManager, "race-administrator-1", "password");
            var second = await CreateUserAsync(userManager, "race-administrator-2", "password");
            Assert.True((await userManager.AddToRoleAsync(
                first,
                StaffRoleNames.Administrator)).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(
                second,
                StaffRoleNames.Administrator)).Succeeded);
            firstId = first.Id;
            secondId = second.Id;
        }

        var firstAttempt = RunDisableAsync(
            database,
            ActionActor.Staff(secondId, [StaffRole.Administrator]),
            firstId,
            "disable-race-1");
        var secondAttempt = RunDemotionAsync(
            database,
            ActionActor.Staff(firstId, [StaffRole.Administrator]),
            secondId,
            "demote-race-2");
        var outcomes = await Task.WhenAll(firstAttempt, secondAttempt);

        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Single(
            outcomes,
            outcome => outcome is StaffAccountAdministrationException
            {
                Error: StaffAccountAdministrationError.LastAdministrator
                    or StaffAccountAdministrationError.OperationConflict
            });
        await using var verify = await database.CreateContextAsync();
        var administratorRoleName = StaffRoleNames.Administrator.ToUpperInvariant();
        var enabledAdministrators = await (
            from user in verify.Users
            join userRole in verify.UserRoles on user.Id equals userRole.UserId
            join role in verify.Roles on userRole.RoleId equals role.Id
            where user.IsEnabled && role.NormalizedName == administratorRoleName
            select user.Id).Distinct().CountAsync();
        Assert.True(enabledAdministrators >= 1);
    }

    private static async Task<Exception?> RunDisableAsync(
        LocalDbTestDatabase database,
        ActionActor actor,
        Guid targetId,
        string operationKey)
    {
        await using var scope = database.CreateAsyncScope();
        var store = new EfStaffAccountAdministration(
            scope.ServiceProvider.GetRequiredService<PegasusDbContext>(),
            scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>(),
            TimeProvider.System);
        try
        {
            await store.DisableAsync(
                new(actor, targetId, "Concurrent access removal", operationKey),
                default);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task<Exception?> RunDemotionAsync(
        LocalDbTestDatabase database,
        ActionActor actor,
        Guid targetId,
        string operationKey)
    {
        await using var scope = database.CreateAsyncScope();
        var store = new EfStaffAccountAdministration(
            scope.ServiceProvider.GetRequiredService<PegasusDbContext>(),
            scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>(),
            TimeProvider.System);
        try
        {
            await store.AssignAsync(
                new(actor, targetId, [StaffRole.User], "Concurrent duties change", operationKey),
                default);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static void AddValidAuthorizationAndToken(PegasusDbContext context, Guid staffId)
    {
        context.Set<OpenIddictEntityFrameworkCoreAuthorization>().Add(new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Subject = staffId.ToString("D"),
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.AdHoc,
            ConcurrencyToken = Guid.NewGuid().ToString("N")
        });
        context.Set<OpenIddictEntityFrameworkCoreToken>().Add(new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Subject = staffId.ToString("D"),
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeHints.RefreshToken,
            Payload = "test-token-payload",
            ConcurrencyToken = Guid.NewGuid().ToString("N")
        });
    }

    private static async Task<PegasusIdentityUser> CreateUserAsync(
        UserManager<PegasusIdentityUser> userManager,
        string userName,
        string password,
        bool isEnabled = true)
    {
        var user = new PegasusIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            IsEnabled = isEnabled,
            MustChangePassword = false,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        var result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded);
        return user;
    }
}

internal static class IdentityPersistenceTestServices
{
    public static void Configure(IServiceCollection services)
    {
        services
            .AddIdentity<PegasusIdentityUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Lockout.AllowedForNewUsers = false;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<PegasusDbContext>()
            .AddDefaultTokenProviders();
        services.AddOpenIddict()
            .AddCore(options => options
                .UseEntityFrameworkCore()
                .UseDbContext<PegasusDbContext>());
    }
}
