using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Pegasus.Web.Mcp;

/// <summary>
/// Token issuance for the single Automation client. OpenIddict has already
/// authenticated the client id and secret against the seeded registration
/// (and, for the authorization-code and refresh grants, validated the code or
/// refresh token, the redirect URI and the PKCE verifier) before this
/// passthrough handler runs; the handler re-checks the Administrator kill
/// switch, then issues a short-lived access token carrying the granted
/// per-area scopes and the fixed MCP audience. Every grant yields the same
/// principal shape — subject is the client id — so the actor resolver and the
/// tool authorization treat connector tokens exactly like client-credentials
/// tokens. Routine successful issuance stays content-safe telemetry; denials
/// write security events.
/// <para>
/// Pegasus Desktop's client is the one exception (ADR-0092): its code and its
/// refresh token name the member of staff who signed in, and each exchange
/// reads that account again before a token is issued.
/// </para>
/// </summary>
internal static class AutomationTokenEndpoint
{
    public static async Task<IResult> ExchangeAsync(
        HttpContext httpContext,
        AutomationClientRegistry registry,
        EfStaffAccountQueries staffAccounts,
        ISecurityEventWriter securityEvents,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var request = httpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException(
                "The OpenIddict server request is unavailable.");
        var connectorGrant = request.IsAuthorizationCodeGrantType()
            || request.IsRefreshTokenGrantType();
        if (!request.IsClientCredentialsGrantType() && !connectorGrant)
        {
            return Forbid(
                Errors.UnsupportedGrantType,
                "Only the client-credentials, authorization-code and refresh-token grants are supported.");
        }

        var clientId = request.ClientId
            ?? throw new InvalidOperationException(
                "The authenticated token request is missing its client identifier.");
        if (registry.IsDesktop(clientId))
        {
            return connectorGrant
                ? await ExchangeDesktopAsync(
                    httpContext,
                    request.IsRefreshTokenGrantType(),
                    staffAccounts,
                    timeProvider,
                    cancellationToken)
                : Forbid(
                    Errors.UnauthorizedClient,
                    "Only the authorization-code and refresh-token grants are permitted for this client.");
        }

        if (!await registry.IsEnabledAsync(clientId, cancellationToken))
        {
            await securityEvents.AppendAsync(
                new SecurityEvent(
                    Guid.NewGuid(),
                    SecurityEventType.Client,
                    SecurityEventOutcome.Denied,
                    clientId,
                    timeProvider.GetUtcNow(),
                    httpContext.TraceIdentifier,
                    "automation_client_disabled")
                    .By(ActionActor.Automation(clientId)),
                cancellationToken);
            return Forbid(
                Errors.UnauthorizedClient,
                "The Automation client registration is disabled.");
        }

        IEnumerable<string> scopes;
        string grantId;
        if (connectorGrant)
        {
            // The scopes were fixed at consent; the code/refresh-token
            // principal carries them and OpenIddict has already validated it.
            var granted = await httpContext.AuthenticateAsync(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var principal = granted.Principal;
            if (principal is null
                || !string.Equals(principal.GetClaim(Claims.Subject), clientId, StringComparison.Ordinal))
            {
                return Forbid(Errors.InvalidGrant, "The authorization is no longer valid.");
            }

            scopes = principal.GetScopes();
            grantId = principal.GetClaim(AutomationMcp.GrantIdentityClaim) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(grantId))
            {
                return Forbid(
                    Errors.InvalidGrant,
                    "The connector authorization identity is missing.");
            }
        }
        else
        {
            scopes = request.GetScopes();
            grantId = clientId;
        }

        return Results.SignIn(
            AutomationPrincipal.Create(clientId, grantId, scopes),
            properties: null,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// A Pegasus Desktop code or refresh token becomes tokens only while the
    /// account it names could still sign in to Pegasus: enabled, holding a
    /// staff role, not waiting on a password change, and with the security
    /// stamp it had at the sign-in. A disable, a role change, a password
    /// reset and a forced sign-out each move the stamp, so each ends the
    /// sign-in here (R1298). The transport records the refusal.
    /// </summary>
    private static async Task<IResult> ExchangeDesktopAsync(
        HttpContext httpContext,
        bool renewal,
        EfStaffAccountQueries staffAccounts,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var granted = await httpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = granted.Principal;
        var grantId = principal?.GetClaim(AutomationMcp.GrantIdentityClaim);
        if (principal is null
            || !DesktopPrincipal.IsDesktop(principal)
            || string.IsNullOrWhiteSpace(grantId)
            || !Guid.TryParse(principal.GetClaim(Claims.Subject), out var staffId))
        {
            return Forbid(Errors.InvalidGrant, "The authorization is no longer valid.");
        }

        var account = await staffAccounts.GetAccountReadAsync(staffId, cancellationToken);
        var stamp = principal.GetClaim(AutomationMcp.StaffStampClaim);
        if (account is not { IsEnabled: true, MustChangePassword: false, Role: not null }
            || string.IsNullOrEmpty(stamp)
            || !StaffPrincipalValidator.StampMatches(account.SecurityStamp, stamp))
        {
            return Forbid(Errors.InvalidGrant, "The authorization is no longer valid.");
        }

        var issued = DesktopPrincipal.Create(staffId, stamp, grantId, principal.GetScopes());
        if (renewal && principal.GetExpirationDate() is { } signInEnds)
        {
            // A renewed refresh token keeps the sign-in's end, and the access
            // token issued with it does not outlive it.
            var remaining = signInEnds - timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                return Forbid(Errors.InvalidGrant, "The authorization is no longer valid.");
            }
            if (remaining < AutomationMcp.AccessTokenLifetime)
            {
                issued.SetAccessTokenLifetime(remaining);
            }
        }

        return Results.SignIn(
            issued,
            properties: null,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IResult Forbid(string error, string description) =>
        Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}

/// <summary>
/// The one principal shape every Automation grant issues: subject is the
/// client id, the granted per-area scopes (plus <c>offline_access</c> when a
/// refresh token is wanted), the fixed MCP audience, and access-token
/// destinations. Refresh tokens are issued only from the connector flow, so
/// client-credentials tokens keep their previous shape.
/// </summary>
internal static class AutomationPrincipal
{
    public static ClaimsPrincipal Create(
        string clientId,
        string grantId,
        IEnumerable<string> scopes)
    {
        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            Claims.Name,
            Claims.Role);
        identity.SetClaim(Claims.Subject, clientId);
        identity.SetClaim(AutomationMcp.GrantIdentityClaim, grantId);
        identity.SetScopes(scopes);
        identity.SetResources(AutomationMcp.Audience);
        identity.SetDestinations(_ => [Destinations.AccessToken]);
        return new ClaimsPrincipal(identity);
    }
}

/// <summary>
/// The principal a Pegasus Desktop sign-in issues (ADR-0092): subject is the
/// member of staff's id, never a client id; its grant is the sign-in's own
/// identity; and it carries the account's security stamp, which every later
/// check compares with the account. Audience and destinations are the
/// Automation principal's. Its refresh token lasts
/// <see cref="AutomationMcp.DesktopSignInLifetime"/> from the sign-in, and
/// because the server's refresh tokens do not slide, a renewal keeps that end.
/// </summary>
internal static class DesktopPrincipal
{
    public static ClaimsPrincipal Create(
        Guid staffId,
        string securityStamp,
        string grantId,
        IEnumerable<string> scopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);
        ArgumentException.ThrowIfNullOrWhiteSpace(grantId);
        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            Claims.Name,
            Claims.Role);
        identity.SetClaim(Claims.Subject, staffId.ToString("D"));
        identity.SetClaim(AutomationMcp.GrantIdentityClaim, grantId);
        identity.SetClaim(AutomationMcp.StaffStampClaim, securityStamp);
        identity.SetScopes(scopes);
        identity.SetResources(AutomationMcp.Audience);
        identity.SetDestinations(_ => [Destinations.AccessToken]);
        var principal = new ClaimsPrincipal(identity);
        principal.SetRefreshTokenLifetime(AutomationMcp.DesktopSignInLifetime);
        return principal;
    }

    /// <summary>The identity of one sign-in, kept across its renewals.</summary>
    public static string NewGrantId() => $"desktop:{Guid.NewGuid():N}";

    /// <summary>
    /// Whether a validated principal came from a Pegasus Desktop sign-in:
    /// only its tokens carry the member of staff's security stamp.
    /// </summary>
    public static bool IsDesktop(ClaimsPrincipal principal) =>
        principal.HasClaim(claim => claim.Type == AutomationMcp.StaffStampClaim);
}
