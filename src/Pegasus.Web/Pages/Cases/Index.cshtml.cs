using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Access;
using Pegasus.Core.Cases;

namespace Pegasus.Web.Pages.Cases;

public sealed class IndexModel(ICaseQueries queries, IStaffActorAccessor actorAccessor) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? CaseReference { get; set; }
    [BindProperty(SupportsGet = true)] public string? Registration { get; set; }
    [BindProperty(SupportsGet = true)] public string? Claimant { get; set; }
    [BindProperty(SupportsGet = true)] public string? ClaimNumber { get; set; }
    [BindProperty(SupportsGet = true)] public string? PrincipalCode { get; set; }
    [BindProperty(SupportsGet = true)] public CaseWorkflowState? State { get; set; }
    [BindProperty(SupportsGet = true)] public string? Origin { get; set; }
    [BindProperty(SupportsGet = true)] public string? Queue { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public IReadOnlyList<CaseSummary> Items { get; private set; } = [];
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var actor = actorAccessor.Current;
        if (actor is null) return Challenge();
        try
        {
            CaseQueue? queue = null;
            if (!string.IsNullOrWhiteSpace(Queue) && Enum.TryParse<CaseQueue>(Queue.Replace("_", string.Empty), true, out var parsed)) queue = parsed;
            Items = await queries.ListAsync(new CaseQuery(CaseReference, Registration, Claimant, ClaimNumber, PrincipalCode, State, null, null, null, null, null, Origin, queue, PageNumber), actor, cancellationToken);
        }
        catch (ArgumentException)
        {
            Error = "The requested filter is not valid.";
        }
        catch (Exception)
        {
            Error = "Cases are temporarily unavailable. Retry the page.";
        }
        return Page();
    }
}
