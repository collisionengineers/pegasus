using Microsoft.AspNetCore.Authorization;
using Azure.Core;
using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server;
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
        services.AddHttpContextAccessor();
        services.AddScoped<AutomationClientRegistry>();
        services.AddScoped<AutomationActorResolver>();
        services.AddScoped<AutomationMcpAuditor>();
        services.AddScoped<AutomationEditLease>();

        if (!options.UseDevelopmentKeys)
        {
            // Key Vault is a remote read behind a managed-identity token, so the
            // certificates load after the port binds, not while services are
            // composed. Registered before OpenIddict so the credentials are in
            // the options before the server's own configuration reads them; the
            // store's gate keeps any request from building the options earlier.
            var managedCredential = credential ?? throw new InvalidOperationException(
                "A managed-identity credential is required for Automation OAuth certificates.");
            var store = new OAuthCertificateStore();
            services.AddSingleton(store);
            services.AddHostedService(provider => new OAuthCertificateLoadService(
                store,
                () => KeyVaultOAuthCertificateLoader.Load(
                    managedCredential,
                    options.SigningCertificateSecretUris,
                    options.EncryptionCertificateSecretUris),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(30),
                provider.GetRequiredService<ILogger<OAuthCertificateLoadService>>()));
            services.Configure<OpenIddictServerOptions>(store.AddTo);
        }

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
                // authorization requests: one per server.
                server.RegisterResources(
                    [.. AutomationMcp.Servers.Select(row => options.ResourceUri(row).AbsoluteUri)]);
                server.SetAccessTokenLifetime(AutomationMcp.AccessTokenLifetime);
                server.SetRefreshTokenLifetime(AutomationMcp.RefreshTokenLifetime);
                // A hard cap: a connector re-consents at least fortnightly.
                server.DisableSlidingRefreshTokenExpiration();
                if (options.UseDevelopmentKeys)
                {
                    server.AddEncryptionCertificate(CreateIsolatedCertificate("Pegasus MCP development encryption"));
                    server.AddSigningCertificate(CreateIsolatedCertificate("Pegasus MCP development signing"));
                }
                // Outside development keys the certificates join the options
                // once OAuthCertificateLoadService has read them (registered
                // above). TLS terminates at the App Service front end (the site is
                // HTTPS only); the app listens on plain HTTP behind it, as
                // does the in-process integration test server.
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
                    // No fixed document: a challenge names the document under
                    // the requested path, and each server's document lists
                    // only its own scopes.
                    mcpOptions.ResourceMetadataUri = null;
                    mcpOptions.Events.OnResourceMetadataRequest = new ServerResourceMetadata(options).Serve;
                });
        var authorization = services.AddAuthorizationBuilder();
        foreach (var row in AutomationMcp.Servers)
        {
            authorization.AddPolicy(row.EndpointPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(AutomationMcp.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                    context.User.GetAudiences().Contains(
                        AutomationMcp.Audience,
                        StringComparer.Ordinal)
                    && row.Scopes.Any(context.User.HasScope));
            });
        }

        authorization
            .AddPolicy(AutomationMcp.DocumentsEndpointPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(AutomationMcp.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                    context.User.GetAudiences().Contains(
                        AutomationMcp.Audience,
                        StringComparer.Ordinal)
                    && context.User.HasScope(AutomationMcp.DocumentsScope));
            })
            .AddPolicy(AutomationMcp.IntakeEndpointPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(AutomationMcp.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                    context.User.GetAudiences().Contains(
                        AutomationMcp.Audience,
                        StringComparer.Ordinal)
                    && context.User.HasScope(AutomationMcp.IntakeScope));
            });

        // One registration serves every server: each request takes the name,
        // instructions and tools of the server its endpoint names.
        var toolListing = new AutomationToolListing();
        var sessions = new AutomationMcpSessions(productVersion);
        services.AddMcpServer()
            .WithHttpTransport(transport =>
            {
                transport.Stateless = true;
                transport.ConfigureSessionOptions = sessions.Configure;
            })
            .WithRequestFilters(filters => filters.AddListToolsFilter(toolListing.Filter))
            .WithTools<CaseMcpTools>()
            .WithTools<CaseEditMcpTools>()
            .WithTools<CaseLifecycleMcpTools>()
            .WithTools<CaseQueryMcpTools>()
            .WithTools<EditLeaseMcpTools>()
            .WithTools<IntakeMcpTools>()
            .WithTools<DocumentMcpTools>()
            .WithTools<DocumentUploadMcpTools>()
            .WithTools<DocumentActionMcpTools>()
            .WithTools<ReportMcpTools>()
            .WithTools<ReportPreparationMcpTools>()
            .WithTools<AssessmentMcpTools>()
            .WithTools<MailMcpTools>()
            .WithTools<UnidentifiedMcpTools>()
            .WithTools<TriageMcpTools>()
            .WithTools<AiJobMcpTools>()
            .WithTools<IntakeActionMcpTools>()
            .WithTools<WorkCentreMcpTools>()
            .WithTools<SendMcpTools>();
        return services;
    }

    private const string DocumentContentPath = "/automation/documents";
    private const string IntakeSourceContentPath = "/automation/intake-sources";

    /// <summary>
    /// The RFC 9728 documents, one per server and built once: a server's lists
    /// only its own scopes. The document under a content route's path is the
    /// document of the server whose scope the route requires, so the route's
    /// 401 still leads to one; any other path has none.
    /// </summary>
    private sealed class ServerResourceMetadata
    {
        private readonly FrozenDictionary<string, ProtectedResourceMetadata> byPath;
        private readonly (PathString Prefix, ProtectedResourceMetadata Metadata)[] contentRoutes;

        public ServerResourceMetadata(AutomationMcpOptions options)
        {
            var documents = AutomationMcp.Servers.ToDictionary(
                row => row,
                row => new ProtectedResourceMetadata
                {
                    Resource = options.ResourceUri(row).AbsoluteUri,
                    AuthorizationServers = { options.PublicOrigin.AbsoluteUri },
                    ScopesSupported = [.. row.Scopes],
                    ResourceName = "Pegasus Automation MCP"
                });
            byPath = documents.ToFrozenDictionary(
                pair => pair.Key.Path, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            contentRoutes =
            [
                (DocumentContentPath, Of(AutomationMcp.DocumentsScope)),
                (IntakeSourceContentPath, Of(AutomationMcp.IntakeScope))
            ];

            ProtectedResourceMetadata Of(string scope) =>
                documents[AutomationMcp.Servers.First(row => row.Scopes.Contains(scope))];
        }

        public Task Serve(ResourceMetadataRequestContext context)
        {
            // The handler calls this only under the metadata prefix.
            context.Request.Path.StartsWithSegments(
                AutomationMcp.ResourceMetadataPathPrefix, StringComparison.OrdinalIgnoreCase, out var resource);
            var metadata = byPath.GetValueOrDefault(resource.Value?.TrimEnd('/') ?? string.Empty);
            for (var index = 0; metadata is null && index < contentRoutes.Length; index++)
            {
                if (resource.StartsWithSegments(contentRoutes[index].Prefix, StringComparison.OrdinalIgnoreCase))
                {
                    metadata = contentRoutes[index].Metadata;
                }
            }

            if (metadata is null)
            {
                context.SkipHandler();
            }
            else
            {
                context.ResourceMetadata = metadata;
            }

            return Task.CompletedTask;
        }
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
    /// credentials, authorization code, refresh), one streamable-HTTP MCP
    /// endpoint per server at <c>/mcp/&lt;word&gt;</c>, whose GET and DELETE answer
    /// 405 because the transport is stateless, and the two original-bytes routes
    /// the download tools name as <c>contentUrl</c>. The Administrator consent
    /// page at <c>/authorize</c> is a Razor Page (staff cookie), not mapped here.
    /// A staff browser cookie is never accepted on a server: its endpoint policy
    /// authenticates exclusively with the automation bearer scheme, and an
    /// unauthenticated call receives 401 with WWW-Authenticate naming that
    /// server's resource metadata.
    /// </summary>
    public static void MapPegasusAutomationMcp(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapPost(AutomationMcp.TokenEndpointPath, AutomationTokenEndpoint.ExchangeAsync)
            .AllowAnonymous()
            .RequireRateLimiting(AutomationMcp.RateLimitPolicy);
        foreach (var row in AutomationMcp.Servers)
        {
            app.MapMcp(row.Path)
                .WithMetadata(row)
                .RequireAuthorization(row.EndpointPolicy)
                .RequireRateLimiting(AutomationMcp.RateLimitPolicy);
            // A stateless MapMcp maps POST only; without these, GET and DELETE
            // fall to the staff fallback policy and its sign-in redirect.
            app.MapMethods(
                    row.Path,
                    [HttpMethods.Get, HttpMethods.Delete],
                    context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                        context.Response.Headers.Allow = HttpMethods.Post;
                        return Task.CompletedTask;
                    })
                .RequireAuthorization(row.EndpointPolicy)
                .RequireRateLimiting(AutomationMcp.RateLimitPolicy);
        }

        app.MapGet(
                DocumentContentPath + "/{occurrenceId:guid}/versions/{versionId:guid}",
                AutomationDocumentStreaming.GetAsync)
            .RequireAuthorization(AutomationMcp.DocumentsEndpointPolicy)
            .RequireRateLimiting(AutomationMcp.RateLimitPolicy);
        app.MapGet(
                IntakeSourceContentPath + "/{receiptId:guid}",
                AutomationDocumentStreaming.GetIntakeSourceAsync)
            .RequireAuthorization(AutomationMcp.IntakeEndpointPolicy)
            .RequireRateLimiting(AutomationMcp.RateLimitPolicy);
    }
}
