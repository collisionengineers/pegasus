using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's post-report and lifecycle outcomes: report approval, the four named
/// terminal outcomes, reopening through the destination gates, and archiving. Every action
/// redirects back to the workspace.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ClosureModel(
    IRecordCaseReportApproval recordCaseReportApproval,
    ICloseCase closeCase,
    IReopenCase reopenCase,
    IReturnCaseToEngineer returnToEngineer,
    IArchiveCase archiveCase,
    ILogger<ClosureModel> logger) : CaseMutationPageModel(logger)
{
    public Task<IActionResult> OnPostRecordReportApprovalAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        Guid approvalId,
        string artifactIdentity,
        string artifactSha256,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "record_report_approval",
            actor => recordCaseReportApproval.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    new(
                        approvalId,
                        artifactIdentity,
                        artifactSha256)),
                cancellationToken),
            "The immutable report artifact was approved; this does not claim it was sent.");

    /// <summary>
    /// The Case workspace's adverse disposition. The outcome is nullable and
    /// checked because the chooser is required: an omitted or unrecognised
    /// value must be refused, not fall through to the enum's default, which
    /// would complete the Case instead of closing it. Which outcomes exist for
    /// the current state is Core's decision and is applied by
    /// <see cref="ICloseCase"/> itself.
    /// </summary>
    public Task<IActionResult> OnPostCloseAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CaseClosureOutcome? outcome,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "close",
            actor => closeCase.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    RequireOutcome(outcome)),
                cancellationToken),
            "The selected terminal outcome was recorded.");

    private static CaseClosureOutcome RequireOutcome(CaseClosureOutcome? outcome) =>
        outcome is { } selected && Enum.IsDefined(selected)
            ? selected
            : throw new InvalidOperationException("A closure outcome is required.");

    public Task<IActionResult> OnPostCompleteAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "complete_case",
            actor => closeCase.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    CaseClosureOutcome.PostReportComplete),
                cancellationToken),
            "The case is now Completed.");

    public Task<IActionResult> OnPostReopenAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CaseReopenDestination destination,
        bool instructionsComplete,
        bool imagesComplete,
        string? evidenceReference,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "reopen",
            actor => reopenCase.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    destination,
                    destination == CaseReopenDestination.Review
                        ? Readiness(
                            instructionsComplete,
                            imagesComplete,
                            evidenceReference ?? string.Empty)
                        : null),
                cancellationToken),
            "The case was reopened through the selected destination gates.");

    public Task<IActionResult> OnPostReturnToEngineerAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "return_to_engineer",
            actor => returnToEngineer.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, reason, editLeaseToken),
                cancellationToken),
            "The case was returned to Engineer.");

    public Task<IActionResult> OnPostArchiveAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "archive_case",
            actor => archiveCase.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, reason, editLeaseToken),
                cancellationToken),
            "The terminal case was archived and is now read-only.");
}
