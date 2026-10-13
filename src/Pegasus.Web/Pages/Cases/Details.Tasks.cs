using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Tasks;
using TaskLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Tasks;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Tasks dialog (FRD-16, CASE-20): complete, cancel, assign and add a Case task from the
/// Case record, in and out of an edit session (operator, 13 October 2026). In a session each
/// action runs under the session's lease, which is claimed again so the session carries on
/// (save as you go, 29 September 2026); outside one each claims the Case's lease for that
/// one command, as the other Actions-menu dialogs do. Each records one fixed reason, so the
/// dialog carries no reason box, and the action lands back on the Tasks dialog.
/// </summary>
public sealed partial class DetailsModel
{
    /// <summary>
    /// The staff an open task may be assigned to: the enabled named staff, read only with the
    /// Tasks dialog while it can be changed, so no other read pays for them.
    /// </summary>
    public IReadOnlyList<CaseEngineerChoice> TaskAssigneeChoices { get; private set; } = [];

    public Task<IActionResult> OnPostCreateCaseTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        string operationKey,
        string? editLeaseToken,
        string description,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "create_case_task",
            async (actor, lease) =>
            {
                await createCaseTask.ExecuteAsync(
                    new(
                        id,
                        taskId,
                        expectedVersion,
                        actor,
                        RequireOperationKey(operationKey),
                        TaskLabels.AddReason,
                        lease,
                        description),
                    cancellationToken);
                return TaskLabels.AddedNotice;
            },
            _ => CaseCommandRefused,
            RedirectToTasks);

    public Task<IActionResult> OnPostAssignCaseTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        long expectedTaskVersion,
        string operationKey,
        string? editLeaseToken,
        Guid assigneeId,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "assign_case_task",
            async (actor, lease) =>
            {
                await assignCaseTask.ExecuteAsync(
                    new(
                        id,
                        taskId,
                        expectedVersion,
                        expectedTaskVersion,
                        actor,
                        RequireOperationKey(operationKey),
                        TaskLabels.AssignReason,
                        lease,
                        assigneeId),
                    cancellationToken);
                return TaskLabels.AssignedNotice;
            },
            _ => CaseCommandRefused,
            RedirectToTasks);

    public Task<IActionResult> OnPostCompleteCaseTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        long expectedTaskVersion,
        string operationKey,
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "complete_case_task",
            async (actor, lease) =>
            {
                await completeCaseTask.ExecuteAsync(
                    new(
                        id,
                        taskId,
                        expectedVersion,
                        expectedTaskVersion,
                        actor,
                        RequireOperationKey(operationKey),
                        TaskLabels.CompleteReason,
                        lease),
                    cancellationToken);
                return TaskLabels.CompletedNotice;
            },
            _ => CaseCommandRefused,
            RedirectToTasks);

    public Task<IActionResult> OnPostCancelCaseTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        long expectedTaskVersion,
        string operationKey,
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "cancel_case_task",
            async (actor, lease) =>
            {
                await cancelCaseTask.ExecuteAsync(
                    new(
                        id,
                        taskId,
                        expectedVersion,
                        expectedTaskVersion,
                        actor,
                        RequireOperationKey(operationKey),
                        TaskLabels.CancelReason,
                        lease),
                    cancellationToken);
                return TaskLabels.CancelledNotice;
            },
            _ => CaseCommandRefused,
            RedirectToTasks);

    /// <summary>
    /// The Tasks dialog is where a task is read back: the action returns to the section and view
    /// it was taken from with the dialog drawn open.
    /// </summary>
    private RedirectToPageResult RedirectToTasks(Guid id) =>
        RedirectToPage("/Cases/Details", new { id, section = ReturnSection, view = ReturnView, dialog = "tasks" });
}
