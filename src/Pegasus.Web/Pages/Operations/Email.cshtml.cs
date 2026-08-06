using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Operations;

/// <summary>
/// The retired Email operations screen.
/// </summary>
/// <remarks>
/// Its content is now the Inbox's Received and Sent tabs and its Failed
/// filter, which is where an operator was going to look anyway. Merging the
/// screen away solves its discoverability — its only entry was a dashboard
/// card carrying an "Unavailable" chip — rather than labelling it better.
///
/// The route survives as a redirect so existing links and bookmarks land on
/// what they were pointing at: the failed-processing view.
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class EmailModel : PageModel
{
    public IActionResult OnGet() =>
        RedirectToPagePermanent("/Intake/Index", new { decision = "failed" });
}
