using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Pages.Connect;

namespace Pegasus.Web.Authentication;

internal static class DevelopmentOfflineIdentity
{
    public static Guid AdministratorId { get; } =
        Guid.Parse("d47fbbae-ea22-4ca6-b983-01e2ed1fbd13");

    public const string UserName = "development-offline-administrator";
}

/// <summary>
/// Initializes the ignored local test fixture only. Normal Web startup and production account
/// provisioning never call this path.
/// </summary>
internal static class DevelopmentOfflineInitialization
{
    private const string DevelopmentOfflineProfile = "DevelopmentOffline";
    private const string DevelopmentMcpClientId = "pegasus-development-mcp";
    private const string DevelopmentMcpRedirectUri = "http://127.0.0.1:7890/callback";

    public static async Task InitializeAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        RequireLocalOnly(services);
        await MigrateAsync(services, cancellationToken);
        await EnsureIdentityAsync(services);
        await EnsureQdosPrincipalAsync(services, cancellationToken);
        await RegisterMcpClientAsync(
            services,
            "development-initialization",
            cancellationToken);
    }

    public static async Task MigrateAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        RequireLocalOnly(services);
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
    }

    public static async Task RegisterMcpClientAsync(
        IServiceProvider services,
        string operationKey = "development-mcp-client-register",
        CancellationToken cancellationToken = default)
    {
        RequireLocalOnly(services);
        var command = services.GetRequiredService<IRegisterPublicMcpClient>();
        var result = await command.ExecuteAsync(
            new(
                DevelopmentAdministratorActor(),
                CreateMcpClientMetadata(
                    services.GetRequiredService<StaffMcpOAuthOptions>().Resource),
                "Register the deterministic DevelopmentOffline public MCP client.",
                operationKey),
            cancellationToken);
        if (!result.WasReplay)
        {
            await AppendClientEventAsync(
                services,
                operationKey,
                "development_mcp_client_registered",
                cancellationToken);
        }
    }

    public static async Task RevokeMcpClientAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        RequireLocalOnly(services);
        var command = services.GetRequiredService<IRevokePublicMcpClient>();
        var result = await command.ExecuteAsync(
            new(
                DevelopmentAdministratorActor(),
                DevelopmentMcpClientId,
                "Revoke the deterministic DevelopmentOffline public MCP client.",
                "development-mcp-client-revoke"),
            cancellationToken);
        if (!result.WasReplay)
        {
            await AppendClientEventAsync(
                services,
                "development-mcp-client-revoke",
                "development_mcp_client_revoked",
                cancellationToken);
        }
    }

    private static async Task EnsureIdentityAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var roleName in StaffRoleNames.All)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                ThrowIfFailed(await roleManager.CreateAsync(new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName
                }));
            }
            else if (!string.Equals(role.Name, roleName, StringComparison.Ordinal))
            {
                role.Name = roleName;
                ThrowIfFailed(await roleManager.UpdateAsync(role));
            }
        }

        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var userId = DevelopmentOfflineIdentity.AdministratorId.ToString("D");
        var normalizedUserName = userManager.NormalizeName(DevelopmentOfflineIdentity.UserName);
        var user = await userManager.FindByIdAsync(userId);
        var userWithReservedName = await userManager.FindByNameAsync(
            DevelopmentOfflineIdentity.UserName);
        if (userWithReservedName is not null
            && userWithReservedName.Id != DevelopmentOfflineIdentity.AdministratorId)
        {
            throw new InvalidOperationException(
                "The DevelopmentOffline user name is already assigned to another identity.");
        }

        if (user is null)
        {
            user = new PegasusIdentityUser
            {
                Id = DevelopmentOfflineIdentity.AdministratorId,
                UserName = DevelopmentOfflineIdentity.UserName,
                IsEnabled = true,
                MustChangePassword = false,
                LockoutEnabled = false,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            ThrowIfFailed(await userManager.CreateAsync(user));
        }
        else if (!string.Equals(
                     user.UserName,
                     DevelopmentOfflineIdentity.UserName,
                     StringComparison.Ordinal)
                 || !string.Equals(
                     user.NormalizedUserName,
                     normalizedUserName,
                     StringComparison.Ordinal)
                 || string.IsNullOrWhiteSpace(user.SecurityStamp)
                 || !user.IsEnabled
                 || user.MustChangePassword
                 || user.LockoutEnabled
                 || user.PasswordHash is not null
                 || user.TwoFactorEnabled
                 || user.LockoutEnd is not null
                 || user.AccessFailedCount != 0)
        {
            user.UserName = DevelopmentOfflineIdentity.UserName;
            user.IsEnabled = true;
            user.MustChangePassword = false;
            user.LockoutEnabled = false;
            user.PasswordHash = null;
            user.TwoFactorEnabled = false;
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            ThrowIfFailed(await userManager.UpdateAsync(user));
        }

        var roleNames = await userManager.GetRolesAsync(user);
        var rolesToRemove = roleNames
            .Where(roleName => !string.Equals(
                roleName,
                StaffRoleNames.Administrator,
                StringComparison.Ordinal))
            .ToArray();
        if (rolesToRemove.Length > 0)
        {
            ThrowIfFailed(await userManager.RemoveFromRolesAsync(user, rolesToRemove));
        }

        if (!roleNames.Contains(StaffRoleNames.Administrator, StringComparer.Ordinal))
        {
            ThrowIfFailed(await userManager.AddToRoleAsync(
                user,
                StaffRoleNames.Administrator));
        }
    }

    private static async Task EnsureQdosPrincipalAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var actor = DevelopmentAdministratorActor();
        var organization = await services.GetRequiredService<ICreateOrganization>().ExecuteAsync(
            new(
                "QDOS development fixture",
                [OrganizationRole.WorkProvider],
                actor,
                "development-qdos-organization"),
            cancellationToken);
        await services.GetRequiredService<ICreatePrincipal>().ExecuteAsync(
            new(
                organization.Id,
                QdosAlphaCaseActivationPolicy.PrincipalCode,
                actor,
                "development-qdos-principal"),
            cancellationToken);
    }

    private static Task AppendClientEventAsync(
        IServiceProvider services,
        string operationKey,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        var timeProvider = services.GetRequiredService<TimeProvider>();
        return services.GetRequiredService<ISecurityEventWriter>().AppendAsync(
            new(
                Guid.NewGuid(),
                SecurityEventType.Client,
                SecurityEventOutcome.Succeeded,
                DevelopmentMcpClientId,
                timeProvider.GetUtcNow(),
                operationKey,
                reasonCode),
            cancellationToken);
    }

    private static PublicMcpClientMetadata CreateMcpClientMetadata(Uri resource) =>
        new(
            DevelopmentMcpClientId,
            "Pegasus Development MCP client",
            [new Uri(DevelopmentMcpRedirectUri)],
            resource,
            [StaffMcpClientContract.ReadScope, StaffMcpClientContract.WriteScope]);

    private static ActionActor DevelopmentAdministratorActor() =>
        ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);

    private static void RequireLocalOnly(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var environment = services.GetRequiredService<IHostEnvironment>();
        var configuration = services.GetRequiredService<IConfiguration>();
        if (!environment.IsDevelopment()
            || !string.Equals(
                configuration["Runtime:Profile"],
                DevelopmentOfflineProfile,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "DevelopmentOffline initialization is a local test-fixture operation and requires " +
                "the Development environment with Runtime:Profile=DevelopmentOffline.");
        }
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            "DevelopmentOffline identity initialization failed without changing its deterministic contract.");
    }
}
