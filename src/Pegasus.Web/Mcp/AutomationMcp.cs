using Microsoft.Extensions.Configuration;

namespace Pegasus.Web.Mcp;

/// <summary>
/// Fixed names for the Automation Actor MCP ingress (ADR-0011, ADR-0013
/// clause 10): one named vendor-neutral Automation client, authenticated by
/// client credentials or, for an external connector, by authorization code
/// with PKCE (ADR-0027), per-area scopes, and a dedicated rate-limit policy.
/// The same authorization server also signs a member of staff in from
/// Pegasus Desktop, where that is configured (ADR-0092).
/// </summary>
public static class AutomationMcp
{
    public const string FeatureFlag = "Features:AutomationMcp";
    public const string AuthenticationScheme = "PegasusAutomationMcp";

    /// <summary>
    /// The single Automation client's operator-facing name. Registered as its
    /// OpenIddict display name (<see cref="AutomationClientRegistry"/>) and used
    /// wherever the client's raw subject id would otherwise be shown to an operator
    /// (Action logs and AI jobs).
    /// </summary>
    public const string ClientDisplayName = "Pegasus Automation Actor";

    /// <summary>The one server's <c>ServerInfo.Name</c> (ADR-0087).</summary>
    public const string ServerName = "pegasus-automation";
    public const string EndpointPolicy = "AutomationMcpEndpoint";
    public const string DocumentsEndpointPolicy = "AutomationMcpDocumentsEndpoint";
    public const string IntakeEndpointPolicy = "AutomationMcpIntakeEndpoint";
    public const string RateLimitPolicy = "AutomationMcp";
    public const string Audience = "pegasus-automation-mcp";
    public const string TokenEndpointPath = "/connect/token";
    public const string AuthorizationEndpointPath = "/authorize";

    /// <summary>
    /// RFC 7009: where Pegasus Desktop ends its sign-in. The Automation
    /// client holds no permission for it (ADR-0092).
    /// </summary>
    public const string RevocationEndpointPath = "/connect/revoke";

    /// <summary>
    /// Pegasus Desktop's client: public, native, and registered only where
    /// <c>AutomationMcp:DesktopRedirectUris</c> is configured (ADR-0092, R1297).
    /// Its tokens name the member of staff who signed in, never the
    /// Automation Actor.
    /// </summary>
    public const string DesktopClientId = "pegasus-desktop";

    /// <summary>The product's name, as the registration's display name.</summary>
    public const string DesktopClientDisplayName = "Pegasus Desktop";

    /// <summary>The one server's path (ADR-0087): every tool is served here.</summary>
    public const string McpEndpointPath = "/mcp";

    /// <summary>The original-bytes routes a download tool names as its <c>contentUrl</c>.</summary>
    public const string DocumentContentPath = "/automation/documents";
    public const string IntakeSourceContentPath = "/automation/intake-sources";

    /// <summary>RFC 9728: the server's metadata document, which lists every scope.</summary>
    public const string ResourceMetadataPath = "/.well-known/oauth-protected-resource/mcp";
    public const string CasesScope = "automation.cases";
    public const string IntakeScope = "automation.intake";
    public const string DocumentsScope = "automation.documents";
    public const string AssessmentScope = "automation.assessment";
    public const string MailScope = "automation.mail";
    public const string JobsScope = "automation.jobs";

    /// <summary>
    /// Outward sending: a report to the Principal, staff mail (new, reply,
    /// forward, the Triage reply) and a photo link by e-mail or text (R1039,
    /// R1040). Its own scope, so a grant can hold casework without sending
    /// (operator, 7 October 2026; ADR-0064).
    /// </summary>
    public const string SendScope = "automation.send";
    public const string GrantIdentityClaim = "pegasus_automation_grant";

    /// <summary>
    /// The security stamp the member of staff's account had when they signed
    /// in from Pegasus Desktop. Only a Pegasus Desktop token carries it, and
    /// the token is refused once the account's stamp has moved (ADR-0092).
    /// Codes, access tokens and refresh tokens are all encrypted, so the stamp
    /// is never disclosed to the application.
    /// </summary>
    public const string StaffStampClaim = "pegasus_staff_stamp";

    /// <summary>
    /// The security event reason of a token request from Pegasus Desktop's
    /// client that was refused. Not an <c>automation_</c> reason, so it is
    /// not read as Automation activity.
    /// </summary>
    public const string DesktopTokenRejected = "desktop_token_rejected";

    /// <summary>The security event reason of a Pegasus Desktop token refused at a tool or content route.</summary>
    public const string DesktopAccessDenied = "desktop_access_denied";

    /// <summary>
    /// The <c>HttpContext.Items</c> key the actor resolver sets on a request
    /// it refuses and records itself: a stopped client, once
    /// <c>automation_client_disabled</c> is written for it (R1097), and a
    /// Pegasus Desktop token, once <c>desktop_access_denied</c> is. That
    /// refusal writes the resolver's event only, so the transport denial is
    /// not recorded again.
    /// </summary>
    public const string RefusalRecordedItem = "Pegasus.AutomationMcp.RefusalRecorded";
    /// <summary>The rate limit's permits a minute, partitioned per IP address (ADR-0087).</summary>
    public const int RequestsPerIpAddressPerMinute = 120;
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(14);

