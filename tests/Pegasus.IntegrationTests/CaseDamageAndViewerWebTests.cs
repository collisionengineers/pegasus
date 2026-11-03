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
/// plan carries the damage markers for the report's picture and the impacts
/// are entered by hand beside it (v46); the crop taken on the viewer stage is
/// staged into the one Case Save.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseDamageAndViewerWebTests
{
    private const string PlanViewBox = "0 0 240 434";

    /// <summary>
    /// v46 (operator, 3 November 2026): one comic burst per marker over its
    /// disc as drawn, unclipped and unnumbered; beside the plan, one row per
    /// impact as staff entered it: Impact · Severity · Location · Note, with
    /// the ×'s column kept. Nothing is derived from the markers: no chips, no
    /// readout, no area list, no Impact location or Impact severity cells.
    /// The Incident narrative is a sentence for each complete impact.
    /// </summary>
    [Fact]
    public async Task TheDamageSectionRendersTheMarkersAndTheImpactsAsEntered()
    {
        var source = new DamageSource(
            "[{\"x\":0.5,\"y\":0.53,\"r\":0.08},{\"x\":0.5,\"y\":0.92,\"r\":0.12}]",
            "[{\"severity\":\"light\",\"location\":\"front\",\"note\":\"Front bumper scuffed.\"},"
            + "{\"severity\":\"moderate\",\"location\":\"\",\"note\":\"\"},"
            + "{\"severity\":\"moderate_to_heavy\",\"location\":\"rear\",\"note\":\"\"}]");
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
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
        Assert.Contains("data-damage-marks", damage, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(damage, "<circle class=\"area\""));
        var disc = new DamageDisc(
            DamagePlanGeometry.PlanLeft + 0.5 * DamagePlanGeometry.PlanWidth,
            DamagePlanGeometry.PlanTop + 0.53 * DamagePlanGeometry.PlanHeight,
            0.08 * DamagePlanGeometry.PlanWidth);
        Assert.Contains(
            string.Create(CultureInfo.InvariantCulture, $"data-mark=\"0\"><circle class=\"area\" cx=\"{disc.CentreX:0.#}\" cy=\"{disc.CentreY:0.#}\" r=\"{disc.Radius:0.#}\" /><path class=\"burst\" d=\"{DamagePlanGeometry.BurstPath(disc)}\" fill=\"#ffeb69\" fill-opacity=\"0.96\" stroke=\"#1d1d1d\""),
            damage,
            StringComparison.Ordinal);
        // One look for every burst: no severity shade, no number, no clip.
        Assert.Equal(2, Regex.Count(damage, "<path class=\"burst\""));
        Assert.DoesNotContain("clip-path", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"n\"", damage, StringComparison.Ordinal);
        // No Vehicle type recorded: the car.
        Assert.Contains(DamagePlanGeometry.Artwork(DamagePlanGeometry.Car), damage, StringComparison.Ordinal);
        Assert.DoesNotContain("dm-guides", damage, StringComparison.Ordinal);

        // v46 item I: what the markers no longer carry is gone.
        foreach (var removed in new[]
        {
            "data-damage-plan-area", "data-damage-area", "data-damage-readout", "data-damage-reset",
            "data-damage-location", "data-damage-severity", "data-damage-impact-list", "data-damage-row",
            "Multiple", "Impact location", "Impact severity",
        })
        {
            Assert.DoesNotContain(removed, damage, StringComparison.Ordinal);
        }

        // The impacts as entered, in their order, values reading where the
        // controls edit; an incomplete row is shown as entered.
        var rows = Rows(damage);
        Assert.Equal(3, rows.Length);
        Assert.All(rows, row => Assert.Contains("<span class=\"impact-entry-label\">Impact</span>", row, StringComparison.Ordinal));
        Assert.Contains("<div class=\"fv\">Light</div>", rows[0], StringComparison.Ordinal);
        Assert.Contains("<div class=\"fv\">Front</div>", rows[0], StringComparison.Ordinal);
        Assert.Contains("Front bumper scuffed.", rows[0], StringComparison.Ordinal);
        Assert.Contains("<div class=\"fv empty\"></div>", rows[1], StringComparison.Ordinal);
        Assert.Contains("<div class=\"fv\">Moderate to heavy</div>", rows[2], StringComparison.Ordinal);
        Assert.All(rows, row => Assert.Contains("<span class=\"fc ro\" data-caption=\"Severity\">", row, StringComparison.Ordinal));
        Assert.All(rows, row => Assert.Contains("aria-label=\"Clear\" title=\"Clear\" disabled=\"disabled\"", row, StringComparison.Ordinal));
        Assert.Contains(">Severity</span><span class=\"lbl\">Location</span><span class=\"lbl\">Note</span>", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("<select", damage, StringComparison.Ordinal);

        // The narrative is the report's Nature of Incident: a sentence for
        // each impact naming both a severity and a location, each its own
        // line block, spaced apart by the stylesheet (v47).
        Assert.Equal(
            [
                "The vehicle has suffered light collision/impact damage to the front.",
                "The vehicle has suffered moderate to heavy collision/impact damage to the rear.",
            ],
            NarrativeLines(damage));
        Assert.DoesNotContain("damage to the .", damage, StringComparison.Ordinal);
        // The impacts block is the cell a readiness jump outlines; with rows
        // there is no Skip impacts.
        Assert.Contains($"data-impacts data-field=\"{AssessmentVocabulary.Impacts}\"", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("Skip impacts", damage, StringComparison.Ordinal);

        // Read mode: neither hidden JSON field the Save reads is rendered
        // while nothing edits, and neither are Clear markers and Add impact.
        Assert.DoesNotContain("name=\"damageMarkers\"", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-damage-input", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-impacts-input", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-damage-clear-markers", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-impacts-add", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-impacts-template", damage, StringComparison.Ordinal);
        Assert.Equal("false", DamageEditable(damage));
    }

    /// <summary>
    /// With no impact entered the section says so, and the narrative is not
    /// recorded.
    /// </summary>
    [Fact]
    public async Task WithNoImpactTheSectionSaysNoDamageRecorded()
    {
        var source = new DamageSource("[]", "[]");
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
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

        Assert.Empty(Rows(damage));
        Assert.Contains("data-impacts-empty>No damage recorded.</p>", damage, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\" hidden=\"hidden\" data-impacts-head", damage, StringComparison.Ordinal);
        Assert.Contains($"data-damage-narrative>{OperatorLabels.CaseWorkspace.AbsentValue}</div>", damage, StringComparison.Ordinal);
        // Reading, Skip impacts is a static tick under the empty line, off (v47 C).
        Assert.Matches(
            "data-impacts-empty>No damage recorded\\.</p>\\s*<div class=\"impact-entry-foot impact-entry-foot--read\">\\s*<span></span>\\s*"
            + "<span class=\"choice is-static impact-entry-skip\" data-on=\"false\">\\s*<span class=\"tick\">\\s*</span>\\s*<span>Skip impacts</span>",
            damage);
        Assert.DoesNotContain("type=\"checkbox\"", damage, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Case saved with Skip impacts reads the static tick on (v47 C), and the
    /// narrative stays not recorded: nothing prints for the skip.
    /// </summary>
    [Fact]
    public async Task ASkippedCaseReadsSkipImpactsTicked()
    {
        var source = new DamageSource("[]", "[]", skipped: "true");
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
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

        Assert.Matches(
            "<span class=\"choice is-static impact-entry-skip\" data-on=\"true\">\\s*<span class=\"tick\">\\s*<svg class=\"icon\" aria-hidden=\"true\"><use href=\"[^\"]*icon-check\" /></svg>",
            damage);
        Assert.Contains($"data-damage-narrative>{OperatorLabels.CaseWorkspace.AbsentValue}</div>", damage, StringComparison.Ordinal);
    }

    /// <summary>
    /// While the Engineer's session holds the section, the hidden impacts
    /// field joins the one Case form and the plan is live.
    /// </summary>
    /// <summary>
    /// The plan is the recorded vehicle's (28 September 2026): a van draws
    /// the van and a motorcycle or scooter the motorbike, each pressable only
    /// on its own outline.
    /// </summary>
    [Theory]
    [InlineData("van", DamagePlanGeometry.Van)]
    [InlineData("motorcycle", DamagePlanGeometry.Motorbike)]
    [InlineData("scooter", DamagePlanGeometry.Motorbike)]
    public async Task TheDamageSectionDrawsTheRecordedVehicle(string vehicleType, string profile)
    {
        var source = new DamageSource("[{\"x\":0.5,\"y\":0.1,\"r\":0.12}]", "[]", vehicleType);
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
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

        Assert.Contains(DamagePlanGeometry.Artwork(profile), damage, StringComparison.Ordinal);
        Assert.DoesNotContain(DamagePlanGeometry.Artwork(DamagePlanGeometry.Car), damage, StringComparison.Ordinal);
        foreach (var hitPath in DamagePlanGeometry.HitPaths(profile))
        {
            Assert.Contains($"<path d=\"{hitPath}\" />", damage, StringComparison.Ordinal);
        }
        Assert.Single(Regex.Matches(damage, "<path class=\"burst\""));
    }

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
        Assert.Contains("name=\"damageMarkers\" form=\"case-edit-form\"", damage, StringComparison.Ordinal);
        Assert.Contains("data-damage-input", damage, StringComparison.Ordinal);
        Assert.Contains($"viewBox=\"{PlanViewBox}\"", damage, StringComparison.Ordinal);
        // The impacts join the one Case form as one JSON field; a new row
        // starts blank (v46 item L); Add impact and Clear markers show only
        // while editing; there is no Reset and no area control.
        Assert.Contains(
            $"name=\"{CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.Impacts)}\" form=\"case-edit-form\" value=\"[]\" data-impacts-input",
            damage,
            StringComparison.Ordinal);
        Assert.Contains("data-impacts-empty>No damage recorded.</p>", damage, StringComparison.Ordinal);
        Assert.Contains("<span>Add impact</span>", damage, StringComparison.Ordinal);
        // v47 B: with no row, Skip impacts stands beside Add impact, unticked,
        // posting "true" ahead of its hidden "false"; Add impact is live.
        var skipName = CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.ImpactsSkipped);
        Assert.Matches(
            $"<label class=\"choice impact-entry-skip\" data-field=\"{Regex.Escape(AssessmentVocabulary.ImpactsSkipped)}\" data-impacts-skip>\\s*"
            + $"<input type=\"checkbox\" name=\"{Regex.Escape(skipName)}\" value=\"true\" form=\"case-edit-form\" />\\s*"
            + $"<input type=\"hidden\" name=\"{Regex.Escape(skipName)}\" value=\"false\" form=\"case-edit-form\" />\\s*"
            + "<span>Skip impacts</span>",
            damage);
        Assert.Contains("<button type=\"button\" class=\"btn impact-entry-add\" data-impacts-add>", damage, StringComparison.Ordinal);
        Assert.Contains("data-damage-clear-markers", damage, StringComparison.Ordinal);
        Assert.Contains("<span>Clear markers</span>", damage, StringComparison.Ordinal);
        Assert.Contains("class=\"dm-guides\"", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-damage-reset", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-damage-plan-area", damage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-damage-area", damage, StringComparison.Ordinal);
        var template = damage[damage.IndexOf("data-impacts-template", StringComparison.Ordinal)..];
        Assert.Contains("<select class=\"fi\" aria-label=\"Severity\" data-impact-severity>", template, StringComparison.Ordinal);
        Assert.Contains("<select class=\"fi\" aria-label=\"Location\" data-impact-location>", template, StringComparison.Ordinal);
        Assert.Contains("<input class=\"fi\" maxlength=\"200\" aria-label=\"Note\" value=\"\" data-impact-note />", template, StringComparison.Ordinal);
        Assert.DoesNotContain("selected", template[..template.IndexOf("</template>", StringComparison.Ordinal)], StringComparison.Ordinal);
        // v46 item B: the Location list is the eleven areas, no Multiple.
        foreach (var (code, name) in AssessmentVocabulary.DamageAreas)
        {
            Assert.Contains($"<option value=\"{code}\">{name}</option>", template, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("multiple", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-label=\"Clear\" title=\"Clear\" data-impact-clear", template, StringComparison.Ordinal);
        // The editor is handed Core's disc limits and the words of Core's
        // Nature of Incident sentence, so the browser invents no wording.
        Assert.Contains("minRadius", damage, StringComparison.Ordinal);
        Assert.Contains("maxRadius", damage, StringComparison.Ordinal);
        Assert.Contains("collision/impact damage to the {location}.", damage, StringComparison.Ordinal);
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
                null,
                CaseAssetRotation.Clockwise90,
                new(0.25m, 0.2m, 0.5m, 0.4m)),
            edit);
    }

    /// <summary>
    /// While editing, a saved Skip impacts is ticked and greys out Add impact
    /// (v47, round 2); a row hides the tick, which the page still carries so
    /// clearing the last row can offer it again.
    /// </summary>
    [Fact]
    public async Task WhileEditingSkipImpactsGreysOutAddImpactAndARowHidesIt()
    {
        var skippedStore = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation,
        };
        skippedStore.AssessmentFields[AssessmentVocabulary.ImpactsSkipped] = "true";
        using (var workspace = await EnterEngineerEditModeAsync(skippedStore, services =>
        {
            Substitute<IGetAssessmentWorkspace>(services, skippedStore);
        }))
        {
            var damage = Section(await workspace.GetWorkspaceAsync(), "section-damage-title");
            Assert.Contains("<button type=\"button\" class=\"btn impact-entry-add\" disabled=\"disabled\" data-impacts-add>", damage, StringComparison.Ordinal);
            Assert.Matches("data-impacts-skip>\\s*<input type=\"checkbox\" name=\"[^\"]+\" value=\"true\" form=\"case-edit-form\" checked=\"checked\" />", damage);
        }

        var rowStore = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation,
        };
        rowStore.AssessmentFields[AssessmentVocabulary.Impacts] = "[{\"severity\":\"moderate\",\"location\":\"\",\"note\":\"\"}]";
        using (var workspace = await EnterEngineerEditModeAsync(rowStore, services =>
        {
            Substitute<IGetAssessmentWorkspace>(services, rowStore);
        }))
        {
            var damage = Section(await workspace.GetWorkspaceAsync(), "section-damage-title");
            Assert.Single(Rows(damage));
            Assert.Contains("<label class=\"choice impact-entry-skip\" hidden=\"hidden\"", damage, StringComparison.Ordinal);
            Assert.Contains("<button type=\"button\" class=\"btn impact-entry-add\" data-impacts-add>", damage, StringComparison.Ordinal);
        }
    }

    /// <summary>The Incident narrative's line blocks, in order.</summary>
    private static string[] NarrativeLines(string damage)
    {
        var start = damage.IndexOf("data-damage-narrative>", StringComparison.Ordinal);
        Assert.True(start >= 0, "The Incident narrative is not rendered.");
        var end = damage.IndexOf("</div>", start, StringComparison.Ordinal);
        return [.. Regex.Matches(damage[start..end], "<span class=\"fv-line\">([^<]*)</span>").Select(match => match.Groups[1].Value)];
    }

    private static string DamageEditable(string damage)
    {
        var match = Regex.Match(damage, "data-damage-editable=\"(true|false)\"");
        Assert.True(match.Success, "The Damage section does not state whether it edits.");
        return match.Groups[1].Value;
    }

    /// <summary>The impact rows, in order, each up to the next.</summary>
    private static string[] Rows(string damage)
    {
        var rows = damage.IndexOf("data-impacts-rows", StringComparison.Ordinal);
        Assert.True(rows >= 0, "The impact rows are not rendered.");
        var end = damage.IndexOf("data-impacts-empty", rows, StringComparison.Ordinal);
        return damage[rows..end].Split("data-impact-row>")[1..];
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
    /// A With Engineer Case whose assessment carries damage markers and
    /// impacts, read without a lease so the Damage section renders its read
    /// view.
    /// </summary>
    private sealed class DamageSource(string markers, string impacts, string? vehicleType = null, string? skipped = null) :
        IGetCaseEditBasis,
        IGetCasePageFrame,
        IGetAssessmentWorkspace,
        ICaseReportSnapshotSource,
        IListCaseEstimates
    {
        public Guid CaseId { get; } = Guid.NewGuid();

        public void Substitute(IServiceCollection services)
        {
            services.RemoveAll<IGetCasePageFrame>();
            services.RemoveAll<IGetAssessmentWorkspace>();
            services.RemoveAll<ICaseReportSnapshotSource>();
            services.RemoveAll<IListCaseEstimates>();
            services.AddSingleton<IGetCaseEditBasis>(this);
            services.AddSingleton<IGetCasePageFrame>(this);
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
                new(AssessmentVocabulary.DamageMarkers, markers, ActorKind.Staff, "engineer-1",
                    DateTimeOffset.UtcNow),
                new(AssessmentVocabulary.Impacts, impacts, ActorKind.Staff, "engineer-1",
                    DateTimeOffset.UtcNow),
                .. (vehicleType is null
                    ? Array.Empty<AssessmentFieldValue>()
                    : [new(AssessmentVocabulary.VehicleType, vehicleType, ActorKind.Staff, "engineer-1", DateTimeOffset.UtcNow)]),
                .. (skipped is null
                    ? Array.Empty<AssessmentFieldValue>()
                    : [new(AssessmentVocabulary.ImpactsSkipped, skipped, ActorKind.Staff, "engineer-1", DateTimeOffset.UtcNow)])
            ],
            [],
            new("AB12CDE", null, null, null, null, null, "tbc", null, new DateOnly(2026, 8, 2), null, null,
                null, "Alex Example", "P-100"));

        private CaseSectionFrame Frame()
        {
            var workflow = Workflow;
            return new(
                new CaseSearchItem(
                    CaseId, workflow.Identity.Reference, null, CaseType.Inspection, "Approved Principal",
                    workflow.State, null, "AB12CDE", "Alex Example", "P-100", DateTimeOffset.UtcNow,
                    "Email", DateTimeOffset.UtcNow),
                workflow,
                null);
        }

        private CaseDataProjection Data() => AssessmentWorkspaceTestData.Create(Assessment()).Data;

        Task<CaseEditBasis?> IGetCaseEditBasis.ExecuteAsync(
            GetCaseQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<CaseEditBasis?>(query.CaseId == CaseId ? new(Frame(), Data()) : null);

        Task<CasePageFrame?> IGetCasePageFrame.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<CasePageFrame?>(query.CaseId == CaseId
                ? new(Frame(), [], [], new(null, null), Data())
                : null);

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
            CaseWorkSelector work, ReportProjectionReuse? reuse, CancellationToken cancellationToken)
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
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation
        };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILeaseCaseForEdit>();
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCaseEditBasis>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<ILeaseCaseForEdit>(store);
                Substitute<ICaseWorkflowQueries>(services, store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        const string markers = "[{\"x\":0.5,\"y\":0.53,\"r\":0.08}]";
        const string impacts = "[{\"severity\":\"moderate\",\"location\":\"rear\",\"note\":\"Boot lid\"}]";
        using var saveResponse = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=Save",
            Form(
                AntiforgeryValue(initialHtml),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", DetailsModelOperationKey),
                ("editLeaseToken", store.LeaseToken),
                ("reason", "Recorded damage observations"),
                ("damageMarkers", markers),
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.Impacts), impacts),
                (CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.DamageUnrelated), "Old rear bumper scrape")));
        AssertPrg(saveResponse, store.CaseId);

        var damage = Assert.Single(store.Saves).Damage;
        Assert.NotNull(damage);
        Assert.Equal([new DamageDisc(0.5, 0.53, 0.08)], damage.Markers);
        Assert.Equal(impacts, damage.AssessmentFields![AssessmentVocabulary.Impacts]);
        Assert.Equal("Old rear bumper scrape", damage.AssessmentFields![AssessmentVocabulary.DamageUnrelated]);
    }

    /// <summary>
    /// The Skip impacts tick joins the Damage section of the one Case save: a
    /// ticked box posts "true" ahead of its hidden "false" and the command
    /// records true; an unticked one posts the "false" alone (v47 B).
    /// </summary>
    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public async Task ASaveCarriesSkipImpactsToTheWorkspaceCommand(bool ticked, string recorded)
    {
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
        var store = new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            CaseState = CaseLifecycleState.ReportPreparation
        };
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILeaseCaseForEdit>();
                services.RemoveAll<ISaveCaseWorkspace>();
                services.AddSingleton<IGetCaseEditBasis>(store);
                SubstituteDetailsPageReaders(services, store);
                services.AddSingleton<ILeaseCaseForEdit>(store);
                Substitute<ICaseWorkflowQueries>(services, store);
                services.AddSingleton<ISaveCaseWorkspace>(store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var initialHtml = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        var skipName = CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.ImpactsSkipped);
        List<KeyValuePair<string, string>> fields =
        [
            new("__RequestVerificationToken", AntiforgeryValue(initialHtml)),
            new("id", store.CaseId.ToString("D")),
            new("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
            new("operationKey", DetailsModelOperationKey),
            new("editLeaseToken", store.LeaseToken),
            new(CaseWorkspaceLabels.Editors.FormName(AssessmentVocabulary.Impacts), "[]"),
        ];
        if (ticked)
        {
            fields.Add(new(skipName, "true"));
        }
        fields.Add(new(skipName, "false"));
        using var form = new FormUrlEncodedContent(fields);
        using var saveResponse = await client.PostAsync($"/Cases/{store.CaseId:D}?handler=Save", form);
        AssertPrg(saveResponse, store.CaseId);

        var damage = Assert.Single(store.Saves).Damage;
        Assert.NotNull(damage);
        Assert.Equal(recorded, damage.AssessmentFields![AssessmentVocabulary.ImpactsSkipped]);
    }

}
