using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class UploadStatusModel(
    IQueuedIntakeStatusQueries queries,
    IUploadOutcomeQueries outcomeQueries,
    IUploadCaseDecision caseDecision) : PageModel
{
    public QueuedIntakeStatus Status { get; private set; } = null!;
    public bool IsDuplicate { get; private set; }

    /// <summary>
    /// The confirmation outcome, once processing has left Received/Processing.
    /// Built from the same status read this page already queries — no second
    /// endpoint, no second poll.
    /// </summary>
    public UploadOutcomeView? Outcome { get; private set; }

    public bool RefreshAutomatically =>
        Status.Status is QueuedIntakeStatusKind.Received or QueuedIntakeStatusKind.Processing;

    public string Heading => Status.Status switch
    {
        QueuedIntakeStatusKind.Received => "Received",
        QueuedIntakeStatusKind.Processing => "Processing",
        QueuedIntakeStatusKind.Complete => "Complete",
        QueuedIntakeStatusKind.Failed => "Failed",
        _ => throw new InvalidOperationException("The queued intake status is not recognized.")
    };

    public string Message => IsDuplicate
        ? $"{Status.SourceFileName} was already received. No duplicate was created. {StateMessage}"
        : StateMessage;

    private string StateMessage => Status.Status switch
    {
        QueuedIntakeStatusKind.Received =>
            "The file is safely received and waiting for background processing.",
        QueuedIntakeStatusKind.Processing => "The file is being processed.",
        QueuedIntakeStatusKind.Complete => "Processing is complete.",
        QueuedIntakeStatusKind.Failed => OperatorLabels.IntakeFailure(Status.FailureCode) + ".",
        _ => throw new InvalidOperationException("The queued intake status is not recognized.")
    };

    /// <param name="duplicate">
    /// Carried on the URL, as <c>/Received/{id}?duplicate=true</c> already does,
    /// so the notice survives the page's own refreshes. It only changes wording.
    /// </param>
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        bool duplicate,
        CancellationToken cancellationToken)
    {
        var status = await queries.GetAsync(id, cancellationToken);
        if (status is null)
        {
            return NotFound();
        }

        Status = status;
        IsDuplicate = duplicate;

        // The confirmation decision needs a full receipt read for a terminal
        // status; Received/Processing never reach the branch that needs one.
        if (status.Status is QueuedIntakeStatusKind.Complete or QueuedIntakeStatusKind.Failed
            && StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            Outcome = await outcomeQueries.BuildAsync(status, submissionGroupId: null, actor, cancellationToken);
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
        bool duplicate,
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
            return RedirectToPage("/UploadStatus", new { id, duplicate = duplicate ? "true" : null });
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

        return RedirectToPage("/UploadStatus", new { id, duplicate = duplicate ? "true" : null });
    }
}
