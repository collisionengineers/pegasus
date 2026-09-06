using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's document custody actions: custody retry, logical removal,
/// third-party vehicle evidence, and request-scoped upload links. Every action redirects back
/// to the workspace.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class CustodyModel(
    IRetryCaseCustody retryCaseCustody,
    ILogicallyRemoveDocument logicallyRemoveDocument,
    IConfirmThirdPartyVehicleEvidence confirmThirdPartyVehicleEvidence,
    ICreateRequestUploadLink createRequestUploadLink,
    IRevokeRequestUploadLink revokeRequestUploadLink,
    ILogger<CustodyModel> logger) : CaseMutationPageModel(logger)
{
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
        string? recipient,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            // The shared contract keeps both optional; the create dialog
            // requires the recipient itself. An omitted reason is null before
            // Core, while supplied text (blank included) is Core's to judge.
            var result = await createRequestUploadLink.ExecuteAsync(
                new(
                    id,
                    actor,
                    operationKey,
                    expectedVersion,
                    editLeaseToken,
                    recipient,
                    string.IsNullOrEmpty(reason) ? null : reason),
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
}
