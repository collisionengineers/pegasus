using Microsoft.AspNetCore.Mvc.Testing;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Vehicle;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Vehicle page — lookups and suggestion decisions — and the Case
/// workspace's Vehicle section that calls them (EPIC-011 §1.8).
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
    public async Task VehiclePageBindsLookupAndOneFieldSuggestionAcceptance()
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
                ("field", "Make")));

        AssertPrg(requested, store.CaseId);
        AssertPrg(accepted, store.CaseId);

        var lookup = Assert.Single(store.LookupRequests);
        AssertClaimant(workspace, lookup.Actor);
        Assert.Equal(store.CaseVersion, lookup.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, lookup.EditLeaseToken);
        Assert.Equal("request-lookup", lookup.OperationKey);
        Assert.Equal("AB12 CDE", lookup.Registration);

        var acceptance = Assert.Single(store.SuggestionDecisions);
        AssertClaimant(workspace, acceptance.Actor);
        Assert.Equal(store.CaseVersion, acceptance.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, acceptance.EditLeaseToken);
        Assert.Equal("accept-suggestion", acceptance.OperationKey);
        Assert.Equal("Accepted vehicle lookup suggestion.", acceptance.Reason);
        Assert.Equal(observationId, acceptance.LookupObservationId);
        Assert.Equal(VehicleSuggestionDecision.Accept, acceptance.Decision);
        Assert.Null(acceptance.Correction);
        Assert.Equal(VehicleSuggestionField.Make, acceptance.Field);

        await AssertRefusalKeepsEditModeAsync(
            workspace,
            "Vehicle?handler=RequestVehicleLookup",
            workspace.MutationForm("request-lookup-2", "Try again", ("registration", "AB12CDE")));
    }

    /// <summary>
    /// EPIC-011 §1.8 Vehicle checks: the two refresh controls post the one
    /// lookup handler the case already has, because a single lookup returns
    /// both the vehicle record and the MOT observations. The recorded checks
    /// are the case's own lookup observations.
    /// </summary>
    [Fact]
    public async Task VehicleSectionDrawsOneLookupAndNoLegacyChecksSurface()
    {
        var store = new RecordingCaseDetailsStore { VehicleLookupEvidence = LookupEvidence() };
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=vehicle");

        Assert.Contains(
            System.Net.WebUtility.HtmlEncode(CaseWorkspaceLabels.Vehicle.LookupDvlaMot),
            html,
            StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(html, $"/Cases/{store.CaseId:D}/Vehicle?handler=RequestVehicleLookup"));
        Assert.Contains("AB12CDE", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Vehicle checks", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Recorded checks", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Refresh DVLA", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Refresh DVSA/MOT", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The suggestion decisions are the only surface for
    /// <see cref="IAcceptVehicleSuggestion"/>, whose forms PR #599 removed. They
    /// render in edit context only, because Core refuses the command without the
    /// edit authority, so a read-only visit draws neither them nor the lookup.
    /// </summary>
    [Fact]
    public async Task VehicleSuggestionDecisionsRenderOnlyInEditContext()
    {
        var store = new RecordingCaseDetailsStore
        {
            VehicleLookupEvidence = LookupEvidence(),
            IncludeVehicleSuggestions = true
        };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var readOnlyFactory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => Substitute<IGetCase>(services, store)));
        using var readOnlyClient = readOnlyFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var readOnly = await GetHtmlAsync(readOnlyClient, $"/Cases/{store.CaseId:D}?section=vehicle");

        Assert.DoesNotContain("handler=AcceptVehicleSuggestion", readOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=RequestVehicleLookup", readOnly, StringComparison.Ordinal);

        using var workspace = await EnterEditModeAsync(store, _ => { });
        var editing = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=vehicle");

        Assert.Equal(
            3,
            CountOccurrences(editing, $"/Cases/{store.CaseId:D}/Vehicle?handler=AcceptVehicleSuggestion"));
        Assert.Contains("name=\"field\" value=\"Make\"", editing, StringComparison.Ordinal);
        Assert.Contains("name=\"field\" value=\"Model\"", editing, StringComparison.Ordinal);
        Assert.Contains("name=\"field\" value=\"Mileage\"", editing, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"Correct\"", editing, StringComparison.Ordinal);
    }

    /// <summary>
    /// EPIC-011 D7/D22 and ENG-001: Experian is not connected, so its control is
    /// drawn as a real disabled button with the reason named on it and no
    /// handler behind it. It is drawn, never claimed. PLAT-061: a
    /// <c>.gated</c> wrapper with no condition paints an empty pill, so no gate
    /// on this page may carry an empty one.
    /// </summary>
    [Fact]
    public async Task ExperianRendersAsANamedDisabledSeamWithNoHandler()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=vehicle");
        var seam = GatedSpan(html, OperatorLabels.CaseWorkspace.ExperianSeamCondition);

        Assert.Contains("class=\"gated\"", seam, StringComparison.Ordinal);
        Assert.Contains("type=\"button\"", seam, StringComparison.Ordinal);
        Assert.Contains("disabled", seam, StringComparison.Ordinal);
        Assert.Contains("aria-disabled=\"true\"", seam, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.CaseWorkspace.RunExperianCheck, seam, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=", seam, StringComparison.Ordinal);
        Assert.DoesNotContain("data-condition=\"\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The lookup needs a registration to search on. Without one the control is
    /// present and disabled with its condition named — legitimate state, not an
    /// uncomposed seam.
    /// </summary>
    [Fact]
    public async Task RefreshControlsStateTheirConditionWhenNoRegistrationIsRecorded()
    {
        var store = new RecordingCaseDetailsStore { OmitVehicleValues = true };
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=vehicle");

        Assert.Contains("data-condition=\"No registration recorded\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-condition=\"\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// PLAT-061: `.gated::after` renders `attr(data-condition)` with no
    /// `[data-condition]` guard, so a gate whose condition is absent paints an
    /// empty pill. No state of the workspace may render one — including the
    /// state where the gated control is enabled and there is no condition left
    /// to state.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NoWorkspaceGateEverRendersAnEmptyCondition(bool canOpenAssessment)
    {
        var store = new RecordingCaseDetailsStore();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetAssessmentAccess>(
                    services,
                    (IGetAssessmentAccess)new FakeGetAssessmentAccess(canOpenAssessment));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        foreach (var section in new[] { string.Empty, "?section=vehicle", "?section=files", "?section=inspection", "?section=notes" })
        {
            var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}{section}");
            Assert.DoesNotContain("data-condition=\"\"", html, StringComparison.Ordinal);
        }
    }

    /// <summary>The whole <c>.gated</c> element carrying the named condition.</summary>
    private static string GatedSpan(string html, string condition)
    {
        var marker = html.IndexOf(
            "data-condition=\"" + condition + "\"",
            StringComparison.Ordinal);
        Assert.True(marker >= 0, $"No gate states '{condition}'.");
        var start = html.LastIndexOf('<', marker);
        var end = html.IndexOf("</span>", marker, StringComparison.Ordinal);
        Assert.True(end > start, "The gate is not closed.");
        return html[start..end];
    }

    private static int CountOccurrences(string html, string value)
    {
        var count = 0;
        var index = html.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = html.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>
    /// Two recorded lookups on the documented estate's registration: one that
    /// answered, and one the provider refused.
    /// </summary>
    private static CaseVehicleEvidence LookupEvidence()
    {
        var caseId = Guid.NewGuid();
        var workItemId = Guid.NewGuid();
        var recordedAtUtc = new DateTimeOffset(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);
        VehicleLookupObservation answered = new(
            Guid.NewGuid(),
            workItemId,
            caseId,
            2,
            VehicleLookupOutcome.Current,
            "AB12CDE",
            new("dvla", "1", "response-1", recordedAtUtc, null, null),
            new("Ford", "Transit", 2018, 1998, "Diesel"),
            [],
            new(43_210, VehicleMileageUnit.Miles, new(2031, 2, 1), "latest-mot-observation", 2, 1),
            null,
            recordedAtUtc);
        VehicleLookupObservation refused = new(
            Guid.NewGuid(),
            workItemId,
            caseId,
            1,
            VehicleLookupOutcome.NotFound,
            "AB12CDE",
            new("dvla", "1", "response-0", recordedAtUtc.AddHours(-1), null, null),
            null,
            [],
            null,
            new("rate_limited", Retryable: true),
            recordedAtUtc.AddHours(-1));
        return new(caseId, null, answered, [answered, refused], []);
    }

    private sealed partial class RecordingCaseDetailsStore :
        IRequestVehicleLookup,
        IAcceptVehicleSuggestion
    {
        public List<RequestVehicleLookupCommand> LookupRequests { get; } = [];
        public List<AcceptVehicleSuggestionCommand> SuggestionDecisions { get; } = [];

        /// <summary>The case's recorded vehicle lookups, when a test supplies them.</summary>
        public CaseVehicleEvidence? VehicleLookupEvidence { get; init; }

        /// <summary>
        /// Drops the vehicle values from the projection, so the section renders
        /// the state a case with no registration is actually in.
        /// </summary>
        public bool OmitVehicleValues { get; init; }

        public bool IncludeVehicleSuggestions { get; init; }

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
