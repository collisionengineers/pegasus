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
/// Client-credentials token issuance for the single Automation client.
/// OpenIddict has already authenticated the client id and secret against the
/// seeded registration before this passthrough handler runs; the handler
/// re-checks the Administrator kill switch, then issues a short-lived access
/// token carrying the granted per-area scopes and the fixed MCP audience.
/// Routine successful issuance stays content-safe telemetry; denials write
/// security events.
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
        if (!request.IsClientCredentialsGrantType())
        {
            return Forbid(
                Errors.UnsupportedGrantType,
                "Only the client-credentials grant is supported.");
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

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            Claims.Name,
            Claims.Role);
        identity.SetClaim(Claims.Subject, clientId);
        identity.SetScopes(request.GetScopes());
        identity.SetResources(AutomationMcp.Audience);
        identity.SetDestinations(_ => [Destinations.AccessToken]);
        return Results.SignIn(
            new ClaimsPrincipal(identity),
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
