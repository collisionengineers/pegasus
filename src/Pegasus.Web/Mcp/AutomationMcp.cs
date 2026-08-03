using Microsoft.Extensions.Configuration;

namespace Pegasus.Web.Mcp;

/// <summary>
/// Fixed names for the Automation Actor MCP ingress (ADR-0011, ADR-0013
/// clause 10): one named vendor-neutral Automation client, client-credentials
/// authentication, per-area scopes, and a dedicated rate-limit policy.
/// </summary>
public static class AutomationMcp
{
    public const string FeatureFlag = "Features:AutomationMcp";
    public const string AuthenticationScheme = "PegasusAutomationMcp";
    public const string EndpointPolicy = "AutomationMcpEndpoint";
    public const string RateLimitPolicy = "AutomationMcp";
    public const string Audience = "pegasus-automation-mcp";
    public const string TokenEndpointPath = "/connect/token";
    public const string McpEndpointPath = "/mcp";
    public const string ResourceMetadataPath = "/.well-known/oauth-protected-resource/mcp";
    public const string CasesScope = "automation.cases";
    public const string IntakeScope = "automation.intake";
    public const string DocumentsScope = "automation.documents";
    public const int RequestsPerClientPerMinute = 120;
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(10);

    public static IReadOnlyList<string> Scopes { get; } =
        [CasesScope, IntakeScope, DocumentsScope];
}

/// <summary>
/// Composition-time options for the gated Automation MCP ingress. The whole
/// surface stays absent unless <c>Features:AutomationMcp</c> is enabled, and
/// enabling it outside the DevelopmentOffline runtime profile fails closed:
/// production activation remains separately approved work.
/// </summary>
public sealed record AutomationMcpOptions(
    string ClientId,
    string ClientSecret,
    Uri PublicOrigin,
    TimeSpan RegistrationCacheLifetime)
{
    public Uri ResourceUri => new(PublicOrigin, AutomationMcp.McpEndpointPath);

    public static AutomationMcpOptions? TryCreate(
        IConfiguration configuration,
        bool developmentOfflineProfile)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!configuration.GetValue<bool>(AutomationMcp.FeatureFlag))
        {
            return null;
        }
        if (!developmentOfflineProfile)
        {
            throw new InvalidOperationException(
                $"{AutomationMcp.FeatureFlag} requires the DevelopmentOffline runtime profile.");
        }

        var clientId = configuration["AutomationMcp:ClientId"]?.Trim();
        if (string.IsNullOrWhiteSpace(clientId) || clientId.Length > 100)
        {
            throw new InvalidOperationException(
                "AutomationMcp:ClientId is required and cannot exceed 100 characters.");
        }

        // The secret comes from configuration/user-secrets only; it is never
        // tracked, logged, or shown after registration.
        var clientSecret = configuration["AutomationMcp:ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientSecret) || clientSecret.Length < 32)
        {
            throw new InvalidOperationException(
                "AutomationMcp:ClientSecret is required and must be at least 32 characters.");
        }

        var configuredOrigin = configuration["AutomationMcp:PublicOrigin"];
        if (!Uri.TryCreate(configuredOrigin, UriKind.Absolute, out var publicOrigin)
            || (publicOrigin.Scheme != Uri.UriSchemeHttps && publicOrigin.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(publicOrigin.Query)
            || publicOrigin.AbsolutePath != "/")
        {
            throw new InvalidOperationException(
                "AutomationMcp:PublicOrigin must be an absolute http(s) origin without path or query.");
        }

        var cacheSeconds = configuration.GetValue<double?>(
            "AutomationMcp:RegistrationCacheSeconds") ?? 5;
        if (cacheSeconds is < 0 or > 60)
        {
            throw new InvalidOperationException(
                "AutomationMcp:RegistrationCacheSeconds must be between 0 and 60.");
        }

        return new(
            clientId,
            clientSecret,
            publicOrigin,
            TimeSpan.FromSeconds(cacheSeconds));
    }
}
