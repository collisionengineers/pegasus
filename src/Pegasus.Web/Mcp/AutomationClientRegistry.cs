using Microsoft.Extensions.Caching.Memory;
using OpenIddict.Abstractions;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

public sealed record AutomationClientStatus(
    string ClientId,
    bool IsRegistered,
    bool IsEnabled,
    string? DisplayName,
    IReadOnlyList<string> GrantedScopes);

/// <summary>
/// Owns the single seeded Automation client registration and its
/// Administrator-held kill switch, and Pegasus Desktop's registration, which
/// exists only as configuration says (ADR-0092). The enabled state is the presence of the
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
    IAutomationSwitchStore automationSwitch,
    TimeProvider timeProvider,
    AutomationMcpOptions options)
{
    private static readonly TimeSpan EnsureLifetime = TimeSpan.FromHours(24);

    /// <summary>The action-history area of a Pegasus Desktop sign-in decision.</summary>
    public const string DesktopAggregateType = "pegasus_desktop";

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

        await EnsureDesktopRegisteredAsync(cancellationToken);
        cache.Set(EnsuredCacheKey, value: true, EnsureLifetime);
        cache.Remove(EnabledCacheKey);
    }

    /// <summary>
    /// Pegasus Desktop's registration follows configuration (R1297): with
    /// redirect URIs configured it is created or reconciled; without them a
    /// registration left by an earlier configuration is rewritten to permit
    /// nothing, because the Web's database role may not delete one. With
    /// neither, there is no such client.
    /// </summary>
    private async Task EnsureDesktopRegisteredAsync(CancellationToken cancellationToken)
    {
        var application = await applications.FindByClientIdAsync(
            AutomationMcp.DesktopClientId,
            cancellationToken);
        if (application is not null)
        {
            await applications.UpdateAsync(application, DesktopDescriptor(), cancellationToken);
        }
        else if (options.DesktopSignInEnabled)
        {
            await applications.CreateAsync(DesktopDescriptor(), cancellationToken);
        }
    }

    /// <summary>
    /// Whether <paramref name="clientId"/> is Pegasus Desktop's client in a
    /// deployment that offers its sign-in. The kill switch
    /// (<see cref="IsEnabledAsync"/>) is the Automation client's alone.
    /// </summary>
    public bool IsDesktop(string? clientId) =>
        options.DesktopSignInEnabled
        && string.Equals(clientId, AutomationMcp.DesktopClientId, StringComparison.Ordinal);

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
    /// Stop also releases every lease Automation holds; the history entry
    /// records what was released. The grant change, the release and the
    /// history row are one transaction (<see cref="IAutomationSwitchStore"/>),
    /// and the cache drops only once it has committed.
    /// </summary>
    public async Task<AutomationClientStatus> SetEnabledAsync(
        bool enabled,
        ActionActor actor,
        string? reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);

        await EnsureRegisteredAsync(cancellationToken);
        await automationSwitch.SetAsync(
            new(options.ClientId, enabled, actor, reason, operationKey),
            async token =>
            {
                var application = await applications.FindByClientIdAsync(options.ClientId, token)
                    ?? throw new InvalidOperationException(
                        "The Automation client registration is unavailable.");
                await applications.UpdateAsync(application, CanonicalDescriptor(enabled), token);
            },
            cancellationToken);
        cache.Remove(EnabledCacheKey);
        return await GetStatusAsync(actor, cancellationToken);
    }

    private OpenIddictApplicationDescriptor CanonicalDescriptor(bool enabled)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = options.ClientId,
            ClientSecret = options.ClientSecret,
            DisplayName = AutomationMcp.ClientDisplayName,
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
        if (enabled && options.ConnectorAuthorizationEnabled)
        {
            // Authorization code + PKCE for external connectors, with refresh
            // tokens so a connector re-authenticates without a new consent
            // every ten minutes. Redirect URIs are exact and administrator
            // managed; disabling the client removes these grants with the rest.
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            descriptor.Permissions.Add(
                OpenIddictConstants.Permissions.Prefixes.Resource + options.ResourceUri.AbsoluteUri);
            descriptor.Requirements.Add(
                OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
            foreach (var redirectUri in options.RedirectUris)
            {
                descriptor.RedirectUris.Add(redirectUri);
            }
        }

        return descriptor;
    }

    /// <summary>
    /// Pegasus Desktop's client (ADR-0092): public and native, so it holds no
    /// secret and its loopback redirect may carry any port where the
    /// configured URI names none (RFC 8252 section 7.3). It may ask for an
    /// authorisation code with PKCE, renew its token and revoke it, and it
    /// holds every scope: what a call may do is decided by the member of
    /// staff's own rights, not by a consent. Without configuration it permits
    /// nothing.
    /// </summary>
    private OpenIddictApplicationDescriptor DesktopDescriptor()
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = AutomationMcp.DesktopClientId,
            DisplayName = AutomationMcp.DesktopClientDisplayName,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ApplicationType = OpenIddictConstants.ApplicationTypes.Native,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit
        };
        if (!options.DesktopSignInEnabled)
        {
            return descriptor;
        }

        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
        descriptor.Permissions.Add(
            OpenIddictConstants.Permissions.Prefixes.Resource + options.ResourceUri.AbsoluteUri);
        foreach (var scope in AutomationMcp.Scopes)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }

        descriptor.Requirements.Add(
            OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
        foreach (var redirectUri in options.DesktopRedirectUris)
        {
            descriptor.RedirectUris.Add(redirectUri);
        }

        return descriptor;
    }

    /// <summary>
    /// A member of staff's decision on the Pegasus Desktop sign-in page is
    /// permanent history under their own name: the sign-in's grant and where
    /// the application was listening, or the refusal.
    /// </summary>
    public Task RecordDesktopSignInAsync(
        ActionActor actor,
        string grantId,
        Uri redirectUri,
        bool approved,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(grantId);
        ArgumentNullException.ThrowIfNull(redirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        return actionHistory.AppendAsync(
            new ActionHistoryEntry(
                Guid.NewGuid(),
                DesktopAggregateType,
                grantId,
                approved ? "desktop_sign_in" : "desktop_sign_in_refused",
                actor,
                timeProvider.GetUtcNow(),
                approved ? "Succeeded" : "Denied",
                operationKey.Trim(),
                redirectUri.GetLeftPart(UriPartial.Authority)),
            cancellationToken);
    }

    /// <summary>
    /// The Administrator's consent decision for an external connector is
    /// permanent history: who authorised which redirect target for which
    /// scopes, or refused it.
    /// </summary>
    public Task RecordConnectorDecisionAsync(
        ActionActor actor,
        string grantId,
        Uri redirectUri,
        IReadOnlyCollection<string> scopes,
        bool approved,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(grantId);
        ArgumentNullException.ThrowIfNull(redirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        return actionHistory.AppendAsync(
            new ActionHistoryEntry(
                Guid.NewGuid(),
                "automation_client",
                grantId,
                approved ? "automation_connector_authorized" : "automation_connector_denied",
                actor,
                timeProvider.GetUtcNow(),
                approved ? "Succeeded" : "Denied",
                operationKey.Trim(),
                $"Connector {redirectUri.GetLeftPart(UriPartial.Authority)}; scopes: {string.Join(' ', scopes)}"),
            cancellationToken);
    }
}
