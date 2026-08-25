using Pegasus.Core.Vehicle;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Vehicle page — lookups and suggestion decisions.
///
/// ENG-016 removed the EVA half of this file with the act it covered: the
/// GenerateEvaHandoff handler and the Eva/Download page are gone, and the
/// export that replaced them is covered where it now lives — the Details
/// action bar (<c>CaseDetailsWebTests</c>) and the store
/// (<c>CustodyOutboxIntegrationTests</c>).
/// </summary>
public sealed partial class CaseDetailsWebTests
{
    [Fact]
    public async Task VehiclePageBindsLookupAndSuggestionDecisions()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IRequestVehicleLookup>(services, store);
            Substitute<IAcceptVehicleSuggestion>(services, store);
        });
        var observationId = Guid.NewGuid();

        using var requested = await workspace.PostAsync(
            "Vehicle?handler=RequestVehicleLookup",
            workspace.MutationForm("request-lookup", "Registration on the instruction", ("registration", "AB12 CDE")));
        using var accepted = await workspace.PostAsync(
            "Vehicle?handler=AcceptVehicleSuggestion",
            workspace.MutationForm(
                "accept-suggestion",
                "Matches the photographs",
                ("lookupObservationId", observationId.ToString("D")),
                ("decision", "Accept")));
        using var corrected = await workspace.PostAsync(
            "Vehicle?handler=AcceptVehicleSuggestion",
            workspace.MutationForm(
                "correct-suggestion",
                "Odometer photographed",
                ("lookupObservationId", observationId.ToString("D")),
                ("decision", "Correct"),
                ("registration", "AB12CDE"),
                ("make", "Ford"),
                ("model", "Transit"),
                ("mileage", "43210"),
                ("mileageUnit", "Miles")));

        AssertPrg(requested, store.CaseId);
        AssertPrg(accepted, store.CaseId);
        AssertPrg(corrected, store.CaseId);

        var lookup = Assert.Single(store.LookupRequests);
        AssertClaimant(workspace, lookup.Actor);
        Assert.Equal(store.CaseVersion, lookup.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, lookup.EditLeaseToken);
        Assert.Equal("request-lookup", lookup.OperationKey);
        Assert.Equal("AB12 CDE", lookup.Registration);

        Assert.Equal(2, store.SuggestionDecisions.Count);
        var acceptance = store.SuggestionDecisions[0];
        AssertClaimant(workspace, acceptance.Actor);
        Assert.Equal(store.CaseVersion, acceptance.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, acceptance.EditLeaseToken);
        Assert.Equal("accept-suggestion", acceptance.OperationKey);
        Assert.Equal("Matches the photographs", acceptance.Reason);
        Assert.Equal(observationId, acceptance.LookupObservationId);
        Assert.Equal(VehicleSuggestionDecision.Accept, acceptance.Decision);
        Assert.Null(acceptance.Correction);
        var correction = store.SuggestionDecisions[1];
        Assert.Equal(VehicleSuggestionDecision.Correct, correction.Decision);
        Assert.Equal("correct-suggestion", correction.OperationKey);
        Assert.Equal(
            new VehicleConfirmationValues("AB12CDE", "Ford", "Transit", 43210, VehicleMileageUnit.Miles),
            correction.Correction);

        await AssertRefusalKeepsEditModeAsync(
            workspace,
            "Vehicle?handler=RequestVehicleLookup",
            workspace.MutationForm("request-lookup-2", "Try again", ("registration", "AB12CDE")));
    }

    private sealed partial class RecordingCaseDetailsStore :
        IRequestVehicleLookup,
        IAcceptVehicleSuggestion
    {
        public List<RequestVehicleLookupCommand> LookupRequests { get; } = [];
        public List<AcceptVehicleSuggestionCommand> SuggestionDecisions { get; } = [];

        Task<RequestedVehicleLookup> IRequestVehicleLookup.ExecuteAsync(
            RequestVehicleLookupCommand command,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            LookupRequests.Add(command);
            return Task.FromResult(new RequestedVehicleLookup(
                Guid.NewGuid(),
                CaseId,
                command.Registration,
                VehicleLookupWorkState.Pending,
                CaseVersion + 1,
                IsReplay: false));
        }

        Task<AcceptedVehicleSuggestion> IAcceptVehicleSuggestion.ExecuteAsync(
            AcceptVehicleSuggestionCommand command,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            SuggestionDecisions.Add(command);
            return Task.FromResult(new AcceptedVehicleSuggestion(
                Guid.NewGuid(),
                CaseId,
                command.LookupObservationId,
                command.Decision,
                command.Correction ?? new("AB12CDE", "Ford", "Transit", 42_000, VehicleMileageUnit.Miles),
                new("dvla", "1", "response-1", _now, null, null),
                CaseVersion + 1,
                IsReplay: false));
        }
    }
}
