using System.Security.Cryptography;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

public sealed class AssessmentReportRenderingTests
{
    private static readonly DateTimeOffset RecordedAtUtc = new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExpandedSnapshotUsesVersionThreeAndImpactHasOnlyD45Members()
    {
        Assert.Equal("rendererref1-v3", Snapshot(AssessmentReportOutcome.Repairable).PayloadVersion);
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
        var result = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(Snapshot(outcome), CaseReportArtifactKind.AssessmentReport);

        Assert.NotEmpty(result.Pdf);
        Assert.Equal(outcome, renderer.Received!.Outcome);
    }

    [Theory]
    [InlineData(CaseReportArtifactKind.AssessmentReport)]
    [InlineData(CaseReportArtifactKind.FeeNote)]
    public async Task OnlyTheRequestedKindIsRendered(CaseReportArtifactKind kind)
    {
        var renderer = new FakeRenderer();

        var result = await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(Snapshot(AssessmentReportOutcome.Repairable), kind);

        Assert.Equal([kind], renderer.ReceivedKinds);
        Assert.Equal($"{kind}.pdf", result.SuggestedFileName);
    }

    [Fact]
    public async Task IncompleteSnapshotFailsBeforeAdapter()
    {
        var renderer = new FakeRenderer();
        var invalid = Snapshot(AssessmentReportOutcome.Repairable) with { Photos = [] };

        await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => new GenerateAssessmentReportDraft(renderer)
                .ExecuteAsync(invalid, CaseReportArtifactKind.AssessmentReport));
        Assert.Null(renderer.Received);
    }

    [Fact]
    public void ContractRepairCapIsTheEstimatesOwnPrintedTotal()
    {
        var costs = Snapshot(AssessmentReportOutcome.ContractRepair).Costs;

        Assert.Equal(150m, costs.Printed.PanelLabour);
        Assert.Equal(225m, costs.Printed.Net);
        Assert.Equal(45m, costs.Printed.Vat);
        Assert.Equal(270m, costs.Total);
    }

    [Fact]
    public void ThePrintedComponentsReconcileToThePrintedTotal()
    {
        var costs = Snapshot(AssessmentReportOutcome.Repairable).Costs;

        Assert.Equal(
            costs.Printed.Net,
            costs.Printed.Parts + costs.Printed.PanelLabour + costs.Printed.PaintLabour
                + costs.Printed.Materials + costs.Printed.Specialist);
        Assert.Equal(costs.Printed.Gross, costs.Printed.Net + costs.Printed.Vat);
        costs.Validate();
    }

    [Fact]
    public void TheVatLabelIsTheEstimatesOwnPercentageNotABoolean()
    {
        Assert.Equal("VAT (20%)", Costs(20m).VatLabel);
        Assert.Equal("VAT (5%)", Costs(5m).VatLabel);
        Assert.Equal("VAT (0%)", Costs(0m).VatLabel);
        Assert.Equal("VAT (17.5%)", Costs(17.5m).VatLabel);
    }

    [Fact]
    public void ComponentsThatDoNotReconcileFailClosed()
    {
        var costs = Costs(20m);
        var broken = costs with
        {
            Totals = costs.Totals with
            {
                Printed = costs.Printed with { Net = costs.Printed.Net + 1m },
            },
        };

        Assert.Throws<ReportRenderRejectedException>(broken.Validate);
    }

    [Fact]
    public async Task AnOversizedImageIsRefusedNamingTheImage()
    {
        var oversized = new byte[AssessmentReportRenderPolicy.MaximumImageBytes + 1];
        var renderer = new FakeRenderer();
        var invalid = Snapshot(AssessmentReportOutcome.Repairable) with
        {
            Photos =
            [
                new ReportImageEvidence(
                    "box://case/oversized.png",
                    "image/png",
                    oversized,
                    Convert.ToHexStringLower(SHA256.HashData(oversized))),
            ],
        };

        var exception = await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => new GenerateAssessmentReportDraft(renderer)
                .ExecuteAsync(invalid, CaseReportArtifactKind.AssessmentReport));
        Assert.Contains("box://case/oversized.png", exception.Message, StringComparison.Ordinal);
        Assert.Null(renderer.Received);
    }

    [Fact]
    public async Task MoreImagesThanTheBoundAreRefused()
    {
        var renderer = new FakeRenderer();
        var photo = Snapshot(AssessmentReportOutcome.Repairable).Photos.Single();
        var invalid = Snapshot(AssessmentReportOutcome.Repairable) with
        {
            Photos = Enumerable
                .Range(0, AssessmentReportRenderPolicy.MaximumImages + 1)
                .Select(index => photo with { CustodyReference = $"box://case/photo-{index}" })
                .ToArray(),
        };

        await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => new GenerateAssessmentReportDraft(renderer)
                .ExecuteAsync(invalid, CaseReportArtifactKind.AssessmentReport));
        Assert.Null(renderer.Received);
    }

    [Fact]
    public void ImagesArePrintedCloseUpFirstOverviewSecondThenSupportingByOrder()
    {
        var photo = Snapshot(AssessmentReportOutcome.Repairable).Photos.Single();
        var snapshot = Snapshot(AssessmentReportOutcome.Repairable) with
        {
            Photos =
            [
                photo with { CustodyReference = "supporting-2", Role = CaseAssetReportRole.Supporting, Order = 2 },
                photo with { CustodyReference = "overview", Role = CaseAssetReportRole.Overview },
                photo with { CustodyReference = "supporting-1", Role = CaseAssetReportRole.Supporting, Order = 1 },
                photo with { CustodyReference = "close-up", Role = CaseAssetReportRole.CloseUp },
            ],
        };

        Assert.Equal(
            ["close-up", "overview", "supporting-1", "supporting-2"],
            snapshot.OrderedPhotos.Select(item => item.CustodyReference));
    }

    [Fact]
    public async Task ValuationCommentarySelectedWithoutCommentaryFailsBeforeAdapter()
    {
        var renderer = new FakeRenderer();
        var invalid = Snapshot(AssessmentReportOutcome.Repairable) with
        {
            Content = new CaseReportContentSwitches(false, true, false),
            ValuationCommentary = null,
        };

        await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => new GenerateAssessmentReportDraft(renderer)
                .ExecuteAsync(invalid, CaseReportArtifactKind.AssessmentReport));
        Assert.Null(renderer.Received);
    }

    [Fact]
    public void TheGuideSentenceNamesGlassesOnlyWhenDisclosedAndUsed()
    {
        var snapshot = Snapshot(AssessmentReportOutcome.Repairable);

        Assert.False(snapshot.PrintsGuideDisclosure);
        Assert.True((snapshot with
        {
            Content = new CaseReportContentSwitches(true, false, false),
            Guides = new ReportGuideSources([ValuationSource.Glasses]),
        }).PrintsGuideDisclosure);
        Assert.False((snapshot with
        {
            Content = new CaseReportContentSwitches(true, false, false),
            Guides = new ReportGuideSources([ValuationSource.Cazana]),
        }).PrintsGuideDisclosure);
        Assert.False((snapshot with
        {
            Content = CaseReportContentSwitches.None,
            Guides = new ReportGuideSources([ValuationSource.Glasses]),
        }).PrintsGuideDisclosure);
        // The accepted sentence is the only one that names Glass's, and no
        // substitute wording exists for another guide (H5).
        Assert.Contains("Glass's", AssessmentReportContract.StatementOfTruthGuide, StringComparison.Ordinal);
        Assert.DoesNotContain("Glass's", AssessmentReportContract.StatementOfTruth3, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AlteredPhotoFailsCustodyValidationBeforeAdapter()
    {
        var renderer = new FakeRenderer();
        var valid = Snapshot(AssessmentReportOutcome.Repairable);
        var photo = valid.Photos.Single() with { Content = [1, 2, 3] };

        await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => new GenerateAssessmentReportDraft(renderer)
                .ExecuteAsync(valid with { Photos = [photo] }, CaseReportArtifactKind.AssessmentReport));
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
    [InlineData("", true, "image/png")]
    [InlineData("Ed Mawdsley", false, "image/png")]
    [InlineData("Ed Mawdsley", true, "image/gif")]
    public async Task IncompleteSignatoryFailsBeforeAdapter(
        string printedName,
        bool hasSignature,
        string contentType)
    {
        var renderer = new FakeRenderer();
        var invalid = Snapshot(AssessmentReportOutcome.Repairable) with
        {
            Signatory = new ReportSignatory(
                printedName,
                "ATA VDA AQP",
                hasSignature ? [1, 2, 3] : [],
                contentType),
        };

        await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => new GenerateAssessmentReportDraft(renderer)
                .ExecuteAsync(invalid, CaseReportArtifactKind.AssessmentReport));
        Assert.Null(renderer.Received);
    }

    [Fact]
    public async Task SignatoryWithoutQualificationsIsAccepted()
    {
        var renderer = new FakeRenderer();
        var snapshot = Snapshot(AssessmentReportOutcome.Repairable) with
        {
            Signatory = new ReportSignatory("Neil O'Reilly", null, [1, 2, 3], "image/png"),
        };

        await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(snapshot, CaseReportArtifactKind.AssessmentReport);

        Assert.Equal("Neil O'Reilly", renderer.Received!.Signatory.PrintedName);
        Assert.Null(renderer.Received.Signatory.Qualifications);
    }

    [Fact]
    public async Task PreviousPayloadVersionFailsBeforeAdapter()
    {
        var renderer = new FakeRenderer();
        var invalid = Snapshot(AssessmentReportOutcome.Repairable) with
        {
            PayloadVersion = "rendererref1-v1",
        };

        await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => new GenerateAssessmentReportDraft(renderer)
                .ExecuteAsync(invalid, CaseReportArtifactKind.AssessmentReport));
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
            Costs: Costs(20m),
            NewParts: ["Front bumper"], Repairs: ["Bonnet"], Operations: ["Paint front panels"],
            Damage: Damage(), Settlement: Settlement(),
            HistoryCheck: "History clear", EngineerComments: null,
            Signatory: new ReportSignatory("Ed Mawdsley", "ATA VDA AQP", [1, 2, 3], "image/png"),
            AgreedFee: 120m, FeeDescriptionLines: ["Engineering assessment"],
            Photos: [new ReportImageEvidence("box://case/photo-1", "image/png", image, Convert.ToHexStringLower(SHA256.HashData(image)))],
            Sources: [new AcceptedReportSource("assessment", "7", new string('a', 64))],
            Content: CaseReportContentSwitches.None,
            Guides: ReportGuideSources.None);
    }

    /// <summary>
    /// The one cost block every snapshot here uses: 50 parts, five panel hours
    /// at 30, 20 materials and 5 specialist, printed net 225.
    /// </summary>
    internal static ReportRepairCosts Costs(decimal vatPercent) => ReportRepairCosts.For(
        new RepairSpecificationVersion(
            Guid.NewGuid(), Guid.NewGuid(), 2, RepairSpecificationState.Accepted,
            new(RepairSpecificationSourceRoute.Manual, null, null, null),
            [
                Line(1, "repair", "Nearside door", workUnits: 5m, price: null),
                Line(2, "new_part", "Door skin", workUnits: null, price: 50m),
            ],
            null, "engineer-1", RecordedAtUtc, "engineer-1", RecordedAtUtc, null, null,
            new EstimateDetails("Repairer", null, 30m, 20m, 5m, vatPercent, null),
            IsCurrent: true));

    private static CaseEstimateLineRecord Line(
        int position, string type, string description, decimal? workUnits, decimal? price) => new(
            Guid.NewGuid(), position, type, null, description, workUnits, price, false, null, null,
            "confirmed", "case", "Test evidence",
            ActorKind.Staff, "engineer-1", RecordedAtUtc, "engineer-1", RecordedAtUtc, Quantity: 1);

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
        private readonly List<CaseReportArtifactKind> kinds = [];

        public AssessmentReportSnapshot? Received { get; private set; }

        public IReadOnlyList<CaseReportArtifactKind> ReceivedKinds => kinds;

        public string EngineVersion => "fake";

        public Task<RenderedReportArtifact> RenderAsync(
            AssessmentReportSnapshot snapshot,
            CaseReportArtifactKind kind,
            CancellationToken cancellationToken = default)
        {
            Received = snapshot;
            kinds.Add(kind);
            byte[] pdf = [1, 2, 3];
            return Task.FromResult(new RenderedReportArtifact(
                $"{kind}.pdf", pdf, 1,
                Convert.ToHexStringLower(SHA256.HashData(pdf)),
                AssessmentReportContract.TemplateVersion, EngineVersion));
        }
    }
}
