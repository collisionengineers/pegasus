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

    /// <summary>
    /// The single Automation client's operator-facing name. Registered as its
    /// OpenIddict display name (<see cref="AutomationClientRegistry"/>) and used
    /// wherever the client's raw subject id would otherwise be shown to an operator
    /// (the Automation activity view).
    /// </summary>
    public const string ClientDisplayName = "Pegasus Automation Actor";
    public const string EndpointPolicy = "AutomationMcpEndpoint";
    public const string DocumentsEndpointPolicy = "AutomationMcpDocumentsEndpoint";
    public const string RateLimitPolicy = "AutomationMcp";
    public const string Audience = "pegasus-automation-mcp";
    public const string TokenEndpointPath = "/connect/token";
    public const string AuthorizationEndpointPath = "/authorize";
    public const string McpEndpointPath = "/mcp";
    public const string ResourceMetadataPath = "/.well-known/oauth-protected-resource/mcp";
    public const string CasesScope = "automation.cases";
    public const string IntakeScope = "automation.intake";
    public const string DocumentsScope = "automation.documents";
    public const string AssessmentScope = "automation.assessment";
    public const string MailScope = "automation.mail";
    public const string JobsScope = "automation.jobs";
    public const string GrantIdentityClaim = "pegasus_automation_grant";
    public const int RequestsPerClientPerMinute = 120;
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(14);

    public static IReadOnlyList<string> Scopes { get; } =
        [CasesScope, IntakeScope, DocumentsScope, AssessmentScope, MailScope, JobsScope];
}

/// <summary>
/// Composition-time options for the gated Automation MCP ingress. The whole
/// surface stays absent unless <c>Features:AutomationMcp</c> is enabled, and
/// required configuration is valid. The gate is off by default, so the whole
/// surface remains absent until an explicitly configured deployment enables it.
/// </summary>
public sealed record AutomationMcpOptions(
    string ClientId,
    string ClientSecret,
    Uri PublicOrigin,
    TimeSpan RegistrationCacheLifetime,
    IReadOnlyList<Uri> RedirectUris,
    bool UseDevelopmentKeys,
    Uri? KeyVaultUri,
    IReadOnlyList<Uri> SigningCertificateSecretUris,
    IReadOnlyList<Uri> EncryptionCertificateSecretUris)
{
    public Uri ResourceUri => new(PublicOrigin, AutomationMcp.McpEndpointPath);

    /// <summary>
    /// The authorization-code flow for external connectors exists only when
    /// an administrator has configured at least one exact redirect URI.
    /// </summary>
    public bool ConnectorAuthorizationEnabled => RedirectUris.Count > 0;

    public static AutomationMcpOptions? TryCreate(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!configuration.GetValue<bool>(AutomationMcp.FeatureFlag))
        {
            return null;
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

        // Exact redirect URIs for external MCP connectors (comma or semicolon
        // separated). https only, except loopback for local evidence runs; no
        // fragment. Absent means the authorization-code flow is not offered.
        var redirectUris = new List<Uri>();
        foreach (var candidate in (configuration["AutomationMcp:RedirectUris"] ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var redirectUri)
                || !string.IsNullOrEmpty(redirectUri.Fragment)
                || (redirectUri.Scheme != Uri.UriSchemeHttps
                    && !(redirectUri.Scheme == Uri.UriSchemeHttp && redirectUri.IsLoopback)))
            {
                throw new InvalidOperationException(
                    "AutomationMcp:RedirectUris entries must be absolute https URIs (http only for loopback) without a fragment.");
            }

            redirectUris.Add(redirectUri);
        }

        var production = string.Equals(
            configuration["Runtime:Profile"], "Production", StringComparison.Ordinal);
        var useDevelopmentKeys = configuration.GetValue<bool>(
            "AutomationMcp:UseDevelopmentKeys");
        if (production && useDevelopmentKeys)
        {
            throw new InvalidOperationException(
                "AutomationMcp:UseDevelopmentKeys cannot be enabled in Production.");
        }
        Uri? keyVaultUri = null;
        var configuredVault = configuration["AutomationMcp:KeyVaultUri"];
        if (!string.IsNullOrWhiteSpace(configuredVault)
            && (!Uri.TryCreate(configuredVault, UriKind.Absolute, out keyVaultUri)
                || keyVaultUri.Scheme != Uri.UriSchemeHttps
                || !keyVaultUri.Host.EndsWith(".vault.azure.net", StringComparison.OrdinalIgnoreCase)
                || !keyVaultUri.IsDefaultPort
                || keyVaultUri.UserInfo.Length != 0
                || keyVaultUri.AbsolutePath != "/"
                || keyVaultUri.Query.Length != 0
                || keyVaultUri.Fragment.Length != 0))
        {
            throw new InvalidOperationException("AutomationMcp:KeyVaultUri must be an Azure Key Vault HTTPS origin.");
        }
        var signingCertificates = LoadSecretUris(
            configuration.GetSection("AutomationMcp:SigningCertificateSecretUris").Get<string[]>(),
            "AutomationMcp:SigningCertificateSecretUris", keyVaultUri);
        var encryptionCertificates = LoadSecretUris(
            configuration.GetSection("AutomationMcp:EncryptionCertificateSecretUris").Get<string[]>(),
            "AutomationMcp:EncryptionCertificateSecretUris", keyVaultUri);
        if (!useDevelopmentKeys
            && (keyVaultUri is null || signingCertificates.Count == 0 || encryptionCertificates.Count == 0))
        {
            throw new InvalidOperationException(
                "Separate AutomationMcp signing and encryption certificates are required.");
        }

        return new(
            clientId,
            clientSecret,
            publicOrigin,
            TimeSpan.FromSeconds(cacheSeconds),
            redirectUris,
            useDevelopmentKeys,
            keyVaultUri,
            signingCertificates,
            encryptionCertificates);
    }

    private static List<Uri> LoadSecretUris(
        IReadOnlyList<string>? configured,
        string configurationKey,
        Uri? keyVaultUri)
    {
        var secretUris = new List<Uri>();
        foreach (var value in configured ?? [])
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !uri.Host.EndsWith(".vault.azure.net", StringComparison.OrdinalIgnoreCase)
                || !uri.IsDefaultPort
                || uri.UserInfo.Length != 0
                || keyVaultUri is null
                || !string.Equals(uri.Host, keyVaultUri.Host, StringComparison.OrdinalIgnoreCase)
                || uri.Query.Length != 0
                || uri.Fragment.Length != 0
                || uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) is not
                    ["secrets", { Length: > 0 }, { Length: > 0 }])
            {
                throw new InvalidOperationException(
                    $"{configurationKey} entries must be exact versioned Azure Key Vault secret URIs.");
            }
            secretUris.Add(uri);
        }
        return secretUris;
    }
}
