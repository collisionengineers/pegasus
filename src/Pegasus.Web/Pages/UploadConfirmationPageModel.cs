using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages;

/// <summary>
/// The confirmation decision both upload status surfaces share: the
/// case-search suggestions behind the autocomplete, and the explicit staff
/// decision to add uploaded material to a found case. One implementation
/// here so the two pages cannot drift; each concrete page supplies only its
/// own redirect. Authorisation stays on the concrete page's [Authorize],
/// which covers these handlers, and the queries and mutations require
/// casework access themselves.
/// </summary>
public abstract class UploadConfirmationPageModel(IUploadCaseDecision caseDecision) : StaffPageModel
{
    /// <summary>Back to the concrete status surface after a decision.</summary>
    protected abstract IActionResult RedirectToSurface(Guid id);

    public async Task<IActionResult> OnGetCaseSearchAsync(
        string? term,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            return new JsonResult(
                await caseDecision.SearchAsync(term ?? string.Empty, actor, cancellationToken));
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
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (receiptId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
        {
            TempData["UploadConfirmationError"] = "A reason is required to add this to a case.";
            return RedirectToSurface(id);
        }

        try
        {
            var result = await caseDecision.AttachAsync(
                receiptId, caseId, reference, reason, actor, cancellationToken);
            // Success uses the layout's one-time confirmation slot — the
            // existing convention for an action completed on another page;
            // only the failure banner is this surface's own.
            TempData[result.Succeeded ? "Confirmation" : "UploadConfirmationError"] = result.Message;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        return RedirectToSurface(id);
    }
}
