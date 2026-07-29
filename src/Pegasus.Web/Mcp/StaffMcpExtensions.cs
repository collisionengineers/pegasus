using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Pegasus.Web.Pages.Connect;

namespace Pegasus.Web.Mcp;

public static class StaffMcpExtensions
{
    private const string McpAuthenticationScheme = "PegasusStaffMcp";

    public static IServiceCollection AddPegasusStaffMcp(
        this IServiceCollection services,
        StaffMcpOAuthOptions oauthOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(oauthOptions);

        services.AddHttpContextAccessor();
        services.AddScoped<StaffMcpActorResolver>();
        services.AddScoped<IAuthorizationHandler, CurrentStaffAuthorizationHandler>();
        services.AddAuthentication()
            .AddMcp(
                McpAuthenticationScheme,
                displayName: "Pegasus staff MCP",
                options =>
                {
                    options.ForwardAuthenticate =
                        OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                    options.ResourceMetadataUri = new Uri(
                        "/.well-known/oauth-protected-resource/mcp",
                        UriKind.Relative);
                    options.ResourceMetadata = new()
                    {
                        Resource = oauthOptions.Resource.AbsoluteUri,
                        AuthorizationServers = { oauthOptions.Issuer.AbsoluteUri },
                        ScopesSupported =
                        [
                            StaffMcpPolicies.ReadScope,
                            StaffMcpPolicies.WriteScope
                        ],
                        ResourceName = "Pegasus staff MCP"
                    };
                });
        services.AddAuthorizationBuilder()
            .AddPolicy(
                StaffMcpPolicies.Endpoint,
                policy =>
                {
                    policy.AddAuthenticationSchemes(McpAuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                    policy.RequireAssertion(context =>
                        context.User.GetAudiences().Contains(
                            oauthOptions.Resource.AbsoluteUri,
                            StringComparer.Ordinal)
                        && (context.User.HasScope(StaffMcpPolicies.ReadScope)
                            || context.User.HasScope(StaffMcpPolicies.WriteScope)));
                    policy.AddRequirements(new CurrentStaffRequirement());
                });

        services.AddMcpServer()
            .WithTools<IntakeMcpTools>()
            .WithTools<AddressMcpTools>()
            .WithTools<TriageMcpTools>()
            .WithTools<EvaMcpTools>()
            .WithTools<DocumentMcpTools>()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            });

        return services;
    }

    public static IEndpointConventionBuilder MapPegasusStaffMcp(
        this IEndpointRouteBuilder endpoints,
        string rateLimitPolicy)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(rateLimitPolicy);
        return endpoints.MapMcp("/mcp")
            .RequireAuthorization(StaffMcpPolicies.Endpoint)
            .RequireRateLimiting(rateLimitPolicy);
    }
}
