using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's post-report and lifecycle outcomes: the four named
/// terminal outcomes, reopening through the destination gates, and archiving. Every action
/// redirects back to the workspace.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ClosureModel(
    ICloseCase closeCase,
    IReopenCase reopenCase,
    IReturnCaseToEngineer returnToEngineer,
    IRaiseCaseQuery raiseQuery,
    IResolveCaseQueries resolveQueries,
    IArchiveCase archiveCase,
    ICaseWorkflowQueries workflows,
    IAcquireCaseEditLease acquireLease,
    ILogger<ClosureModel> logger) : CaseMutationPageModel(logger)
{
    /// <summary>
    /// An action taken inside the edit session keeps it unless the Case is
    /// now terminal or archived (operator, 9 October 2026).
    /// </summary>
    protected override (ICaseWorkflowQueries Workflows, IAcquireCaseEditLease Leases)? LeaseReclaim => (workflows, acquireLease);

    public IActionResult OnGet() => NotFound();

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
        string? editLeaseToken,
        CaseClosureOutcome? outcome,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "close",
            (actor, lease) => closeCase.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    lease,
                    RequireOutcome(outcome)),
                cancellationToken),
            "The selected terminal outcome was recorded.");

    private static CaseClosureOutcome RequireOutcome(CaseClosureOutcome? outcome) =>
        outcome is { } selected && Enum.IsDefined(selected)
            ? selected
            : throw new InvalidOperationException("A closure outcome is required.");

    /// <summary>
    /// Log query (operator, 11 October 2026): a query that reached the office
    /// outside Pegasus's mail, by the channel chosen, with what it is. The
    /// envelope's reason is the details, so history reads them.
    /// </summary>
    public Task<IActionResult> OnPostLogQueryAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? editLeaseToken,
        CaseQueryChannel channel,
        string? details,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "log_query",
            (actor, lease) => raiseQuery.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, details?.Trim() ?? string.Empty, lease, channel, details),
                cancellationToken),
            "The query was logged.");

    /// <summary>Resolve query: every open query on the Case closes at once.</summary>
    public Task<IActionResult> OnPostResolveQueryAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "resolve_query",
            (actor, lease) => resolveQueries.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, string.Empty, lease),
                cancellationToken),
            "The query was resolved.");

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
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "return_to_engineer",
            (actor, lease) => returnToEngineer.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, reason, lease),
                cancellationToken),
            "The case was returned to Engineer.");

    public Task<IActionResult> OnPostArchiveAsync(
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
            "archive_case",
            (actor, lease) => archiveCase.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, reason, lease),
                cancellationToken),
            "The terminal case was archived and is now read-only.");
}
