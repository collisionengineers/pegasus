using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's due work: tasks, manual chases, and the report-Sent evidence links
/// that drive chasing. Every action redirects back to the workspace.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed partial class TasksModel(
    ICreateCaseTask createCaseTask,
    IAssignCaseTask assignCaseTask,
    ICompleteCaseTask completeCaseTask,
    ICancelCaseTask cancelCaseTask,
    IRecordManualCaseChase recordManualCaseChase,
    ILinkReportEvidence linkReportEvidence,
    IUnlinkReportEvidence unlinkReportEvidence,
    ILogger<TasksModel> logger) : CaseMutationPageModel(logger)
{
    public Task<IActionResult> OnPostCreateTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        string description,
        Guid? assigneeId,
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
                    operationKey,
                    reason,
                    editLeaseToken,
                    description,
                    assigneeId),
                cancellationToken),
            "The case task was created.");

    public Task<IActionResult> OnPostAssignTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        long expectedTaskVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        Guid? assigneeId,
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
                    operationKey,
                    reason,
                    editLeaseToken,
                    assigneeId),
                cancellationToken),
            "The case task assignment was updated.");

    public Task<IActionResult> OnPostCompleteTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        long expectedTaskVersion,
        string operationKey,
        string reason,
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
                    operationKey,
                    reason,
                    editLeaseToken),
                cancellationToken),
            "The case task was completed.");

    public Task<IActionResult> OnPostCancelTaskAsync(
        Guid id,
        Guid taskId,
        long expectedVersion,
        long expectedTaskVersion,
        string operationKey,
        string reason,
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
                    operationKey,
                    reason,
                    editLeaseToken),
                cancellationToken),
            "The case task was cancelled.");

    public Task<IActionResult> OnPostRecordManualChaseAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        DateTimeOffset attemptedAtUtc,
        string channel,
        string targetPartyOrAddress,
        string outcome,
        string? note,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "record_manual_chase",
            actor => recordManualCaseChase.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    editLeaseToken,
                    actor,
                    operationKey,
                    reason,
                    channel,
                    targetPartyOrAddress,
                    attemptedAtUtc,
                    outcome,
                    note),
                cancellationToken),
            "The manual chase was recorded and the next chase date was scheduled.");

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
