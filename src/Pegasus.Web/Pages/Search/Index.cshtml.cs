using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Search;

/// <summary>
/// The retired Search screen.
/// </summary>
/// <remarks>
/// Search and Cases ran the identical Core query and differed only in which
/// filters they exposed, so two nav items led to one capability and the two
/// screens disagreed about what a query failure meant — Cases returned 503,
/// Search returned nothing at all.
///
/// Cases absorbs it. The route survives as a redirect that carries the
/// keyword through, so existing links and bookmarks land on their results.
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class IndexModel : PageModel
{
    [BindProperty(SupportsGet = true, Name = "query")]
    public string? Query { get; set; }

    public IActionResult OnGet() =>
        RedirectToPagePermanent("/Cases/Index", new { query = Query });
}
