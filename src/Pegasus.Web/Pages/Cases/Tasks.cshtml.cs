using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's due work: manual chases and the report-Sent evidence links
/// that drive chasing. Every action redirects back to the workspace.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class TasksModel(
    IRecordManualCaseChase recordManualCaseChase,
    IAddCaseNote addCaseNote,
    ILinkReportEvidence linkReportEvidence,
    IUnlinkReportEvidence unlinkReportEvidence,
    TimeProvider timeProvider,
    ILogger<TasksModel> logger) : CaseMutationPageModel(logger)
{
    public IActionResult OnGet() => NotFound();

    // These retired task actions must refuse direct posts as well as disappear
    // from the workspace. Razor Pages otherwise answers an unknown handler
    // with an empty success response.
    public IActionResult OnPostCreateTask() => NotFound();
    public IActionResult OnPostAssignTask() => NotFound();
    public IActionResult OnPostCompleteTask() => NotFound();
    public IActionResult OnPostCancelTask() => NotFound();

    /// <summary>
    /// A note takes no edit lease and no expected version: it adds to the case's
    /// record rather than changing the case, so it must not contend with an
    /// engineer editing the same case.
    /// </summary>
    public async Task<IActionResult> OnPostAddNoteAsync(
        Guid id,
        string operationKey,
        string note,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await addCaseNote.ExecuteAsync(new(id, actor, operationKey, note), cancellationToken);
            TempData["CaseStatus"] = "The note was added.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TempData["CaseError"] = "The note was not added.";
        }

        return RedirectToNotes(id);
    }

    /// <summary>The Notes section is where a note or a chase is read back.</summary>
    private RedirectToPageResult RedirectToNotes(Guid id) =>
        RedirectToPage("/Cases/Details", new { id, section = "notes" });

    public Task<IActionResult> OnPostRecordManualChaseAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        string channel,
        string recipient,
        string outcome,
        string? content,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "record_manual_chase",
            // The attempt time is the server's: a chase is recorded as it is
            // asserted, never at a time the form carried (PR 670 port).
            actor => recordManualCaseChase.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    editLeaseToken,
                    actor,
                    operationKey,
                    channel,
                    recipient,
                    timeProvider.GetUtcNow(),
                    outcome,
                    content),
                cancellationToken),
            "The manual chase was recorded and the next chase date was scheduled.",
            RedirectToNotes);

    public Task<IActionResult> OnPostLinkReportEvidenceAsync(
        Guid id,
        Guid evidenceId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "link_report_evidence",
            actor => linkReportEvidence.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    evidenceId),
                cancellationToken),
            "The exact retained report-Sent evidence was linked.");

    public Task<IActionResult> OnPostUnlinkReportEvidenceAsync(
        Guid id,
        Guid evidenceId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "unlink_report_evidence",
            actor => unlinkReportEvidence.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    evidenceId),
                cancellationToken),
            "The report-Sent evidence was unlinked; retained evidence and history were preserved.");
}
