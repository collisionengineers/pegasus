using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Vehicle;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Vehicle page — the DVLA and MOT lookup — and the Case workspace's
/// Vehicle section that calls it (EPIC-011 §1.8).
///
/// ENG-016 removed the EVA half of this file with the act it covered: the
/// GenerateEvaHandoff handler and the Eva/Download page are gone, and the
/// export that replaced them is covered where it now lives — the Details
/// action bar (<c>CaseDetailsWebTests</c>) and the store
/// (<c>CustodyOutboxIntegrationTests</c>).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseVehicleWebTests
{
    [Fact]
    public async Task CaseOverviewUsesAcceptedFactsAndLeavesVehicleFactsInVehicleSection()
    {
        var store = new RecordingCaseDetailsStore();
        var data = await store.GetAsync(store.CaseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The vehicle fixture did not return case data.");
        var make = data.Vehicle.Make.Confirmed
            ?? throw new InvalidOperationException("The vehicle fixture has no confirmed make.");
        var model = data.Vehicle.Model.Confirmed
            ?? throw new InvalidOperationException("The vehicle fixture has no confirmed model.");
        var registration = data.Vehicle.Registration.Confirmed
            ?? throw new InvalidOperationException("The vehicle fixture has no confirmed registration.");
        var circumstances = data.Accident.Circumstances.Confirmed
            ?? throw new InvalidOperationException("The vehicle fixture has no confirmed circumstances.");

        CaseField<string> Values(CaseDataValue<string> source, string fact, string? confirmed = null) =>
            new(
                source with
                {
                    Value = fact,
                    Kind = CaseDataValueKind.Fact,
                    ConfirmedByActor = null,
                    ConfirmedAtUtc = null
                },
                null,
                confirmed is null ? null : source with { Value = confirmed });

        store.DataOverride = data with
        {
            Vehicle = data.Vehicle with
            {
                Registration = new(null, null, registration),
                Make = Values(make, "Fact make", "Confirmed make"),
                Model = Values(model, "Fact model")
            },
            Accident = data.Accident with
            {
                Circumstances = Values(circumstances, "Fact circumstances")
            }
        };
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}");
        var overview = OverviewPanel(html);

        Assert.Contains("Fact circumstances", overview, StringComparison.Ordinal);
        Assert.Contains("<label for=\"f-incident-date\">Incident date</label>", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("Incident detail", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("Confirmed make Fact model", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("AB12CDE", overview, StringComparison.Ordinal);
        Assert.DoesNotContain("Fact make", overview, StringComparison.Ordinal);
        // WP6: while the lease is held the vehicle's identity is edited where
        // it is read, still through the record's one Save form.
        Assert.Contains(
            "id=\"edit-make\" class=\"fi\" name=\"vehicleMake\" form=\"case-edit-form\" maxlength=\"100\" value=\"Confirmed make\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "id=\"edit-model\" class=\"fi\" name=\"vehicleModel\" form=\"case-edit-form\" maxlength=\"100\" value=\"Fact model\"",
            html,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The combined description an instruction states instead of a make and a
    /// model stays on the record as its own source field, but the section no
    /// longer draws a row for it: the operator acts on Make and Model, and the
    /// description is neither of them.
    /// </summary>
    [Fact]
    public async Task TheSourceDescriptionStaysOnTheRecordAndLeavesTheVehicleSection()
    {
        var store = new RecordingCaseDetailsStore();
        var data = await store.GetAsync(store.CaseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The vehicle fixture did not return case data.");
        store.DataOverride = data with
        {
            Vehicle = data.Vehicle with
            {
                Make = new(null, null, null),
                Model = new(null, null, null),
                Description = new(
                    new(
                        "SEAT LEON SPORT TDI 105",
                        CaseDataValueKind.Fact,
                        new(
                            CaseDataSourceKind.IntakeEvidence,
                            "receipt-token",
                            "attachment-6 page-1",
                            "qdos_instruction",
                            8)),
                    null,
                    null)
            }
        };
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await GetHtmlAsync(
            workspace.Client,
            $"/Cases/{store.CaseId:D}?section=vehicle");

        var held = await store.GetAsync(store.CaseId, CancellationToken.None);
        Assert.Equal("SEAT LEON SPORT TDI 105", held!.Vehicle.Description.Fact!.Value);

        Assert.DoesNotContain("Source vehicle description", html, StringComparison.Ordinal);
        Assert.DoesNotContain("SEAT LEON SPORT TDI 105", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"vehicleDescription\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VehiclePageBindsTheLookupRequest()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<IRequestVehicleLookup>(services, store));

        using var requested = await workspace.PostAsync(
            "Vehicle?handler=RequestVehicleLookup",
            workspace.MutationForm("request-lookup", "Registration on the instruction", ("registration", "AB12 CDE")));

        AssertPrg(requested, store.CaseId, "?section=vehicle");

        var lookup = Assert.Single(store.LookupRequests);
        AssertClaimant(workspace, lookup.Actor);
        Assert.Equal(store.CaseVersion, lookup.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, lookup.EditLeaseToken);
        Assert.Equal("request-lookup", lookup.OperationKey);
        Assert.Equal("AB12 CDE", lookup.Registration);

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
    /// The lookup is the section's one command and Core refuses it without the
    /// edit authority, so a read-only visit draws no handler at all.
    /// </summary>
    [Fact]
    public async Task TheLookupRendersOnlyInEditContext()
    {
        var store = new RecordingCaseDetailsStore { VehicleLookupEvidence = LookupEvidence() };

        var readOnly = await ReadOnlyVehicleSectionAsync(store);

        Assert.DoesNotContain("handler=RequestVehicleLookup", readOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=AcceptVehicleSuggestion", readOnly, StringComparison.Ordinal);

        using var workspace = await EnterEditModeAsync(store, _ => { });
        var editing = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=vehicle");

        Assert.Equal(
            1,
            CountOccurrences(editing, $"/Cases/{store.CaseId:D}/Vehicle?handler=RequestVehicleLookup"));
        Assert.DoesNotContain("handler=AcceptVehicleSuggestion", editing, StringComparison.Ordinal);
    }

    /// <summary>
    /// One mileage, stated once, with the provenance its report code is read
    /// from. The section used to draw the case's figure, the MOT reading and a
    /// suggested reading as three rows, which left the operator to work out
    /// which of them the report would print.
    /// </summary>
    [Fact]
    public async Task MileageShowsOneBoxWithItsProvenance()
    {
        var store = new RecordingCaseDetailsStore { VehicleLookupEvidence = LookupEvidence() };
        var data = await store.GetAsync(store.CaseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The vehicle fixture did not return case data.");
        store.DataOverride = data with
        {
            Vehicle = data.Vehicle with
            {
                Mileage = LookedUp(49_089L),
                MileageUnit = LookedUp("Miles")
            }
        };

        var html = await ReadOnlyVehicleSectionAsync(store);

        var mileage = MileageCell(html);

        // v26: one odometer figure in the mileage cell, its provenance as a
        // text tag beside it, and the Mileage source cell reading the same word.
        Assert.Equal(1, CountOccurrences(html, "49,089"));
        Assert.Contains("49,089 mi", mileage, StringComparison.Ordinal);
        Assert.Contains(
            "<span class=\"src-tag src-tag--lookup\" data-vehicle-mileage-source-read>Lookup</span>",
            mileage,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MOT mileage", html, StringComparison.Ordinal);
        Assert.DoesNotContain("DVLA suggests", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"vehicleMileageSource\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A lookup fills the fields the case left empty, so the values themselves
    /// are the lookup's answer and each says so.
    /// </summary>
    [Fact]
    public async Task LookupFilledValuesRenderWithLookupProvenance()
    {
        var store = new RecordingCaseDetailsStore { VehicleLookupEvidence = LookupEvidence() };
        var data = await store.GetAsync(store.CaseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The vehicle fixture did not return case data.");
        store.DataOverride = data with
        {
            Vehicle = data.Vehicle with
            {
                Make = LookedUp("Ford"),
                Model = LookedUp("Transit"),
                Year = LookedUp("2018")
            }
        };

        var html = await ReadOnlyVehicleSectionAsync(store);

        // v26: the value cell carries the lookup's text tag rather than an icon.
        const string lookupTag = "<span class=\"src-tag src-tag--lookup\" data-provenance-word=\"Lookup\">Lookup</span>";
        Assert.Matches(">Ford\\s*" + Regex.Escape(lookupTag), html);
        Assert.Matches(">Transit\\s*" + Regex.Escape(lookupTag), html);
        Assert.Matches(">2018\\s*" + Regex.Escape(lookupTag), html);
        Assert.True(CountOccurrences(html, "data-provenance-word=\"Lookup\"") >= 3);
    }

    /// <summary>
    /// Only staff know who told them a mileage they typed, so the three people
    /// a case hears one from ride beside the box for as long as the box can be
    /// typed into — including on a case whose current figure was looked up,
    /// because the next figure typed over it may be an owner's. A read-only
    /// case is asked nothing, and a looked-up figure that stands keeps
    /// answering the question on its own.
    /// </summary>
    [Fact]
    public async Task TheMileageSourceChoiceIsOfferedInEditMode()
    {
        var staffStore = new RecordingCaseDetailsStore();
        using var staffWorkspace = await EnterEditModeAsync(staffStore, _ => { });

        var staffHtml = await GetHtmlAsync(
            staffWorkspace.Client,
            $"/Cases/{staffStore.CaseId:D}?section=vehicle");

        Assert.Contains(
            "<select id=\"edit-mileage-source\" class=\"fi\" name=\"vehicleMileageSource\" form=\"case-edit-form\">",
            staffHtml,
            StringComparison.Ordinal);
        Assert.Contains("<label for=\"edit-mileage-source\">Mileage source</label>", staffHtml, StringComparison.Ordinal);
        foreach (var code in CaseVehicleMileageSourcePolicy.StaffChoices)
        {
            Assert.Contains(
                $"<option value=\"{code}\"",
                staffHtml,
                StringComparison.Ordinal);
            Assert.Contains(
                CaseWorkspaceLabels.Vehicle.MileageSource(code),
                staffHtml,
                StringComparison.Ordinal);
        }

        var lookupStore = new RecordingCaseDetailsStore { VehicleLookupEvidence = LookupEvidence() };
        var data = await lookupStore.GetAsync(lookupStore.CaseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The vehicle fixture did not return case data.");
        lookupStore.DataOverride = data with
        {
            Vehicle = data.Vehicle with { Mileage = LookedUp(49_089L) }
        };
        using var lookupWorkspace = await EnterEditModeAsync(lookupStore, _ => { });

        var lookupHtml = await GetHtmlAsync(
            lookupWorkspace.Client,
            $"/Cases/{lookupStore.CaseId:D}?section=vehicle");

        Assert.Contains("name=\"vehicleMileage\"", lookupHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"vehicleMileageSource\"", lookupHtml, StringComparison.Ordinal);

        var readOnlyHtml = await ReadOnlyVehicleSectionAsync(new RecordingCaseDetailsStore());

        Assert.DoesNotContain("name=\"vehicleMileageSource\"", readOnlyHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// The history check is prose about the vehicle, not one of its facts, so
    /// it is titled and read as its own area rather than a row in the grid.
    /// </summary>
    [Fact]
    public async Task VehicleHistoryIsItsOwnLabelledArea()
    {
        var store = new RecordingCaseDetailsStore();

        var readOnly = await ReadOnlyVehicleSectionAsync(store);

        Assert.Contains("data-vehicle-history>", readOnly, StringComparison.Ordinal);
        Assert.Contains("<h3>Vehicle history", readOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"edit-vehicle-history\"", readOnly, StringComparison.Ordinal);

        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<IGetAssessmentAccess>(services, store));
        var editing = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=vehicle");

        Assert.Contains("data-vehicle-history>", editing, StringComparison.Ordinal);
        Assert.Contains("<h3>Vehicle history", editing, StringComparison.Ordinal);
        // The Engineer fields on Vehicle join the edit session in Not ready.
        Assert.Contains("id=\"edit-vehicle-condition\"", editing, StringComparison.Ordinal);
        Assert.Contains("id=\"edit-vehicle-history\"", editing, StringComparison.Ordinal);
    }

    /// <summary>
    /// EPIC-011 D7/D22 and ENG-001: Experian is not connected, so the seam is
    /// named where its control would sit — a <c>.gated</c> pill carrying the
    /// reason in its text — with no button, no handler and (v26) no
    /// <c>data-condition</c> tooltip behind it. It is drawn, never claimed.
    /// </summary>
    [Fact]
    public async Task ExperianRendersAsANamedDisabledSeamWithNoHandler()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=vehicle");
        var seam = ExperianSeam(html);

        Assert.Contains("class=\"gated\"", seam, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.CaseWorkspace.ExperianSeamCondition, seam, StringComparison.Ordinal);
        Assert.DoesNotContain("<button", seam, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=", seam, StringComparison.Ordinal);
        Assert.DoesNotContain("data-condition", seam, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.CaseWorkspace.RunExperianCheck, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The lookup needs a registration to search on. Without one the control is
    /// absent (v26: never disabled with a tooltip), and the head's lookup line
    /// still says what the lookup has done — nothing yet.
    /// </summary>
    [Fact]
    public async Task RefreshControlsStateTheirConditionWhenNoRegistrationIsRecorded()
    {
        var store = new RecordingCaseDetailsStore { OmitVehicleValues = true };
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=vehicle");

        Assert.DoesNotContain("handler=RequestVehicleLookup", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-vehicle-lookup>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-condition", html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.VehicleLookup.NotYetLookedUp, LookupLine(html), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LookupUsesAnAcceptedFactButNeverARegistrationSuggestion()
    {
        var acceptedStore = new RecordingCaseDetailsStore();
        var acceptedData = await acceptedStore.GetAsync(acceptedStore.CaseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The vehicle fixture did not return case data.");
        var acceptedRegistration = acceptedData.Vehicle.Registration.Confirmed
            ?? throw new InvalidOperationException("The vehicle fixture has no confirmed registration.");
        acceptedStore.DataOverride = acceptedData with
        {
            Vehicle = acceptedData.Vehicle with
            {
                Registration = new(acceptedRegistration with
                {
                    Kind = CaseDataValueKind.Fact,
                    ConfirmedByActor = null,
                    ConfirmedAtUtc = null
                }, null, null)
            }
        };
        using var acceptedWorkspace = await EnterEditModeAsync(acceptedStore, _ => { });

        var acceptedHtml = await GetHtmlAsync(
            acceptedWorkspace.Client,
            $"/Cases/{acceptedStore.CaseId:D}?section=vehicle");
        var acceptedLookup = LookupForm(acceptedHtml);

        Assert.Contains("name=\"registration\" value=\"AB12CDE\"", acceptedLookup, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", acceptedLookup, StringComparison.Ordinal);

        var suggestedStore = new RecordingCaseDetailsStore();
        var suggestedData = await suggestedStore.GetAsync(suggestedStore.CaseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The vehicle fixture did not return case data.");
        suggestedStore.DataOverride = suggestedData with
        {
            Vehicle = suggestedData.Vehicle with
            {
                Registration = new(
                    null,
                    suggestedData.Vehicle.Registration.Confirmed! with
                    {
                        Kind = CaseDataValueKind.Suggestion,
                        ConfirmedByActor = null,
                        ConfirmedAtUtc = null
                    },
                    null)
            }
        };
        using var suggestedWorkspace = await EnterEditModeAsync(suggestedStore, _ => { });

        var suggestedHtml = await GetHtmlAsync(
            suggestedWorkspace.Client,
            $"/Cases/{suggestedStore.CaseId:D}?section=vehicle");
        // v26: a suggestion is not a registration to search on, and a lookup
        // that cannot run is absent rather than disabled.
        Assert.DoesNotContain("handler=RequestVehicleLookup", suggestedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"registration\" value=\"AB12CDE\"", suggestedHtml, StringComparison.Ordinal);
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
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentAccess>(
                    services,
                    (IGetAssessmentAccess)new FakeGetAssessmentAccess(canOpenAssessment));
                Substitute<IGetAssessmentWorkspace>(services, store);
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

    /// <summary>
    /// WP8: read mode says what the lookup did. A live Case recorded
    /// <c>not_found</c> for its registration and the section drew nothing at
    /// all — a failed, not-found or throttled outcome fills no field and
    /// rendered nowhere — so the operator concluded no lookup had ever run.
    /// The outcome line is what tells them otherwise.
    /// </summary>
    [Fact]
    public async Task ReadModeStatesTheLookupOutcome()
    {
        var recordedAtUtc = new DateTimeOffset(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);
        var store = new RecordingCaseDetailsStore
        {
            VehicleLookupEvidence = NotFoundEvidence(recordedAtUtc)
        };

        var html = await ReadOnlyVehicleSectionAsync(store);

        // v26: the head's one lookup line, with no row title.
        Assert.Contains(
            $"Not found for AB12CDE ({OperatorLabels.OfficeTime(recordedAtUtc)})",
            LookupLine(html),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            OperatorLabels.VehicleLookup.NotYetLookedUp,
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain("handler=AcceptVehicleSuggestion", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A case with no recorded lookup at all says exactly that, rather than
    /// leaving the section silent.
    /// </summary>
    [Fact]
    public async Task ACaseWithNoRecordedLookupSaysSo()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, _ => { });

        var html = await GetHtmlAsync(workspace.Client, $"/Cases/{store.CaseId:D}?section=vehicle");

        Assert.Contains(
            OperatorLabels.VehicleLookup.NotYetLookedUp,
            html,
            StringComparison.Ordinal);
    }

    /// <summary>The case's vehicle section as an operator without the lease sees it.</summary>
    private static async Task<string> ReadOnlyVehicleSectionAsync(RecordingCaseDetailsStore store)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var readOnlyFactory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
            }));
        using var readOnlyClient = readOnlyFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        return await GetHtmlAsync(readOnlyClient, $"/Cases/{store.CaseId:D}?section=vehicle");
    }

    /// <summary>A field the DVLA and MOT lookup filled, as the fill records it.</summary>
    private static CaseField<T> LookedUp<T>(T value)
        where T : notnull =>
        new(
            new(
                value,
                CaseDataValueKind.Fact,
                new(
                    CaseDataSourceKind.VehicleLookup,
                    "latest-observation",
                    "DVLA lookup",
                    "vehicle-lookup",
                    1)),
            null,
            null);

    /// <summary>One recorded lookup that answered "no such vehicle".</summary>
    private static CaseVehicleEvidence NotFoundEvidence(DateTimeOffset recordedAtUtc)
    {
        var caseId = Guid.NewGuid();
        VehicleLookupObservation notFound = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            caseId,
            1,
            VehicleLookupOutcome.NotFound,
            "AB12CDE",
            new("dvla-ves+dvsa-mot-history", "1", "response-1", recordedAtUtc, null, null),
            null,
            [],
            null,
            null,
            recordedAtUtc);
        return new(caseId, null, notFound, [notFound], []);
    }

    /// <summary>The v26 Experian seam: the head's `.gated` pill and its text.</summary>
    private static string ExperianSeam(string html)
    {
        var marker = html.IndexOf("data-vehicle-experian-seam", StringComparison.Ordinal);
        Assert.True(marker >= 0, "The Experian seam is not rendered.");
        var start = html.LastIndexOf('<', marker);
        // Up to the close of the pill's text span: the icon and the wording.
        var end = html.IndexOf("</span>", marker, StringComparison.Ordinal);
        Assert.True(end > start, "The Experian seam is not closed.");
        return html[start..end];
    }

    /// <summary>The head's one lookup line (v26): what the latest lookup did.</summary>
    private static string LookupLine(string html)
    {
        var marker = html.IndexOf("data-vehicle-lookup-line>", StringComparison.Ordinal);
        Assert.True(marker >= 0, "The lookup line is not rendered.");
        var end = html.IndexOf("</span>", marker, StringComparison.Ordinal);
        Assert.True(end > marker, "The lookup line is not closed.");
        return html[marker..end];
    }

    /// <summary>The v26 mileage read cell, with its provenance tag.</summary>
    private static string MileageCell(string html)
    {
        var marker = html.IndexOf("data-vehicle-mileage-read>", StringComparison.Ordinal);
        Assert.True(marker >= 0, "The mileage cell is not rendered.");
        var end = html.IndexOf("</div>", marker, StringComparison.Ordinal);
        Assert.True(end > marker, "The mileage cell is not closed.");
        return html[marker..end];
    }

    private static string LookupForm(string html)
    {
        const string handler = "handler=RequestVehicleLookup";
        var start = html.IndexOf(handler, StringComparison.Ordinal);
        Assert.True(start >= 0, "The vehicle lookup form must render in edit mode.");
        start = html.LastIndexOf("<form", start, StringComparison.Ordinal);
        var end = html.IndexOf("</form>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The vehicle lookup form must close.");
        return html[start..(end + "</form>".Length)];
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


}
