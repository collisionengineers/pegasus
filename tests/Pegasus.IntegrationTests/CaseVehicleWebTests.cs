using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Vehicle page — lookups, suggestion decisions, and the deterministic EVA handoff — and the
/// EVA download page, the one case action that answers with a file.
/// </summary>
public sealed partial class CaseDetailsWebTests
{
    private static readonly byte[] EvaBundleBytes = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00];

    [Fact]
    public async Task VehiclePageBindsLookupSuggestionDecisionsAndEvaGeneration()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IRequestVehicleLookup>(services, store);
            Substitute<IAcceptVehicleSuggestion>(services, store);
            Substitute<IEvaHandoffQueries>(services, store);
            Substitute<IGenerateEvaHandoff>(services, store);
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
        using var generated = await workspace.PostAsync(
            "Vehicle?handler=GenerateEvaHandoff",
            workspace.MutationForm("generate-eva", "Images reviewed"));

        AssertPrg(requested, store.CaseId);
        AssertPrg(accepted, store.CaseId);
        AssertPrg(corrected, store.CaseId);
        AssertPrg(generated, store.CaseId);

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

        var generation = Assert.Single(store.EvaGenerations);
        AssertClaimant(workspace, generation.Actor);
        Assert.Equal(store.CaseVersion, generation.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, generation.EditLeaseToken);
        Assert.Equal("generate-eva", generation.OperationKey);
        Assert.Equal("Images reviewed", generation.Reason);
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("EVA handoff revision 2 was generated deterministically.", html, StringComparison.Ordinal);

        await AssertRefusalKeepsEditModeAsync(
            workspace,
            "Vehicle?handler=RequestVehicleLookup",
            workspace.MutationForm("request-lookup-2", "Try again", ("registration", "AB12CDE")));
    }

    [Fact]
    public async Task EvaDownloadPageStreamsTheRevisionWithIntegrityHeadersAndReturnsToTheWorkspaceWhenRefused()
    {
        var store = new RecordingCaseDetailsStore { ExposeCustodyAndEva = true };
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<IDownloadEvaHandoff>(services, store));

        using var download = await workspace.PostAsync(
            "Eva/Download",
            workspace.MutationForm("download-eva", "Manual EVA drag-and-drop", ("revision", "1")));
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(EvaHandoffRevisionArtifact.MediaType, download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("EVA-QDOS3100042-Revision-001.zip", download.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(EvaBundleBytes.Length, download.Content.Headers.ContentLength);
        Assert.Equal("nosniff", Assert.Single(download.Headers.GetValues("X-Content-Type-Options")));
        Assert.True(download.Headers.CacheControl is { Private: true, NoStore: true });
        Assert.Equal(
            $"sha-256=:{Convert.ToBase64String(SHA256.HashData(EvaBundleBytes))}:",
            Assert.Single(download.Headers.GetValues("Content-Digest")));
        Assert.Equal(EvaBundleBytes, await download.Content.ReadAsByteArrayAsync());

        var request = Assert.Single(store.EvaDownloads);
        AssertClaimant(workspace, request.Actor);
        Assert.Equal(1, request.Revision);
        Assert.Equal(store.CaseVersion, request.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, request.EditLeaseToken);
        Assert.Equal("download-eva", request.OperationKey);
        Assert.Equal("Manual EVA drag-and-drop", request.Reason);

        store.RefuseEvaDownload = true;
        using var refused = await workspace.PostAsync(
            "Eva/Download",
            workspace.MutationForm("download-eva-2", "Second attempt", ("revision", "1")));
        AssertPrg(refused, store.CaseId);
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("The handoff revision is not available.", html, StringComparison.Ordinal);
        Assert.Equal(store.LeaseToken, InputValue(html, "editLeaseToken"));

        using var missing = await workspace.PostAsync(
            "Eva/Download",
            workspace.MutationForm("download-eva-3", "Nothing there", ("revision", "0")));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private sealed partial class RecordingCaseDetailsStore :
        IRequestVehicleLookup,
        IAcceptVehicleSuggestion,
        IEvaHandoffQueries,
        IGenerateEvaHandoff,
        IDownloadEvaHandoff
    {
        public List<RequestVehicleLookupCommand> LookupRequests { get; } = [];
        public List<AcceptVehicleSuggestionCommand> SuggestionDecisions { get; } = [];
        public List<GenerateEvaHandoffRequest> EvaGenerations { get; } = [];
        public List<DownloadEvaHandoffRequest> EvaDownloads { get; } = [];
        public bool RefuseEvaDownload { get; set; }

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

        Task<EvaHandoffPreparation?> IEvaHandoffQueries.GetPreparationAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<EvaHandoffPreparation?>(new(
                CaseId,
                CaseVersion,
                "QDOS3100042",
                [new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "damage.jpg", "image/jpeg", 12, new string('a', 64), DocumentSource.StaffUpload, "fixture", 1)],
                [],
                null,
                []));

        Task<EvaHandoffRevisionArtifact?> IEvaHandoffQueries.GetRevisionAsync(
            Guid caseId,
            int revision,
            ActionActor actor,
            CancellationToken cancellationToken) =>
            Task.FromResult<EvaHandoffRevisionArtifact?>(null);

        Task<GenerateEvaHandoffResult> IGenerateEvaHandoff.ExecuteAsync(
            GenerateEvaHandoffRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            EvaGenerations.Add(request);
            return Task.FromResult(new GenerateEvaHandoffResult(
                GenerateEvaHandoffOutcome.Generated,
                null,
                [],
                Revision: 2));
        }

        Task<DownloadEvaHandoffResult> IDownloadEvaHandoff.ExecuteAsync(
            DownloadEvaHandoffRequest request,
            CancellationToken cancellationToken)
        {
            EvaDownloads.Add(request);
            if (RefuseEvaDownload)
            {
                return Task.FromResult(new DownloadEvaHandoffResult(
                    DownloadEvaHandoffOutcome.Refused,
                    null,
                    "The handoff revision is not available."));
            }

            return Task.FromResult(new DownloadEvaHandoffResult(
                DownloadEvaHandoffOutcome.Prepared,
                new(
                    request.Revision,
                    $"EVA-QDOS3100042-Revision-{request.Revision.ToString("000", CultureInfo.InvariantCulture)}.zip",
                    EvaBundleBytes,
                    new string('b', 64)),
                "Prepared."));
        }
    }
}
