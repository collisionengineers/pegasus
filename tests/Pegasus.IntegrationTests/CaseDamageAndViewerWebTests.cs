using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// v26 § Damage clicker (Plan) and § Image viewer, on the one Case record: the
/// Plan diagram drives Core's zone model and the recorded list; the crop taken
/// on the viewer stage is staged into the one Case Save.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseDamageAndViewerWebTests
{
    private const string PlanViewBox = "0 0 240 434";

    /// <summary>
    /// Damage by area (v28 P5): one disc per recorded damage, as drawn or,
    /// recorded by area, from its areas, the three chips for the areas the plan cannot show, the
    /// five graded fills as a legend, and the recorded-areas list numbered
    /// in the same order as the discs, with the derived cells.
    /// </summary>
    [Fact]
    public async Task TheDamageSectionRendersTheRecordedAreasOnThePlan()
    {
        var source = new DamageSource(
            "[{\"areas\":[\"rear\",\"left_rear\"],\"severity\":\"moderate\",\"note\":\"Rear panel deformed\"}," +
            "{\"areas\":[\"underside\"],\"severity\":\"heavy\",\"note\":\"\"}," +
            "{\"areas\":[\"left_side\",\"right_side\"],\"disc\":{\"x\":0.5,\"y\":0.53,\"r\":0.08},\"severity\":\"light\",\"note\":\"Roof dent\"}]");
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => source.Substitute(services)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");

        var html = await GetHtmlAsync(client, $"/Cases/{source.CaseId:D}?section=damage");
        var damage = Section(html, "section-damage-title");

        Assert.Contains($"viewBox=\"{PlanViewBox}\"", damage, StringComparison.Ordinal);
        // The disc of the first damage, drawn from its two areas; the
        // underside damage has no disc; the third keeps the small disc drawn
        // on the centre line (23 September 2026), clipped to the body. No
        // panel zones remain.
        Assert.Contains("data-damage-marks", damage, StringComparison.Ordinal);
        Assert.Contains("data-mark=\"0\" data-sev=\"moderate\"", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-mark=\"1\"", damage, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(damage, "<circle class=\"area\" clip-path=\"url\\(#damage-plan-clip\\)\""));
        var centreX = DamagePlanGeometry.PlanLeft + 0.5 * DamagePlanGeometry.PlanWidth;
        var centreY = DamagePlanGeometry.PlanTop + 0.53 * DamagePlanGeometry.PlanHeight;
        var radius = 0.08 * DamagePlanGeometry.PlanWidth;
        Assert.Contains(
            string.Create(CultureInfo.InvariantCulture, $"data-mark=\"2\" data-sev=\"light\"><circle class=\"area\" clip-path=\"url(#damage-plan-clip)\" cx=\"{centreX:0.#}\" cy=\"{centreY:0.#}\" r=\"{radius:0.#}\""),
            damage,
            StringComparison.Ordinal);
        Assert.DoesNotContain("data-damage-zone", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("dm-guides", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-damage-plan-area", damage, StringComparison.Ordinal);
        foreach (var chip in AssessmentVocabulary.DamageOtherAreas)
        {
            Assert.Contains($"data-damage-area=\"{chip}\"", damage, StringComparison.Ordinal);
        }
        Assert.Equal(3, Regex.Count(damage, "data-damage-area=\""));
        // Read mode draws the chips where they stand while editing, greyed and
        // inert: no buttons, no pressed state.
        Assert.Contains("is-static is-damaged\" data-damage-area=\"underside\"", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-pressed", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("<button type=\"button\" class=\"btn btn--small", damage, StringComparison.Ordinal);
        foreach (var severity in AssessmentVocabulary.DamageSeverities.Keys)
        {
            Assert.Contains($"data-sev=\"{severity}\"", damage, StringComparison.Ordinal);
        }

        // The recorded areas, numbered in recorded order, and the derived cells.
        Assert.Contains(">Recorded areas<", damage, StringComparison.Ordinal);
        var first = Row(damage, "0");
        var second = Row(damage, "1");
        var third = Row(damage, "2");
        Assert.Contains("<i class=\"zn\">1</i>Rear, LH Rear<", first, StringComparison.Ordinal);
        Assert.Contains("<i class=\"zn\">2</i>Underside<", second, StringComparison.Ordinal);
        Assert.Contains("<i class=\"zn\">3</i>LH Side, RH Side<", third, StringComparison.Ordinal);
        Assert.Contains("Rear panel deformed", first, StringComparison.Ordinal);
        // One row layout in both modes: the severity and note read as greyed
        // value boxes that stay put while editing, and no area checkboxes.
        Assert.Contains("<span class=\"fc ro\">", first, StringComparison.Ordinal);
        Assert.DoesNotContain("data-damage-area-set", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("impact-row--areas", damage, StringComparison.Ordinal);
        Assert.Contains(">Multiple &#xB7; LH Side, RH Side, Rear, LH Rear, Underside<", Cell(damage, "data-damage-location"), StringComparison.Ordinal);
        Assert.Contains(">Heavy<", Cell(damage, "data-damage-severity"), StringComparison.Ordinal);
        Assert.Contains(">3<", Cell(damage, "data-damage-count"), StringComparison.Ordinal);

        // Read mode: the one hidden JSON field the Save reads is not rendered
        // while nothing edits, and neither is Reset.
        Assert.DoesNotContain("name=\"damageImpacts\"", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-damage-input", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-damage-reset", damage, StringComparison.Ordinal);
        Assert.Equal("false", DamageEditable(damage));
    }

    /// <summary>
    /// While the Engineer's session holds the section, the hidden impacts
    /// field joins the one Case form and the plan is live.
    /// </summary>
    [Fact]
    public async Task TheDamageSectionStagesItsImpactsIntoTheCaseFormWhileEditing()
    {
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation
        };
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<IGetAssessmentWorkspace>(services, store);
        });

        var leased = await workspace.GetWorkspaceAsync();
        var damage = Section(leased, "section-damage-title");

        Assert.Equal("true", DamageEditable(damage));
        Assert.Contains("name=\"damageImpacts\" form=\"case-edit-form\"", damage, StringComparison.Ordinal);
        Assert.Contains("data-damage-input", damage, StringComparison.Ordinal);
        Assert.Contains($"viewBox=\"{PlanViewBox}\"", damage, StringComparison.Ordinal);
        Assert.Contains("data-damage-empty", damage, StringComparison.Ordinal);
        // The band guides and Reset show only while editing; the chips are
        // live buttons in the same place they read from, and a damage's areas
        // come from its disc, so no row carries an area checkbox set.
        Assert.Contains("class=\"dm-guides\"", damage, StringComparison.Ordinal);
        Assert.Contains("data-damage-reset", damage, StringComparison.Ordinal);
        Assert.Contains("data-damage-area=\"underside\" aria-pressed=\"false\"", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("is-static", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-damage-area-set", damage, StringComparison.Ordinal);
        // The editor is handed Core's disc limits and the plan box whose shape
        // Core judges coverage on.
        Assert.Contains("minRadius", damage, StringComparison.Ordinal);
        Assert.Contains("maxRadius", damage, StringComparison.Ordinal);
        Assert.Equal(
            DamageAreaGeometry.CanonicalHeight / DamageAreaGeometry.CanonicalWidth,
            (double)DamagePlanGeometry.PlanHeight / DamagePlanGeometry.PlanWidth,
            10);
        Assert.Equal(AssessmentVocabulary.DamagePlanAreas.Count, Regex.Count(damage, "data-damage-plan-area=\""));
        Assert.Equal(AssessmentVocabulary.DamagePlanAreas.Count, Regex.Count(damage, "role=\"button\" tabindex=\"0\" data-damage-plan-area=\""));
        foreach (var area in AssessmentVocabulary.DamagePlanAreas)
        {
            Assert.Contains($"data-damage-plan-area=\"{area}\"", damage, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ExactDamageCoverageKeepsTheBoundaryCrossingLeftFrontArea()
    {
        // The centre and eight circumference samples used to miss this area:
        // the disc crosses the left-front/front boundary between sample points.
        var disc = new DamageDisc(0.38, 0.42, 0.10);

        Assert.Contains(
            "left_front",
            DamageAreaGeometry.IntersectedPlanAreas(disc, 1, 1));
    }

    /// <summary>
    /// v26 § Crop and tag: the crop is taken on the viewer stage, staged into
    /// the tile's preparation and carried by the one Case Save. The page
    /// renders the viewer with its Save crop control, the tile carries the
    /// stored rectangle and version the stage starts from, and the posted
    /// edit reaches the workspace command as Core's crop.
    /// </summary>
    [Fact]
    public async Task TheViewerCropIsStagedOnTheTileAndSavedWithTheCase()
    {
        var fixture = new PreparedImages();
        var store = fixture.Store(CaseLifecycleState.ReportPreparation);
        using var workspace = await EnterEngineerEditModeAsync(store, services =>
        {
            Substitute<ICaseAssetPreparationQueries>(services, store);
            Substitute<ISaveCaseWorkspace>(services, store);
        });
        const string operationKey = "0a0b0c0d0e0f01020304050607080901";

        var leased = await workspace.GetWorkspaceAsync();
        Assert.Contains("data-case-viewer", leased, StringComparison.Ordinal);
        Assert.Contains("data-viewer-crop-save", leased, StringComparison.Ordinal);
        Assert.DoesNotContain("data-case-crop-dialog", leased, StringComparison.Ordinal);

        var files = await GetFilesFragmentAsync(workspace, leased);
        var tile = Tile(files, fixture.OverviewOccurrenceId);
        Assert.Contains("data-preparation-card", tile, StringComparison.Ordinal);
        Assert.Contains("data-preparation-version=\"4\"", tile, StringComparison.Ordinal);
        Assert.Contains("data-preparation-crop-left=\"0.1\"", tile, StringComparison.Ordinal);
        Assert.Contains("data-preparation-crop-top=\"0.1\"", tile, StringComparison.Ordinal);
        Assert.Contains("data-preparation-crop-width=\"0.8\"", tile, StringComparison.Ordinal);
        Assert.Contains("data-preparation-crop-height=\"0.8\"", tile, StringComparison.Ordinal);
        Assert.Contains($"data-evidence-preparation-occurrence=\"{fixture.OverviewOccurrenceId:D}\"", tile, StringComparison.Ordinal);
        Assert.Contains("data-preparation-crop", tile, StringComparison.Ordinal);

        using var response = await workspace.Client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            workspace.MutationForm(
                operationKey,
                "Cropped on the viewer stage.",
                ("preparationEdits[0].OccurrenceId", fixture.OverviewOccurrenceId.ToString("D")),
                ("preparationEdits[0].ExpectedPreparationVersion", "4"),
                ("preparationEdits[0].Role", nameof(CaseAssetReportRole.Overview)),
                ("preparationEdits[0].Order", string.Empty),
                ("preparationEdits[0].Rotation", "90"),
                ("preparationEdits[0].CropLeft", "0.25"),
                ("preparationEdits[0].CropTop", "0.2"),
                ("preparationEdits[0].CropWidth", "0.5"),
                ("preparationEdits[0].CropHeight", "0.4")));

        AssertPrg(response, store.CaseId);
        var preparation = Assert.Single(store.Saves).ImagePreparation;
        Assert.NotNull(preparation);
        Assert.NotNull(preparation.Edits);
        var edit = Assert.Single(preparation.Edits);
        Assert.Equal(
            new CaseAssetPreparationEdit(
                fixture.OverviewOccurrenceId,
                4,
                CaseAssetReportRole.Overview,
                null,
                CaseAssetRotation.Clockwise90,
                new(0.25m, 0.2m, 0.5m, 0.4m)),
            edit);
    }

    private static string DamageEditable(string damage)
    {
        var match = Regex.Match(damage, "data-damage-editable=\"(true|false)\"");
        Assert.True(match.Success, "The Damage section does not state whether it edits.");
        return match.Groups[1].Value;
    }

    /// <summary>One recorded-damage row of the list, by its index, and nothing beside it.</summary>
    private static string Row(string damage, string zone)
    {
        var start = damage.IndexOf($"data-damage-row=\"{zone}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The recorded zone '{zone}' is not listed.");
        var end = damage.IndexOf("</li>", start, StringComparison.Ordinal);
        Assert.True(end > start, $"The row for '{zone}' is not closed.");
        return damage[start..end];
    }

    /// <summary>One derived cell's value box, by its hook.</summary>
    private static string Cell(string damage, string hook)
    {
        var start = damage.IndexOf(hook, StringComparison.Ordinal);
        Assert.True(start >= 0, $"The '{hook}' cell is not rendered.");
        var end = damage.IndexOf("</div>", start, StringComparison.Ordinal);
        return damage[start..end] + "<";
    }

    /// <summary>One image tile of the Images tab grid, by its occurrence.</summary>
    private static string Tile(string files, Guid occurrenceId)
    {
        var start = files.IndexOf($"data-image-tile=\"{occurrenceId:D}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The tile for '{occurrenceId:D}' is not rendered.");
        var end = files.IndexOf("</li>", start, StringComparison.Ordinal);
        Assert.True(end > start, $"The tile for '{occurrenceId:D}' is not closed.");
        return files[start..end];
    }

    /// <summary>
    /// A With Engineer Case whose assessment carries recorded impacts, read
    /// without a lease so the Damage section renders its read view.
    /// </summary>
    private sealed class DamageSource(string impacts) :
        IGetCase,
        IGetCasePageFrame,
        IGetAssessmentAccess,
        IGetAssessmentWorkspace,
        ICaseReportSnapshotSource,
        IListCaseEstimates
    {
        public Guid CaseId { get; } = Guid.NewGuid();

        public void Substitute(IServiceCollection services)
        {
            services.RemoveAll<IGetCase>();
            services.RemoveAll<IGetCasePageFrame>();
            services.RemoveAll<IGetAssessmentAccess>();
            services.RemoveAll<IGetAssessmentWorkspace>();
            services.RemoveAll<ICaseReportSnapshotSource>();
            services.RemoveAll<IListCaseEstimates>();
            services.AddSingleton<IGetCase>(this);
            services.AddSingleton<IGetCasePageFrame>(this);
            services.AddSingleton<IGetAssessmentAccess>(this);
            services.AddSingleton<IGetAssessmentWorkspace>(this);
            services.AddSingleton<ICaseReportSnapshotSource>(this);
            services.AddSingleton<IListCaseEstimates>(this);
        }

        private CaseWorkflowRecord Workflow => new(
            CaseId,
            new CaseIdentity(CaseId, "QDOS", 2026, 43, "QDOS-2026-00043"),
            CaseLifecycleState.ReportPreparation,
            null, null, null, null, null, null, null, 7);

        private CaseAssessmentProjection Assessment() => new(
            CaseId,
            Workflow.Identity.Reference,
            Workflow.Version,
            CaseLifecycleState.ReportPreparation,
            null,
            [
                new(AssessmentVocabulary.DamageImpacts, impacts, ActorKind.Staff, "engineer-1",
                    DateTimeOffset.UtcNow, "engineer-1", DateTimeOffset.UtcNow),
                new(AssessmentVocabulary.ImpactLocation, "rear", ActorKind.Staff, "engineer-1",
                    DateTimeOffset.UtcNow, "engineer-1", DateTimeOffset.UtcNow),
                new(AssessmentVocabulary.ImpactSeverity, "heavy", ActorKind.Staff, "engineer-1",
                    DateTimeOffset.UtcNow, "engineer-1", DateTimeOffset.UtcNow)
            ],
            [],
            new("AB12CDE", null, null, null, null, null, "tbc", null, new DateOnly(2026, 8, 2), null, null,
                null, "Alex Example", "P-100"));

        private CaseDetails Details()
        {
            var workflow = Workflow;
            var workspace = AssessmentWorkspaceTestData.Create(Assessment());
            return new(
                new CaseSearchItem(
                    CaseId, workflow.Identity.Reference, null, CaseType.Inspection, "Approved Principal",
                    workflow.State, null, "AB12CDE", "Alex Example", "P-100", DateTimeOffset.UtcNow,
                    "Email", DateTimeOffset.UtcNow),
                workflow,
                null,
                [],
                null,
                CaseCustodyState.Pending,
                [],
                [])
            {
                Data = workspace.Data
            };
        }

        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CaseDetails?>(query.CaseId == CaseId ? Details() : null);

        Task<CasePageFrame?> IGetCasePageFrame.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            if (query.CaseId != CaseId)
            {
                return Task.FromResult<CasePageFrame?>(null);
            }

            var details = Details();
            return Task.FromResult<CasePageFrame?>(new(
                new(details.Summary, details.Workflow, details.ActiveEditLease),
                details.Documents,
                details.AvailableReportSentEvidence,
                details.RecordNotes,
                details.Data!));
        }

        public Task<AssessmentAccessState?> ExecuteAsync(
            GetAssessmentAccessQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AssessmentAccessState?>(
                query.CaseId == CaseId ? new(CaseLifecycleState.ReportPreparation) : null);

        public Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AssessmentWorkspace?>(
                query.CaseId == CaseId ? AssessmentWorkspaceTestData.Create(Assessment()) : null);

        public Task<IReadOnlyList<RepairSpecificationVersion>> ExecuteAsync(
            Guid caseId,
            CaseWorkSelector work,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RepairSpecificationVersion>>([]);

        public Task<CaseReportFreezeInputs?> GetAsync(
            Guid caseId,
            ActionActor actor,
            CaseWorkSelector work, CancellationToken cancellationToken)
        {
            var assessment = Assessment();
            return Task.FromResult<CaseReportFreezeInputs?>(caseId != CaseId ? null : new(
                new(assessment, assessment.Reference, [], null, [], []),
                new(assessment, null, null, [], null, null, [], new Dictionary<Guid, DocumentVersion>()),
                assessment.Reference, Workflow.Version));
        }
    }

    [Fact]
    public async Task ASaveCarriesTheDamageWorkbenchFieldsThroughToTheWorkspaceCommand()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation
        };
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
        // A damage recorded by area, and one drawn as a small disc on the
        // centre line whose posted areas Core replaces with the two it touches.
        const string closedBoundaryImpact =
            "[{\"areas\":[\"front\",\"left_front\",\"right_front\",\"left_side\",\"right_side\"],\"severity\":\"light\",\"note\":\"Boundary\"},"
            + "{\"areas\":[\"front\"],\"disc\":{\"x\":0.5,\"y\":0.53,\"r\":0.08},\"severity\":\"moderate\",\"note\":\"Roof\"}]";
        using var saveResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                ("reason", "Recorded damage observations"),
                ("damageImpacts", closedBoundaryImpact),
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.DamageTyreRightFront), "damaged"),
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.DamageBeltLeftRear), "deployed"),
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.DamageUnrelated), "Old rear bumper scrape"),
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.DamageUnrelatedDeduction), "125.50"),
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.DamageMaterialTransfer), "White paint transfer")));
        AssertPrg(saveResponse, store.CaseId);

        var damage = Assert.Single(store.Saves).Damage;
        Assert.NotNull(damage);
        Assert.Equal(2, damage.Impacts!.Count);
        var impact = damage.Impacts[0];
        Assert.Equal(["front", "left_front", "right_front", "left_side", "right_side"], impact.Areas);
        Assert.Equal(("light", "Boundary"), (impact.Severity, impact.Note));
        Assert.Null(impact.Disc);
        var drawn = damage.Impacts[1];
        Assert.Equal(["left_side", "right_side"], drawn.Areas);
        Assert.Equal(new DamageDisc(0.5, 0.53, 0.08), drawn.Disc);
        Assert.Equal("damaged", damage.AssessmentFields![AssessmentVocabulary.DamageTyreRightFront]);
        Assert.Equal("deployed", damage.AssessmentFields[AssessmentVocabulary.DamageBeltLeftRear]);
        Assert.Equal("Old rear bumper scrape", damage.AssessmentFields[AssessmentVocabulary.DamageUnrelated]);
        Assert.Equal("125.50", damage.AssessmentFields[AssessmentVocabulary.DamageUnrelatedDeduction]);
        Assert.Equal("White paint transfer", damage.AssessmentFields[AssessmentVocabulary.DamageMaterialTransfer]);
    }

}
