using Microsoft.Extensions.Caching.Memory;
using OpenIddict.Abstractions;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Mcp;

public sealed record AutomationClientStatus(
    string ClientId,
    bool IsRegistered,
    bool IsEnabled,
    string? DisplayName,
    IReadOnlyList<string> GrantedScopes);

/// <summary>
/// Owns the single seeded Automation client registration and its
/// Administrator-held kill switch. The enabled state is the presence of the
/// client-credentials grant permission on the OpenIddict application: while
/// disabled the token endpoint refuses new tokens outright, and because the
/// per-request check below is cached for seconds (not minutes) and access
/// tokens are short-lived, a disable takes immediate effect for tokens that
/// were already issued.
/// </summary>
public sealed class AutomationClientRegistry(
    IOpenIddictApplicationManager applications,
    IMemoryCache cache,
    IActionHistoryWriter actionHistory,
    TimeProvider timeProvider,
    AutomationMcpOptions options)
{
    private static readonly TimeSpan EnsureLifetime = TimeSpan.FromHours(24);
    private const string DisplayName = "Pegasus Automation Actor";

    private string EnabledCacheKey => $"automation-mcp:enabled:{options.ClientId}";

    private string EnsuredCacheKey => $"automation-mcp:ensured:{options.ClientId}";

    /// <summary>
    /// Seeds or reconciles the single Automation client registration from
    /// configuration. Idempotent; preserves an Administrator-set disabled
    /// state across reconciliations.
    /// </summary>
    public async Task EnsureRegisteredAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(EnsuredCacheKey, out _))
        {
            return;
        }

        var application = await applications.FindByClientIdAsync(
            options.ClientId,
            cancellationToken);
        if (application is null)
        {
            await applications.CreateAsync(
                CanonicalDescriptor(enabled: true),
                cancellationToken);
        }
        else
        {
            var enabled = await applications.HasPermissionAsync(
                application,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                cancellationToken);
            await applications.UpdateAsync(
                application,
                CanonicalDescriptor(enabled),
                cancellationToken);
        }

        cache.Set(EnsuredCacheKey, value: true, EnsureLifetime);
        cache.Remove(EnabledCacheKey);
    }

    /// <summary>
    /// The per-request kill-switch check. Unknown client identities are
    /// disabled; the result is cached only for the configured seconds.
    /// </summary>
    public async Task<bool> IsEnabledAsync(string clientId, CancellationToken cancellationToken)
    {
        if (!string.Equals(clientId, options.ClientId, StringComparison.Ordinal))
        {
            return false;
        }
        if (cache.TryGetValue<bool>(EnabledCacheKey, out var cached))
        {
            return cached;
        }

        var application = await applications.FindByClientIdAsync(clientId, cancellationToken);
        var enabled = application is not null
            && await applications.HasPermissionAsync(
                application,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                cancellationToken);
        if (options.RegistrationCacheLifetime > TimeSpan.Zero)
        {
            cache.Set(EnabledCacheKey, enabled, options.RegistrationCacheLifetime);
        }

        return enabled;
    }

    public async Task<AutomationClientStatus> GetStatusAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        await EnsureRegisteredAsync(cancellationToken);
        var application = await applications.FindByClientIdAsync(
            options.ClientId,
            cancellationToken);
        if (application is null)
        {
            return new(options.ClientId, false, false, null, []);
        }

        var permissions = await applications.GetPermissionsAsync(application, cancellationToken);
        var scopes = permissions
            .Where(permission => permission.StartsWith(
                OpenIddictConstants.Permissions.Prefixes.Scope,
                StringComparison.Ordinal))
            .Select(permission =>
                permission[OpenIddictConstants.Permissions.Prefixes.Scope.Length..])
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new(
            options.ClientId,
            IsRegistered: true,
            IsEnabled: permissions.Contains(
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                StringComparer.Ordinal),
            await applications.GetDisplayNameAsync(application, cancellationToken),
            scopes);
    }

    /// <summary>
    /// The Administrator enable/disable action. Attributable permanent
    /// history is written for every request and the cached enabled state is
    /// dropped so the change takes effect on the next automation request.
    /// </summary>
    public async Task<AutomationClientStatus> SetEnabledAsync(
        bool enabled,
        ActionActor actor,
        string reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);

        await EnsureRegisteredAsync(cancellationToken);
        var application = await applications.FindByClientIdAsync(
            options.ClientId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The Automation client registration is unavailable.");
        var currentlyEnabled = await applications.HasPermissionAsync(
            application,
            OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
            cancellationToken);
        if (currentlyEnabled != enabled)
        {
            await applications.UpdateAsync(
                application,
                CanonicalDescriptor(enabled),
                cancellationToken);
        }

        cache.Remove(EnabledCacheKey);
        await actionHistory.AppendAsync(
            new ActionHistoryEntry(
                Guid.NewGuid(),
                "automation_client",
                options.ClientId,
                enabled ? "automation_client_enabled" : "automation_client_disabled",
                actor,
                timeProvider.GetUtcNow(),
                currentlyEnabled == enabled ? "Unchanged" : "Succeeded",
                operationKey.Trim(),
                reason.Trim()),
            cancellationToken);
        return await GetStatusAsync(actor, cancellationToken);
    }

    private OpenIddictApplicationDescriptor CanonicalDescriptor(bool enabled)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = options.ClientId,
            ClientSecret = options.ClientSecret,
            DisplayName = DisplayName,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit
        };
        foreach (var scope in AutomationMcp.Scopes)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }
        if (enabled)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
        }

        return descriptor;
    }
}
