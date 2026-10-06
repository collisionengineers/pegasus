using System.Security.Cryptography;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

public sealed class AssessmentReportRenderingTests
{
    private static readonly DateTimeOffset RecordedAtUtc = new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Version seven carries only what the template prints (operator, 27
    /// September 2026): the tyres, belts, airbags, temporary repairs, the
    /// vehicle's colour, body, transmission and expiry dates, the damage
    /// tables and the settlement rows are recorded on the Case and are no
    /// part of the report. The damage carries the drawing its marks are
    /// printed on (28 September 2026).
    /// </summary>
    [Fact]
    public void TheSnapshotIsVersionSevenAndCarriesOnlyWhatTheTemplatePrints()
    {
        Assert.Equal("rendererref1-v7", AssessmentReportContract.TemplateVersion);
        Assert.Equal("rendererref1-v7", Snapshot(AssessmentReportOutcome.Repairable).PayloadVersion);
        Assert.Equal(
            [
                "Registration", "Make", "Model", "Year", "Condition",
                "MileageDescription", "MileageSource", "Vin", "Engine", "Fuel",
            ],
            Printed(typeof(ReportVehicle)));
        Assert.Equal(["Impacts", "Unrelated", "Profile"], Printed(typeof(ReportDamage)));
        Assert.Equal(["Codes", "Severity", "Disc"], Printed(typeof(ReportImpact)));
        Assert.Equal(["ContractSum"], Printed(typeof(ReportSettlement)));
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
    public void ContractRepairUsesTheAgreedSumWhileKeepingTheComputedRepairTotal()
    {
        var snapshot = Snapshot(AssessmentReportOutcome.ContractRepair);
        var costs = snapshot.Costs;

        Assert.Equal(150m, costs.Printed.PanelLabour);
        Assert.Equal(225m, costs.Printed.Net);
        Assert.Equal(45m, costs.Printed.Vat);
        Assert.Equal(270m, costs.Total);
        Assert.Equal(300m, snapshot.Presentation().RecommendedSettlement);
        Assert.Contains("£300.00", snapshot.Presentation().SettlementText, StringComparison.Ordinal);
        Assert.DoesNotContain("£270.00", snapshot.Presentation().SettlementText, StringComparison.Ordinal);
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
    public void ReportHoursUseTheSamePricedClassifierAsTheMoney()
    {
        var draft = new RepairSpecificationVersion(
            Guid.NewGuid(), Guid.NewGuid(), 1, RepairSpecificationState.Draft,
            new(RepairSpecificationSourceRoute.Json, null, null, null),
            [
                Line(1, "repair", "Repair", workUnits: 2m, price: null),
                Line(2, "specialist_wu", "Calibration", workUnits: 1m, price: null),
                Line(3, "specialist_fixed", "Tyre", workUnits: 4m, price: 180m),
            ],
            "engineer", RecordedAtUtc,
            new("Estimate", 60m, null, 20m,
                Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
            IsCurrent: true);
        var costs = ReportRepairCosts.For(draft);

        Assert.Equal(3m, costs.LabourHours);
        Assert.Equal(0m, costs.PaintHours);
        Assert.Equal(180m, costs.Totals.Raw.PanelLabour);
        Assert.Equal(costs.Totals.Raw.PanelLabour, costs.LabourHours * costs.HourlyRate);
    }

    /// <summary>
    /// The template prints one Labour Hours figure and one Total Labour
    /// figure. Both are computed once: the hours are panel and paint hours
    /// together, as the Case page shows them, and the money is the printed
    /// panel and paint labour, so the rows add up to the printed Sub Total.
    /// </summary>
    [Fact]
    public void TotalLabourAndItsHoursAreComputedOnceAndReconcileToTheSubTotal()
    {
        var estimate = new RepairSpecificationVersion(
            Guid.NewGuid(), Guid.NewGuid(), 1, RepairSpecificationState.Draft,
            new(RepairSpecificationSourceRoute.Manual, null, null, null),
            [
                Line(1, "repair", "Repair wing", workUnits: 3m, price: null),
                Line(2, "paint_repair", "Paint wing", workUnits: 0.5m, price: null)
                    with { PaintWorkUnits = 2.5m, Materials = 60m },
                Line(3, "new_part", "Bonnet", workUnits: null, price: 310m),
            ],
            "engineer-1", RecordedAtUtc,
            new("Repairer", 41.33m, 15m, 20m,
                new EstimateDiscounts(0m, 0m, 0m, 0.025m),
                EstimateVatPolicy.For(RepairerVatStatus.Registered)),
            IsCurrent: true);

        var costs = ReportRepairCosts.For(estimate);

        Assert.Equal(3.5m, costs.LabourHours);
        Assert.Equal(2.5m, costs.PaintHours);
        Assert.Equal(6m, costs.TotalLabourHours);
        Assert.Equal(EstimateHours.Of(estimate).PricedTotal, costs.TotalLabourHours);
        Assert.Equal(costs.Printed.PanelLabour + costs.Printed.PaintLabour, costs.TotalLabour);
        Assert.Equal(costs.Printed.Labour, costs.TotalLabour);
        Assert.Equal(
            costs.Printed.Net,
            costs.TotalLabour + costs.Printed.Parts + costs.Printed.Materials + costs.Printed.Specialist);
        costs.Validate();
    }

    /// <summary>
    /// The template words two VAT rows (DESIGN_SPEC.md, values and repair cost
    /// calculation): "VAT (20%)" for a registered repairer, and
    /// "VAT (20% — parts &amp; paint only)" for one who is not. The
    /// percentage is the estimate's own.
    /// </summary>
    [Fact]
    public void TheVatRowIsWordedAsTheTemplateWordsIt()
    {
        Assert.Equal("VAT (20%)", Costs(20m).VatLabel);
        Assert.Equal("VAT (5%)", Costs(5m).VatLabel);
        Assert.Equal("VAT (17.5%)", Costs(17.5m).VatLabel);
        Assert.Equal(
            "VAT (20% — parts & paint only)",
            ReportRepairCosts.VatLabelOf(EstimateVatPolicy.For(RepairerVatStatus.NotRegistered), 20m));
        // Charging parts and paint only by hand is the same row.
        Assert.Equal(
            "VAT (20% — parts & paint only)",
            ReportRepairCosts.VatLabelOf(
                new EstimateVatPolicy(
                    RepairerVatStatus.Registered,
                    EstimateVatCategories.Parts | EstimateVatCategories.Materials,
                    CategoriesOverridden: true),
                20m));
    }

    /// <summary>
    /// An unknown repairer VAT status, and a hand-picked set of costs the
    /// template has no words for, has no VAT row: the report is refused
    /// rather than printed with a row nobody accepted.
    /// </summary>
    [Fact]
    public void AVatTreatmentTheTemplateCannotWordHasNoRowAndFailsClosed()
    {
        Assert.Null(ReportRepairCosts.VatLabelOf(EstimateVatPolicy.For(RepairerVatStatus.Unknown), 20m));
        Assert.Null(ReportRepairCosts.VatLabelOf(
            new EstimateVatPolicy(RepairerVatStatus.Unknown, EstimateVatCategories.All, CategoriesOverridden: true),
            20m));
        Assert.Null(ReportRepairCosts.VatLabelOf(
            new EstimateVatPolicy(RepairerVatStatus.Registered, EstimateVatCategories.Labour, CategoriesOverridden: true),
            20m));

        var registered = Costs(20m);
        var unknown = registered with
        {
            Totals = registered.Totals with { VatPolicy = EstimateVatPolicy.For(RepairerVatStatus.Unknown) },
        };

        Assert.Null(unknown.VatLabel);
        var refusal = Assert.Throws<ReportRenderRejectedException>(unknown.Validate);
        Assert.Contains("the Repair Spec section", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FeeVatUsesTheSharedReportContractRate()
    {
        var snapshot = Snapshot(AssessmentReportOutcome.Repairable) with { AgreedFee = 123.45m };

        Assert.Equal(
            decimal.Round(
                snapshot.FeeNet * AssessmentReportContract.FeeVatRate,
                2,
                MidpointRounding.AwayFromZero),
            snapshot.FeeVat);
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

    /// <summary>
    /// The report prints the recorded category's accepted wording (operator,
    /// 26 September 2026): the badge names the category and the salvage block
    /// carries that category's sentence.
    /// </summary>
    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("S")]
    [InlineData("N")]
    public void ATotalLossPrintsItsRecordedCategory(string category)
    {
        var snapshot = Snapshot(AssessmentReportOutcome.TotalLoss) with { SalvageCategory = category };

        snapshot.Validate();

        Assert.Equal($"TOTAL LOSS — CATEGORY {category}", snapshot.Presentation().Badge);
        var salvage = Assert.Single(snapshot.PrintedWording, block => block.Key == ReportWordingComposition.Salvage);
        Assert.Contains($"Category {category}", salvage.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A total loss recorded as Category N/A prints the badge TOTAL LOSS with
    /// no category and no salvage paragraph (operator, 26 September 2026).
    /// </summary>
    [Fact]
    public void ATotalLossWithNoCategoryPrintsTheBadgeAloneAndNoSalvageParagraph()
    {
        var snapshot = Snapshot(AssessmentReportOutcome.TotalLoss) with
        {
            SalvageCategory = AssessmentReportContract.NoSalvageCategory,
        };

        snapshot.Validate();

        Assert.Equal("TOTAL LOSS", snapshot.Presentation().Badge);
        Assert.DoesNotContain(snapshot.PrintedWording, block => block.Key == ReportWordingComposition.Salvage);
    }

    [Fact]
    public void ATotalLossWithoutARecordedCategoryIsRefused()
    {
        var snapshot = Snapshot(AssessmentReportOutcome.TotalLoss) with { SalvageCategory = null };

        Assert.Throws<ReportRenderRejectedException>(snapshot.Validate);
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
        var disclosedGlasses = snapshot with
        {
            Content = new CaseReportContentSwitches(true, false, false),
            Guides = new ReportGuideSources([ValuationSource.Glasses]),
        };

        Assert.False(snapshot.PrintsGuideDisclosure);
        Assert.True(disclosedGlasses.PrintsGuideDisclosure);
        Assert.Contains(AssessmentReportContract.StatementOfTruthGuide, disclosedGlasses.StatementOfTruth);
        Assert.DoesNotContain(AssessmentReportContract.StatementOfTruthGuide, snapshot.StatementOfTruth);
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

    /// <summary>
    /// Verification moved to open time (issue 850): the snapshot no longer
    /// holds image bytes, so an image whose bytes are not the ones custody
    /// pinned is refused when the renderer opens it, naming the file in staff's words.
    /// </summary>
    [Fact]
    public async Task AnAlteredPhotoIsRefusedWhenItIsOpened()
    {
        var valid = Snapshot(AssessmentReportOutcome.Repairable);
        var photo = valid.Photos.Single() with
        {
            CustodyReference = "page-1-image-2.jpg",
            Content = ReportImageContent.Opened(_ => Task.FromResult(new byte[] { 1, 2, 3 })),
        };

        // The snapshot is accepted; nothing has been read yet.
        (valid with { Photos = [photo] }).Validate();
        var refusal = await Assert.ThrowsAsync<ReportRenderRejectedException>(() => photo.OpenAsync());

        // Staff read this refusal, so it names the file in their words.
        Assert.Equal(
            "The stored version of page-1-image-2.jpg has changed. "
            + "Open the Files section to see the image as it is stored now.",
            refusal.Message);
    }

    [Fact]
    public async Task AnImageIsReadOnlyWhenItIsOpenedAndEachOpenReadsItAgain()
    {
        var bytes = new byte[] { 137, 80, 78, 71, 1, 2, 3, 4 };
        var reads = 0;
        var photo = Snapshot(AssessmentReportOutcome.Repairable).Photos.Single() with
        {
            Content = ReportImageContent.Opened(_ =>
            {
                reads++;
                return Task.FromResult(bytes);
            }),
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
        };

        photo.Validate();
        Assert.Equal(0, reads);
        Assert.Same(bytes, await photo.OpenAsync());
        Assert.Same(bytes, await photo.OpenAsync());
        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task AnImageThatOpensEmptyIsRefused()
    {
        var photo = Snapshot(AssessmentReportOutcome.Repairable).Photos.Single() with
        {
            Content = ReportImageContent.Opened(_ => Task.FromResult(Array.Empty<byte>())),
        };

        await Assert.ThrowsAsync<ReportRenderRejectedException>(() => photo.OpenAsync());
    }

    /// <summary>
    /// Intake records a file's hash in capitals and a staff upload records it
    /// in small letters. Both name the same bytes, so the report prints both.
    /// </summary>
    [Fact]
    public async Task APhotoWhoseHashIsRecordedInCapitalsIsPrinted()
    {
        var renderer = new FakeRenderer();
        var valid = Snapshot(AssessmentReportOutcome.Repairable);
        var photo = valid.Photos.Single();
        var recordedByIntake = photo with { Sha256 = photo.Sha256.ToUpperInvariant() };

        await new GenerateAssessmentReportDraft(renderer)
            .ExecuteAsync(valid with { Photos = [recordedByIntake] }, CaseReportArtifactKind.AssessmentReport);

        Assert.Equal(recordedByIntake, Assert.Single(renderer.Received!.Photos));
        Assert.Equal(
            await photo.OpenAsync(CancellationToken.None),
            await recordedByIntake.OpenAsync(CancellationToken.None));
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

    /// <summary>
    /// A report frozen on the earlier layout is refused before it reaches the
    /// renderer, in words staff can act on: no version, no developer's term.
    /// </summary>
    [Fact]
    public async Task PreviousPayloadVersionFailsBeforeAdapter()
    {
        var renderer = new FakeRenderer();
        var invalid = Snapshot(AssessmentReportOutcome.Repairable) with
        {
            PayloadVersion = "rendererref1-v6",
        };

        var refusal = await Assert.ThrowsAsync<ReportRenderRejectedException>(
            () => new GenerateAssessmentReportDraft(renderer)
                .ExecuteAsync(invalid, CaseReportArtifactKind.AssessmentReport));

        Assert.Equal(
            "This report was generated before the report's layout changed, so it cannot be printed again. "
            + "Save a change to the Case, then generate the report again.",
            refusal.Message);
        Assert.DoesNotContain("rendererref1", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("payload", refusal.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(renderer.Received);
    }

    /// <summary>
    /// Core says whether the vehicle is unroadworthy and whether it was
    /// assessed from images, so the layout compares no strings.
    /// </summary>
    [Fact]
    public void TheSnapshotSaysWhetherTheVehicleIsUnroadworthyAndHowItWasAssessed()
    {
        var roadworthy = Snapshot(AssessmentReportOutcome.Repairable);
        var unroadworthy = roadworthy with
        {
            LegalStatus = "unroadworthy",
            UnroadworthyReason = "the illegal offside front tyre",
            AssessmentMethod = "physical",
            LocationAddress = "1 Test Street, London",
        };

        Assert.False(roadworthy.IsUnroadworthy);
        Assert.True(roadworthy.IsImageBased);
        Assert.True(unroadworthy.IsUnroadworthy);
        Assert.False(unroadworthy.IsImageBased);
        unroadworthy.Validate();
    }

    internal static AssessmentReportSnapshot Snapshot(AssessmentReportOutcome outcome)
    {
        var image = File.ReadAllBytes(Path.Combine(RepositoryRoot(), "docs", "design", "brand", "logos", "logo_no_margin.png"));
        return new(
            OurReference: "CE-100", YourReference: "P-100", ReportDate: new DateOnly(2026, 8, 19),
            ClaimantName: "Alex Example", IncidentDate: new DateOnly(2026, 8, 1),
            InstructionsReceived: new DateOnly(2026, 8, 2), Assessed: new DateOnly(2026, 8, 3),
            ReportFor: ["Approved Principal", "1 Example Street"],
            Vehicle: new ReportVehicle("PK12 TMZ", "Ford", "Focus", "2012", "good", "80,000 miles", "online_data", "VIN", "1600", "Petrol"),
            Outcome: outcome, LegalStatus: "roadworthy", UnroadworthyReason: null,
            ImpactSeverity: "moderate", ImpactLocation: "right_rear", AssessmentMethod: "image_based", LocationAddress: null,
            EngineerValue: 5_000m, RetailValue: 5_000m, TradeValue: 4_000m,
            SalvageCategory: outcome == AssessmentReportOutcome.TotalLoss ? "S" : null,
            SalvageValue: outcome == AssessmentReportOutcome.TotalLoss ? 500m : null,
            Costs: Costs(20m),
            NewParts: ["Front bumper"], Repairs: ["Bonnet"], Operations: ["Paint front panels"],
            Damage: Damage(), Settlement: outcome == AssessmentReportOutcome.ContractRepair
                ? Settlement() with { ContractSum = 300m } : Settlement(),
            HistoryCheck: "History clear", EngineerComments: null,
            Signatory: new ReportSignatory("Ed Mawdsley", "ATA VDA AQP", [1, 2, 3], "image/png"),
            AgreedFee: 120m, FeeDescriptionLines: ["Engineering assessment"],
            Photos: [new ReportImageEvidence("box://case/photo-1", "image/png", ReportImageContent.Opened(_ => Task.FromResult(image)), Convert.ToHexStringLower(SHA256.HashData(image)))],
            Sources: [new AcceptedReportSource("assessment", "7", new string('a', 64))],
            Content: CaseReportContentSwitches.None,
            Guides: ReportGuideSources.None);
    }

    /// <summary>
    /// The one cost block every snapshot here uses: 50 parts, five panel hours
    /// at 30, 20 materials and 5 specialist, printed net 225.
    /// </summary>
    internal static ReportRepairCosts Costs(decimal vatPercent)
    {
        var draft = new RepairSpecificationVersion(
            Guid.NewGuid(), Guid.NewGuid(), 2, RepairSpecificationState.Draft,
            new(RepairSpecificationSourceRoute.Manual, null, null, null),
            [
                Line(1, "repair", "Nearside door", workUnits: 5m, price: null),
                Line(2, "new_part", "Door skin", workUnits: null, price: 50m) with { Materials = 20m },
            ],
            "engineer-1", RecordedAtUtc,
            new EstimateDetails("Repairer", 30m, 5m, vatPercent, Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
            IsCurrent: true);
        return ReportRepairCosts.For(draft);
    }

    private static CaseEstimateLineRecord Line(
        int position, string type, string description, decimal? workUnits, decimal? price) => new(
            Guid.NewGuid(), position, type, null, description, workUnits, price, false, null, null,
            "case", "Test evidence",
            ActorKind.Staff, "engineer-1", RecordedAtUtc, Quantity: 1);

    internal static ReportDamage Damage() => new([new(["right_rear"], "moderate")], "Door scratch", DamagePlanGeometry.Car);

    internal static ReportSettlement Settlement() => new();

    /// <summary>The members of a snapshot record, in the order it declares them.</summary>
    private static IEnumerable<string> Printed(Type record) =>
        record.GetProperties().Select(property => property.Name);

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
