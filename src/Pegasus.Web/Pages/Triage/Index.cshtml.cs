using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Access;
using Pegasus.Core.Triage;

namespace Pegasus.Web.Pages.Triage;

public sealed class IndexModel(ITriageQueries queries, IStaffActorAccessor actorAccessor) : PageModel
{
    [BindProperty(SupportsGet = true)] public TriageState? State { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? AssigneeId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Registration { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public IReadOnlyList<TriageSummary> Items { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var actor = actorAccessor.Current;
        if (actor is null) return Challenge();
        try
        {
            Items = await queries.ListAsync(new TriageQuery(State, AssigneeId, Registration, PageNumber), actor, cancellationToken);
        }
        catch (ArgumentOutOfRangeException)
        {
            Error = "The requested page is not available.";
        }
        catch (Exception)
        {
            Error = "Triage is temporarily unavailable. Retry the page.";
        }
        return Page();
    }
}
