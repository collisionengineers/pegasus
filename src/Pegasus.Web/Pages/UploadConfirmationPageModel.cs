using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages;

/// <summary>
/// The confirmation decision both upload status surfaces share: Find within
/// the Cases the upload may join, and the explicit staff decision to add the
/// uploaded material to the chosen Case. One implementation here so the two
/// pages cannot drift; each concrete page supplies only its own redirect.
/// Authorisation stays on the concrete page's [Authorize], which covers these
/// handlers, and the queries and mutations require casework access themselves.
/// </summary>
public abstract class UploadConfirmationPageModel(IUploadCaseDecision caseDecision) : StaffPageModel
{
    /// <summary>The rendered confirmation step: the exact target the review dialog repeats before the write.</summary>
    public UploadCaseAttachmentConfirmation? UploadCaseConfirmation { get; protected set; }

    /// <summary>Retained values for a recoverable first-step failure.</summary>
    public UploadCaseAttachmentDraft? UploadCaseDraft { get; protected set; }

    /// <summary>The Find term the address carries, trimmed; null when none.</summary>
    public string? SearchTerm { get; protected set; }

    /// <summary>The Cases and Triage Cases the term found that every file in the upload may join.</summary>
    public IReadOnlyList<UploadCaseSuggestion> SearchResults { get; protected set; } = [];

    /// <summary>The search itself could not run; distinct from no matches (FRD-18).</summary>
    public bool SearchFailed { get; protected set; }

    /// <summary>
    /// True when the surface has no review dialog: the first post resolves the
    /// Case and attaches in the same request. False (the default) renders the
    /// prepared target for the dialog and attaches on the second post.
    /// </summary>
    protected virtual bool AttachInOnePress => false;

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

    /// <summary>The receipts a Find term must fit, once the surface is loaded.</summary>
    protected abstract IReadOnlyList<Guid> SearchReceiptIds { get; }

    /// <summary>Reloads the concrete surface before returning Page after a post.</summary>
    protected abstract Task<IActionResult> RenderSurfaceAsync(
        Guid surfaceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Find within the Cases this upload may join (FRD-18): a search failure
    /// is reported as such, never as an empty result.
    /// </summary>
    protected async Task SearchAsync(string? term, ActionActor actor, CancellationToken cancellationToken)
    {
        SearchTerm = string.IsNullOrWhiteSpace(term) ? null : term.Trim();
        SearchResults = [];
        SearchFailed = false;
        if (SearchTerm is null || SearchReceiptIds.Count == 0)
        {
            return;
        }

        try
        {
            SearchResults = await caseDecision.SearchForUploadsAsync(SearchReceiptIds, SearchTerm, actor, cancellationToken);
        }
        catch (Exception exception) when (exception is not StaffAuthorizationException && !cancellationToken.IsCancellationRequested)
        {
            SearchFailed = true;
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
        Guid operationId,
        long? receiptVersion,
        long? caseVersion,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (receiptId == Guid.Empty)
        {
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
                reference);
            if (receiptVersion is not { } reviewedReceiptVersion || reviewedReceiptVersion < 0
                || operationId == Guid.Empty)
            {
                TempData["UploadConfirmationError"] = "This confirmation is incomplete. Refresh and try again.";
                return await RenderSurfaceAsync(id, cancellationToken);
            }

            // A chosen or typed Case is resolved and re-read on the server first.
            // A surface with a review dialog renders that prepared target and
            // writes nothing until staff posts the rendered decision. A surface
            // with no dialog (AttachInOnePress) uses the prepared target in this
            // same request, so one press attaches.
            if (caseId is null || caseVersion is null)
            {
                var prepared = caseId is null
                    ? await caseDecision.PrepareAsync(
                        receiptId, reference, operationId, reviewedReceiptVersion, actor, cancellationToken)
                    : await caseDecision.PrepareByCaseAsync(
                        receiptId, caseId.Value, operationId, reviewedReceiptVersion, actor, cancellationToken);
                if (prepared is null)
                {
                    TempData["UploadConfirmationError"] = caseId is null
                        ? "No single viable case matched that reference. Search and choose a case from the suggestions."
                        : "That case is not currently available for this upload. Search and choose another case.";
                    return await RenderSurfaceAsync(id, cancellationToken);
                }
                if (!AttachInOnePress)
                {
                    UploadCaseConfirmation = prepared;
                    return await RenderSurfaceAsync(id, cancellationToken);
                }

                caseId = prepared.CaseId;
                caseVersion = prepared.Input.ExpectedCaseVersion;
            }
            if (caseVersion is not { } reviewedCaseVersion || reviewedCaseVersion < 0)
            {
                TempData["UploadConfirmationError"] = "This confirmation is incomplete. Choose the case again.";
                return await RenderSurfaceAsync(id, cancellationToken);
            }

            var result = await caseDecision.AttachAsync(
                receiptId, caseId, reference,
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

    /// <summary>The candidate facts the review dialog repeats for a prepared confirmation.</summary>
    protected static UploadReviewConfirmation? ReviewConfirmation(
        UploadCaseAttachmentConfirmation? confirmation,
        IEnumerable<UploadCaseSuggestion> known)
    {
        if (confirmation is null)
        {
            return null;
        }

        var target = confirmation.Target
            ?? known.FirstOrDefault(candidate => candidate.CaseId == confirmation.CaseId)
            ?? new UploadCaseSuggestion(confirmation.CaseId, confirmation.Reference, null, null, string.Empty, confirmation.Input.ExpectedCaseVersion);
        return new(UploadReviewCandidate.From(target), confirmation.Input.OperationId, confirmation.Input.ExpectedCaseVersion);
    }
}
