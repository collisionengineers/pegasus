using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.ProviderApi;

/// <summary>
/// Authenticates <c>Authorization: Bearer pgs_&lt;key id&gt;_&lt;secret&gt;</c>
/// through <see cref="IAuthenticatePrincipalCredential"/> (TICK-061). No
/// cookie, no session, no antiforgery: a staff browser cookie is never
/// accepted here and a provider secret is never accepted anywhere else.
/// Every refused presentation is a security event that names the key id
/// when one was well-formed and never the secret.
/// </summary>
internal sealed class ProviderApiAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAuthenticatePrincipalCredential authenticate,
    ISecurityEventWriter securityEvents,
    TimeProvider timeProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header))
        {
            return AuthenticateResult.NoResult();
        }

        var keyId = ProviderApi.TryReadKeyId(header);
        var secret = ProviderApi.TryReadSecret(header);
        var credential = keyId is null || secret is null
            ? null
            : await authenticate.ExecuteAsync(keyId, secret, Context.RequestAborted);
        if (credential is null)
        {
            await DenyAsync(keyId ?? "anonymous", "provider_credential_rejected");
            return AuthenticateResult.Fail("The provider credential is not valid.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, credential.PrincipalId.ToString("D")),
                new Claim(ProviderApi.PrincipalIdClaim, credential.PrincipalId.ToString("D")),
                new Claim(ProviderApi.KeyIdClaim, credential.KeyId),
                new Claim(ProviderApi.CredentialStateClaim, credential.State.ToString())
            ],
            Scheme.Name);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (string.IsNullOrEmpty(Request.Headers.Authorization.ToString()))
        {
            await DenyAsync("anonymous", "provider_credential_missing");
        }

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"Bearer realm=\"{ProviderApi.Realm}\"";
        await Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "The provider credential is missing or not valid.")
            .ExecuteAsync(Context);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "The provider credential may not perform this operation.")
            .ExecuteAsync(Context);

    /// <summary>
    /// The credential the endpoint acts as, rebuilt from the ticket's claims
    /// so the Core use cases receive the same record the authentication
    /// decision produced.
    /// </summary>
    internal static PrincipalCredentialAuthentication? ReadCredential(ClaimsPrincipal user)
    {
        var principalId = user.FindFirstValue(ProviderApi.PrincipalIdClaim);
        var keyId = user.FindFirstValue(ProviderApi.KeyIdClaim);
        var state = user.FindFirstValue(ProviderApi.CredentialStateClaim);
        return Guid.TryParse(principalId, out var id)
            && keyId is not null
            && Enum.TryParse<PrincipalCredentialState>(state, out var parsedState)
            ? new(id, keyId, parsedState)
            : null;
    }

    private Task DenyAsync(string subjectId, string reasonCode) =>
        securityEvents.AppendAsync(
            new SecurityEvent(
                Guid.NewGuid(),
                SecurityEventType.Token,
                SecurityEventOutcome.Denied,
                subjectId,
                timeProvider.GetUtcNow(),
                Context.TraceIdentifier,
                reasonCode),
            Context.RequestAborted);
}
