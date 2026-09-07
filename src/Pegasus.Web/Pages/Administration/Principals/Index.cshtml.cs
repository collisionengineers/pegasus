using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Principals;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(IListPrincipals listPrincipals)
    : AdministrationPageModel
{
    public PrincipalListPage Customers { get; private set; } = new([], 1, false);

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        PageNumber = Math.Clamp(PageNumber, 1, int.MaxValue / ListPrincipals.PageSize);
        Customers = await listPrincipals.ExecuteAsync(actor, PageNumber, cancellationToken);
        return Page();
    }
}
