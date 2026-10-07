using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.PrincipalApi;

/// <summary>
/// Authenticates <c>Authorization: Bearer pgs_&lt;key id&gt;_&lt;secret&gt;</c>
/// through <see cref="IAuthenticatePrincipalCredential"/>. No
/// cookie, no session, no antiforgery: a staff browser cookie is never
/// accepted here and a principal secret is never accepted anywhere else.
/// Every refused presentation is a security event that names the key id
/// when one was well-formed and never the secret.
/// </summary>
internal sealed class PrincipalApiAuthenticationHandler(
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

        const string bearer = "Bearer ";
        var secret = header.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)
            ? header[bearer.Length..].Trim()
            : null;
        var keyId = PrincipalCredentialPolicy.KeyIdOf(secret);
        var credential = keyId is null
            ? null
            : await authenticate.ExecuteAsync(keyId, secret!, Context.RequestAborted);
        if (credential is null)
        {
            await DenyAsync(keyId ?? "anonymous", "principal_credential_rejected");
            return AuthenticateResult.Fail("The principal credential is not valid.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, credential.PrincipalId.ToString("D")),
                new Claim(PrincipalApi.PrincipalIdClaim, credential.PrincipalId.ToString("D")),
                new Claim(PrincipalApi.KeyIdClaim, credential.KeyId),
                new Claim(PrincipalApi.CredentialStateClaim, credential.State.ToString())
            ],
            Scheme.Name);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (string.IsNullOrEmpty(Request.Headers.Authorization.ToString()))
        {
            await DenyAsync("anonymous", "principal_credential_missing");
        }

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = $"Bearer realm=\"{PrincipalApi.Realm}\"";
        await Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "The principal credential is missing or not valid.")
            .ExecuteAsync(Context);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "The principal credential may not perform this operation.")
            .ExecuteAsync(Context);

    /// <summary>
    /// The credential the endpoint acts as, rebuilt from the ticket's claims
    /// so the Core use cases receive the same record the authentication
    /// decision produced. The endpoint policy admits only a ticket this
    /// handler built, so the claims are always present.
    /// </summary>
    internal static PrincipalCredentialAuthentication ReadCredential(ClaimsPrincipal user) =>
        new(
            Guid.Parse(user.FindFirstValue(PrincipalApi.PrincipalIdClaim)!),
            user.FindFirstValue(PrincipalApi.KeyIdClaim)!,
            Enum.Parse<PrincipalCredentialState>(user.FindFirstValue(PrincipalApi.CredentialStateClaim)!));

    /// <summary>
    /// A refusal here happens before any credential authenticates, so no
    /// Principal is established: the key id names what was presented, not who
    /// acted, and the event is deliberately left unattributed rather than
    /// recording a key as a principal. Readers label it by what it is.
    /// </summary>
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
