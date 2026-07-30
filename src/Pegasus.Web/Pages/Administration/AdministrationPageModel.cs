using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration;

public abstract class AdministrationPageModel : PageModel
{
    protected bool TryGetActor([NotNullWhen(true)] out ActionActor? actor) =>
        StaffActorFactory.TryCreate(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
            out actor);

    protected static string NewOperationKey() => Guid.NewGuid().ToString("N");

    protected static bool IsOperationKeyValid(string value) =>
        Guid.TryParseExact(value, "N", out var operationId) && operationId != Guid.Empty;
}
