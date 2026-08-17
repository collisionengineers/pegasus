using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's workflow actions: hold and release, return to Review, Engineer
/// assignment and findings, starting report preparation, and the linked replacement for a
/// case created in error. Every action redirects back to the workspace.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class WorkflowModel(
    IHoldCase holdCase,
    IReleaseCase releaseCase,
    ITransitionCase transitionCase,
    IAssignCaseEngineer assignEngineer,
    IRecordEngineerFinding recordEngineerFinding,
    ICreateLinkedReplacement createLinkedReplacement,
    ILogger<WorkflowModel> logger) : CaseMutationPageModel(logger)
{
    public Task<IActionResult> OnPostHoldAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "hold",
            actor => holdCase.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, reason, editLeaseToken),
                cancellationToken),
            "The case was put on hold.");

    public Task<IActionResult> OnPostReleaseHoldAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "release_hold",
            actor => releaseCase.ExecuteAsync(
                new ChangeCaseStateRequest(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken),
                cancellationToken),
            "The case hold was released.");

    public Task<IActionResult> OnPostReturnToReviewAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        bool instructionsComplete,
        bool imagesComplete,
        bool instructionsReviewedByStaff,
        bool imagesReviewedByStaff,
        string evidenceReference,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "return_to_review",
            actor => transitionCase.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    CaseTransitionDestination.Review,
                    Readiness(
                        instructionsComplete,
                        imagesComplete,
                        instructionsReviewedByStaff,
                        imagesReviewedByStaff,
                        evidenceReference)),
                cancellationToken),
            "The case returned to Review.");

    public Task<IActionResult> OnPostAssignEngineerAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        Guid engineerId,
        bool instructionsComplete,
        bool imagesComplete,
        bool instructionsReviewedByStaff,
        bool imagesReviewedByStaff,
        string evidenceReference,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "assign_engineer",
            actor => assignEngineer.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    engineerId,
                    Readiness(
                        instructionsComplete,
                        imagesComplete,
                        instructionsReviewedByStaff,
                        imagesReviewedByStaff,
                        evidenceReference)),
                cancellationToken),
            "The Engineer was assigned.");

    public Task<IActionResult> OnPostStartWorkAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "start_work",
            actor => transitionCase.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    CaseTransitionDestination.ReportPreparation),
                cancellationToken),
            "Report preparation was started.");

    public Task<IActionResult> OnPostRecordEngineerFindingAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        AuditAssessment assessment,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "record_engineer_finding",
            actor => recordEngineerFinding.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    assessment),
                cancellationToken),
            "The Engineer finding was recorded.");

    public async Task<IActionResult> OnPostCreateLinkedReplacementAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        string replacementPrincipalCode,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var outcome = await createLinkedReplacement.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    replacementPrincipalCode),
                cancellationToken);
            ClearLeaseState();
            TempData["CaseStatus"] = outcome.IsDuplicate
                ? $"Replacement case {outcome.Identity.Reference} was already allocated."
                : $"Replacement case {outcome.Identity.Reference} was allocated and linked.";
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "create_linked_replacement", exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            RetainProposedValues(id);
            TempData["CaseError"] =
                "The corrected replacement could not be created because the case changed or the request is not permitted.";
        }

        return RedirectToDetails(id);
    }
}
