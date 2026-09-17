using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's document custody actions: custody retry, logical removal,
/// and image tags. Every action redirects back to the workspace.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class CustodyModel(
    IRetryCaseCustody retryCaseCustody,
    ILogicallyRemoveDocument logicallyRemoveDocument,
    MarkAsOriginalReport markAsOriginalReport,
    ITagCaseImage tagCaseImage,
    IUntagCaseImage untagCaseImage,
    ICreateImageTag createImageTag,
    IGetCase getCase,
    IAcquireCaseEditLease acquireLease,
    ILogger<CustodyModel> logger) : CaseMutationPageModel(logger)
{
    /// <summary>
    /// Tagging and removing are immediate posts inside the edit
    /// session (v25 decision F): after one succeeds the session carries on.
    /// </summary>
    protected override (IGetCase Cases, IAcquireCaseEditLease Leases)? LeaseReclaim => (getCase, acquireLease);

    public IActionResult OnGet() => NotFound();

    private RedirectToPageResult RedirectToFiles(Guid id) =>
        RedirectToPage("/Cases/Details", new { id, section = "files" });

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

        return RedirectToFiles(id);
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
            "The document occurrence was logically removed; custody content and history were retained.",
            RedirectToFiles,
            keepEditing: true);

    public Task<IActionResult> OnPostMarkAsOriginalReportAsync(
        Guid id,
        Guid occurrenceId,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "mark_as_original_report",
            actor => markAsOriginalReport.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    editLeaseToken,
                    occurrenceId),
                cancellationToken),
            "The original report was recorded.",
            RedirectToFiles,
            keepEditing: true);

    public Task<IActionResult> OnPostTagImageAsync(
        Guid id,
        Guid occurrenceId,
        Guid tagId,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteTransportCommandAsync(
            id,
            editLeaseToken,
            "tag_case_image",
            actor => tagCaseImage.ExecuteAsync(
                new(
                    id,
                    occurrenceId,
                    tagId,
                    actor,
                    operationKey,
                    expectedVersion,
                    editLeaseToken),
                cancellationToken),
            CaseWorkspaceLabels.ImageTags.WasApplied,
            RedirectToDetailsFilesImages,
            keepEditing: true);

    public Task<IActionResult> OnPostUntagImageAsync(
        Guid id,
        Guid occurrenceId,
        Guid tagId,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteTransportCommandAsync(
            id,
            editLeaseToken,
            "untag_case_image",
            actor => untagCaseImage.ExecuteAsync(
                new(
                    id,
                    occurrenceId,
                    tagId,
                    actor,
                    operationKey,
                    expectedVersion,
                    editLeaseToken),
                cancellationToken),
            CaseWorkspaceLabels.ImageTags.WasRemoved,
            RedirectToDetailsFilesImages,
            keepEditing: true);

    /// <summary>
    /// Adds a word to the shared tag vocabulary from the picker on a Case's
    /// image. The vocabulary is not the Case, so this consumes neither the
    /// Case version nor the edit lease: the editor keeps editing and the new
    /// tag is there to apply.
    /// </summary>
    public async Task<IActionResult> OnPostCreateImageTagAsync(
        Guid id,
        string? name,
        ImageTagColour colour,
        string operationKey,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            PreserveLeaseState(id, editLeaseToken);
            TempData["CaseError"] = CaseWorkspaceLabels.ImageTags.NameRequired;
            return RedirectToDetailsFilesImages(id);
        }

        try
        {
            await createImageTag.ExecuteAsync(
                new(name, colour, actor, operationKey),
                cancellationToken);
            PreserveLeaseState(id, editLeaseToken);
            TempData["CaseStatus"] = CaseWorkspaceLabels.ImageTags.WasCreated;
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (ImageTagNameInUseException)
        {
            PreserveLeaseState(id, editLeaseToken);
            TempData["CaseError"] = CaseWorkspaceLabels.ImageTags.NameInUse;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "create_image_tag", exception);
            PreserveLeaseState(id, editLeaseToken);
            TempData["CaseError"] = CaseWorkspaceLabels.ImageTags.NotCreated;
        }

        return RedirectToDetailsFilesImages(id);
    }

}