    /// <summary>
    /// How long a sign-in from Pegasus Desktop lasts, measured from the
    /// sign-in and not extended by use (R1298).
    /// </summary>
    public static readonly TimeSpan DesktopSignInLifetime = TimeSpan.FromHours(8);

    public static IReadOnlyList<string> Scopes { get; } =
        [CasesScope, IntakeScope, DocumentsScope, AssessmentScope, MailScope, JobsScope, SendScope];

    /// <summary>
    /// The bearer-only automation surface a program calls: the token and
    /// revocation endpoints, the one server and the two content routes. The
    /// consent page at <see cref="AuthorizationEndpointPath"/> is a staff page
    /// and is not one.
    /// </summary>
    public static bool IsIngressPath(PathString path) =>
        path.StartsWithSegments(McpEndpointPath)
        || path.Equals(TokenEndpointPath, StringComparison.OrdinalIgnoreCase)
        || path.Equals(RevocationEndpointPath, StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments(DocumentContentPath)
        || path.StartsWithSegments(IntakeSourceContentPath);

    // The cross-tool rules FRD-10 states, as the server's instructions restate
    // them (ADR-0087 decision 1): agent-facing copy the
    // operator approved as built (R526); the take-over clause added 10 November 2026 (R901).
    private const string Paging = "Lists page by cursor, 50 by default and 100 at most.";
    private const string OneCommandLease =
        "A write takes expectedVersion and operationKey and accepts an editLeaseToken from "
        + "pegasus_edit_begin; without one it holds the record's edit lease for that one command.";
    private const string ExplicitLease =
        "pegasus_edit_begin, pegasus_edit_renew and pegasus_edit_end hold a record for multi-step work; "
        + "pegasus_edit_begin with takeOver asks the member of staff editing a Case to hand it over.";
    private const string NativeContent =
        "A download returns the file as content: an image as an image, a PDF as its page text, "
        + "a small text file as text, and anything else as metadata and an authenticated URL for "
        + "the original bytes.";

    /// <summary>
    /// The one server's <c>ServerInstructions</c>: the four approved sentences,
    /// restating FRD-10's cross-tool rules and making none.
    /// </summary>
    public const string Instructions = $"{Paging} {OneCommandLease} {ExplicitLease} {NativeContent}";
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
    IReadOnlyList<Uri> DesktopRedirectUris,
    bool UseDevelopmentKeys,
    IReadOnlyList<Uri> SigningCertificateSecretUris,
    IReadOnlyList<Uri> EncryptionCertificateSecretUris)
{
    /// <summary>The server's RFC 8707 resource: <c>/mcp</c> on the public origin.</summary>
    public Uri ResourceUri => new(PublicOrigin, AutomationMcp.McpEndpointPath);

    /// <summary>
    /// The authorization-code flow for external connectors exists only when
    /// an administrator has configured at least one exact redirect URI.
    /// </summary>
    public bool ConnectorAuthorizationEnabled => RedirectUris.Count > 0;

    /// <summary>
    /// Sign-in from Pegasus Desktop is offered only where its redirect URIs
    /// are configured (R1297). No deployment definition sets them, so
    /// production offers none.
    /// </summary>
    public bool DesktopSignInEnabled => DesktopRedirectUris.Count > 0;

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
        if (string.Equals(clientId, AutomationMcp.DesktopClientId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"AutomationMcp:ClientId cannot be '{AutomationMcp.DesktopClientId}', which names Pegasus Desktop's client.");
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
        foreach (var candidate in SplitUris(configuration["AutomationMcp:RedirectUris"]))
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

        // Pegasus Desktop's redirect URIs (comma or semicolon separated):
        // plain http on a loopback IP literal, as RFC 8252 section 7.3 asks of
        // a native application, never a host name. An entry without a port
        // admits any port the application listens on; one with a port admits
        // that port only. Absent means sign-in from Pegasus Desktop is not
        // offered (R1297).
        var desktopRedirectUris = new List<Uri>();
        foreach (var candidate in SplitUris(configuration["AutomationMcp:DesktopRedirectUris"]))
        {
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var redirectUri)
                || redirectUri.Scheme != Uri.UriSchemeHttp
                || redirectUri.HostNameType is not (UriHostNameType.IPv4 or UriHostNameType.IPv6)
                || !redirectUri.IsLoopback
                || redirectUri.UserInfo.Length != 0
                || !string.IsNullOrEmpty(redirectUri.Fragment))
            {
                throw new InvalidOperationException(
                    "AutomationMcp:DesktopRedirectUris entries must be absolute http URIs on a loopback IP address (127.0.0.1 or [::1]) without a fragment.");
            }

            desktopRedirectUris.Add(redirectUri);
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
            desktopRedirectUris,
            useDevelopmentKeys,
            signingCertificates,
            encryptionCertificates);
    }

    private static readonly char[] UriSeparators = [',', ';'];

    private static string[] SplitUris(string? configured) =>
        (configured ?? string.Empty).Split(
            UriSeparators,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
