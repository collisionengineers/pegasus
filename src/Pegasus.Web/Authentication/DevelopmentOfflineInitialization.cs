using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
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
        if (context.Database.IsSqlite())
        {
            await DevelopmentSqliteBaselineGuard.ValidateAsync(context, cancellationToken);
        }

        await context.Database.MigrateAsync(cancellationToken);
    }

    public static async Task RegisterMcpClientAsync(
        IServiceProvider services,
        string correlationId = "development-mcp-client-command",
        CancellationToken cancellationToken = default)
    {
        RequireLocalOnly(services);
        var applicationManager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var resource = services.GetRequiredService<StaffMcpOAuthOptions>().Resource;
        var descriptor = CreateMcpClientDescriptor(resource);
        var application = await applicationManager.FindByClientIdAsync(
            DevelopmentMcpClientId,
            cancellationToken);
        var changed = false;
        if (application is null)
        {
            await applicationManager.CreateAsync(descriptor, cancellationToken);
            changed = true;
        }
        else if (!await MatchesAsync(
                     applicationManager,
                     application,
                     descriptor,
                     cancellationToken))
        {
            await applicationManager.UpdateAsync(application, descriptor, cancellationToken);
            changed = true;
        }

        if (changed)
        {
            await AppendClientEventAsync(
                services,
                correlationId,
                "development_mcp_client_registered",
                cancellationToken);
        }
    }

    public static async Task RevokeMcpClientAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        RequireLocalOnly(services);
        var applicationManager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await applicationManager.FindByClientIdAsync(
            DevelopmentMcpClientId,
            cancellationToken);
        if (application is null)
        {
            return;
        }

        await applicationManager.DeleteAsync(application, cancellationToken);
        await AppendClientEventAsync(
            services,
            "development-mcp-client-command",
            "development_mcp_client_revoked",
            cancellationToken);
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

    private static OpenIddictApplicationDescriptor CreateMcpClientDescriptor(Uri resource)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = DevelopmentMcpClientId,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            DisplayName = "Pegasus Development MCP client"
        };
        descriptor.RedirectUris.Add(new Uri(DevelopmentMcpRedirectUri));
        descriptor.Permissions.UnionWith(
        [
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Revocation,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            OpenIddictConstants.Permissions.Scopes.Profile,
            OpenIddictConstants.Permissions.Prefixes.Scope + StaffMcpOAuthOptions.ReadScope,
            OpenIddictConstants.Permissions.Prefixes.Scope + StaffMcpOAuthOptions.WriteScope
        ]);
        descriptor.AddResourcePermissions(resource.AbsoluteUri);
        descriptor.Requirements.Add(
            OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
        return descriptor;
    }

    private static async Task<bool> MatchesAsync(
        IOpenIddictApplicationManager applicationManager,
        object application,
        OpenIddictApplicationDescriptor expected,
        CancellationToken cancellationToken)
    {
        var current = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(current, application, cancellationToken);
        return string.Equals(current.ClientId, expected.ClientId, StringComparison.Ordinal)
            && string.Equals(current.ClientType, expected.ClientType, StringComparison.Ordinal)
            && string.Equals(current.ConsentType, expected.ConsentType, StringComparison.Ordinal)
            && string.Equals(current.DisplayName, expected.DisplayName, StringComparison.Ordinal)
            && string.IsNullOrEmpty(current.ClientSecret)
            && current.RedirectUris.SetEquals(expected.RedirectUris)
            && current.PostLogoutRedirectUris.Count == 0
            && current.Permissions.SetEquals(expected.Permissions)
            && current.Requirements.SetEquals(expected.Requirements);
    }

    private static async Task AppendClientEventAsync(
        IServiceProvider services,
        string correlationId,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        var securityEvents = services.GetRequiredService<ISecurityEventWriter>();
        await securityEvents.AppendAsync(
            new(
                Guid.NewGuid(),
                SecurityEventType.Client,
                SecurityEventOutcome.Succeeded,
                DevelopmentMcpClientId,
                services.GetRequiredService<TimeProvider>().GetUtcNow(),
                correlationId,
                reasonCode),
            cancellationToken);
    }

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
            "DevelopmentOffline identity initialization failed: " +
            string.Join(
                "; ",
                result.Errors.Select(error => $"{error.Code}: {error.Description}")));
    }
}
