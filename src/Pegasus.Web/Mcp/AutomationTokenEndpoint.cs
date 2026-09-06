using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Pegasus.Core.Identity;
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
/// </summary>
internal static class AutomationTokenEndpoint
{
    public static async Task<IResult> ExchangeAsync(
        HttpContext httpContext,
        AutomationClientRegistry registry,
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
                    "automation_client_disabled"),
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
