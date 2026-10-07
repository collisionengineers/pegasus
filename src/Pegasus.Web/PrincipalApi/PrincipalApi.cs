namespace Pegasus.Web.PrincipalApi;

/// <summary>
/// Fixed names for the composition-gated Principal API (API-01, ADR-0004):
/// one versioned machine surface, one dedicated bearer scheme that accepts a
/// Principal credential and nothing else, and a rate-limit policy partitioned
/// by calling address — the limiter runs before authentication, so the
/// presented key id is a claim and cannot be the partition.
/// </summary>
public static class PrincipalApi
{
    public const string FeatureFlag = "Features:PrincipalApi";
    public const string AuthenticationScheme = "PegasusPrincipalApi";
    public const string EndpointPolicy = "PrincipalApiEndpoint";
    public const string RateLimitPolicy = "PrincipalApi";
    public const string BasePath = "/api/principal/v1";
    public const string SubmissionsPath = BasePath + "/submissions";
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string Realm = "pegasus-principal-api";
    public const int RequestsPerCallerPerMinute = 60;

    public const string PrincipalIdClaim = "pegasus:principal_id";
    public const string KeyIdClaim = "pegasus:key_id";
    public const string CredentialStateClaim = "pegasus:credential_state";
}
