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

    public Task<IActionResult> OnPostCloseAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CaseClosureOutcome outcome,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "close",
            actor => closeCase.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, reason, editLeaseToken, outcome),
                cancellationToken),
            "The selected terminal outcome was recorded.");

    public Task<IActionResult> OnPostReopenAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CaseReopenDestination destination,
        bool instructionsComplete,
        bool imagesComplete,
        bool instructionsReviewedByStaff,
        bool imagesReviewedByStaff,
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
                            instructionsReviewedByStaff,
                            imagesReviewedByStaff,
                            evidenceReference ?? string.Empty)
                        : null),
                cancellationToken),
            "The case was reopened through the selected destination gates.");

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
