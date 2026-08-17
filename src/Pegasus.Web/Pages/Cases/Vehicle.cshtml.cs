using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case workspace's vehicle and EVA actions: DVLA/DVSA lookups, accepting or correcting a
/// vehicle suggestion, and generating the deterministic EVA handoff. Every action redirects back
/// to the workspace; the handoff download is its own page.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class VehicleModel(
    IRequestVehicleLookup requestVehicleLookup,
    IAcceptVehicleSuggestion acceptVehicleSuggestion,
    IEvaHandoffQueries evaHandoffQueries,
    IGenerateEvaHandoff generateEvaHandoff,
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
        VehicleSuggestionDecision decision,
        string? registration,
        string? make,
        string? model,
        long? mileage,
        VehicleMileageUnit? mileageUnit,
        string operationKey,
        string reason,
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
                    decision,
                    decision == VehicleSuggestionDecision.Correct
                        ? new(
                            registration ?? string.Empty,
                            make,
                            model,
                            mileage,
                            mileageUnit)
                        : null,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken),
                cancellationToken),
            decision == VehicleSuggestionDecision.Accept
                ? "The vehicle suggestion was accepted with its external provenance."
                : "The corrected vehicle values were confirmed with attributable provenance.");

    public async Task<IActionResult> OnPostGenerateEvaHandoffAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var preparation = await evaHandoffQueries.GetPreparationAsync(id, cancellationToken);
            if (preparation is null || preparation.Images.Count == 0)
            {
                PreserveLeaseState(id, editLeaseToken);
                TempData["CaseError"] = "The EVA handoff was not generated because no eligible images are available.";
                return RedirectToDetails(id);
            }

            var result = await generateEvaHandoff.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    reason,
                    editLeaseToken),
                cancellationToken);
            if (result.Outcome == GenerateEvaHandoffOutcome.Generated)
            {
                ClearLeaseState();
                TempData["CaseStatus"] =
                    $"EVA handoff revision {result.Revision} was generated deterministically.";
            }
            else
            {
                PreserveLeaseState(id, editLeaseToken);
                TempData["CaseError"] = result.Reasons.Count == 0
                    ? "The EVA handoff was not generated because the case evidence changed."
                    : string.Join(" ", result.Reasons);
            }
        }
        catch (StaffAuthorizationException)
        {
            ClearLeaseState();
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "generate_eva_handoff", exception);
            HandleLeaseFailure(id, editLeaseToken, exception);
            RetainProposedValues(id);
            TempData["CaseError"] =
                "The EVA handoff was not generated because the case changed, edit mode was lost, or bundle generation is unavailable.";
        }

        return RedirectToDetails(id);
    }
}
