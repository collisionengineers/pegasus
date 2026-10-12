using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Tasks;
using TaskLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Tasks;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Tasks section (FRD-16, CASE-20): complete, cancel, assign and add a Case task from the
/// Case record. Each action runs under the edit session's lease with one fixed recorded reason,
/// so the section carries no reason box. A task the lease consumed is claimed again (save as
/// you go, 29 September 2026), so the session carries on, and the action lands back on the
/// Tasks section.
/// </summary>
public sealed partial class DetailsModel
{
    /// <summary>
    /// The staff an open task may be assigned to: the enabled named staff, read only with the
    /// Tasks section's body while it can be changed, so no other read pays for them.
    /// </summary>
    public IReadOnlyList<CaseEngineerChoice> TaskAssigneeChoices { get; private set; } = [];

    /// <summary>Whether the Tasks body being built offers its actions and so its assignees.</summary>
    private bool TasksReadAssignees => TasksSection is not null && CanEditCaseData;

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

    public Task<IActionResult> OnPostAssignCaseTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        long expectedTaskVersion,
        string operationKey,
        string editLeaseToken,
        Guid assigneeId,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "assign_case_task",
            actor => assignCaseTask.ExecuteAsync(
                new(
                    id,
                    taskId,
                    expectedVersion,
                    expectedTaskVersion,
                    actor,
                    RequireOperationKey(operationKey),
                    TaskLabels.AssignReason,
                    editLeaseToken,
                    assigneeId),
                cancellationToken),
            TaskLabels.AssignedNotice,
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
