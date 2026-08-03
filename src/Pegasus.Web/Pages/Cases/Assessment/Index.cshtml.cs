using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Cases.Assessment;

// Design-only surface. This page model deliberately holds no query, command or
// injected dependency: the Engineers assessment screen is being wired in a
// separate worktree, and this pass owns layout, copy and conformance only.
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class IndexModel : PageModel
{
    public IActionResult OnGet() => Page();
}
