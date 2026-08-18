using Microsoft.AspNetCore.Authorization;
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
        string productVersion)
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
                server.AllowClientCredentialsFlow();
                server.RegisterScopes([.. AutomationMcp.Scopes]);
                server.SetAccessTokenLifetime(AutomationMcp.AccessTokenLifetime);
                // This deployment has one always-on replica, so local keys are
                // sufficient for its short-lived client-credentials tokens.
                server.AddEphemeralEncryptionKey();
                server.AddEphemeralSigningKey();
                // TLS terminates at the Container Apps ingress
                // (allowInsecure: false); the app listens on plain HTTP behind
                // it, as does the in-process integration test server.
                server.UseAspNetCore()
                    .EnableTokenEndpointPassthrough()
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
            .WithTools<AssessmentMcpTools>();
        return services;
    }

    /// <summary>
    /// Maps the bearer-only automation surface: the client-credentials token
    /// endpoint and the streamable-HTTP MCP endpoint. A staff browser cookie
    /// is never accepted on <c>/mcp</c>: the endpoint policy authenticates
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
    }
}
