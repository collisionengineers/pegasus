using System.Net;
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
            CurrentDraft = AssessmentEstimateImportWebTests.DraftSpecification(caseId),
        };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = AssessmentEstimateImportWebTests.Compose(baseFactory, store);
        using var client = AssessmentEstimateImportWebTests.CreateEngineerClient(factory);
        var html = await AssessmentEstimateImportWebTests.EnterEditModeAsync(
            client, caseId, $"?section=estimate&estimate={store.CurrentDraft.SpecificationId:D}");
        var fields = AssessmentEstimateImportWebTests.NewEnumerable(
            ("__RequestVerificationToken", AssessmentEstimateImportWebTests.AntiforgeryValue(html)),
            ("id", caseId.ToString("D")),
            ("operationKey", AssessmentEstimateImportWebTests.NewOperationKey()),
            ("editLeaseToken", AssessmentEstimateImportWebTests.InputValue(html, "editLeaseToken")),
            ("expectedVersion", staleVersion ? "6" : AssessmentEstimateImportWebTests.InputValue(html, "expectedVersion")),
            ("estimateId", store.CurrentDraft.SpecificationId.ToString("D")),
            ("estimateName", "New repairer draft"),
            ("estimateLabourRate", "81.25"),
            ("estimateOtherCosts", "14.40"),
            ("estimateVatPercent", "17.5"),
            ("estimateVatStatus", "Registered"),
            ("lineId", store.CurrentDraft.Lines[0].Id.ToString("D")),
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
        var acceptedDraft = AssessmentEstimateImportWebTests.DraftSpecification(caseId) with
        {
            SpecificationId = Guid.NewGuid(),
        };
        // Accepting a version freezes its calculation with it, and the report
        // reads only that frozen record. A version that says Accepted while
        // carrying none is a state the store cannot produce, and asking the
        // page to project it is what turned this into a 500.
        var baseEstimate = acceptedDraft with
        {
            State = RepairSpecificationState.Accepted,
            RecordedTotals = EstimateTotals.Compute(acceptedDraft),
        };
        var store = new AssessmentEstimateImportWebTests.RecordingStores(caseId, 1_000m)
        {
            CurrentDraft = AssessmentEstimateImportWebTests.DraftSpecification(caseId) with
            {
                Details = new("Repairer", 80m, null, 20m,
                    Vat: Pegasus.Core.Assessment.EstimateVatPolicy.For(
                        Pegasus.Core.Assessment.RepairerVatStatus.Registered)),
            },
            CurrentAccepted = baseEstimate,
        };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = AssessmentEstimateImportWebTests.Compose(baseFactory, store);
        using var client = AssessmentEstimateImportWebTests.CreateEngineerClient(factory);

        var html = await AssessmentEstimateImportWebTests.EnterEditModeAsync(
            client,
            caseId,
            $"?section=estimate&estimate={store.CurrentDraft.SpecificationId:D}");
        var draft = store.CurrentDraft;
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
        Assert.True(store.CurrentDraft!.Details.BaseHourlyRate < 80m);
        Assert.Null(store.CurrentDraft.Details.Rate);

        var afterApply = await AssessmentEstimateImportWebTests.GetHtmlAsync(
            client, response.Headers.Location!.OriginalString);
        Assert.Contains("data-case-editing=\"true\"", afterApply, StringComparison.Ordinal);
        // The scale form is not an editor: it clears no draft of the Case's.
        Assert.DoesNotContain("data-editor-commit=\"", afterApply, StringComparison.Ordinal);
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

    [Fact]
    public async Task RegionalUpliftSuggestionsUseTheRepairerAddress()
    {
        var caseId = Guid.NewGuid();
        var store = new AssessmentEstimateImportWebTests.RecordingStores(caseId, 1_000m)
        {
            CurrentDraft = AssessmentEstimateImportWebTests.DraftSpecification(caseId),
            DataOverride = RegionalData(caseId, repairerAddress: "Repairer Yard, SW1A 1AA", inspectionAddress: "Workshop, B1 1AA")
        };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = AssessmentEstimateImportWebTests.Compose(baseFactory, store);
        using var client = AssessmentEstimateImportWebTests.CreateEngineerClient(factory);

        var html = await AssessmentEstimateImportWebTests.EnterEditModeAsync(
            client,
            caseId,
            $"?section=estimate&estimate={store.CurrentDraft.SpecificationId:D}");

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
            new(null, null, null, null, null, null, "tbc", null, null, null, null));
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
            CurrentDraft = AssessmentEstimateImportWebTests.DraftSpecification(caseId) with
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
            $"?section=estimate&estimate={store.CurrentDraft.SpecificationId:D}");
        var draft = store.CurrentDraft;
        Assert.Contains("Agreed sum suggests 45%.", html, StringComparison.Ordinal);
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
