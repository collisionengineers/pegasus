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
    /// <summary>The second, non-script confirmation step for the current render.</summary>
    public UploadCaseAttachmentConfirmation? UploadCaseConfirmation { get; protected set; }

    /// <summary>Retained values for a recoverable first-step failure.</summary>
    public UploadCaseAttachmentDraft? UploadCaseDraft { get; protected set; }

    /// <summary>Back to the concrete status surface after a decision.</summary>
    protected abstract IActionResult RedirectToSurface(Guid id);

    /// <summary>
    /// Verifies that a receipt named by a form is the receipt this concrete
    /// surface actually displayed. Posted ids are never authority to attach a
    /// different upload.
    /// </summary>
    protected abstract Task<bool> SurfaceContainsReceiptAsync(
        Guid surfaceId,
        Guid receiptId,
        CancellationToken cancellationToken);

    protected abstract Task<IReadOnlyList<Guid>> SearchReceiptIdsAsync(
        Guid surfaceId,
        CancellationToken cancellationToken);

    /// <summary>Reloads the concrete surface before returning Page after a post.</summary>
    protected abstract Task<IActionResult> RenderSurfaceAsync(
        Guid surfaceId,
        CancellationToken cancellationToken);

    public async Task<IActionResult> OnGetCaseSearchAsync(
        Guid id,
        Guid? receiptId,
        string? term,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            if (receiptId is { } scopedReceiptId)
            {
                if (!await SurfaceContainsReceiptAsync(id, scopedReceiptId, cancellationToken))
                {
                    return new JsonResult(Array.Empty<UploadCaseSuggestion>());
                }
                return new JsonResult(
                    await caseDecision.SearchForUploadAsync(scopedReceiptId, term ?? string.Empty, actor, cancellationToken));
            }

            return new JsonResult(await caseDecision.SearchForUploadsAsync(
                await SearchReceiptIdsAsync(id, cancellationToken), term ?? string.Empty, actor, cancellationToken));
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
        Guid operationId,
        long? receiptVersion,
        long? caseVersion,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (receiptId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            TempData["UploadConfirmationError"] = "A reason is required to add this to a case.";
            if (receiptId != Guid.Empty)
            {
                UploadCaseDraft = new(
                    receiptId, operationId, receiptVersion, caseId, caseVersion,
                    reference, reason ?? string.Empty);
                return await RenderSurfaceAsync(id, cancellationToken);
            }
            return RedirectToSurface(id);
        }
        if (!await SurfaceContainsReceiptAsync(id, receiptId, cancellationToken))
        {
            TempData["UploadConfirmationError"] = "This upload is no longer available on this page. Refresh and try again.";
            return RedirectToSurface(id);
        }

        try
        {
            UploadCaseDraft = new(
                receiptId, operationId, receiptVersion, caseId, caseVersion,
                reference, reason);
            if (receiptVersion is not { } reviewedReceiptVersion || reviewedReceiptVersion < 0
                || operationId == Guid.Empty)
            {
                TempData["UploadConfirmationError"] = "This confirmation is incomplete. Refresh and try again.";
                return await RenderSurfaceAsync(id, cancellationToken);
            }

            // Without script a typed reference takes an explicit server
            // confirmation round-trip. It resolves and re-reads the viable
            // target, then renders its receipt and Case versions; no write
            // happens until staff posts that rendered decision.
            if (caseId is null)
            {
                UploadCaseConfirmation = await caseDecision.PrepareAsync(
                    receiptId, reference, reason, operationId, reviewedReceiptVersion, actor, cancellationToken);
                if (UploadCaseConfirmation is null)
                {
                    TempData["UploadConfirmationError"] = "No single viable case matched that reference. Search and choose a case from the suggestions.";
                    return await RenderSurfaceAsync(id, cancellationToken);
                }

                return await RenderSurfaceAsync(id, cancellationToken);
            }
            if (caseVersion is not { } reviewedCaseVersion || reviewedCaseVersion < 0)
            {
                TempData["UploadConfirmationError"] = "This confirmation is incomplete. Choose the case again.";
                return await RenderSurfaceAsync(id, cancellationToken);
            }

            var result = await caseDecision.AttachAsync(
                receiptId, caseId, reference, reason,
                new(operationId, reviewedReceiptVersion, reviewedCaseVersion), actor, cancellationToken);
            if (!result.Succeeded)
            {
                // Preserve the exact page operation and reviewed versions on
                // a recoverable conflict. A new page identity here would
                // turn a safe retry into a different decision.
                UploadCaseConfirmation = new(
                    receiptId,
                    caseId.Value,
                    reference?.Trim() ?? "Selected case",
                    reason,
                    new(operationId, reviewedReceiptVersion, reviewedCaseVersion));
                TempData["UploadConfirmationError"] = result.Message;
                return await RenderSurfaceAsync(id, cancellationToken);
            }
            // Success uses the layout's one-time confirmation slot — the
            // existing convention for an action completed on another page;
            // only the failure banner is this surface's own.
            TempData["Confirmation"] = result.Message;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        return RedirectToSurface(id);
    }
}
