using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Cases.Assessment;

/// <summary>
/// The Engineer workbench moved into the Case record (D30, ENG-034). The old
/// route remains as a permanent redirect so retained links land on Estimate.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class IndexModel : PageModel
{
    public IActionResult OnGet(Guid id) =>
        RedirectPermanent($"/Cases/{id:D}?section=estimate");
}
