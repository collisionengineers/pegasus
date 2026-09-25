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
/// folded panels painted by the server from their cookies. Issue #834 adds
/// the editors for report facts that had no writer (the transmission in the
/// Vehicle section, the airbags beside the belts and an unroadworthy
/// vehicle's temporary repairs in the Decisions) and the facts only the
/// DVLA/DVSA lookup records, which read Lookup and never edit.
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
            $"<option value=\"{source.OrganizationId:D}\" data-notes=\"Quote the Acme reference.\" data-contact-name=\"A Handler\" data-contact-phone=\"0113 000 0000\" data-contact-email=\"claims@acme.example\">Acme Claims</option>",
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
    public async Task AuditOriginalReportFieldsPassTheCaseEditorAllowListAndPersist()
    {
        var store = new RecordingCaseDetailsStore
        {
            SummaryCaseType = CaseType.Audit,
            State = CaseLifecycleState.NotReady,
            CaseState = CaseLifecycleState.NotReady
        };
        var ports = new RecordGapPorts(store);
        using var workspace = await EnterEditModeAsync(store, ports.Register);

        var originalReport = SectionHtml(await workspace.GetWorkspaceAsync(), "original-report");
        var values = new[]
        {
            (AssessmentVocabulary.OriginalReportAssessor, "Northside Assessors"),
            (AssessmentVocabulary.OriginalReportDate, "2031-05-06"),
            (AssessmentVocabulary.OriginalReportRoadworthiness, "roadworthy"),
            (AssessmentVocabulary.OriginalReportOutcome, "repairable")
        };
        foreach (var (path, _) in values)
        {
            Assert.Contains(
                $"name=\"{CaseWorkspaceLabels.Editors.FormName(path)}\" form=\"case-edit-form\"",
                originalReport,
                StringComparison.Ordinal);
        }

        using var response = await SaveAsync(
            workspace,
            values.Select(item => (CaseWorkspaceLabels.Editors.FormName(item.Item1), item.Item2)).ToArray());

        AssertPrg(response, store.CaseId);
        var fields = Assert.Single(store.Saves).Settlement!.AssessmentFields!;
        Assert.Equal(4, fields.Count);
        foreach (var (path, value) in values)
        {
            Assert.Equal(value, fields[path]);
        }
    }

    [Fact]
    public async Task AnOriginalReportCellFilledFromTheFiledReportIsTaggedExtracted()
    {
        var store = new RecordingCaseDetailsStore
        {
            SummaryCaseType = CaseType.Audit,
            State = CaseLifecycleState.NotReady,
            CaseState = CaseLifecycleState.NotReady
        };
        var ports = new RecordGapPorts(store);
        ports.Fields.Add(new(
            AssessmentVocabulary.OriginalReportAssessor, "Laird Assessors", ActorKind.Automation,
            OriginalReportPrefillPolicy.RecorderId, DateTimeOffset.UnixEpoch));
        ports.Staff(AssessmentVocabulary.OriginalReportRoadworthiness, "roadworthy");
        using var workspace = await EnterEditModeAsync(store, ports.Register);

        var originalReport = SectionHtml(await workspace.GetWorkspaceAsync(), "original-report");

        string Cell(string path)
        {
            var start = originalReport.IndexOf($"data-field=\"{path}\"", StringComparison.Ordinal);
            Assert.True(start >= 0, path);
            var end = originalReport.IndexOf("data-field=\"", start + 1, StringComparison.Ordinal);
            return end < 0 ? originalReport[start..] : originalReport[start..end];
        }

        Assert.Contains("data-provenance-word=\"Extracted\"", Cell(AssessmentVocabulary.OriginalReportAssessor), StringComparison.Ordinal);
        Assert.Contains("Laird Assessors", Cell(AssessmentVocabulary.OriginalReportAssessor), StringComparison.Ordinal);
        Assert.DoesNotContain("data-provenance-word", Cell(AssessmentVocabulary.OriginalReportRoadworthiness), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForgedOriginalReportFieldIsRefusedOnANonAuditCase()
    {
        var store = new RecordingCaseDetailsStore
        {
            SummaryCaseType = CaseType.Inspection,
            State = CaseLifecycleState.NotReady,
            CaseState = CaseLifecycleState.NotReady
        };
        using var workspace = await EnterEditModeAsync(store, new RecordGapPorts(store).Register);

        using var response = await SaveAsync(
            workspace,
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.OriginalReportAssessor), "Forged"));

        AssertPrg(response, store.CaseId);
        Assert.Empty(store.Saves);
    }

    [Fact]
    public async Task TheValuationSectionOwnsTheReportContentSummaryAndAllThreeSwitches()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation
        };
        var ports = new RecordGapPorts(store);
        ports.Staff(AssessmentVocabulary.ReportDiscloseGuideSource, "true");
        ports.Staff(AssessmentVocabulary.ReportValuationCommentary, "true");
        ports.Staff(AssessmentVocabulary.ReportIncludeUnrelatedDamage, "true");
        using var workspace = await EnterEditModeAsync(store, ports.Register);

        var html = await workspace.GetWorkspaceAsync();
        var valuation = SectionHtml(html, "valuation");
        var report = SectionHtml(html, "report");

        Assert.Equal(1, Occurrences(valuation, "data-report-content"));
        Assert.Equal(1, Occurrences(valuation, "data-report-switches"));
        Assert.Contains(
            "Guide source disclosed · valuation commentary · unrelated damage",
            WebUtility.HtmlDecode(valuation),
            StringComparison.Ordinal);
        foreach (var path in new[]
        {
            AssessmentVocabulary.ReportDiscloseGuideSource,
            AssessmentVocabulary.ReportValuationCommentary,
            AssessmentVocabulary.ReportIncludeUnrelatedDamage
        })
        {
            Assert.Equal(1, Occurrences(valuation, $"data-report-switch=\"{path}\""));
        }
        Assert.DoesNotContain("data-report-content", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheVehicleSectionEditsVinTypeAndBodyOutsideTheEngineerSections()
    {
        var store = new RecordingCaseDetailsStore();
        var ports = new RecordGapPorts(store);
        ports.Lookup(AssessmentVocabulary.VehicleVin, "WVWZZZ1JZXW000001");
        using var workspace = await EnterEditModeAsync(store, ports.Register);

        var vehicle = SectionHtml(await workspace.GetWorkspaceAsync(), "vehicle");
        // The lookup's tag sits in the cell's label line; the box holds the value.
        Assert.Matches(
            new Regex(
                $"data-vehicle-identity=\"{Regex.Escape(AssessmentVocabulary.VehicleVin)}\">\\s*<label[^>]*>[^<]*<span class=\"src-tag src-tag--lookup\" data-provenance-word=\"Lookup\">Lookup</span></label>\\s*<div class=\"fv mono\">WVWZZZ1JZXW000001</div>",
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

    [Fact]
    public async Task TheVehicleSectionShowsTheLookupsOwnFactsReadOnly()
    {
        var store = new RecordingCaseDetailsStore();
        var ports = new RecordGapPorts(store);
        (string Path, string Label, string Recorded, string Read)[] facts =
        [
            (AssessmentVocabulary.VehicleEngineCc, CaseWorkspaceLabels.Vehicle.EngineCc, "1461", "1461 cc"),
            (AssessmentVocabulary.VehicleFuel, CaseWorkspaceLabels.Vehicle.Fuel, "DIESEL", "DIESEL"),
            (AssessmentVocabulary.VehicleColour, CaseWorkspaceLabels.Vehicle.Colour, "BLUE", "BLUE"),
            (AssessmentVocabulary.VehicleTaxExpiry, CaseWorkspaceLabels.Vehicle.TaxExpiry, "2027-03-01", "1 Mar 2027"),
            (AssessmentVocabulary.VehicleMotExpiry, CaseWorkspaceLabels.Vehicle.MotExpiry, "2026-09-24", "24 Sep 2026")
        ];
        foreach (var fact in facts)
        {
            ports.LookupDerived(fact.Path, fact.Recorded);
        }
        using var workspace = await EnterEditModeAsync(store, ports.Register);

        var vehicle = SectionHtml(await workspace.GetWorkspaceAsync(), "vehicle");
        foreach (var (path, label, _, read) in facts)
        {
            // Read-only while the record edits: the lookup's tag in the label
            // line, the value in the box and no control.
            Assert.Matches(
                new Regex(
                    $"<div class=\"fc ro\" data-vehicle-provenance-row=\"{Regex.Escape(path)}\">\\s*<span class=\"lbl\">{Regex.Escape(label)}<span class=\"src-tag src-tag--lookup\" data-provenance-word=\"Lookup\">Lookup</span></span>\\s*<div class=\"fv\">{Regex.Escape(read)}</div>",
                    RegexOptions.Singleline),
                vehicle);
            Assert.DoesNotContain(
                $"name=\"{CaseWorkspaceLabels.Editors.FormName(path)}\"", vehicle, StringComparison.Ordinal);
        }
        var rows = Regex.Matches(vehicle, "data-vehicle-provenance-row=\"(?<path>[^\"]+)\"")
            .Select(match => match.Groups["path"].Value)
            .Order(StringComparer.Ordinal);
        Assert.Equal([.. AssessmentVocabulary.LookupDerivedPaths.Order(StringComparer.Ordinal)], rows);
    }

    [Fact]
    public async Task TheVehicleSectionEditsTransmissionInItsOwnPlace()
    {
        var store = new RecordingCaseDetailsStore();
        var ports = new RecordGapPorts(store);
        ports.Staff(AssessmentVocabulary.VehicleTransmission, "cvt");
        using var workspace = await EnterEditModeAsync(store, ports.Register);

        var vehicle = SectionHtml(await workspace.GetWorkspaceAsync(), "vehicle");
        var name = CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.VehicleTransmission);
        Assert.Contains(
            $"<select id=\"edit-vehicle-transmission\" class=\"fi\" name=\"{name}\" form=\"case-edit-form\">",
            vehicle,
            StringComparison.Ordinal);
        // The codes read in the words the report prints.
        Assert.Contains("<option value=\"cvt\" selected=\"selected\">CVT</option>", vehicle, StringComparison.Ordinal);
        Assert.Contains(">Semi-automatic<", vehicle, StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"data-vehicle-provenance-row=\"{AssessmentVocabulary.VehicleTransmission}\"", vehicle, StringComparison.Ordinal);
        // The v29 slot: after the colour, before the tax expiry.
        var colour = vehicle.IndexOf(
            $"data-vehicle-provenance-row=\"{AssessmentVocabulary.VehicleColour}\"", StringComparison.Ordinal);
        var transmission = vehicle.IndexOf(
            $"data-vehicle-identity=\"{AssessmentVocabulary.VehicleTransmission}\"", StringComparison.Ordinal);
        var tax = vehicle.IndexOf(
            $"data-vehicle-provenance-row=\"{AssessmentVocabulary.VehicleTaxExpiry}\"", StringComparison.Ordinal);
        Assert.True(
            colour >= 0 && colour < transmission && transmission < tax,
            "The transmission must sit between the colour and the tax expiry.");

        using var response = await SaveAsync(workspace, (name, "automatic"));

        AssertPrg(response, store.CaseId);
        var saved = Assert.Single(store.Saves);
        Assert.Equal("automatic", saved.Vehicle!.AssessmentFields![AssessmentVocabulary.VehicleTransmission]);
        Assert.Null(saved.Damage);
        Assert.Null(saved.Settlement);

        var reading = SectionHtml(await ReadCaseAsync(store, ports.Register, section: "vehicle"), "vehicle");
        Assert.Matches(
            new Regex(
                $"class=\"fc ro\" data-vehicle-identity=\"{Regex.Escape(AssessmentVocabulary.VehicleTransmission)}\">\\s*<span class=\"lbl\">{CaseWorkspaceLabels.Vehicle.Transmission}</span>\\s*<div class=\"fv\">CVT</div>",
                RegexOptions.Singleline),
            reading);
        Assert.DoesNotContain($"name=\"{name}\"", reading, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDamageSectionRecordsWhichAirbagsDeployedBesideTheBelts()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation
        };
        var ports = new RecordGapPorts(store);
        ports.Staff(AssessmentVocabulary.VehicleAirbagsDeployed, "Driver front");
        using var workspace = await EnterEditModeAsync(store, ports.Register);

        var damage = SectionHtml(await workspace.GetWorkspaceAsync(), "damage");
        var name = CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.VehicleAirbagsDeployed);
        Assert.Contains(
            $"<label for=\"edit-{AssessmentVocabulary.VehicleAirbagsDeployed}\">Airbags deployed</label>",
            damage,
            StringComparison.Ordinal);
        Assert.Contains(
            $"name=\"{name}\" form=\"case-edit-form\" value=\"Driver front\" maxlength=\"200\"",
            damage,
            StringComparison.Ordinal);
        // Beside the belts: after the material transfer, before the unrelated damage.
        var transfer = damage.IndexOf(
            $"name=\"{CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.DamageMaterialTransfer)}\"", StringComparison.Ordinal);
        var airbags = damage.IndexOf($"name=\"{name}\"", StringComparison.Ordinal);
        var unrelated = damage.IndexOf(
            $"name=\"{CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.DamageUnrelated)}\"", StringComparison.Ordinal);
        Assert.True(
            transfer >= 0 && transfer < airbags && airbags < unrelated,
            "The airbags must sit after the material transfer and before the unrelated damage.");

        using var response = await SaveAsync(workspace, (name, "None"));

        AssertPrg(response, store.CaseId);
        var saved = Assert.Single(store.Saves);
        Assert.Equal("None", saved.Damage!.AssessmentFields![AssessmentVocabulary.VehicleAirbagsDeployed]);
        Assert.Null(saved.Vehicle);

        // Damage is never a lazy section, so a read without the lease carries it.
        var reading = SectionHtml(await ReadCaseAsync(store, ports.Register), "damage");
        Assert.Contains("<span class=\"lbl\">Airbags deployed</span>", reading, StringComparison.Ordinal);
        Assert.Contains("<div class=\"fv\">Driver front</div>", reading, StringComparison.Ordinal);
        Assert.DoesNotContain($"name=\"{name}\"", reading, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDecisionsCarryTemporaryRepairsOnlyForAnUnroadworthyVehicle()
    {
        string[] paths =
        [
            AssessmentVocabulary.VehicleTemporaryRepairsPossible,
            AssessmentVocabulary.VehicleTemporaryRepairMethod,
            AssessmentVocabulary.VehicleTemporaryRepairCost
        ];

        // Roadworthy: the rows are hidden, and their controls still join the one Save form.
        var roadworthy = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation
        };
        var roadworthyPorts = new RecordGapPorts(roadworthy);
        roadworthyPorts.Staff(AssessmentVocabulary.LegalStatus, "roadworthy");
        using (var hidden = await EnterEditModeAsync(roadworthy, roadworthyPorts.Register))
        {
            var decisions = SectionHtml(await hidden.GetWorkspaceAsync(), "settlement");
            foreach (var path in paths)
            {
                Assert.Contains(
                    $"<div class=\"dec\" data-decision=\"{path}\" data-shown-when=\"unroadworthy\" hidden=\"hidden\">",
                    decisions,
                    StringComparison.Ordinal);
                Assert.Matches(
                    $"<(input|textarea|select)[^>]*name=\"{Regex.Escape(CaseWorkspaceLabels.Editors.FormName(path))}\"[^>]*form=\"case-edit-form\"",
                    decisions);
            }
        }

        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation
        };
        var ports = new RecordGapPorts(store);
        ports.Staff(AssessmentVocabulary.LegalStatus, "unroadworthy");
        ports.Staff(AssessmentVocabulary.UnroadworthyReason, "Brake line severed");
        ports.Staff(AssessmentVocabulary.VehicleTemporaryRepairsPossible, "true");
        ports.Staff(AssessmentVocabulary.VehicleTemporaryRepairMethod, "Cable-tie the bumper");
        ports.Staff(AssessmentVocabulary.VehicleTemporaryRepairCost, "45.00");
        using var workspace = await EnterEditModeAsync(store, ports.Register);

        var settlement = SectionHtml(await workspace.GetWorkspaceAsync(), "settlement");
        foreach (var path in paths)
        {
            Assert.Contains(
                $"<div class=\"dec\" data-decision=\"{path}\" data-shown-when=\"unroadworthy\">",
                settlement,
                StringComparison.Ordinal);
        }
        var possible = Regex.Match(
            settlement,
            "<select id=\"f-vehicle-temporary-repairs-possible\"[^>]*>(?<options>.*?)</select>",
            RegexOptions.Singleline);
        Assert.True(possible.Success, "The temporary repairs select must render.");
        Assert.Contains(
            "<option value=\"true\" selected=\"selected\">Yes</option>",
            possible.Groups["options"].Value,
            StringComparison.Ordinal);
        Assert.Contains(
            $"<textarea id=\"f-vehicle-temporary-repair-method\" class=\"fi\" name=\"{CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.VehicleTemporaryRepairMethod)}\" form=\"case-edit-form\" maxlength=\"2000\">Cable-tie the bumper</textarea>",
            settlement,
            StringComparison.Ordinal);
        // The input's later attributes sit on the next source lines.
        Assert.Matches(
            $"name=\"{Regex.Escape(CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.VehicleTemporaryRepairCost))}\" form=\"case-edit-form\" value=\"45\\.00\"\\s+type=\"number\"\\s+step=\"0\\.01\"",
            settlement);
        // They follow the unroadworthy reason, in order.
        var reason = settlement.IndexOf(
            $"data-decision=\"{AssessmentVocabulary.UnroadworthyReason}\"", StringComparison.Ordinal);
        var order = paths.Select(path => settlement.IndexOf($"data-decision=\"{path}\"", StringComparison.Ordinal)).ToArray();
        Assert.True(
            reason >= 0 && reason < order[0] && order[0] < order[1] && order[1] < order[2],
            "The temporary repair rows must follow the unroadworthy reason in order.");

        using var response = await SaveAsync(
            workspace,
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.VehicleTemporaryRepairsPossible), "false"),
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.VehicleTemporaryRepairMethod), "Tape the lamp"),
            (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.VehicleTemporaryRepairCost), "12.50"));

        AssertPrg(response, store.CaseId);
        var saved = Assert.Single(store.Saves);
        var fields = saved.Settlement!.AssessmentFields!;
        Assert.Equal("false", fields[AssessmentVocabulary.VehicleTemporaryRepairsPossible]);
        Assert.Equal("Tape the lamp", fields[AssessmentVocabulary.VehicleTemporaryRepairMethod]);
        Assert.Equal("12.50", fields[AssessmentVocabulary.VehicleTemporaryRepairCost]);
        Assert.Null(saved.Vehicle);
        Assert.Null(saved.Damage);

        var reading = WebUtility.HtmlDecode(SectionHtml(await ReadCaseAsync(store, ports.Register), "settlement"));
        foreach (var (path, read) in new[]
        {
            (AssessmentVocabulary.VehicleTemporaryRepairsPossible, "Yes"),
            (AssessmentVocabulary.VehicleTemporaryRepairMethod, "Cable-tie the bumper"),
            (AssessmentVocabulary.VehicleTemporaryRepairCost, "£45.00")
        })
        {
            Assert.Contains(
                $"<div class=\"dec\" data-decision=\"{path}\" data-shown-when=\"unroadworthy\">",
                reading,
                StringComparison.Ordinal);
            Assert.Matches(
                new Regex(
                    $"<div class=\"fc ro\" data-field=\"{Regex.Escape(path)}\">\\s*<span class=\"lbl\">{Regex.Escape(CaseWorkspaceLabels.Editors.Settlement[path])}</span>\\s*<div class=\"fv[^\"]*\">{Regex.Escape(read)}</div>",
                    RegexOptions.Singleline),
                reading);
            Assert.DoesNotContain(
                $"name=\"{CaseWorkspaceLabels.Editors.FormName(path)}\"", reading, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A value the Automation actor recorded is the Case's value (operator,
    /// 25 September 2026): it shows in its own control with its source tag,
    /// and the Decisions strip draws no proposal column.
    /// </summary>
    [Fact]
    public async Task AnAutomationValueShowsInItsControlWithItsSourceTag()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation
        };
        var ports = new RecordGapPorts(store);
        ports.Automation(AssessmentVocabulary.Outcome, "repairable");
        ports.Staff(AssessmentVocabulary.LegalStatus, "unroadworthy");
        ports.Staff(AssessmentVocabulary.UnroadworthyReason, "Brake line severed");
        ports.Automation(AssessmentVocabulary.VehicleTemporaryRepairsPossible, "true");
        using var workspace = await EnterEditModeAsync(store, ports.Register);

        var settlement = SectionHtml(await workspace.GetWorkspaceAsync(), "settlement");

        Assert.DoesNotContain("has-proposal", settlement, StringComparison.Ordinal);
        Assert.DoesNotContain("data-proposal", settlement, StringComparison.Ordinal);
        Assert.DoesNotContain("awaiting review", settlement, StringComparison.Ordinal);
        var outcome = Regex.Match(
            settlement,
            "<select id=\"f-assessment-outcome\"[^>]*>(?<options>.*?)</select>",
            RegexOptions.Singleline);
        Assert.True(outcome.Success, "The outcome select must render.");
        Assert.Contains("value=\"repairable\" selected=\"selected\"", outcome.Groups["options"].Value, StringComparison.Ordinal);
        var control = Regex.Match(
            settlement,
            "<select id=\"f-vehicle-temporary-repairs-possible\"[^>]*>(?<options>.*?)</select>",
            RegexOptions.Singleline);
        Assert.True(control.Success, "The temporary repairs select must render.");
        Assert.Contains("value=\"true\" selected=\"selected\"", control.Groups["options"].Value, StringComparison.Ordinal);
        // The strip hides the cell's own label, so the AI value's source tag
        // is in the row's visible label column.
        Assert.Matches(
            new Regex(
                $"<div class=\"dec\" data-decision=\"{Regex.Escape(AssessmentVocabulary.VehicleTemporaryRepairsPossible)}\"[^>]*>\\s*"
                    + "<span class=\"dl\">[^<]*<span class=\"src-tag src-tag--ai\" data-provenance-word=\"AI\">AI</span></span>",
                RegexOptions.Singleline),
            settlement);
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

    /// <summary>
    /// The reads the five gaps add, substituted together: the assessment
    /// workspace and access and the Claim source records.
    /// </summary>
    private sealed class RecordGapPorts(RecordingCaseDetailsStore store) :
        IGetAssessmentWorkspace,
        IGetAssessmentAccess,
        IContactDirectoryQueries
    {
        private static readonly DateTimeOffset At = new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);

        public List<AssessmentFieldValue> Fields { get; } = [];

        public List<ContactDirectoryRecord> ClaimSources { get; } = [];

        public void Register(IServiceCollection services)
        {
            Substitute<ISaveCaseWorkspace>(services, store);
            Substitute<ICaseReportSnapshotSource>(services, store);
            Substitute<IGetAssessmentWorkspace>(services, this);
            Substitute<IGetAssessmentAccess>(services, this);
            Substitute<IContactDirectoryQueries>(services, this);
        }

        public void Automation(string path, string value) =>
            Fields.Add(new(path, value, ActorKind.Automation, "pegasus-automation", At));

        public void Lookup(string path, string value) =>
            Fields.Add(new(path, value, ActorKind.Automation, Pegasus.Core.Vehicle.VehicleLookupFillPolicy.RecorderId, At));

        /// <summary>A fact only the lookup records.</summary>
        public void LookupDerived(string path, string value) => Lookup(path, value);

        public void Staff(string path, string value) =>
            Fields.Add(new(path, value, ActorKind.Staff, "recorded-engineer", At));

        Task<AssessmentWorkspace?> IGetAssessmentWorkspace.ExecuteAsync(
            GetAssessmentWorkspaceQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<AssessmentWorkspace?>(AssessmentWorkspaceTestData.Create(new CaseAssessmentProjection(
                store.CaseId, "QDOS3100042", store.CaseVersion, store.State, null, [.. Fields], [],
                new("AB12CDE", null, null, null, null, null, "tbc", null, new DateOnly(2026, 8, 2), null, null, null, null, null))));

        Task<AssessmentAccessState?> IGetAssessmentAccess.ExecuteAsync(
            GetAssessmentAccessQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<AssessmentAccessState?>(new(store.State));

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
        // The mode reads once, in Inspection type.
        Assert.Contains("Physical address", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("status--navy", AddressCell(panel), StringComparison.Ordinal);

        // The Principal's own setting, recorded on the Case unchanged: the
        // cell's label line carries its source tag, no default cell is shown,
        // and no mode chip repeats the value.
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
        var data = await store.GetAsync(store.CaseId, CaseWorkSelector.Current, CancellationToken.None)
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
