using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Phase 5b, the five Case record gaps and the first-paint preferences:
/// Notes from client, the claim source chosen in Overview, Settlement's
/// Proposed column with Awaiting / Accepted / Corrected, the valuation
/// commentary text, the vehicle's VIN / type / body, and the rail, layout and
/// folded panels painted by the server from their cookies.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseRecordGapsV26WebTests
{
    [Fact]
    public async Task NotesFromClientSitBesideTheAccidentCircumstancesAndTravelWithTheSave()
    {
        var store = new RecordingCaseDetailsStore();
        var reading = OverviewPanel(await ReadCaseAsync(store));
        var band = Regex.Match(reading, "data-accident-band>(?<band>.*?)</section>|data-accident-band>(?<band>.*)", RegexOptions.Singleline).Groups["band"].Value;
        Assert.Contains(CaseWorkspaceLabels.Frame.AccidentCircumstances, band, StringComparison.Ordinal);
        Assert.Contains("data-client-notes", band, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Frame.NotesFromClient, band, StringComparison.Ordinal);
        Assert.Contains($"<div class=\"fv empty multi\">{OperatorLabels.CaseWorkspace.AbsentValue}</div>", band, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"clientNotes\"", reading, StringComparison.Ordinal);

        var ports = new RecordGapPorts(store);
        using var workspace = await EnterEditModeAsync(store, ports.Register);
        var editing = OverviewPanel(await workspace.GetWorkspaceAsync());
        Assert.Contains("name=\"clientNotes\" form=\"case-edit-form\" maxlength=\"4000\"", editing, StringComparison.Ordinal);

        using var response = await SaveAsync(workspace, ("clientNotes", "The claimant says the car was parked."));

        AssertPrg(response, store.CaseId);
        Assert.Equal("The claimant says the car was parked.", Assert.Single(store.Saves).Overview!.ClientNotes);
        Assert.Equal(new DateOnly(2031, 5, 10), store.Saves[0].Overview!.DueBy);
    }

    [Fact]
    public async Task TheClaimSourceIsChosenFromActiveRecordsAndCopiedOntoTheCase()
    {
        var store = new RecordingCaseDetailsStore();
        var ports = new RecordGapPorts(store);
        var source = new ContactDirectoryRecord(
            Guid.NewGuid(), "Acme Claims", "A Handler", "claims@acme.example", "0113 000 0000", null, null, true,
            [ContactRole.ClaimSource], 4, null, null, NotesOnEveryCase: "Quote the Acme reference.");
        ports.ClaimSources.Add(source);
        using var workspace = await EnterEditModeAsync(store, ports.Register);

        var editing = WebUtility.HtmlDecode(OverviewPanel(await workspace.GetWorkspaceAsync()));
        Assert.Contains("name=\"claimSourceId\" form=\"case-edit-form\" data-claim-source-select", editing, StringComparison.Ordinal);
        Assert.Matches(
            $"<option value=\"{source.OrganizationId:D}\" data-notes=\"Quote the Acme reference.\" data-contact=\"A Handler · 0113 000 0000 · claims@acme.example\">Acme Claims</option>",
            editing);
        Assert.Contains("data-record-notes-slot=\"claim-source\" hidden", editing, StringComparison.Ordinal);

        using (var chosen = await SaveAsync(workspace, ("claimSourceId", source.OrganizationId.ToString("D"))))
        {
            AssertPrg(chosen, store.CaseId);
        }
        var snapshot = Assert.Single(store.Saves).Overview!.ClaimSource;
        Assert.Equal(new CaseWorkspaceClaimSource(
            source.OrganizationId, 4, "Acme Claims", "A Handler", "0113 000 0000", "claims@acme.example"), snapshot);

        using (var none = await SaveAsync(workspace, ("claimSourceId", string.Empty)))
        {
            AssertPrg(none, store.CaseId);
        }
        Assert.Null(store.Saves[^1].Overview!.ClaimSource);

        using var refused = await SaveAsync(workspace, ("claimSourceId", Guid.NewGuid().ToString("D")));
        Assert.Equal(2, store.Saves.Count);
    }

    [Fact]
    public async Task SettlementProposedColumnShowsEachStatusAndOffersAcceptOnlyWhileAwaiting()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation
        };
        var ports = new RecordGapPorts(store);
        ports.Automation(AssessmentVocabulary.Outcome, "repairable");
        ports.Staff(AssessmentVocabulary.LegalStatus, "roadworthy");
        ports.Staff(AssessmentVocabulary.SalvageCategory, "N");
        ports.Propose(AssessmentVocabulary.Outcome, "repairable", CaseFieldProposalStatus.Awaiting);
        ports.Propose(AssessmentVocabulary.LegalStatus, "roadworthy", CaseFieldProposalStatus.Accepted);
        ports.Propose(AssessmentVocabulary.SalvageCategory, "S", CaseFieldProposalStatus.Corrected);
        using var workspace = await EnterEditModeAsync(store, ports.Register);

        var settlement = SectionHtml(await workspace.GetWorkspaceAsync(), "settlement");

        Assert.Contains("class=\"decisions has-proposal\"", settlement, StringComparison.Ordinal);
        Assert.Contains($"1 {CaseWorkspaceLabels.Settlement.AwaitingReview}", settlement, StringComparison.Ordinal);
        var outcome = ProposalCell(settlement, AssessmentVocabulary.Outcome);
        Assert.Contains("data-proposal-status=\"Awaiting\"", outcome, StringComparison.Ordinal);
        Assert.Contains($"status--amber\">{CaseWorkspaceLabels.Settlement.Awaiting}<", outcome, StringComparison.Ordinal);
        Assert.Contains($"data-accept-proposal=\"{AssessmentVocabulary.Outcome}\"", outcome, StringComparison.Ordinal);
        var legal = ProposalCell(settlement, AssessmentVocabulary.LegalStatus);
        Assert.Contains($"status--green\">{CaseWorkspaceLabels.Settlement.Accepted}<", legal, StringComparison.Ordinal);
        Assert.DoesNotContain("data-accept-proposal", legal, StringComparison.Ordinal);
        var category = ProposalCell(settlement, AssessmentVocabulary.SalvageCategory);
        Assert.Contains($"status--blue\">{CaseWorkspaceLabels.Settlement.Corrected}<", category, StringComparison.Ordinal);
        Assert.Contains("data-proposal-value=\"S\"", category, StringComparison.Ordinal);
        Assert.DoesNotContain("data-accept-proposal", category, StringComparison.Ordinal);
        // No proposal on the Engineer's Value row: the column is drawn, the cell is empty.
        Assert.Matches($"data-proposal=\"{Regex.Escape(AssessmentVocabulary.ValueEngineer)}\" data-proposal-status=\"\">\\s*</div>", settlement);
    }

    [Fact]
    public async Task ASaveWithoutAcceptLeavesAnAwaitingProposalUndecided()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation
        };
        var ports = new RecordGapPorts(store);
        ports.Automation(AssessmentVocabulary.Outcome, "repairable");
        using var workspace = await EnterEditModeAsync(store, ports.Register);

        using var response = await SaveAsync(
            workspace,
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.Outcome), string.Empty),
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.SettlementExcess), "250.00"));

        AssertPrg(response, store.CaseId);
        var fields = Assert.Single(store.Saves).Settlement!.AssessmentFields!;
        Assert.False(fields.ContainsKey(AssessmentVocabulary.Outcome), "An empty control beside an awaiting proposal is not a decision.");
        Assert.Equal("250.00", fields[AssessmentVocabulary.SettlementExcess]);
    }

    [Fact]
    public async Task TheReportSectionEditsTheValuationCommentaryTextBesideItsSwitch()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation
        };
        var ports = new RecordGapPorts(store);
        ports.Staff(AssessmentVocabulary.ReportValuationCommentaryText, "Guide adjusted up for low mileage.");
        using var workspace = await EnterEditModeAsync(store, ports.Register);

        var report = SectionHtml(await workspace.GetWorkspaceAsync(), "report");
        var name = CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.ReportValuationCommentaryText);
        Assert.Contains($"data-field=\"{AssessmentVocabulary.ReportValuationCommentaryText}\"", report, StringComparison.Ordinal);
        Assert.Contains(
            $"name=\"{name}\" form=\"case-edit-form\" maxlength=\"4000\">Guide adjusted up for low mileage.</textarea>",
            report,
            StringComparison.Ordinal);

        using var response = await SaveAsync(workspace, (name, "Guide adjusted down for prior damage."));

        AssertPrg(response, store.CaseId);
        Assert.Equal(
            "Guide adjusted down for prior damage.",
            Assert.Single(store.Saves).Report!.AssessmentFields![AssessmentVocabulary.ReportValuationCommentaryText]);
    }

    [Fact]
    public async Task TheVehicleSectionEditsVinTypeAndBodyOutsideTheEngineerSections()
    {
        var store = new RecordingCaseDetailsStore();
        var ports = new RecordGapPorts(store);
        ports.Automation(AssessmentVocabulary.VehicleVin, "WVWZZZ1JZXW000001");
        using var workspace = await EnterEditModeAsync(store, ports.Register);

        var vehicle = SectionHtml(await workspace.GetWorkspaceAsync(), "vehicle");
        Assert.Matches(
            new Regex(
                $"data-vehicle-identity=\"{Regex.Escape(AssessmentVocabulary.VehicleVin)}\">.*?WVWZZZ1JZXW000001<span class=\"src-tag src-tag--lookup\">",
                RegexOptions.Singleline),
            vehicle);
        // The input's later attributes sit on the next source line, so the
        // markup carries a line break between them.
        Assert.Matches(
            $"name=\"{Regex.Escape(CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.VehicleVin))}\" form=\"case-edit-form\"\\s+maxlength=\"17\"\\s+pattern=",
            vehicle);
        Assert.Matches(
            $"<select id=\"edit-vehicle-vehicle-type\" class=\"fi\" name=\"{Regex.Escape(CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.VehicleType))}\"",
            vehicle);
        Assert.Matches(
            $"name=\"{Regex.Escape(CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.VehicleBody))}\" form=\"case-edit-form\"\\s+maxlength=\"100\"",
            vehicle);
        Assert.DoesNotContain($"data-vehicle-provenance-row=\"{AssessmentVocabulary.VehicleVin}\"", vehicle, StringComparison.Ordinal);

        using var response = await SaveAsync(
            workspace,
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.VehicleVin), "1HGCM82633A004352"),
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.VehicleType), "van"),
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.VehicleBody), "Panel van"));

        AssertPrg(response, store.CaseId);
        var fields = Assert.Single(store.Saves).Vehicle!.AssessmentFields!;
        Assert.Equal("1HGCM82633A004352", fields[AssessmentVocabulary.VehicleVin]);
        Assert.Equal("van", fields[AssessmentVocabulary.VehicleType]);
        Assert.Equal("Panel van", fields[AssessmentVocabulary.VehicleBody]);
    }

    [Theory]
    [InlineData("pegasus-rail=collapsed; pegasus-case-layout=tabs; pegasus-collapsed=case.report|case.notes", true)]
    [InlineData("pegasus-rail=sideways; pegasus-case-layout=grid; pegasus-collapsed=CASE.REPORT|case report|case.report-with-a-key-far-longer-than-forty-chars", false)]
    [InlineData(null, false)]
    public async Task TheRailLayoutAndFoldedPanelsArePaintedFromTheirCookies(string? cookie, bool remembered)
    {
        var store = new RecordingCaseDetailsStore();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        if (cookie is not null)
        {
            client.DefaultRequestHeaders.Add("Cookie", cookie);
        }

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        var shell = Regex.Match(html, "<div class=\"(?<classes>app-shell[^\"]*)\" data-app-shell>").Groups["classes"].Value;
        var toggle = Regex.Match(html, "<button[^>]*data-rail-toggle[^>]*>").Value;
        var record = Regex.Match(html, "<article class=\"record case-record[^>]*>", RegexOptions.Singleline).Value;
        var overviewTag = Regex.Match(html, "<section class=\"[^\"]*\" id=\"section-overview\"").Value;
        // The Report section is never deferred, so its own markup (not a lazy
        // placeholder) carries the fold on a read-only visit.
        var vehicleTag = Regex.Match(html, "<section class=\"[^\"]*\" id=\"section-report\"").Value;
        var vehicleChevron = Regex.Match(SectionHtml(html, "report"), "<button[^>]*data-collapse-toggle[^>]*>", RegexOptions.Singleline).Value;
        if (remembered)
        {
            Assert.Contains("rail-collapsed", shell, StringComparison.Ordinal);
            Assert.Contains("aria-expanded=\"false\"", toggle, StringComparison.Ordinal);
            Assert.Contains("data-layout=\"tabs\"", record, StringComparison.Ordinal);
            Assert.Contains("is-active", overviewTag, StringComparison.Ordinal);
            Assert.Contains("is-collapsed", vehicleTag, StringComparison.Ordinal);
            Assert.Contains("aria-expanded=\"false\"", vehicleChevron, StringComparison.Ordinal);
            Assert.DoesNotContain("is-collapsed", overviewTag, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("rail-collapsed", shell, StringComparison.Ordinal);
            Assert.Contains("aria-expanded=\"true\"", toggle, StringComparison.Ordinal);
            Assert.Contains("data-layout=\"scroll\"", record, StringComparison.Ordinal);
            Assert.DoesNotContain("is-active", overviewTag, StringComparison.Ordinal);
            Assert.DoesNotContain("is-collapsed", vehicleTag, StringComparison.Ordinal);
            Assert.Contains("aria-expanded=\"true\"", vehicleChevron, StringComparison.Ordinal);
        }
    }

    private static Task<HttpResponseMessage> SaveAsync(LeasedWorkspace workspace, params (string Name, string Value)[] fields) =>
        workspace.Client.PostAsync(
            $"/Cases/{workspace.Store.CaseId:D}?handler=Save",
            Form(
                workspace.AntiforgeryToken,
                [
                    ("id", workspace.Store.CaseId.ToString("D")),
                    ("expectedVersion", workspace.Store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                    ("operationKey", Guid.NewGuid().ToString("N")),
                    ("editLeaseToken", workspace.Store.LeaseToken),
                    .. fields
                ]));

    /// <summary>One section element's markup, from its opening tag to the next section's.</summary>
    private static string SectionHtml(string html, string key)
    {
        var start = html.IndexOf($"id=\"section-{key}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The {key} section must render.");
        var next = html.IndexOf("<section class=\"record-section", start, StringComparison.Ordinal);
        return next < 0 ? html[start..] : html[start..next];
    }

    private static string ProposalCell(string settlement, string path)
    {
        var match = Regex.Match(
            settlement,
            $"<div class=\"prop\" data-proposal=\"{Regex.Escape(path)}\"(?<cell>.*?)</div>\\s*<div class=\"fc",
            RegexOptions.Singleline);
        Assert.True(match.Success, $"The proposal cell for {path} must render.");
        return match.Groups["cell"].Value;
    }

    /// <summary>
    /// The reads the five gaps add, substituted together: the assessment
    /// workspace and access, the recorded proposals and the Claim source records.
    /// </summary>
    private sealed class RecordGapPorts(RecordingCaseDetailsStore store) :
        IGetAssessmentWorkspace,
        IGetAssessmentAccess,
        ICaseFieldProposalQueries,
        IContactDirectoryQueries
    {
        private static readonly DateTimeOffset At = new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);

        public List<AssessmentFieldValue> Fields { get; } = [];

        public List<CaseFieldProposal> Proposals { get; } = [];

        public List<ContactDirectoryRecord> ClaimSources { get; } = [];

        public void Register(IServiceCollection services)
        {
            Substitute<ISaveCaseWorkspace>(services, store);
            Substitute<ICaseReportSnapshotSource>(services, store);
            Substitute<IGetAssessmentWorkspace>(services, this);
            Substitute<IGetAssessmentAccess>(services, this);
            Substitute<ICaseFieldProposalQueries>(services, this);
            Substitute<IContactDirectoryQueries>(services, this);
        }

        public void Automation(string path, string value) =>
            Fields.Add(new(path, value, ActorKind.Automation, "pegasus-automation", At, null, null));

        public void Staff(string path, string value) =>
            Fields.Add(new(path, value, ActorKind.Staff, "recorded-engineer", At, "recorded-engineer", At));

        public void Propose(string path, string value, CaseFieldProposalStatus status) =>
            Proposals.Add(new(
                path, value, "pegasus-automation", At, status,
                status == CaseFieldProposalStatus.Awaiting ? null : "recorded-engineer",
                status == CaseFieldProposalStatus.Awaiting ? null : At.AddHours(1)));

        Task<AssessmentWorkspace?> IGetAssessmentWorkspace.ExecuteAsync(
            GetAssessmentWorkspaceQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<AssessmentWorkspace?>(AssessmentWorkspaceTestData.Create(new CaseAssessmentProjection(
                store.CaseId, "QDOS3100042", store.CaseVersion, store.State, null, [.. Fields], [],
                new("AB12CDE", null, null, null, null, null, "tbc", null, null, null, null))));

        Task<AssessmentAccessState?> IGetAssessmentAccess.ExecuteAsync(
            GetAssessmentAccessQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<AssessmentAccessState?>(new(store.State));

        Task<IReadOnlyList<CaseFieldProposal>> ICaseFieldProposalQueries.ListForCaseAsync(
            Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseFieldProposal>>([.. Proposals]);

        Task<ContactDirectoryRecord?> IContactDirectoryQueries.GetAsync(
            ActionActor actor, Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(ClaimSources.FirstOrDefault(item => item.OrganizationId == organizationId));

        Task<IReadOnlyList<ContactDirectoryRecord>> IContactDirectoryQueries.ListAsync(
            ContactDirectoryQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ContactDirectoryRecord>>([.. ClaimSources]);

        Task<IReadOnlyList<ContactDirectoryRecord>> IContactDirectoryQueries.ListByRoleAsync(
            ActionActor actor, ContactRole role, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ContactDirectoryRecord>>(
                role == ContactRole.ClaimSource ? [.. ClaimSources.Where(item => item.Active)] : []);

        Task<IReadOnlyList<ContactDirectoryRecord>> IContactDirectoryQueries.FindPossibleMatchesAsync(
            ActionActor actor, string name, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ContactDirectoryRecord>>([]);

        Task<IReadOnlyList<PrincipalAdministrationDetails>> IContactDirectoryQueries.ListPrincipalChoicesAsync(
            ActionActor actor, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PrincipalAdministrationDetails>>([]);
    }

    [Fact]
    public async Task TheInspectionPanelPrintsEachFactOnceAndNamesTheDefaultOnlyWhenItDiffers()
    {
        var store = new RecordingCaseDetailsStore();
        var panel = InspectionPanel(await ReadCaseAsync(store));

        // v26: one geometry of labelled cells — the address, then the Repairer
        // and Storage sub-panels (storage money moved here from Settlement).
        Assert.Contains("1 Depot Road", panel, StringComparison.Ordinal);
        Assert.Contains(">Storage location<", panel, StringComparison.Ordinal);
        Assert.Contains("14 Storage Lane", panel, StringComparison.Ordinal);
        Assert.Contains("data-inspection-repairer", panel, StringComparison.Ordinal);
        Assert.Contains(">Repairer<", panel, StringComparison.Ordinal);
        Assert.Contains("data-inspection-storage", panel, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Inspection.StoragePerDay, panel, StringComparison.Ordinal);
        // Read mode: the storage money reads; its control joins only the edit session.
        Assert.DoesNotContain("name=\"storagePerDay\"", panel, StringComparison.Ordinal);
        Assert.DoesNotContain(">Source<", panel, StringComparison.Ordinal);
        // The Principal's default is its own cell only where the Case holds
        // something else; here the Case holds a physical address of its own.
        Assert.Contains("data-inspection-provider-default hidden", panel, StringComparison.Ordinal);
        // A physical address says something the address itself does not, so
        // the mode still rides beside it.
        Assert.Contains("Physical address", panel, StringComparison.Ordinal);

        // The Principal's own setting, recorded on the Case unchanged: the
        // value carries its provenance word, no default cell is shown, and no
        // mode chip repeats the value.
        var imageBased = new RecordingCaseDetailsStore();
        imageBased.DataOverride = await InspectionOverrideAsync(imageBased, null);
        var imagePanel = InspectionPanel(await ReadCaseAsync(imageBased));
        var addressCell = AddressCell(imagePanel);
        Assert.Equal(1, Occurrences(addressCell, "Image Based Assessment"));
        Assert.Contains("data-provenance-word=\"Principal\"", addressCell, StringComparison.Ordinal);
        Assert.DoesNotContain("status--navy", addressCell, StringComparison.Ordinal);
        Assert.Contains("data-inspection-provider-default hidden", imagePanel, StringComparison.Ordinal);

        // The same Principal setting where staff recorded somewhere else: the
        // default is a fact the operator cannot read off the value.
        var corrected = new RecordingCaseDetailsStore();
        corrected.DataOverride = await InspectionOverrideAsync(corrected, "9 Other Road");
        var correctedPanel = InspectionPanel(await ReadCaseAsync(corrected));
        Assert.DoesNotContain("data-inspection-provider-default hidden", correctedPanel, StringComparison.Ordinal);
        Assert.Contains("data-inspection-provider-default", correctedPanel, StringComparison.Ordinal);
        Assert.Contains("Principal default", correctedPanel, StringComparison.Ordinal);
        Assert.Contains("9 Other Road", AddressCell(correctedPanel), StringComparison.Ordinal);
        Assert.Contains("Image Based Assessment", correctedPanel, StringComparison.Ordinal);
    }

    /// <summary>The Inspection section's recorded-address cell (v26 `[data-inspection-address]`).</summary>

    [Fact]
    public async Task ASaveCarriesTheClaimantContactNumberAndAddressThroughToTheCommand()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var saveResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                ("reason", "Corrected the registration"),
                ("claimantName", "Rebecca Claimant"),
                ("claimantContactNumber", "07700 900123"),
                ("claimantAddress", "12 Example Street, Leeds, LS1 1AA"),
                ("inspectionAddress", "7 No Script Road"),
                ("storageLocation", "14 Storage Lane")));
        AssertPrg(saveResponse, store.CaseId);

        var saved = Assert.Single(store.Saves);
        Assert.Equal("07700 900123", saved.Overview!.ClaimantContactNumber);
        Assert.Equal("12 Example Street, Leeds, LS1 1AA", saved.Overview!.ClaimantAddress);
        Assert.Equal("14 Storage Lane", saved.Inspection!.StorageLocation);
        Assert.Equal("7 No Script Road", saved.Inspection!.Address);
        Assert.Equal(CaseReportAddressTreatment.PhysicalVehicleLocation, saved.Inspection!.AddressTreatment);

        // A submitted section still contains the other accepted members.
        Assert.Equal("Rebecca Claimant", saved.Overview!.ClaimantName);
        Assert.Equal("CLM-42", saved.Overview.ClaimNumber);
        Assert.Equal("Case contact", saved.Overview.ContactName);
        Assert.Null(saved.Vehicle);
    }


    [Fact]
    public async Task AutomaticReadinessRendersWithoutManualCompletenessControls()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.Review,
            CaseState = CaseLifecycleState.Review
        };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.AddSingleton<IGetCase>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        // The workflow strip is gone (v28 P4): the ribbon's state chip says where the Case is.
        Assert.DoesNotContain("Case workflow", html, StringComparison.Ordinal);
        Assert.Contains("Review", html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ConfirmCompleteness", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Confirm completeness", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"instructionComplete\"", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A stale-version refusal is not lease loss, but the requirement still makes the rejected editor
    /// "reload and reacquire rather than merge or force the save", so the edit forms must not come
    /// back under the same edit authority.
    /// </summary>

    private static string AddressCell(string panel)
    {
        var start = panel.IndexOf("data-inspection-address>", StringComparison.Ordinal);
        Assert.True(start >= 0, "The inspection address cell must render.");
        var end = panel.IndexOf("data-inspection-provider-default", start, StringComparison.Ordinal);
        Assert.True(end > start, "The inspection address cell must end before the default cell.");
        return panel[start..end];
    }

    /// <summary>The Case as an operator who holds no edit lease reads it.</summary>

    private static async Task<CaseDataProjection> InspectionOverrideAsync(
        RecordingCaseDetailsStore store,
        string? recordedAddress)
    {
        var data = await store.GetAsync(store.CaseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The case fixture returned no data.");
        var setting = new CaseDataSource(
            CaseDataSourceKind.ProviderSetting, "QDOS", "Principal setting", "provider-inspection", 1);
        var staff = new CaseDataSource(
            CaseDataSourceKind.StaffCorrection, "staff", "Staff correction", "case-edit", 1);
        return data with
        {
            Inspection = data.Inspection with
            {
                Address = new(
                    new("Image Based Assessment", CaseDataValueKind.Fact, setting),
                    null,
                    recordedAddress is null
                        ? null
                        : new(recordedAddress, CaseDataValueKind.Confirmed, staff)),
                Mode = new(
                    new(CaseInspectionMode.ImageBasedAssessment, CaseDataValueKind.Fact, setting),
                    null,
                    null)
            }
        };
    }

    /// <summary>The Inspection section's body, between its host and the next.</summary>

    private static string InspectionPanel(string html)
    {
        var start = html.IndexOf("id=\"section-inspection\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Inspection section must render.");
        var end = html.IndexOf("id=\"section-vehicle\"", start, StringComparison.Ordinal);
        Assert.True(end > start, "The Inspection section must end before the Vehicle section.");
        return html[start..end];
    }

    /// <summary>The ribbon's actions cluster (v26), before the section row.</summary>
}
