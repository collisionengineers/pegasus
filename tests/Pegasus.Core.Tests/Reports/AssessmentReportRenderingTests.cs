using System.Security.Cryptography;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

public sealed class AssessmentReportRenderingTests
{
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
        var invalid = Snapshot(AssessmentReportOutcome.Repairable) with { PhotoCustodyReferences = [] };

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

    internal static AssessmentReportSnapshot Snapshot(AssessmentReportOutcome outcome) => new(
        "CE-100", "P-100", new DateOnly(2026, 8, 19), "Alex Example", new DateOnly(2026, 8, 1),
        ["Approved Principal", "1 Example Street"],
        new ReportVehicle("PK12 TMZ", "Ford", "Focus", "2012", "car", "good", "80,000 miles (online data)"),
        outcome, "roadworthy", null, 5_000m, 5_000m, 4_000m,
        outcome == AssessmentReportOutcome.TotalLoss ? "N" : null,
        outcome == AssessmentReportOutcome.TotalLoss ? 500m : null,
        new ReportRepairCosts(5m, 30m, 50m, 20m, 5m, true),
        ["Front bumper"], ["Bonnet"], ["Paint front panels"], "History clear", null,
        new ReportEngineer("A Patterson", "M.Inst.IAEA", "andy_patterson"),
        120m, ["Engineering assessment"], ["box://case/photo-1"],
        [new AcceptedReportSource("assessment", "7", new string('a', 64))]);

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
