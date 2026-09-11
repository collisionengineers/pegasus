using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's vehicle action: one DVLA/DVSA lookup, whose answers
/// fill the Case's own empty fields. It redirects back to the workspace.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class VehicleModel(
    IRequestVehicleLookup requestVehicleLookup,
    ILogger<VehicleModel> logger) : CaseMutationPageModel(logger)
{
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
            "The vehicle lookup was queued. Refresh later for current, stale, partial, no-result, unavailable, or failed evidence.");
}
