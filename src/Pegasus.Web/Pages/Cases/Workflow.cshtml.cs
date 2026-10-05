using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's workflow actions: hold and release, return to Review, Engineer
/// handoff, and the linked replacement for a
/// case created in error. Every action redirects back to the workspace. The
/// Actions-menu items run under the session's lease or, outside a session,
/// one claimed for the action (operator, 29 September 2026).
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class WorkflowModel(
    IHoldCase holdCase,
    IReleaseCase releaseCase,
    ITransitionCase transitionCase,
    IAssignCaseEngineer assignEngineer,
    ISetCaseSignOffEngineer setSignOffEngineer,
    ICreateLinkedReplacement createLinkedReplacement,
    ILogger<WorkflowModel> logger) : CaseMutationPageModel(logger)
{
    public IActionResult OnGet() => NotFound();

    public Task<IActionResult> OnPostHoldAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string? editLeaseToken,
        DateOnly? reviewOn,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "hold",
            (actor, lease) => holdCase.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, reason, lease, reviewOn),
                cancellationToken),
            "The case was put on hold.");

    public Task<IActionResult> OnPostReleaseHoldAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "release_hold",
            (actor, lease) => releaseCase.ExecuteAsync(
                new ChangeCaseStateRequest(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    lease),
                cancellationToken),
            "The case hold was released.");

    public Task<IActionResult> OnPostReturnToReviewAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string? editLeaseToken,
        bool instructionsComplete,
        bool imagesComplete,
        string evidenceReference,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "return_to_review",
            (actor, lease) => transitionCase.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    lease,
                    CaseTransitionDestination.Review,
                    Readiness(
                        instructionsComplete,
                        imagesComplete,
                        evidenceReference)),
                cancellationToken),
            "The case returned to Review.");

    public Task<IActionResult> OnPostAssignEngineerAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? editLeaseToken,
        Guid engineerId,
        bool instructionsComplete,
        bool imagesComplete,
        string evidenceReference,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "assign_engineer",
            (actor, lease) => assignEngineer.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.HandToEngineer,
                    lease,
                    engineerId,
                    Readiness(
                        instructionsComplete,
                        imagesComplete,
                        evidenceReference)),
                cancellationToken),
            "The case was handed to the Engineer.");

    public Task<IActionResult> OnPostSetSignOffEngineerAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        Guid signOffEngineerId,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "set_sign_off_engineer",
            actor => setSignOffEngineer.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken,
                    signOffEngineerId),
                cancellationToken),
            "The Sign-off Engineer was set.");

    public Task<IActionResult> OnPostCreateLinkedReplacementAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string? editLeaseToken,
        string replacementPrincipalCode,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "create_linked_replacement",
            async (actor, lease) =>
            {
                var outcome = await createLinkedReplacement.ExecuteAsync(
                    new(
                        id,
                        expectedVersion,
                        actor,
                        operationKey,
                        reason,
                        lease,
                        replacementPrincipalCode),
                    cancellationToken);
                return outcome.IsDuplicate
                    ? $"Replacement case {outcome.Identity.Reference} was already allocated."
                    : $"Replacement case {outcome.Identity.Reference} was allocated and linked.";
            },
            _ => "The corrected replacement could not be created because the case changed or the request is not permitted.");
}
