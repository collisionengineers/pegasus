using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web.Pages.Connect;

[AllowAnonymous]
[EnableRateLimiting("StaffMcpOAuth")]
public sealed class AuthorizeModel(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    UserManager<PegasusIdentityUser> userManager,
    SignInManager<PegasusIdentityUser> signInManager,
    StaffMcpOAuthOptions oauthOptions) : PageModel
{
    private static readonly Dictionary<string, string> ScopeLabels =
        new(StringComparer.Ordinal)
        {
            [OpenIddictConstants.Scopes.OpenId] = "Identify you as the signed-in Pegasus staff member",
            [OpenIddictConstants.Scopes.Profile] = "Read your Pegasus display name and current role",
            [OpenIddictConstants.Scopes.OfflineAccess] = "Continue for up to the eight-hour staff session limit",
            [StaffMcpOAuthOptions.ReadScope] = "Read authorised Pegasus case, intake and document information",
            [StaffMcpOAuthOptions.WriteScope] = "Perform authorised Pegasus staff actions"
        };

    public string ClientName { get; private set; } = string.Empty;

    public string Resource => oauthOptions.Resource.AbsoluteUri;

    public IReadOnlyList<RequestedScope> RequestedScopes { get; private set; } = [];
    public IReadOnlyList<KeyValuePair<string, string>> AuthorizationParameters { get; private set; } = [];


    public async Task<IActionResult> OnGetAsync()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            return NotFound();
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + Request.QueryString
                },
                IdentityConstants.ApplicationScheme);
        }

        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var application = await applicationManager.FindByClientIdAsync(
            request.ClientId!,
            HttpContext.RequestAborted);
        if (application is null)
        {
            return Reject(
                OpenIddictConstants.Errors.InvalidClient,
                "The OAuth client is not registered.");
        }

        ClientName = await applicationManager.GetDisplayNameAsync(
            application,
            HttpContext.RequestAborted)
            ?? request.ClientId!;
        RequestedScopes = request.GetScopes()
            .Select(scope => new RequestedScope(
                scope,
                ScopeLabels.TryGetValue(scope, out var label) ? label : scope))
            .OrderBy(scope => scope.Name, StringComparer.Ordinal)
            .ToArray();
        var authorizationParameters =
            new List<KeyValuePair<string, string>>(Request.Query.Count);
        foreach (var (name, values) in Request.Query)
        {
            if (name.Equals("decision", StringComparison.OrdinalIgnoreCase)
                || name.Equals(
                    "__RequestVerificationToken",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in values)
            {
                authorizationParameters.Add(
                    KeyValuePair.Create(name, value ?? string.Empty));
            }
        }

        AuthorizationParameters = authorizationParameters;


        return Page();
    }

    public async Task<IActionResult> OnPostAsync([FromForm] string decision)
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            return NotFound();
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + Request.QueryString
                },
                IdentityConstants.ApplicationScheme);
        }

        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        if (string.Equals(decision, "deny", StringComparison.Ordinal))
        {
            return Reject(
                OpenIddictConstants.Errors.AccessDenied,
                "The staff member denied this authorization request.");
        }

        if (!string.Equals(decision, "approve", StringComparison.Ordinal))
        {
            return Reject(
                OpenIddictConstants.Errors.InvalidRequest,
                "An explicit approve or deny decision is required.");
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsEnabled)
        {
            return Reject(
                OpenIddictConstants.Errors.AccessDenied,
                "The signed-in staff account is not available.");
        }

        var application = await applicationManager.FindByClientIdAsync(
            request.ClientId!,
            HttpContext.RequestAborted);
        if (application is null)
        {
            return Reject(
                OpenIddictConstants.Errors.InvalidClient,
                "The OAuth client is not registered.");
        }

        var applicationId = await applicationManager.GetIdAsync(
            application,
            HttpContext.RequestAborted)
            ?? throw new InvalidOperationException("The registered OAuth client has no persistent identifier.");
        var subject = await userManager.GetUserIdAsync(user);
        var scopes = request.GetScopes();
        var principal = await StaffMcpTokenPrincipal.CreateAsync(
            user,
            userManager,
            signInManager,
            scopes,
            [oauthOptions.Resource.AbsoluteUri]);

        object? authorization = null;
        await foreach (var candidate in authorizationManager.FindAsync(
            subject,
            applicationId,
            OpenIddictConstants.Statuses.Valid,
            OpenIddictConstants.AuthorizationTypes.Permanent,
            scopes,
            HttpContext.RequestAborted))
        {
            authorization = candidate;
            break;
        }

        authorization ??= await authorizationManager.CreateAsync(
            principal,
            subject,
            applicationId,
            OpenIddictConstants.AuthorizationTypes.Permanent,
            scopes,
            HttpContext.RequestAborted);
        principal.SetAuthorizationId(await authorizationManager.GetIdAsync(
            authorization,
            HttpContext.RequestAborted));

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private ForbidResult? ValidateRequest(OpenIddictRequest request)
    {
        var resources = request.GetResources();
        if (resources.Length != 1
            || !resources[0].Equals(oauthOptions.Resource.AbsoluteUri, StringComparison.Ordinal))
        {
            return Reject(
                OpenIddictConstants.Errors.InvalidTarget,
                "The exact Pegasus staff MCP resource is required.");
        }

        if (request.GetScopes().Any(scope => !ScopeLabels.ContainsKey(scope)))
        {
            return Reject(
                OpenIddictConstants.Errors.InvalidScope,
                "The authorization request contains an unsupported scope.");
        }

        return null;
    }

    private ForbidResult Reject(string error, string description) => Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
        }),
        OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

    public sealed record RequestedScope(string Name, string Description);
}

public sealed record StaffMcpOAuthOptions(Uri Issuer, Uri Resource)
{
    public const string ReadScope = "pegasus.mcp.read";
    public const string WriteScope = "pegasus.mcp.write";
}

internal static class StaffMcpTokenPrincipal
{
    public static async Task<ClaimsPrincipal> CreateAsync(
        PegasusIdentityUser user,
        UserManager<PegasusIdentityUser> userManager,
        SignInManager<PegasusIdentityUser> signInManager,
        IEnumerable<string> scopes,
        IEnumerable<string> resources)
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        principal.SetClaim(
            OpenIddictConstants.Claims.Subject,
            await userManager.GetUserIdAsync(user));
        principal.SetScopes(scopes);
        principal.SetResources(resources);
        principal.SetDestinations(claim => claim.Type switch
        {
            OpenIddictConstants.Claims.Subject =>
            [
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken
            ],
            OpenIddictConstants.Claims.Name or ClaimTypes.Name
                when principal.HasScope(OpenIddictConstants.Scopes.Profile) =>
            [
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken
            ],
            ClaimTypes.NameIdentifier or ClaimTypes.Role =>
                [OpenIddictConstants.Destinations.AccessToken],
            "AspNet.Identity.SecurityStamp" or "pegasus:original-issued-at" => [],
            _ => [OpenIddictConstants.Destinations.AccessToken]
        });
        return principal;
    }
}
