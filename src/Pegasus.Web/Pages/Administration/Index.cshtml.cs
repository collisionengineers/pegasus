using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Web.Mcp;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel : AdministrationPageModel
{
    /// <summary>
    /// Whether the Automation ingress exists in this deployment.
    /// </summary>
    /// <remarks>
    /// Its gate is set in no shipped configuration, so the card led to a page
    /// that could only ever say the capability was not composed. A capability
    /// a deployment does not carry is absent, not permanently inert.
    /// </remarks>
    public bool AutomationComposed { get; private set; }

    public IActionResult OnGet()
    {
        AutomationComposed =
            HttpContext.RequestServices.GetService<AutomationClientRegistry>() is not null;

        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageStaffAccounts);
        return Page();
    }
}
