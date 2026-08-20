using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Intake;
using Pegasus.Core.Identity;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class UploadGroupStatusModel(
    IIntakeSubmissionGroupStore groups,
    IQueuedIntakeStatusQueries statuses,
    IUploadOutcomeQueries outcomeQueries,
    IUploadCaseDecision caseDecision) : PageModel
{
    public IntakeSubmissionGroup Group { get; private set; } = null!;
    public IReadOnlyDictionary<Guid, QueuedIntakeStatus?> Statuses { get; private set; } =
        new Dictionary<Guid, QueuedIntakeStatus?>();

    /// <summary>
    /// The confirmation outcome per member, built independently per file —
    /// a grouped image upload can terminal-decide its members independently
    /// (a mixed batch's instruction document takes its own route), so this
    /// makes no group-wide assumption and reports each member's own outcome.
    /// </summary>
    public IReadOnlyDictionary<Guid, UploadOutcomeView?> Outcomes { get; private set; } =
        new Dictionary<Guid, UploadOutcomeView?>();

    /// <summary>
    /// Set only when every member's outcome is the same Image-initiated Case
    /// registration. The group is the registration unit (one reference for
    /// the whole submission), so the page reports that registration once for
    /// the group instead of repeating the identical outcome per file. Any
    /// other mix of outcomes keeps the per-file report.
    /// </summary>
    public UploadOutcomeView? GroupRegistrationOutcome { get; private set; }

    public bool RefreshAutomatically => Statuses.Values.Any(status =>
        status is null
            || status.Status is QueuedIntakeStatusKind.Received or QueuedIntakeStatusKind.Processing);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var group = await groups.GetAsync(id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        Group = group;
        var haveActor = StaffActorFactory.TryCreate(
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
            out var actor);

        // Each member's status read, and — once terminal — its confirmation
        // outcome, is an independent read against its own DbContext (every
        // store behind these ports is IDbContextFactory-backed, not shared),
        // so a group's members are read concurrently rather than one durable
        // round-trip at a time. This page polls itself repeatedly while any
        // member is still Received/Processing, so the saving is real.
        var memberResults = await Task.WhenAll(group.Members.Select(async member =>
        {
            var status = await statuses.GetAsync(member.StagedReceiptId, cancellationToken);
            UploadOutcomeView? outcome = null;
            if (status is { Status: QueuedIntakeStatusKind.Complete or QueuedIntakeStatusKind.Failed }
                && haveActor)
            {
                outcome = await outcomeQueries.BuildAsync(status, group.Id, actor!, cancellationToken);
            }

            return (member.StagedReceiptId, status, outcome);
        }));

        Statuses = memberResults.ToDictionary(result => result.StagedReceiptId, result => result.status);
        Outcomes = memberResults.ToDictionary(result => result.StagedReceiptId, result => result.outcome);
        var outcomes = memberResults.Select(result => result.outcome).ToArray();
        if (outcomes.Length > 1
            && outcomes.All(outcome => outcome is { Kind: UploadOutcomeKind.ImageCaseRegistered })
            && outcomes.Select(outcome => outcome!.PrimaryAction?.Url).Distinct().Count() == 1)
        {
            GroupRegistrationOutcome = outcomes[0];
        }

        return Page();
    }

    /// <summary>
    /// The case-search suggestions behind the confirmation surface's
    /// autocomplete. Staff only — the page's authorisation applies to
    /// handlers, and the query itself requires casework access.
    /// </summary>
    public async Task<IActionResult> OnGetCaseSearchAsync(
        string? term,
        CancellationToken cancellationToken)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }

        try
        {
            return new JsonResult(
                await caseDecision.SearchAsync(term ?? string.Empty, actor!, cancellationToken));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// The explicit staff decision to add the uploaded material to the chosen
    /// case, through the existing leased link path. Replays are safe: the
    /// operation keys are deterministic per receipt and case, and a decision
    /// that already took effect reports the same success.
    /// </summary>
    public async Task<IActionResult> OnPostAttachAsync(
        Guid id,
        Guid receiptId,
        Guid? caseId,
        string? reference,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }

        if (receiptId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
        {
            TempData["UploadConfirmationError"] = "A reason is required to add this to a case.";
            return RedirectToPage("/UploadGroupStatus", new { id });
        }

        try
        {
            var result = await caseDecision.AttachAsync(
                receiptId, caseId, reference, reason, actor!, cancellationToken);
            TempData[result.Succeeded ? "UploadConfirmationStatus" : "UploadConfirmationError"] =
                result.Message;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        return RedirectToPage("/UploadGroupStatus", new { id });
    }
}
