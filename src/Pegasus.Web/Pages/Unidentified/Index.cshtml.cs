using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Unidentified;

[Authorize(Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class IndexModel(IUnidentifiedStore store) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    public IReadOnlyList<UnidentifiedItem> Results { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var query = Query?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            if (!UnidentifiedReferenceFormat.TryParse(query, out _))
            {
                return Page();
            }

            var item = await store.GetByReferenceAsync(query, cancellationToken);
            Results = item is null ? [] : [item];
            return Page();
        }

        Results = await store.ListAsync(UnidentifiedState.Open, cancellationToken);
        return Page();
    }

    public static string ReasonLabel(UnidentifiedItem item) =>
        OperatorLabels.UnidentifiedReason(item.ReasonCode);
}
