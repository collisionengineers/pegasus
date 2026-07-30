using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Access;
using Pegasus.Core.Cases;

namespace Pegasus.Web.Pages.Cases;

public sealed class DetailModel(ICaseQueries queries, ICaseWorkflow workflow, ICaseEditing editing, IStaffActorAccessor actorAccessor) : PageModel
{
    public CaseDetail? Detail { get; private set; }
    public string? Error { get; private set; }
    [BindProperty] public long Version { get; set; }
    [BindProperty] public string? LeaseToken { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public bool InstructionsComplete { get; set; }
    [BindProperty] public bool ImagesComplete { get; set; }
    [BindProperty] public CaseTerminalOutcome Outcome { get; set; }
    [BindProperty] public string Channel { get; set; } = "Email";
    [BindProperty] public string Target { get; set; } = string.Empty;
    [BindProperty] public string ChaseOutcome { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken) => await LoadAsync(id, cancellationToken);
    public async Task<IActionResult> OnPostEnterEditAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = actorAccessor.Current; if (actor is null) return Challenge();
        var result = await editing.AcquireAsync(id, actor, cancellationToken);
        if (result.Failure is not null) Error = result.Failure.ToString(); else LeaseToken = result.Token;
        await LoadAsync(id, cancellationToken); return Page();
    }
    public Task<IActionResult> OnPostRenewLeaseAsync(Guid id, CancellationToken cancellationToken) => LeaseAsync(id, false, cancellationToken);
    public Task<IActionResult> OnPostLeaveEditAsync(Guid id, CancellationToken cancellationToken) => LeaseAsync(id, true, cancellationToken);
    public Task<IActionResult> OnPostConfirmCompletenessAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => workflow.ConfirmCompletenessAsync(id, LeaseToken, Version, actor, InstructionsComplete, ImagesComplete, token), cancellationToken);
    public Task<IActionResult> OnPostHoldAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => workflow.HoldAsync(id, LeaseToken, Version, actor, Reason ?? string.Empty, token), cancellationToken);
    public Task<IActionResult> OnPostReleaseAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => workflow.ReleaseAsync(id, LeaseToken, Version, actor, Reason ?? string.Empty, token), cancellationToken);
    public Task<IActionResult> OnPostRecordChaseAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => workflow.RecordChaseAsync(id, LeaseToken, Version, actor, Channel, Target, ChaseOutcome, Reason, token), cancellationToken);
    public Task<IActionResult> OnPostStartReportPreparationAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => workflow.StartReportPreparationAsync(id, LeaseToken, Version, actor, token), cancellationToken);
    public Task<IActionResult> OnPostRecordReportSentAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => workflow.RecordReportSentAsync(id, LeaseToken, Version, actor, token), cancellationToken);
    public Task<IActionResult> OnPostCloseAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => workflow.CloseAsync(id, LeaseToken, Version, actor, Outcome, Reason ?? string.Empty, token), cancellationToken);
    public Task<IActionResult> OnPostReopenAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => workflow.ReopenAsync(id, LeaseToken, Version, actor, Reason ?? string.Empty, token), cancellationToken);
    public Task<IActionResult> OnPostCreateCorrectPrincipalReplacementAsync(Guid id, CancellationToken cancellationToken) => RunAsync(id, (actor, token) => workflow.CreateCorrectPrincipalReplacementAsync(id, LeaseToken, Version, actor, Reason ?? string.Empty, token), cancellationToken);

    private async Task<IActionResult> LeaseAsync(Guid id, bool release, CancellationToken cancellationToken)
    {
        var actor = actorAccessor.Current; if (actor is null) return Challenge();
        var result = release ? await editing.ReleaseAsync(id, LeaseToken ?? string.Empty, actor, cancellationToken) : await editing.RenewAsync(id, LeaseToken ?? string.Empty, actor, cancellationToken);
        if (result.Failure is not null) Error = result.Failure.ToString(); else LeaseToken = result.Token;
        await LoadAsync(id, cancellationToken); return Page();
    }
    private async Task<IActionResult> RunAsync(Guid id, Func<StaffActor, CancellationToken, Task<CaseCommandResult>> action, CancellationToken cancellationToken)
    {
        var actor = actorAccessor.Current; if (actor is null) return Challenge();
        var result = await action(actor, cancellationToken);
        if (!result.Succeeded)
        {
            Error = result.Message ?? result.Failure?.ToString() ?? "The action was not accepted.";
            await LoadAsync(id, cancellationToken);
            return Page();
        }
        await LoadAsync(id, cancellationToken);
        return Page();
    }
    private async Task<IActionResult> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = actorAccessor.Current; if (actor is null) return Challenge();
        Detail = await queries.GetAsync(id, actor, cancellationToken); if (Detail is null) return NotFound(); Version = Detail.Version; return Page();
    }
}
