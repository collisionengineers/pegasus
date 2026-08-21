using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages;

public abstract class StaffPageModel : PageModel
{
    protected bool TryGetActor([NotNullWhen(true)] out ActionActor? actor) =>
        StaffActorFactory.TryCreate(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
            out actor);

    public static string NewOperationKey() => Guid.NewGuid().ToString("N");
}
