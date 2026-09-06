using Microsoft.AspNetCore.Authorization;
using Azure.Core;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ModelContextProtocol.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web.Mcp;

/// <summary>
/// Composition for the configuration-gated Automation Actor MCP ingress.
/// Nothing here is registered unless <c>Features:AutomationMcp</c> enabled it
/// at startup; the application otherwise keeps failing closed by exposing no
/// such ingress.
/// </summary>
public static class AutomationMcpExtensions
{
    public static IServiceCollection AddPegasusAutomationMcp(
        this IServiceCollection services,
        AutomationMcpOptions options,
        string productVersion,
        TokenCredential? credential)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(productVersion);

        services.AddSingleton(options);
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddScoped<AutomationClientRegistry>();
        services.AddScoped<AutomationActorResolver>();
        services.AddScoped<AutomationMcpAuditor>();

        services.AddOpenIddict()
            .AddCore(core => core
                .UseEntityFrameworkCore()
                .UseDbContext<PegasusDbContext>())
            .AddServer(server =>
            {
                server.SetTokenEndpointUris(AutomationMcp.TokenEndpointPath);
                server.SetAuthorizationEndpointUris(AutomationMcp.AuthorizationEndpointPath);
                server.AllowClientCredentialsFlow();
                // External MCP connectors (for example the Claude.ai remote
                // connector) obtain tokens by authorization code + PKCE with an
                // Administrator consent step; the client registration only
                // receives that grant when redirect URIs are configured.
                server.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
                server.AllowRefreshTokenFlow();
                server.RegisterScopes([.. AutomationMcp.Scopes]);
                // MCP clients name the protected resource (RFC 8707) in their
                // authorization requests; the only valid one is this /mcp.
                server.RegisterResources(options.ResourceUri.AbsoluteUri);
                server.SetAccessTokenLifetime(AutomationMcp.AccessTokenLifetime);
                server.SetRefreshTokenLifetime(AutomationMcp.RefreshTokenLifetime);
                // A hard cap: a connector re-consents at least fortnightly.
                server.DisableSlidingRefreshTokenExpiration();
                if (options.UseDevelopmentKeys)
                {
                    server.AddEncryptionCertificate(CreateIsolatedCertificate("Pegasus MCP development encryption"));
                    server.AddSigningCertificate(CreateIsolatedCertificate("Pegasus MCP development signing"));
                }
                else
                {
                    var certificates = KeyVaultOAuthCertificateLoader.Load(
                        credential ?? throw new InvalidOperationException(
                            "A managed-identity credential is required for Automation OAuth certificates."),
                        options.SigningCertificateSecretUris,
                        options.EncryptionCertificateSecretUris);
                    foreach (var certificate in certificates.Encryption)
                    {
                        server.AddEncryptionCertificate(certificate);
                    }
                    foreach (var certificate in certificates.Signing)
                    {
                        server.AddSigningCertificate(certificate);
                    }
                }
                // TLS terminates at the Container Apps ingress
                // (allowInsecure: false); the app listens on plain HTTP behind
                // it, as does the in-process integration test server.
                server.UseAspNetCore()
                    .EnableTokenEndpointPassthrough()
                    .EnableAuthorizationEndpointPassthrough()
                    .DisableTransportSecurityRequirement();
            })
            .AddValidation(validation =>
            {
                validation.UseLocalServer();
                validation.UseAspNetCore();
                validation.AddAudiences(AutomationMcp.Audience);
            });

        services.AddAuthentication()
            .AddMcp(
                AutomationMcp.AuthenticationScheme,
                displayName: "Pegasus Automation MCP",
                mcpOptions =>
                {
                    mcpOptions.ForwardAuthenticate =
                        OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                    mcpOptions.ResourceMetadataUri = new Uri(
                        AutomationMcp.ResourceMetadataPath,
                        UriKind.Relative);
                    mcpOptions.ResourceMetadata = new()
                    {
                        Resource = options.ResourceUri.AbsoluteUri,
                        AuthorizationServers = { options.PublicOrigin.AbsoluteUri },
                        ScopesSupported = [.. AutomationMcp.Scopes],
                        ResourceName = "Pegasus Automation MCP"
                    };
                });
        services.AddAuthorizationBuilder()
            .AddPolicy(AutomationMcp.EndpointPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(AutomationMcp.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                    context.User.GetAudiences().Contains(
                        AutomationMcp.Audience,
                        StringComparer.Ordinal)
                    && AutomationMcp.Scopes.Any(context.User.HasScope));
            })
            .AddPolicy(AutomationMcp.DocumentsEndpointPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(AutomationMcp.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                    context.User.GetAudiences().Contains(
                        AutomationMcp.Audience,
                        StringComparer.Ordinal)
                    && context.User.HasScope(AutomationMcp.DocumentsScope));
            });

        services.AddMcpServer(server => server.ServerInfo = new()
            {
                Name = "pegasus-automation",
                Version = productVersion
            })
            .WithHttpTransport(transport => transport.Stateless = true)
            .WithTools<CaseMcpTools>()
            .WithTools<IntakeMcpTools>()
            .WithTools<DocumentMcpTools>()
            .WithTools<AssessmentMcpTools>()
            .WithTools<MailMcpTools>()
            .WithTools<UnidentifiedMcpTools>()
            .WithTools<TriageMcpTools>()
            .WithTools<AiJobMcpTools>();
        return services;
    }

    private static X509Certificate2 CreateIsolatedCertificate(string subject)
    {
        using var key = RSA.Create(3072);
        var request = new CertificateRequest(
            $"CN={subject}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        using var generated = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(20));
        return X509CertificateLoader.LoadPkcs12(
            generated.Export(X509ContentType.Pkcs12),
            null,
            X509KeyStorageFlags.EphemeralKeySet);
    }

    /// <summary>
    /// Maps the bearer-only automation surface: the token endpoint (client
    /// credentials, authorization code, refresh) and the streamable-HTTP MCP
    /// endpoint. The Administrator consent page at <c>/authorize</c> is a
    /// Razor Page (staff cookie), not mapped here. A staff browser cookie is
    /// never accepted on <c>/mcp</c>: the endpoint policy authenticates
    /// exclusively with the automation bearer scheme, and an unauthenticated
    /// call receives 401 with WWW-Authenticate resource-metadata discovery.
    /// </summary>
    public static void MapPegasusAutomationMcp(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapPost(AutomationMcp.TokenEndpointPath, AutomationTokenEndpoint.ExchangeAsync)
            .AllowAnonymous()
            .RequireRateLimiting(AutomationMcp.RateLimitPolicy);
        app.MapMcp(AutomationMcp.McpEndpointPath)
            .RequireAuthorization(AutomationMcp.EndpointPolicy)
            .RequireRateLimiting(AutomationMcp.RateLimitPolicy);
        app.MapGet(
                "/automation/documents/{occurrenceId:guid}/versions/{versionId:guid}",
                AutomationDocumentStreaming.GetAsync)
            .RequireAuthorization(AutomationMcp.DocumentsEndpointPolicy)
            .RequireRateLimiting(AutomationMcp.RateLimitPolicy);
        app.MapGet(
                "/automation/document-exports",
                AutomationDocumentStreaming.GetExportAsync)
            .RequireAuthorization(AutomationMcp.DocumentsEndpointPolicy)
            .RequireRateLimiting(AutomationMcp.RateLimitPolicy);
    }
}
