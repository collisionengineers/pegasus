using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's vehicle actions: one DVLA/DVSA lookup and accepting
/// one resulting field suggestion. Every action redirects back to the workspace.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class VehicleModel(
    IRequestVehicleLookup requestVehicleLookup,
    IAcceptVehicleSuggestion acceptVehicleSuggestion,
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

    public Task<IActionResult> OnPostAcceptVehicleSuggestionAsync(
        Guid id,
        long expectedVersion,
        Guid lookupObservationId,
        VehicleSuggestionField field,
        string operationKey,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "accept_vehicle_suggestion",
            actor => acceptVehicleSuggestion.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    lookupObservationId,
                    VehicleSuggestionDecision.Accept,
                    null,
                    actor,
                    operationKey,
                    "Accepted vehicle lookup suggestion.",
                    editLeaseToken)
                {
                    Field = field
                },
                cancellationToken),
            "The vehicle field was updated from the lookup suggestion.");
}
