using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Unidentified;

/// <summary>
/// The Unidentified list moved onto the Cases page as a tab (INTK-009, EPIC-011). This
/// route is kept as a permanent redirect rather than deleted outright: the
/// dashboard historically linked here and staff may have it bookmarked, and a
/// dead link is a worse answer than a redirect to where the work now lives.
/// </summary>
[Authorize(Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class IndexModel : PageModel
{
    public IActionResult OnGet() =>
        RedirectPermanent("/Cases?tab=unidentified");
}
