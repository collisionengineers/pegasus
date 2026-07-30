using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Principals;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(IListOrganizations listOrganizations)
    : AdministrationPageModel
{
    public OrganizationListPage Organizations { get; private set; } =
        new([], 1, 25, false, false);

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        PageNumber = Math.Max(1, PageNumber);
        Organizations = await listOrganizations.ExecuteAsync(
            new(actor, PageNumber, 25),
            cancellationToken);
        return Page();
    }
}
