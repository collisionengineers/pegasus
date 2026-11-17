using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's vehicle actions: one DVLA/DVSA lookup, whose answers
/// fill the Case's own empty fields, and Run Experian check, whose answer
/// replaces the Vehicle history text (ADR-0089). Each redirects back to the
/// Vehicle section.
/// The lookup is an immediate post inside the edit session (v25 decision F):
/// the store consumes the lease the request carried, so after success this
/// page claims a fresh one on the new version and the session carries on.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class VehicleModel(
    IRequestVehicleLookup requestVehicleLookup,
    RunVehicleHistoryCheck runVehicleHistoryCheck,
    ICaseWorkflowQueries workflows,
    CaseEditLeases leases,
    ILogger<VehicleModel> logger) : CaseMutationPageModel(logger), Pegasus.Web.Presentation.IShellFreeGet
{
    protected override (ICaseWorkflowQueries Workflows, CaseEditLeases Leases)? LeaseReclaim => (workflows, leases);

    public IActionResult OnGet() => NotFound();

    public Task<IActionResult> OnPostRequestVehicleLookupAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        string registration,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "request_vehicle_lookup",
            actor => requestVehicleLookup.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    registration,
                    actor,
                    operationKey,
                    editLeaseToken),
                cancellationToken),
            "The vehicle lookup was queued. Refresh later for current, stale, partial, no-result, unavailable, or failed evidence.",
            caseId => RedirectToPage("/Cases/Details", new { id = caseId, section = "vehicle", view = ReturnView }),
            keepEditing: true);

    /// <summary>
    /// Run Experian check, pressed while the Vehicle history text is editable
    /// (R746). A recorded answer needs no notice: the text is the answer. A
    /// vehicle Experian does not hold says so (R748), and a check that could not
    /// answer says it is unavailable (R747); neither changes the text.
    /// </summary>
    public Task<IActionResult> OnPostRunExperianCheckAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
        string registration,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandUnderLeaseAsync(
            id,
            expectedVersion,
            editLeaseToken,
            "run_experian_check",
            async (actor, lease) =>
                await runVehicleHistoryCheck.ExecuteAsync(
                    new(id, expectedVersion, actor, operationKey, lease, registration),
                    cancellationToken) == VehicleHistoryCheckOutcome.NotFound
                    ? Pegasus.Web.Presentation.CaseWorkspaceLabels.Vehicle.ExperianNoVehicleData
                    : null,
            exception => exception is VehicleHistoryCheckUnavailableException
                ? Pegasus.Web.Presentation.CaseWorkspaceLabels.Vehicle.ExperianCheckUnavailable
                : CaseCommandRefused,
            caseId => RedirectToPage("/Cases/Details", new { id = caseId, section = "vehicle", view = ReturnView }));
}
