using Pegasus.Core.Cases;

namespace Pegasus.Web.ProviderApi;

/// <summary>
/// Fixed names for the composition-gated Provider API (API-01, ADR-0004):
/// one versioned machine surface, one dedicated bearer scheme that accepts a
/// Principal credential and nothing else, and a per-key rate-limit policy.
/// </summary>
public static class ProviderApi
{
    public const string FeatureFlag = "Features:ProviderApi";
    public const string AuthenticationScheme = "PegasusProviderApi";
    public const string EndpointPolicy = "ProviderApiEndpoint";
    public const string RateLimitPolicy = "ProviderApi";
    public const string BasePath = "/api/provider/v1";
    public const string SubmissionsPath = BasePath + "/submissions";
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string ProviderReferenceField = "providerReference";
    public const string FilesField = "files";
    public const string Realm = "pegasus-provider-api";
    public const int RequestsPerKeyPerMinute = 60;

    public const string PrincipalIdClaim = "pegasus:principal_id";
    public const string KeyIdClaim = "pegasus:key_id";
    public const string CredentialStateClaim = "pegasus:credential_state";

    /// <summary>
    /// The key id embedded in a presented <c>Bearer pgs_&lt;key id&gt;_…</c>
    /// secret, or null when the header does not carry a well-shaped secret.
    /// Shape only — the one parser shared by the authentication handler and
    /// the rate limiter, so both partition on the same identity.
    /// </summary>
    public static string? TryReadKeyId(string? authorizationHeader)
    {
        const string bearer = "Bearer ";
        if (authorizationHeader is null
            || !authorizationHeader.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var secret = authorizationHeader[bearer.Length..].Trim();
        if (secret.Length != PrincipalCredentialPolicy.SecretLength)
        {
            return null;
        }

        var keyId = secret.Substring(
            PrincipalCredentialPolicy.SecretPrefix.Length,
            PrincipalCredentialPolicy.KeyIdLength);
        return PrincipalCredentialPolicy.IsWellFormed(keyId, secret) ? keyId : null;
    }

    public static string? TryReadSecret(string? authorizationHeader)
    {
        const string bearer = "Bearer ";
        return authorizationHeader is not null
            && authorizationHeader.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[bearer.Length..].Trim()
            : null;
    }
}
