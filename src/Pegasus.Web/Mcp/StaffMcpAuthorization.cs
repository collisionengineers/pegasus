using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Pages.Connect;

namespace Pegasus.Web.Mcp;

internal static class StaffMcpPolicies
{
    public const string Endpoint = "StaffMcpEndpoint";
    public const string ReadScope = StaffMcpOAuthOptions.ReadScope;
    public const string WriteScope = StaffMcpOAuthOptions.WriteScope;
}

internal sealed class CurrentStaffRequirement : IAuthorizationRequirement;

internal sealed class CurrentStaffAuthorizationHandler(
    UserManager<PegasusIdentityUser> userManager)
    : AuthorizationHandler<CurrentStaffRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CurrentStaffRequirement requirement)
    {
        var subjectId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.GetClaim(OpenIddictConstants.Claims.Subject);
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return;
        }

        var user = await userManager.FindByIdAsync(subjectId);
        if (user is null || !user.IsEnabled)
        {
            return;
        }

        var roleNames = await userManager.GetRolesAsync(user);
        if (!StaffActorFactory.TryCreate(subjectId, roleNames, out var actor)
            || !StaffAuthorization.IsAuthorized(actor, StaffAccessRight.PerformCasework))
        {
            return;
        }

        context.Succeed(requirement);
    }
}

internal sealed class StaffMcpAuthorizationException(string message)
    : ModelContextProtocol.McpException(message);

internal sealed record StaffMcpActor(ActionActor Actor, string HistoryActor);

internal sealed class StaffMcpActorResolver(
    IHttpContextAccessor httpContextAccessor,
    UserManager<PegasusIdentityUser> userManager)
{
    public async Task<StaffMcpActor> RequireAsync(
        string requiredScope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true || !principal.HasScope(requiredScope))
        {
            throw new StaffMcpAuthorizationException(
                $"The '{requiredScope}' OAuth scope is required for this tool.");
        }

        var subjectId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.GetClaim(OpenIddictConstants.Claims.Subject);
        var user = string.IsNullOrWhiteSpace(subjectId)
            ? null
            : await userManager.FindByIdAsync(subjectId);
        if (user is null || !user.IsEnabled)
        {
            throw new StaffMcpAuthorizationException(
                "The staff authorization is no longer valid.");
        }

        var roleNames = await userManager.GetRolesAsync(user);
        if (!StaffActorFactory.TryCreate(subjectId, roleNames, out var actor))
        {
            throw new StaffMcpAuthorizationException(
                "The staff authorization is no longer valid.");
        }

        try
        {
            StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        }
        catch (StaffAuthorizationException)
        {
            throw new StaffMcpAuthorizationException(
                "The current staff role is not authorized for casework.");
        }

        return new(actor, actor.SubjectId);
    }
}
