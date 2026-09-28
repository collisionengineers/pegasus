using System.Net;
using System.Text.Json;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Estimate Apply post carries percentage intent only and scales the saved
/// spec: the page saves the Case, the spec with it, first (one Save, 23
/// September 2026). A forged monetary target is ignored, the composed act
/// receives one lease, and the persisted scaling state enables removal after
/// Apply and removes the action after a successful removal.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseEstimateScalingWebTests
{
    /// <summary>
    /// The one Case Save carries the Repair Spec: a line that does not read as
    /// a number, or a stale Case, refuses the whole save and records nothing
    /// of the spec, naming the line's reason.
    /// </summary>
    [Theory]
    [InlineData("bad amount", false)]
    [InlineData("120.50", true)]
    public async Task ARefusedCaseSaveRecordsNothingOfTheSpec(string firstPrice, bool staleVersion)
    {
        var caseId = Guid.NewGuid();
        var store = new AssessmentEstimateImportWebTests.RecordingStores(caseId)
        {
            WorkingEstimate = AssessmentEstimateImportWebTests.DraftSpecification(caseId),
        };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = AssessmentEstimateImportWebTests.Compose(baseFactory, store);
        using var client = AssessmentEstimateImportWebTests.CreateEngineerClient(factory);
        var html = await AssessmentEstimateImportWebTests.EnterEditModeAsync(
            client, caseId, $"?section=estimate&estimate={store.WorkingEstimate.SpecificationId:D}");
        var fields = AssessmentEstimateImportWebTests.NewEnumerable(
            ("__RequestVerificationToken", AssessmentEstimateImportWebTests.AntiforgeryValue(html)),
            ("id", caseId.ToString("D")),
            ("operationKey", AssessmentEstimateImportWebTests.NewOperationKey()),
            ("editLeaseToken", AssessmentEstimateImportWebTests.InputValue(html, "editLeaseToken")),
            ("expectedVersion", staleVersion ? "6" : AssessmentEstimateImportWebTests.InputValue(html, "expectedVersion")),
            ("estimateId", store.WorkingEstimate.SpecificationId.ToString("D")),
            ("estimateName", "New repairer draft"),
            ("estimateLabourRate", "81.25"),
            ("estimateOtherCosts", "14.40"),
            ("estimateVatPercent", "17.5"),
            ("estimateVatStatus", "Registered"),
            ("lineId", store.WorkingEstimate.Lines[0].Id.ToString("D")),
            ("lineOperation", "Replace"),
            ("lineDescription", "Front bumper revision"),
            ("linePartNumber", "FB-123"),
            ("lineQuantity", "1"),
            ("linePartPounds", firstPrice),
            ("lineLabourHours", ""),
            ("linePaintHours", ""),
            ("lineMaterials", ""),
            ("lineId", ""),
            ("lineOperation", "Repair"),
            ("lineDescription", "Door repair"),
            ("linePartNumber", ""),
            ("lineQuantity", ""),
            ("linePartPounds", ""),
            ("lineLabourHours", "3.5"),
            ("linePaintHours", "1.5"),
            ("lineMaterials", "12.00")).ToArray();

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=Save&section=estimate",
            new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(store.SavedEstimates);
        Assert.Equal(staleVersion ? 1 : 0, store.SubmittedEstimates.Count);
        if (!staleVersion)
        {
            var refusal = await AssessmentEstimateImportWebTests.GetHtmlAsync(
                client, response.Headers.Location!.OriginalString);
            Assert.Contains("does not read as a number", refusal, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ApplyUsesThePercentageThenRemovalBecomesUnavailable()
    {
        var caseId = Guid.NewGuid();
        // A second estimate on the Case is the one in use; the page scales
        // the one the operator has open.
        var baseEstimate = AssessmentEstimateImportWebTests.DraftSpecification(caseId) with
        {
            SpecificationId = Guid.NewGuid(),
            IsCurrent = true,
        };
        var store = new AssessmentEstimateImportWebTests.RecordingStores(caseId, 1_000m)
        {
            WorkingEstimate = AssessmentEstimateImportWebTests.DraftSpecification(caseId) with
            {
                Details = new("Repairer", 80m, null, 20m,
                    Vat: Pegasus.Core.Assessment.EstimateVatPolicy.For(
                        Pegasus.Core.Assessment.RepairerVatStatus.Registered)),
            },
            OtherEstimate = baseEstimate,
        };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = AssessmentEstimateImportWebTests.Compose(baseFactory, store);
        using var client = AssessmentEstimateImportWebTests.CreateEngineerClient(factory);

        var html = await AssessmentEstimateImportWebTests.EnterEditModeAsync(
            client,
            caseId,
            $"?section=estimate&estimate={store.WorkingEstimate.SpecificationId:D}");
        var draft = store.WorkingEstimate;
        // Apply is its own form, saved first: it carries the scaling intent
        // alone, never the editor's content.
        Assert.Contains("id=\"case-estimate-scale-form\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"targetPercent\" form=\"case-estimate-scale-form\"", html, StringComparison.Ordinal);
        var leaseToken = AssessmentEstimateImportWebTests.InputValue(html, "editLeaseToken");
        var fields = AssessmentEstimateImportWebTests.NewEnumerable(
            ("__RequestVerificationToken", AssessmentEstimateImportWebTests.AntiforgeryValue(html)),
            ("id", caseId.ToString("D")),
            ("operationKey", AssessmentEstimateImportWebTests.NewOperationKey()),
            ("editLeaseToken", leaseToken),
            ("expectedVersion", AssessmentEstimateImportWebTests.InputValue(html, "expectedVersion")),
            ("estimateId", draft!.SpecificationId.ToString("D")),
            ("targetPercent", "45"),
            ("targetGross", "0.01"),
            ("floorRate", "50"),
            ("floorPrice", "65")).ToArray();

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ScaleEstimate&section=estimate",
            new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var applied = Assert.Single(store.ScaleRequests);
        Assert.Equal(45m, applied.TargetPercentOfValue);
        Assert.Equal(1_000m, applied.EngineerValue);
        Assert.Equal(leaseToken, applied.EditLeaseToken);
        Assert.Equal(draft.SpecificationId, applied.SpecificationId);
        Assert.True(store.WorkingEstimate!.Details.BaseHourlyRate < 80m);
        Assert.Null(store.WorkingEstimate.Details.Rate);

        var afterApply = await AssessmentEstimateImportWebTests.GetHtmlAsync(
            client, response.Headers.Location!.OriginalString);
        Assert.Contains("data-case-editing=\"true\"", afterApply, StringComparison.Ordinal);
        // The scale form is not an editor: it clears no draft of the Case's.
        Assert.DoesNotContain("data-editor-commit=\"{", afterApply, StringComparison.Ordinal);
        Assert.NotEqual(leaseToken, AssessmentEstimateImportWebTests.InputValue(afterApply, "editLeaseToken"));
        Assert.DoesNotContain("data-scale-remove disabled=\"disabled\"", afterApply, StringComparison.Ordinal);
        Assert.Contains("id=\"remove-scaling-form\"", afterApply, StringComparison.Ordinal);

        using var removeResponse = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=RemoveEstimateScaling&section=estimate",
            AssessmentEstimateImportWebTests.Form(
                AssessmentEstimateImportWebTests.AntiforgeryValue(afterApply),
                ("id", caseId.ToString("D")),
                ("operationKey", AssessmentEstimateImportWebTests.NewOperationKey()),
                ("expectedVersion", AssessmentEstimateImportWebTests.InputValue(afterApply, "expectedVersion")),
                ("editLeaseToken", AssessmentEstimateImportWebTests.InputValue(afterApply, "editLeaseToken")),
                ("estimateId", draft.SpecificationId.ToString("D"))));
        Assert.Equal(HttpStatusCode.Redirect, removeResponse.StatusCode);

        var afterRemoval = await AssessmentEstimateImportWebTests.EnterEditModeAsync(
            client, caseId, $"?section=estimate&estimate={draft.SpecificationId:D}");
        Assert.Contains("data-scale-remove disabled=\"disabled\"", afterRemoval, StringComparison.Ordinal);
    }

    /// <summary>
    /// Issue 897: moving the slider previews what Apply would make of the spec
    /// as the editor holds it. Core scales and totals it for the bar's
    /// percentage and floors; each line comes back by the posted row it came
    /// from, and nothing is written.
    /// </summary>
    [Fact]
    public async Task ThePreviewScalesTheEditedSpecAndWritesNothing()
    {
        var caseId = Guid.NewGuid();
        var store = new AssessmentEstimateImportWebTests.RecordingStores(caseId, 1_000m)
        {
            WorkingEstimate = AssessmentEstimateImportWebTests.DraftSpecification(caseId) with
            {
                Details = new("Repairer", 80m, null, 20m,
                    Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
            },
        };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = AssessmentEstimateImportWebTests.Compose(baseFactory, store);
        using var client = AssessmentEstimateImportWebTests.CreateEngineerClient(factory);
        var html = await AssessmentEstimateImportWebTests.EnterEditModeAsync(
            client, caseId, $"?section=estimate&estimate={store.WorkingEstimate.SpecificationId:D}");
        var previewUrl = $"/Cases/{caseId:D}?handler=PreviewEstimateScale";
        Assert.Contains($"data-scale-preview-url=\"{previewUrl}\"", html, StringComparison.Ordinal);
        Assert.Contains("data-rollup=\"gross\"", html, StringComparison.Ordinal);
        var draft = store.WorkingEstimate;

        async Task<JsonElement> PreviewAsync(string percent, string partPounds = "620.20")
        {
            var fields = AssessmentEstimateImportWebTests.NewEnumerable(
                ("__RequestVerificationToken", AssessmentEstimateImportWebTests.AntiforgeryValue(html)),
                ("estimateId", draft!.SpecificationId.ToString("D")),
                ("estimateName", "Repairer"),
                ("estimateLabourRate", "80"),
                ("estimateVatPercent", "20"),
                ("estimateVatStatus", "Registered"),
                ("estimateVatLabour", "true"), ("estimateVatLabour", "false"),
                ("estimateVatParts", "true"), ("estimateVatParts", "false"),
                ("estimateVatMaterials", "true"), ("estimateVatMaterials", "false"),
                ("estimateVatSpecialist", "true"), ("estimateVatSpecialist", "false"),
                // A blank row first: the line is the second row posted.
                ("lineId", ""), ("lineOperation", "Replace"), ("lineDescription", ""), ("linePartNumber", ""),
                ("lineQuantity", ""), ("linePartPounds", ""), ("lineLabourHours", ""), ("linePaintHours", ""),
                ("lineMaterials", ""),
                ("lineId", draft.Lines[0].Id.ToString("D")), ("lineOperation", "Replace"),
                ("lineDescription", "FRONT BUMPER"), ("linePartNumber", ""), ("lineQuantity", "1"),
                ("linePartPounds", partPounds), ("lineLabourHours", ""), ("linePaintHours", ""), ("lineMaterials", ""),
                ("targetPercent", percent),
                ("floorRate", "50"),
                ("floorPrice", "65")).ToArray();
            using var response = await client.PostAsync(previewUrl, new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.Clone();
        }

        // 45 % of £1,000 is below what the price floor allows: the part
        // stops at 65 % of its price, and the readout says what it came to.
        var preview = await PreviewAsync("45");
        Assert.Equal("ok", preview.GetProperty("status").GetString());
        var line = Assert.Single(preview.GetProperty("lines").EnumerateArray());
        Assert.Equal(1, line.GetProperty("row").GetInt32());
        Assert.Equal("403.13", line.GetProperty("price").GetString());
        Assert.Equal("£403.13", preview.GetProperty("rollup").GetProperty("parts").GetString());
        Assert.Equal("£483.76", preview.GetProperty("rollup").GetProperty("gross").GetString());
        var readout = preview.GetProperty("readout").GetString();
        Assert.StartsWith("£744.24 ", readout, StringComparison.Ordinal);
        // The readout states the share asked for, as Apply records it, and
        // that the labour rate stopped at its floor.
        Assert.Contains("(45.0 % of value)", readout, StringComparison.Ordinal);
        Assert.EndsWith(" · labour at floor", readout, StringComparison.Ordinal);

        // The spec as edited, not as saved: a part typed at £500 totals
        // £600.00 before scaling.
        var edited = await PreviewAsync("45", "500.00");
        Assert.Equal("ok", edited.GetProperty("status").GetString());
        Assert.StartsWith("£600.00 ", edited.GetProperty("readout").GetString(), StringComparison.Ordinal);
        // A line the save refuses is refused by the preview.
        Assert.Equal("refused", (await PreviewAsync("45", "500.005")).GetProperty("status").GetString());

        // A percentage Apply would refuse is refused here too.
        Assert.Equal("refused", (await PreviewAsync("0.5")).GetProperty("status").GetString());

        Assert.Empty(store.ScaleRequests);
        Assert.Empty(store.SubmittedEstimates);
        Assert.Equal(620.20m, store.WorkingEstimate!.Lines[0].Price);
    }

    [Fact]
    public async Task RegionalUpliftSuggestionsUseTheRepairerAddress()
    {
        var caseId = Guid.NewGuid();
        var store = new AssessmentEstimateImportWebTests.RecordingStores(caseId, 1_000m)
        {
            WorkingEstimate = AssessmentEstimateImportWebTests.DraftSpecification(caseId),
            DataOverride = RegionalData(caseId, repairerAddress: "Repairer Yard, SW1A 1AA", inspectionAddress: "Workshop, B1 1AA")
        };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = AssessmentEstimateImportWebTests.Compose(baseFactory, store);
        using var client = AssessmentEstimateImportWebTests.CreateEngineerClient(factory);

        var html = await AssessmentEstimateImportWebTests.EnterEditModeAsync(
            client,
            caseId,
            $"?section=estimate&estimate={store.WorkingEstimate.SpecificationId:D}");

        Assert.Contains("Repairer (SW1)", html, StringComparison.Ordinal);
    }

    private static CaseDataProjection RegionalData(Guid caseId, string repairerAddress, string inspectionAddress)
    {
        var assessment = new CaseAssessmentProjection(
            caseId,
            "QDOS-2026-00042",
            AssessmentEstimateImportWebTests.RecordingStores.CaseVersion,
            CaseLifecycleState.Review,
            null,
            [],
            [],
            new(null, null, null, null, null, null, "tbc", null, new DateOnly(2026, 8, 2), null, null, null, null, null));
        var source = new CaseDataSource(CaseDataSourceKind.StaffCorrection, "test", "Test", "test", 1);
        var repairer = new CaseField<string>(new(repairerAddress, CaseDataValueKind.Fact, source), null, null);
        var inspection = new CaseField<string>(new(inspectionAddress, CaseDataValueKind.Fact, source), null, null);
        return AssessmentWorkspaceTestData.Create(assessment).Data with
        {
            Inspection = AssessmentWorkspaceTestData.Create(assessment).Data.Inspection with
            {
                Address = inspection,
                RepairerAddress = repairer
            }
        };
    }

    [Fact]
    public async Task ContractSuggestionIsVisibleButTheChosenPercentageWins()
    {
        var caseId = Guid.NewGuid();
        var store = new AssessmentEstimateImportWebTests.RecordingStores(caseId, 1_000m, 450m)
        {
            WorkingEstimate = AssessmentEstimateImportWebTests.DraftSpecification(caseId) with
            {
                Details = new("Repairer", 80m, null, 20m,
                    Vat: Pegasus.Core.Assessment.EstimateVatPolicy.For(
                        Pegasus.Core.Assessment.RepairerVatStatus.Registered)),
            },
        };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = AssessmentEstimateImportWebTests.Compose(baseFactory, store);
        using var client = AssessmentEstimateImportWebTests.CreateEngineerClient(factory);

        var html = await AssessmentEstimateImportWebTests.EnterEditModeAsync(
            client,
            caseId,
            $"?section=estimate&estimate={store.WorkingEstimate.SpecificationId:D}");
        var draft = store.WorkingEstimate;
        Assert.Contains("Agreed sum suggests 45%.", html, StringComparison.Ordinal);
        // The section shows the contract repair sentence the report prints:
        // the template's (Sample - Contract Repair Report.pdf, page 2).
        Assert.Contains("A contract repair has been agreed for the sum of ", html, StringComparison.Ordinal);
        Assert.Contains(
            "450.00 including VAT. Costs cannot increase above this figure.",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain("for the total sum of", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"contractTarget\"", html, StringComparison.Ordinal);
        var fields = AssessmentEstimateImportWebTests.NewEnumerable(
            ("__RequestVerificationToken", AssessmentEstimateImportWebTests.AntiforgeryValue(html)),
            ("id", caseId.ToString("D")),
            ("operationKey", AssessmentEstimateImportWebTests.NewOperationKey()),
            ("editLeaseToken", AssessmentEstimateImportWebTests.InputValue(html, "editLeaseToken")),
            ("expectedVersion", AssessmentEstimateImportWebTests.InputValue(html, "expectedVersion")),
            ("estimateId", draft!.SpecificationId.ToString("D")),
            ("targetPercent", "37"),
            ("targetGross", "0.01"),
            ("contractTarget", "true"),
            ("floorRate", "50"),
            ("floorPrice", "65")).ToArray();

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ScaleEstimate&section=estimate",
            new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var applied = Assert.Single(store.ScaleRequests);
        Assert.Equal(37m, applied.TargetPercentOfValue);
    }
}
