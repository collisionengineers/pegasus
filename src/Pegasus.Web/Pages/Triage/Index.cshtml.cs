using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Triage;

/// <summary>
/// The retired Queues route. The queues moved to <c>/Cases</c> (EPIC-011
/// §1.4, PLAT-029); this route survives as a permanent redirect that carries
/// the tab through, so bookmarks and old links land on the same work.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class IndexModel : PageModel
{
    [BindProperty(SupportsGet = true, Name = "queue")]
    public string? Queue { get; set; }

    public IActionResult OnGet() =>
        RedirectPermanent("/Cases" + (string.IsNullOrWhiteSpace(Queue) ? string.Empty : "?tab=" + Uri.EscapeDataString(Queue)));
}
