using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Tasks;
using TaskLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Tasks;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Tasks section (FRD-16, CASE-20): complete, cancel and add a Case task from the Case record.
/// Each action runs under the edit session's lease with one fixed recorded reason, so the section
/// carries no reason box. A task the lease consumed is claimed again (save as you go, 29 September
/// 2026), so the session carries on, and the action lands back on the Tasks section.
/// </summary>
public sealed partial class DetailsModel
{
    /// <summary>
    /// The Case's open tasks, read only for a Case whose shown report has been sent, for the
    /// Report section's Still to do summary. Empty otherwise.
    /// </summary>
    public IReadOnlyList<CaseTaskRecord> StillToDo { get; private set; } = [];

    private async Task<IReadOnlyList<CaseTaskRecord>> ReadStillToDoAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return
            [
                .. (await caseTaskQueries.ListAsync(id, cancellationToken))
                    .Where(task => task.State == CaseTaskState.Open)
            ];
        }
        catch (KeyNotFoundException)
        {
            // The Case page was drawn from the same store, so a Case with no workflow row
            // has no tasks to report.
            return [];
        }
    }

    public Task<IActionResult> OnPostCreateCaseTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        string description,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "create_case_task",
            actor => createCaseTask.ExecuteAsync(
                new(
                    id,
                    taskId,
                    expectedVersion,
                    actor,
                    RequireOperationKey(operationKey),
                    TaskLabels.AddReason,
                    editLeaseToken,
                    description),
                cancellationToken),
            TaskLabels.AddedNotice,
            RedirectToTasks,
            keepEditing: true);

    public Task<IActionResult> OnPostCompleteCaseTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        long expectedTaskVersion,
        string operationKey,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "complete_case_task",
            actor => completeCaseTask.ExecuteAsync(
                new(
                    id,
                    taskId,
                    expectedVersion,
                    expectedTaskVersion,
                    actor,
                    RequireOperationKey(operationKey),
                    TaskLabels.CompleteReason,
                    editLeaseToken),
                cancellationToken),
            TaskLabels.CompletedNotice,
            RedirectToTasks,
            keepEditing: true);

    public Task<IActionResult> OnPostCancelCaseTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        long expectedTaskVersion,
        string operationKey,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "cancel_case_task",
            actor => cancelCaseTask.ExecuteAsync(
                new(
                    id,
                    taskId,
                    expectedVersion,
                    expectedTaskVersion,
                    actor,
                    RequireOperationKey(operationKey),
                    TaskLabels.CancelReason,
                    editLeaseToken),
                cancellationToken),
            TaskLabels.CancelledNotice,
            RedirectToTasks,
            keepEditing: true);

    /// <summary>The Tasks section is where a task is read back, in the view it was acted from.</summary>
    private RedirectToPageResult RedirectToTasks(Guid id) =>
        RedirectToPage("/Cases/Details", new { id, section = "tasks", view = ReturnView });
}
