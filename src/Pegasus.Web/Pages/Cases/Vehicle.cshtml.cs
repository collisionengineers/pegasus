using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's vehicle action: one DVLA/DVSA lookup, whose answers
/// fill the Case's own empty fields. It redirects back to the Vehicle section.
/// The lookup is an immediate post inside the edit session (v25 decision F):
/// the store consumes the lease the request carried, so after success this
/// page claims a fresh one on the new version and the session carries on.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class VehicleModel(
    IRequestVehicleLookup requestVehicleLookup,
    IGetCase getCase,
    IAcquireCaseEditLease acquireLease,
    ILogger<VehicleModel> logger) : CaseMutationPageModel(logger)
{
    protected override (IGetCase Cases, IAcquireCaseEditLease Leases)? LeaseReclaim => (getCase, acquireLease);

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
            caseId => RedirectToPage("/Cases/Details", new { id = caseId, section = "vehicle" }),
            keepEditing: true);
}
