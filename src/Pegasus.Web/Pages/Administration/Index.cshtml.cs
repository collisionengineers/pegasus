using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel : AdministrationPageModel
{
    public IActionResult OnGet()
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageStaffAccounts);
        return Page();
    }
}
