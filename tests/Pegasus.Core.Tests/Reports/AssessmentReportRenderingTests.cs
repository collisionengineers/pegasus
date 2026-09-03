using System.Security.Cryptography;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

public sealed class AssessmentReportRenderingTests
{
    [Fact]
    public void ExpandedSnapshotUsesVersionTwoAndImpactHasOnlyD45Members()
    {
        Assert.Equal("rendererref1-v2", Snapshot(AssessmentReportOutcome.Repairable).PayloadVersion);
        Assert.Equal(["Zone", "Severity", "Note"], typeof(ReportImpact).GetProperties().Select(property => property.Name));
    }

    [Theory]
    [InlineData(AssessmentReportOutcome.TotalLoss)]
    [InlineData(AssessmentReportOutcome.Repairable)]
    [InlineData(AssessmentReportOutcome.CashInLieu)]
    [InlineData(AssessmentReportOutcome.ContractRepair)]
    public async Task UseCaseAcceptsEachClosedOutcome(AssessmentReportOutcome outcome)
    {
        var renderer = new FakeRenderer();
        var result = await new GenerateAssessmentReportDraft(renderer).ExecuteAsync(Snapshot(outcome));

        Assert.NotEmpty(result.Assessment.Pdf);
        Assert.NotEmpty(result.FeeNote.Pdf);
        Assert.Equal(outcome, renderer.Received!.Outcome);
    }

    [Fact]
    public async Task IncompleteSnapshotFailsBeforeAdapter()
    {
        var renderer = new FakeRenderer();
        var invalid = Snapshot(AssessmentReportOutcome.Repairable) with { Photos = [] };

        await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => new GenerateAssessmentReportDraft(renderer).ExecuteAsync(invalid));
        Assert.Null(renderer.Received);
    }

    [Fact]
    public void ContractRepairCapIsComputedFromRawComponentsOnce()
    {
        var costs = Snapshot(AssessmentReportOutcome.ContractRepair).Costs;

        Assert.Equal(150m, costs.Labour);
        Assert.Equal(225m, costs.Subtotal);
        Assert.Equal(45m, costs.Vat);
        Assert.Equal(270m, costs.Total);
    }

    [Fact]
    public async Task AlteredPhotoFailsCustodyValidationBeforeAdapter()
    {
        var renderer = new FakeRenderer();
        var valid = Snapshot(AssessmentReportOutcome.Repairable);
        var photo = valid.Photos.Single() with { Content = [1, 2, 3] };

        await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => new GenerateAssessmentReportDraft(renderer).ExecuteAsync(valid with { Photos = [photo] }));
        Assert.Null(renderer.Received);
    }

    [Fact]
    public void FeeTotalsAreComputedInCore()
    {
        var snapshot = Snapshot(AssessmentReportOutcome.Repairable);

        Assert.Equal(120m, snapshot.FeeNet);
        Assert.Equal(24m, snapshot.FeeVat);
        Assert.Equal(144m, snapshot.FeeTotal);
    }

    [Theory]
    [InlineData("A Patterson", "Wrong", "andy_patterson")]
    [InlineData("Neil O'Reilly", "", "neil_oreilly")]
    [InlineData("Unknown", "Unknown", "unknown")]
    public async Task EngineerTupleMismatchFailsBeforeAdapter(string name, string qualifications, string signature)
    {
        var renderer = new FakeRenderer();
        var invalid = Snapshot(AssessmentReportOutcome.Repairable) with
        {
            Engineer = new ReportEngineer(name, qualifications, signature),
        };

        await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => new GenerateAssessmentReportDraft(renderer).ExecuteAsync(invalid));
        Assert.Null(renderer.Received);
    }

    internal static AssessmentReportSnapshot Snapshot(AssessmentReportOutcome outcome)
    {
        var image = File.ReadAllBytes(Path.Combine(RepositoryRoot(), "docs", "design", "brand", "logos", "logo_no_margin.png"));
        return new(
            OurReference: "CE-100", YourReference: "P-100", ReportDate: new DateOnly(2026, 8, 19),
            ClaimantName: "Alex Example", IncidentDate: new DateOnly(2026, 8, 1),
            InstructionsReceived: new DateOnly(2026, 8, 2), Assessed: new DateOnly(2026, 8, 3),
            ReportFor: ["Approved Principal", "1 Example Street"],
            Vehicle: new ReportVehicle("PK12 TMZ", "Ford", "Focus", "2012", "car", "good", "80,000 miles", "online_data", "VIN", "1600 cc", "Petrol", true, "manual", "Blue", "Hatchback", new(2027, 1, 2), new(2027, 3, 4), "None", "P0001", true, "Secure bumper", 25m),
            Outcome: outcome, LegalStatus: "roadworthy", UnroadworthyReason: null,
            ImpactSeverity: "moderate", ImpactLocation: "right_rear", AssessmentMethod: "image_based", LocationAddress: null,
            EngineerValue: 5_000m, RetailValue: 5_000m, TradeValue: 4_000m,
            SalvageCategory: outcome == AssessmentReportOutcome.TotalLoss ? "S" : null,
            SalvageValue: outcome == AssessmentReportOutcome.TotalLoss ? 500m : null,
            Costs: new ReportRepairCosts(5m, 30m, 50m, 20m, 5m, true),
            NewParts: ["Front bumper"], Repairs: ["Bonnet"], Operations: ["Paint front panels"],
            Damage: Damage(), Settlement: Settlement(),
            HistoryCheck: "History clear", EngineerComments: null,
            Engineer: new ReportEngineer("A Patterson", "M.Inst.IAEA", "andy_patterson"),
            AgreedFee: 120m, FeeDescriptionLines: ["Engineering assessment"],
            Photos: [new ReportImageEvidence("box://case/photo-1", "image/png", image, Convert.ToHexStringLower(SHA256.HashData(image)))],
            Sources: [new AcceptedReportSource("assessment", "7", new string('a', 64))]);
    }

    internal static ReportDamage Damage() => new(
        [new("Right rear", "Moderate", "Quarter panel")],
        "ok", "worn", "damaged", "illegal", "ok", "locked", "deployed", "not_fitted",
        "repair_kit", "not_fitted", "Door scratch", 75m, "Red paint");

    internal static ReportSettlement Settlement() => new(
        250m, 100m, true, 6_000m, 4_125m, 4, "Parts delay", "None", 20m, 80m,
        new(2026, 8, 4), 35m, 200m, "Repairer", "Salvage Co", "SAL-1", true, false, true,
        new(2026, 8, 20));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Pegasus.slnx")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class FakeRenderer : IAssessmentReportRenderer
    {
        public AssessmentReportSnapshot? Received { get; private set; }

        public Task<AssessmentReportDraft> RenderAsync(AssessmentReportSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Received = snapshot;
            return Task.FromResult(new AssessmentReportDraft(
                Artifact("assessment"), Artifact("fee-note")));
        }

        private static RenderedReportArtifact Artifact(string family)
        {
            byte[] pdf = [1, 2, 3];
            return new($"{family}.pdf", pdf, 1,
                Convert.ToHexStringLower(SHA256.HashData(pdf)), AssessmentReportContract.TemplateVersion, "fake");
        }
    }
}
