using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's document custody actions: custody retry, staff upload, logical removal,
/// third-party vehicle evidence, and request-scoped upload links. Every action redirects back
/// to the workspace.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed partial class CustodyModel(
    IRetryCaseCustody retryCaseCustody,
    IAddCaseDocument addCaseDocument,
    ILogicallyRemoveDocument logicallyRemoveDocument,
    IConfirmThirdPartyVehicleEvidence confirmThirdPartyVehicleEvidence,
    ICreateRequestUploadLink createRequestUploadLink,
    IRevokeRequestUploadLink revokeRequestUploadLink,
    ILogger<CustodyModel> logger) : CaseMutationPageModel(logger)
{
    private const long MaximumStaffUploadBytes = 10 * 1024 * 1024;

    public async Task<IActionResult> OnPostRetryCustodyAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CustodyTargetKind targetKind,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var result = await retryCaseCustody.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, reason, editLeaseToken, targetKind),
                cancellationToken);
            if (result.Outcome is RetryCaseCustodyOutcome.Pending or RetryCaseCustodyOutcome.Replay)
            {
                ClearLeaseState();
                TempData["CaseStatus"] = result.Message;
            }
            else
            {
                PreserveLeaseState(id, editLeaseToken);
                TempData["CaseError"] = result.Message;
            }
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "retry_case_custody", exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            TempData["CaseError"] =
                "Custody retry was not recorded because the case changed or edit mode was lost.";
        }

        return RedirectToDetails(id);
    }

    public async Task<IActionResult> OnPostUploadDocumentAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        DocumentSemanticRole semanticRole,
        IFormFile? upload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!Guid.TryParseExact(operationKey, "N", out var operationId)
            || expectedVersion < 0
            || upload is null
            || upload.Length is <= 0 or > MaximumStaffUploadBytes)
        {
            PreserveLeaseState(id, editLeaseToken);
            TempData["CaseError"] =
                "Choose a non-empty document of 10 MB or less and reload the case before retrying.";
            return RedirectToDetails(id);
        }

        await using var content = new MemoryStream((int)upload.Length);
        await upload.CopyToAsync(content, cancellationToken);
        try
        {
            var result = await addCaseDocument.ExecuteAsync(
                new(
                    id,
                    Path.GetFileName(upload.FileName),
                    SafeMediaType(upload.ContentType),
                    content.GetBuffer().AsMemory(0, checked((int)content.Length)),
                    semanticRole,
                    DocumentSource.StaffUpload,
                    $"staff-upload:{operationId:N}",
                    actor,
                    operationId.ToString("N"),
                    expectedVersion,
                    editLeaseToken),
                cancellationToken);
            ClearLeaseState();
            TempData["CaseStatus"] = result.IsReplay
                ? "This document upload was already completed."
                : "The document was retained in case custody.";
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "add_case_document", exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            RetainProposedValues(id);
            TempData["CaseError"] =
                "The document could not be retained because the case changed, edit mode was lost, or custody is unavailable.";
        }

        return RedirectToDetails(id);
    }

    public Task<IActionResult> OnPostRemoveDocumentAsync(
        Guid id,
        Guid occurrenceId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteTransportCommandAsync(
            id,
            editLeaseToken,
            "remove_document",
            actor => logicallyRemoveDocument.ExecuteAsync(
                new(
                    id,
                    occurrenceId,
                    actor,
                    reason,
                    operationKey,
                    expectedVersion,
                    editLeaseToken),
                cancellationToken),
            "The document occurrence was logically removed; custody content and history were retained.");

    public Task<IActionResult> OnPostConfirmThirdPartyVehicleEvidenceAsync(
        Guid id,
        Guid occurrenceId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteTransportCommandAsync(
            id,
            editLeaseToken,
            "confirm_third_party_vehicle_evidence",
            actor => confirmThirdPartyVehicleEvidence.ExecuteAsync(
                new(
                    id,
                    occurrenceId,
                    actor,
                    reason,
                    operationKey,
                    expectedVersion,
                    editLeaseToken),
                cancellationToken),
            "The custody-confirmed image was recorded as third-party vehicle evidence and is excluded from EVA export.");

    public async Task<IActionResult> OnPostCreateRequestUploadLinkAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var result = await createRequestUploadLink.ExecuteAsync(
                new(id, actor, operationKey, expectedVersion, editLeaseToken),
                cancellationToken);
            ClearLeaseState();
            if (result.Secret is null)
            {
                TempData["CaseStatus"] =
                    "This upload request was already created. Its secret cannot be displayed again.";
            }
            else
            {
                TempData["CaseRequestSecret"] = Url.Page(
                    "/Uploads/Request",
                    pageHandler: null,
                    values: new { token = result.Secret.Token },
                    protocol: Request.Scheme);
                TempData["CaseStatus"] =
                    $"The upload request expires at {result.Link.ExpiresAtUtc:u}. Copy its secret link now.";
            }
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "create_request_upload_link", exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            RetainProposedValues(id);
            TempData["CaseError"] =
                "The upload request could not be created because the case changed, edit mode was lost, or requests are unavailable.";
        }

        return RedirectToDetails(id);
    }

    public Task<IActionResult> OnPostRevokeRequestUploadLinkAsync(
        Guid id,
        Guid requestId,
        long expectedRequestVersion,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteTransportCommandAsync(
            id,
            editLeaseToken,
            "revoke_request_upload_link",
            actor => revokeRequestUploadLink.ExecuteAsync(
                new(
                    id,
                    requestId,
                    actor,
                    reason,
                    operationKey,
                    expectedRequestVersion,
                    expectedVersion,
                    editLeaseToken),
                cancellationToken),
            "The upload request was revoked.");

    private static string SafeMediaType(string? value) =>
        string.IsNullOrWhiteSpace(value)
            || value.Length > 200
            || value.Contains('\r', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
                ? "application/octet-stream"
                : value.Trim();
}
