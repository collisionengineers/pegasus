using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's workflow actions: hold and release, return to Review, Assign
/// Engineer, and the linked replacement for a
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
    AuthoriseGarage authoriseGarage,
    UndoGarageAuthorisation undoGarageAuthorisation,
    ICreateLinkedReplacement createLinkedReplacement,
    ICaseWorkflowQueries workflows,
    CaseEditLeases leases,
    ILogger<WorkflowModel> logger) : CaseMutationPageModel(logger), Pegasus.Web.Presentation.IShellFreeGet
{
    /// <summary>
    /// An action taken inside the edit session keeps it: after one succeeds
    /// the session carries on (operator, 9 October 2026).
    /// </summary>
    protected override (ICaseWorkflowQueries Workflows, CaseEditLeases Leases)? LeaseReclaim => (workflows, leases);

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
                    CaseTransitionDestination.Review),
                cancellationToken),
            "The case returned to Review.");

    /// <summary>
    /// Assign Engineer, and Take, which posts the signed-in person's staff id
    /// (#1213): the Engineer is read through the Work Centre's shared parse,
    /// whose Unassigned value hands the Case to nobody, and Core composes the
    /// history reason.
    /// </summary>
    public Task<IActionResult> OnPostAssignEngineerAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? editLeaseToken,
        string? engineerId,
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
                    string.Empty,
                    lease,
                    Pegasus.Web.Presentation.AssignEngineerChoice.Parse(engineerId)),
                cancellationToken),
            Pegasus.Web.Presentation.OperatorLabels.WorkCentre.Assigned);

    /// <summary>
    /// Authorise garage's Mark done (operator, 17 November 2026; R1142): one
    /// post with no reason, since Core writes the history reason (R1161). It
    /// shows no notice: the Case's own stage and Next action say what happened.
    /// </summary>
    public Task<IActionResult> OnPostAuthoriseGarageAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "authorise_garage",
            async (actor, lease) =>
            {
                await authoriseGarage.ExecuteAsync(
                    new(id, expectedVersion, actor, operationKey, string.Empty, lease),
                    cancellationToken);
                return null;
            },
            _ => CaseCommandRefused);

    /// <summary>Undo garage authorisation, with its reason (R1142).</summary>
    public Task<IActionResult> OnPostUndoGarageAuthorisationAsync(
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
            "undo_garage_authorisation",
            async (actor, lease) =>
            {
                await undoGarageAuthorisation.ExecuteAsync(
                    new(id, expectedVersion, actor, operationKey, reason, lease),
                    cancellationToken);
                return null;
            },
            _ => CaseCommandRefused);

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
