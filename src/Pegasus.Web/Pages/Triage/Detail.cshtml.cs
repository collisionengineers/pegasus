using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Access;
using Pegasus.Core.Triage;

namespace Pegasus.Web.Pages.Triage;

public sealed class DetailModel(ITriageQueries queries, ITriageStore store, IStaffActorAccessor actorAccessor) : PageModel
{
    public TriageDetail? Detail { get; private set; }
    public string? Error { get; private set; }
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public long Version { get; set; }
    [BindProperty] public Guid AssigneeId { get; set; }
    [BindProperty] public string AssigneeName { get; set; } = string.Empty;
    [BindProperty] public RoadworthinessFinding? Roadworthiness { get; set; }
    [BindProperty] public AssessmentFinding? Assessment { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public Guid CaseId { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        Id = id;
        return await LoadAsync(id, cancellationToken);
    }

    public Task<IActionResult> OnPostAssignAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => store.AssignAsync(id, Version, actor, AssigneeId, AssigneeName, token), cancellationToken);
    public Task<IActionResult> OnPostAwaitInformationAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => store.MarkAwaitingInformationAsync(id, Version, actor, Reason ?? string.Empty, token), cancellationToken);
    public Task<IActionResult> OnPostRecordFindingAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => store.RecordFindingAsync(id, Version, actor, Roadworthiness, Assessment, Reason, token), cancellationToken);
    public Task<IActionResult> OnPostCancelAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => store.CancelAsync(id, Version, actor, Reason ?? string.Empty, token), cancellationToken);
    public Task<IActionResult> OnPostReopenAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => store.ReopenAsync(id, Version, actor, Reason ?? string.Empty, token), cancellationToken);
    public Task<IActionResult> OnPostLinkCaseAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => store.LinkCaseAsync(id, Version, actor, CaseId, token), cancellationToken);
    public Task<IActionResult> OnPostUnlinkCaseAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => store.UnlinkCaseAsync(id, Version, actor, Reason ?? string.Empty, token), cancellationToken);

    private async Task<IActionResult> RunAsync(Guid id, Func<StaffActor, CancellationToken, Task<TriageCommandResult>> action, CancellationToken cancellationToken)
    {
        var actor = actorAccessor.Current;
        if (actor is null) return Challenge();
        var result = await action(actor, cancellationToken);
        if (!result.Succeeded)
        {
            Error = result.Message ?? result.Failure?.ToString() ?? "The action was not accepted.";
            await LoadAsync(id, cancellationToken);
            return Page();
        }
        return RedirectToPage("/Triage/Detail", new { id });
    }

    private async Task<IActionResult> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = actorAccessor.Current;
        if (actor is null) return Challenge();
        Detail = await queries.GetAsync(id, actor, cancellationToken);
        if (Detail is null) return NotFound();
        Version = Detail.Version;
        return Page();
    }
}
