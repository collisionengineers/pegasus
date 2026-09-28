using Pegasus.Core.Assessment;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// The report's narrative blocks (v28 P30): what a Case nobody has edited
/// says, how an Engineer's heading, wording, order and removal are laid over
/// it, and the one shape a stored change may take.
/// </summary>
public sealed class ReportWordingTests
{
    [Fact]
    public void ACaseNobodyHasEditedComposesTheStandardBlocksInPrintOrder()
    {
        var snapshot = Repairable();

        var blocks = ReportWordingComposition.Compose(snapshot, []);

        Assert.Equal(
            [
                ReportWordingComposition.NatureOfIncident,
                ReportWordingComposition.EngineersComments,
                ReportWordingComposition.VehicleHistoryCheck,
                ReportWordingComposition.PreIncidentCondition,
                ReportWordingComposition.Settlement
            ],
            blocks.Select(block => block.Key));
        Assert.All(blocks, block => Assert.False(block.HandEdited));
        Assert.All(blocks, block => Assert.False(block.Manual));
    }

    /// <summary>
    /// The composed sentences are the template's, word for word
    /// (reference/rendererref1, Sample - Total Loss Report.pdf, page 2): the
    /// severity, the location and the condition print in small letters.
    /// </summary>
    [Fact]
    public void TheComposedSentencesAreTheReportsAcceptedWording()
    {
        var snapshot = Repairable();
        var blocks = ReportWordingComposition.Compose(snapshot, [])
            .ToDictionary(block => block.Key, StringComparer.Ordinal);

        Assert.Equal(
            "The vehicle has suffered moderate collision/impact damage to the right rear.",
            blocks[ReportWordingComposition.NatureOfIncident].Text);
        Assert.Equal(
            "The mileage has been calculated from online data.",
            blocks[ReportWordingComposition.EngineersComments].Text);
        Assert.Equal(
            "The vehicle is considered to be in good condition for its age and type.",
            blocks[ReportWordingComposition.PreIncidentCondition].Text);
        Assert.Equal("History clear", blocks[ReportWordingComposition.VehicleHistoryCheck].Text);
        Assert.Equal(
            snapshot.Presentation().SettlementText,
            blocks[ReportWordingComposition.Settlement].Text);
        Assert.Equal(
            snapshot.Presentation().SettlementHeading,
            blocks[ReportWordingComposition.Settlement].Title);
    }

    /// <summary>
    /// The unroadworthy note and the Engineer's own comments join the mileage
    /// sentence as paragraphs of the one block, in the order they print.
    /// </summary>
    [Fact]
    public void TheCommentsBlockCarriesTheMileageTheUnroadworthyNoteAndTheEngineersWords()
    {
        var snapshot = Repairable() with
        {
            LegalStatus = "unroadworthy",
            UnroadworthyReason = "the illegal offside front tyre",
            EngineerComments = "The repairer has the vehicle.",
        };

        var comments = ReportWordingComposition
            .Compose(snapshot, [])
            .Single(block => block.Key == ReportWordingComposition.EngineersComments);

        Assert.Equal(
            "The mileage has been calculated from online data."
            + "\n\nPlease note the vehicle is unroadworthy due to the illegal offside front tyre."
            + "\n\nThe repairer has the vehicle.",
            comments.Text);
    }

    [Fact]
    public void WordingTheEngineerWroteReplacesTheComposedSentenceAndSaysSo()
    {
        var snapshot = Repairable();

        var block = ReportWordingComposition
            .Compose(snapshot, [new(ReportWordingComposition.NatureOfIncident, null, "The vehicle was struck from behind.", null)])
            .Single(item => item.Key == ReportWordingComposition.NatureOfIncident);

        Assert.Equal("The vehicle was struck from behind.", block.Text);
        Assert.True(block.HandEdited);
    }

    /// <summary>
    /// Wording that reads the same as the composed sentence is not a hand
    /// edit: the block still tracks its fields.
    /// </summary>
    [Fact]
    public void WordingThatMatchesTheComposedSentenceIsNotAHandEdit()
    {
        var snapshot = Repairable();
        var composed = ReportWordingComposition.ComposedText(
            ReportWordingComposition.PreIncidentCondition, snapshot, snapshot.Presentation());

        var block = ReportWordingComposition
            .Compose(snapshot, [new(ReportWordingComposition.PreIncidentCondition, null, composed, null)])
            .Single(item => item.Key == ReportWordingComposition.PreIncidentCondition);

        Assert.False(block.HandEdited);
    }

    [Fact]
    public void ARenamedBlockKeepsItsOwnHeading()
    {
        var snapshot = Repairable();

        var block = ReportWordingComposition
            .Compose(snapshot, [new(ReportWordingComposition.VehicleHistoryCheck, "  Provenance  ", null, null)])
            .Single(item => item.Key == ReportWordingComposition.VehicleHistoryCheck);

        Assert.Equal("  Provenance  ".Trim(), block.Title);
    }

    /// <summary>
    /// A block taken off the report does not print, and the section still
    /// offers it, so the control that took it off puts it back.
    /// </summary>
    [Fact]
    public void ABlockTakenOffTheReportDoesNotPrintAndIsStillOffered()
    {
        var snapshot = Repairable();
        IReadOnlyList<CaseReportWording> saved =
            [new(ReportWordingComposition.VehicleHistoryCheck, null, null, null, Included: false)];

        var printed = ReportWordingComposition.Compose(snapshot, saved);
        var offered = ReportWordingComposition.Offered(snapshot, saved);

        Assert.DoesNotContain(printed, block => block.Key == ReportWordingComposition.VehicleHistoryCheck);
        Assert.False(offered.Single(block => block.Key == ReportWordingComposition.VehicleHistoryCheck).Included);
    }

    [Fact]
    public void ABlankComposedBlockWithAPersistedChangeRemainsOfferedButDoesNotPrint()
    {
        var snapshot = Repairable() with
        {
            Content = Repairable().Content with { IncludeValuationCommentary = false },
            ValuationCommentary = null,
        };
        var changes = new[]
        {
            new CaseReportWording(ReportWordingComposition.ValuationCommentary, "Valuation note", null, null),
            new CaseReportWording(ReportWordingComposition.ValuationCommentary, null, null, 0),
            new CaseReportWording(ReportWordingComposition.ValuationCommentary, null, null, null, Included: false),
        };

        foreach (var change in changes)
        {
            var offered = ReportWordingComposition.Offered(snapshot, [change]);

            Assert.Contains(offered, block => block.Key == ReportWordingComposition.ValuationCommentary);
            Assert.DoesNotContain(
                ReportWordingComposition.Compose(snapshot, [change]),
                block => block.Key == ReportWordingComposition.ValuationCommentary);
        }

        Assert.DoesNotContain(
            ReportWordingComposition.Offered(
                snapshot,
                [new CaseReportWording(ReportWordingComposition.ValuationCommentary, null, null, null)]),
            block => block.Key == ReportWordingComposition.ValuationCommentary);
    }

    [Fact]
    public void TheOrderTheEngineerGaveIsThePrintOrder()
    {
        var snapshot = Repairable();

        // The panel writes every row's place when one moves, so the order it
        // holds is the whole list's.
        var blocks = ReportWordingComposition.Compose(
            snapshot,
            [
                new(ReportWordingComposition.Settlement, null, null, 0),
                new(ReportWordingComposition.NatureOfIncident, null, null, 1)
            ]);

        Assert.Equal(ReportWordingComposition.Settlement, blocks[0].Key);
        Assert.Equal(ReportWordingComposition.NatureOfIncident, blocks[1].Key);
    }

    [Fact]
    public void AParagraphTheEngineerWrotePrintsWithItsOwnHeading()
    {
        var snapshot = Repairable();

        var blocks = ReportWordingComposition.Compose(
            snapshot,
            [new("manual:1", "Access", "The vehicle was inspected at the repairer.", 99, Manual: true)]);

        var manual = blocks[^1];
        Assert.Equal("Access", manual.Title);
        Assert.True(manual.Manual);
        Assert.True(manual.HandEdited);
    }

    [Fact]
    public void AParagraphWithNothingInItIsNotOffered()
    {
        var snapshot = Repairable();

        var blocks = ReportWordingComposition.Offered(
            snapshot,
            [new("manual:1", "Access", "   ", 99, Manual: true)]);

        Assert.DoesNotContain(blocks, block => block.Manual);
    }

    /// <summary>
    /// The salvage block is the categorisation matrix's own wording for the
    /// recorded category, and a Case with no category has nothing to say.
    /// </summary>
    [Theory]
    [InlineData("S", "Category S (structural damage) and can be sold as repairable salvage")]
    [InlineData("N", "Category N (non-structural damage) and can be sold as repairable salvage")]
    [InlineData("A", "Category A (scrap only — the vehicle must be crushed in its entirety with no parts recovery)")]
    [InlineData("B", "Category B (break for spare parts — the body shell must be crushed)")]
    public void TheSalvageBlockFollowsTheRecordedCategory(string category, string expected)
    {
        var snapshot = TotalLoss() with { SalvageCategory = category };

        var salvage = ReportWordingComposition
            .Compose(snapshot, [])
            .Single(block => block.Key == ReportWordingComposition.Salvage);

        Assert.Contains(expected, salvage.Text, StringComparison.Ordinal);
        Assert.Contains("£500.00", salvage.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Each salvage paragraph whole. Category S is the template's (Sample -
    /// Total Loss Report.pdf, page 2). Categories A, B and N are the manager's
    /// case page's (pegasus_case_dashboard_2026-09-15.html, SALV_T, lines
    /// 1710 to 1713), A and B with its dash.
    /// </summary>
    [Theory]
    [InlineData("A", "Under the current salvage categorisation matrix, within the scope of our inspection, we consider that this is Category A (scrap only — the vehicle must be crushed in its entirety with no parts recovery). We suggest that the sale of the salvage will realise in the order of £500.00. We have not taken any action towards removal of the salvage at this time.")]
    [InlineData("B", "Under the current salvage categorisation matrix, within the scope of our inspection, we consider that this is Category B (break for spare parts — the body shell must be crushed). We suggest that the sale of the salvage will realise in the order of £500.00. We have not taken any action towards removal of the salvage at this time.")]
    [InlineData("S", "Under the current salvage categorisation matrix, within the scope of our inspection, we consider that this is Category S (structural damage) and can be sold as repairable salvage. Further information is available at www.abi.org.uk. We suggest that the sale of the salvage will realise in the order of £500.00. We have not taken any action towards removal of the salvage at this time.")]
    [InlineData("N", "Under the current salvage categorisation matrix, within the scope of our inspection, we consider that this is Category N (non-structural damage) and can be sold as repairable salvage. Further information is available at www.abi.org.uk. We suggest that the sale of the salvage will realise in the order of £500.00. We have not taken any action towards removal of the salvage at this time.")]
    public void EachSalvageParagraphIsItsSourcesWordForWord(string category, string expected)
    {
        var snapshot = TotalLoss() with { SalvageCategory = category };

        var salvage = ReportWordingComposition
            .Compose(snapshot, [])
            .Single(block => block.Key == ReportWordingComposition.Salvage);

        Assert.Equal(expected, salvage.Text);
    }

    /// <summary>
    /// The mileage sentences. Online data is the template's (Sample - Total
    /// Loss Report.pdf, page 2); the other five are the manager's case page's
    /// (pegasus_case_dashboard_2026-09-15.html, MILE, lines 1148 to 1153).
    /// </summary>
    [Theory]
    [InlineData("online_data", "The mileage has been calculated from online data.")]
    [InlineData("owner", "The mileage was advised by the owner.")]
    [InlineData("repairer", "The mileage was advised by the repairer/storage yard.")]
    [InlineData("principal", "The mileage was advised by our instructing principal.")]
    [InlineData("average", "The mileage has been based on average mileage for the vehicle's age and type.")]
    [InlineData("tbc", "The mileage is to be confirmed.")]
    public void EachMileageSentenceIsItsSourcesWordForWord(string source, string expected)
    {
        Assert.Equal(expected, ReportWordingComposition.MileageSentence(source));

        var snapshot = Repairable();
        var comments = ReportWordingComposition
            .Compose(snapshot with { Vehicle = snapshot.Vehicle with { MileageSource = source } }, [])
            .Single(block => block.Key == ReportWordingComposition.EngineersComments);
        Assert.Equal(expected, comments.Text);
    }

    /// <summary>
    /// The template's Nature of Incident and Pre-Incident Condition sentences
    /// (DESIGN_SPEC.md lines 98 to 100 and 112 to 113) carry the recorded
    /// words in small letters, whichever severity, location or condition is
    /// recorded.
    /// </summary>
    [Theory]
    [InlineData("light_to_moderate", "left_front", "The vehicle has suffered light to moderate collision/impact damage to the left front.")]
    [InlineData("moderate_to_heavy", "underside", "The vehicle has suffered moderate to heavy collision/impact damage to the underside.")]
    [InlineData("heavy", "rear", "The vehicle has suffered heavy collision/impact damage to the rear.")]
    public void DamageToOneAreaIsTheTemplatesSentenceInSmallLetters(
        string severity, string location, string expected)
    {
        Assert.Equal(expected, ReportWordingComposition.NatureOfIncidentSentence(severity, location, [location]));

        var snapshot = Repairable() with
        {
            ImpactSeverity = severity,
            ImpactLocation = location,
            Damage = new([new([location], severity)], null, DamagePlanGeometry.Car),
        };
        Assert.Equal(expected, snapshot.PrintedWording[0].Text);
    }

    [Theory]
    [InlineData("poor", "The vehicle is considered to be in poor condition for its age and type.")]
    [InlineData("below_average", "The vehicle is considered to be in below average condition for its age and type.")]
    [InlineData("average", "The vehicle is considered to be in average condition for its age and type.")]
    [InlineData("excellent", "The vehicle is considered to be in excellent condition for its age and type.")]
    public void TheConditionSentenceCarriesTheConditionInSmallLetters(string condition, string expected)
    {
        var snapshot = Repairable();

        var block = ReportWordingComposition
            .Compose(snapshot with { Vehicle = snapshot.Vehicle with { Condition = condition } }, [])
            .Single(item => item.Key == ReportWordingComposition.PreIncidentCondition);

        Assert.Equal(expected, block.Text);
    }

    /// <summary>
    /// Damage to several areas is the manager's case page's sentence
    /// (pegasus_case_dashboard_2026-09-15.html, lines 1161 to 1163): "...to
    /// the following areas:" and then one line to an area, each behind its
    /// dash and named as that page names it (lines 786 to 790), in the order
    /// the damages were recorded and each area once.
    /// </summary>
    [Fact]
    public void DamageToSeveralAreasListsThemOneToALine()
    {
        var snapshot = Repairable() with
        {
            ImpactSeverity = "moderate_to_heavy",
            ImpactLocation = ReportWordingComposition.SeveralAreas,
            Damage = new(
                [
                    new(["right_side", "right_rear"], "moderate"),
                    new(["rear", "right_rear"], "light"),
                    new(["underside"], "heavy"),
                ],
                null,
                DamagePlanGeometry.Car),
        };

        var nature = snapshot.PrintedWording
            .Single(block => block.Key == ReportWordingComposition.NatureOfIncident);

        Assert.Equal(
            "The vehicle has suffered moderate to heavy collision/impact damage to the following areas:"
            + "\n— Right side"
            + "\n— Right rear"
            + "\n— Rear"
            + "\n— Underside",
            nature.Text);
        Assert.False(nature.HandEdited);
        Assert.DoesNotContain("multiple", nature.Text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The area names are the manager's case page's
    /// (pegasus_case_dashboard_2026-09-15.html, lines 786 to 790).
    /// </summary>
    [Theory]
    [InlineData("front", "Front")]
    [InlineData("left_front", "Left front")]
    [InlineData("right_front", "Right front")]
    [InlineData("left_side", "Left side")]
    [InlineData("right_side", "Right side")]
    [InlineData("rear", "Rear")]
    [InlineData("left_rear", "Left rear")]
    [InlineData("right_rear", "Right rear")]
    [InlineData("underside", "Underside")]
    [InlineData("interior", "Interior")]
    [InlineData("mechanical", "Mechanical")]
    public void AListedAreaIsNamedAsTheManagersCasePageNamesIt(string area, string expected)
    {
        Assert.Equal(expected, ReportWordingComposition.LeadingWords(area));
        Assert.Equal(
            "The vehicle has suffered light collision/impact damage to the following areas:\n— " + expected,
            ReportWordingComposition.NatureOfIncidentSentence(
                "light", ReportWordingComposition.SeveralAreas, [area]));
    }

    /// <summary>
    /// The headings. Six are the template's (Sample - Total Loss Report.pdf,
    /// page 2); "Supplementary damage", "PAV commentary" and "Unrelated
    /// damage" are the manager's case page's
    /// (pegasus_case_dashboard_2026-09-15.html, WB, lines 1723 to 1725), and
    /// so is "New paragraph" (line 1879).
    /// </summary>
    [Fact]
    public void TheHeadingsAreTheirSourcesWordForWord()
    {
        Assert.Equal(
            [
                (ReportWordingComposition.NatureOfIncident, "Nature of Incident"),
                (ReportWordingComposition.EngineersComments, "Engineer's Comments"),
                (ReportWordingComposition.SupplementaryDamage, "Supplementary damage"),
                (ReportWordingComposition.ValuationCommentary, "PAV commentary"),
                (ReportWordingComposition.UnrelatedDamage, "Unrelated damage"),
                (ReportWordingComposition.VehicleHistoryCheck, "Vehicle History Check"),
                (ReportWordingComposition.PreIncidentCondition, "Pre-Incident Condition"),
                (ReportWordingComposition.Settlement, "Settlement"),
                (ReportWordingComposition.Salvage, "Salvage"),
            ],
            ReportWordingComposition.Standard);
        Assert.Equal("New paragraph", ReportWordingComposition.ManualTitle);

        var manual = ReportWordingComposition
            .Compose(Repairable(), [new("manual:1", null, "Inspected at the repairer.", 99, Manual: true)])[^1];
        Assert.Equal("New paragraph", manual.Title);
    }

    /// <summary>
    /// The settlement sentences and the value box labels are the template's
    /// four sample reports', page 2, word for word. The cash in lieu and
    /// contract repair labels are the full ones.
    /// </summary>
    [Fact]
    public void TheSettlementWordingIsTheTemplatesForEachOutcome()
    {
        var totalLoss = TotalLoss().Presentation();
        Assert.Equal("Settlement", totalLoss.SettlementHeading);
        Assert.Equal("Recommended equitable settlement (pre-accident value less salvage)", totalLoss.SettlementLabel);
        Assert.Equal(
            "We consider that an equitable settlement would be £4,500.00, which represents the pre-accident engineer value of the vehicle of £5,000.00 less the value of the salvage of £500.00.",
            totalLoss.SettlementText);

        var repairable = Repairable().Presentation();
        Assert.Equal("Settlement", repairable.SettlementHeading);
        Assert.Equal("Recommended settlement (calculated repair cost)", repairable.SettlementLabel);
        Assert.Equal(
            "This vehicle is considered a repairable proposition and we have calculated a repair cost of £270.00.",
            repairable.SettlementText);

        var cashInLieu = AssessmentReportRenderingTests.Snapshot(AssessmentReportOutcome.CashInLieu).Presentation();
        Assert.Equal("Settlement", cashInLieu.SettlementHeading);
        Assert.Equal("Recommended cash in lieu settlement (estimated repair cost)", cashInLieu.SettlementLabel);
        Assert.Equal(
            "We recommend settlement by way of a cash in lieu payment based upon the estimated repair cost of £270.00.",
            cashInLieu.SettlementText);

        var contract = AssessmentReportRenderingTests.Snapshot(AssessmentReportOutcome.ContractRepair).Presentation();
        Assert.Equal("Contract Repair", contract.SettlementHeading);
        Assert.Equal("Agreed contract repair (including VAT)", contract.SettlementLabel);
        Assert.Equal(
            "A contract repair has been agreed for the sum of £300.00 including VAT. Costs cannot increase above this figure.",
            contract.SettlementText);
    }

    /// <summary>
    /// The Repair Spec section shows the contract repair sentence the report
    /// prints: the template's (Sample - Contract Repair Report.pdf, page 2),
    /// from the one owner.
    /// </summary>
    [Fact]
    public void TheContractRepairSentenceIsTheTemplatesAndHasOneOwner()
    {
        Assert.Equal(
            "A contract repair has been agreed for the sum of £1,250.00 including VAT. Costs cannot increase above this figure.",
            ReportWordingComposition.ContractRepairSentence(1_250m));
        Assert.Equal(
            ReportWordingComposition.ContractRepairSentence(300m),
            AssessmentReportRenderingTests.Snapshot(AssessmentReportOutcome.ContractRepair)
                .Presentation().SettlementText);
    }

    /// <summary>
    /// The unrelated damage paragraph is the manager's case page's
    /// (pegasus_case_dashboard_2026-09-15.html, line 1725).
    /// </summary>
    [Fact]
    public void TheUnrelatedDamageParagraphIsTheManagersCasePages()
    {
        var snapshot = Repairable() with
        {
            Content = Repairable().Content with { IncludeUnrelatedDamage = true },
        };

        var block = snapshot.PrintedWording
            .Single(item => item.Key == ReportWordingComposition.UnrelatedDamage);

        Assert.Equal("Unrelated damage", block.Title);
        Assert.Equal(
            "Unrelated pre-existing damage was noted: Door scratch. This damage is inconsistent with the reported incident and has been disregarded for the purposes of this assessment.",
            block.Text);
    }

    /// <summary>
    /// A browser posts a line break as a carriage return and a line feed. The
    /// stored wording holds the line feed alone, as a composed sentence does,
    /// so the listed areas an Engineer never changed still track the damage.
    /// </summary>
    [Fact]
    public void StoredWordingHoldsLineBreaksAsComposedSentencesWriteThem()
    {
        var validated = ReportWordingComposition.Validate(
            new(ReportWordingComposition.NatureOfIncident, null, "First line.\r\nSecond line.\r\n\r\nNext paragraph.", null));

        Assert.Equal("First line.\nSecond line.\n\nNext paragraph.", validated.Text);
    }

    [Fact]
    public void ATotalLossWithNoRecordedCategoryHasNoSalvageBlock()
    {
        var snapshot = TotalLoss() with { SalvageCategory = "N/A" };

        Assert.DoesNotContain(
            ReportWordingComposition.Compose(snapshot, []),
            block => block.Key == ReportWordingComposition.Salvage);
    }

    [Fact]
    public void ARepairableCaseHasNoSalvageBlock()
    {
        Assert.DoesNotContain(
            ReportWordingComposition.Compose(Repairable(), []),
            block => block.Key == ReportWordingComposition.Salvage);
    }

    /// <summary>
    /// A generation frozen before the blocks existed holds none, and prints
    /// the blocks its own frozen facts compose.
    /// </summary>
    [Fact]
    public void ASnapshotHoldingNoBlocksPrintsTheOnesItsFactsCompose()
    {
        var snapshot = Repairable();

        Assert.Null(snapshot.Wording);
        Assert.Equal(
            ReportWordingComposition.Compose(snapshot, []).Select(block => block.Key),
            snapshot.PrintedWording.Select(block => block.Key));
    }

    /// <summary>
    /// A frozen snapshot prints the Engineer's own words, and composes the
    /// blocks they never touched from the facts frozen beside them — so the
    /// narrative can never disagree with the figures on the same page.
    /// </summary>
    [Fact]
    public void AFrozenSnapshotPrintsTheChangesItHoldsOverItsOwnFacts()
    {
        var snapshot = Repairable() with
        {
            Wording =
            [
                new("manual:1", "Access", "Inspected at the repairer.", 99, Manual: true),
                new(ReportWordingComposition.VehicleHistoryCheck, null, null, null, Included: false),
            ],
        };

        var printed = snapshot.PrintedWording;
        Assert.Equal("Access", printed[^1].Title);
        Assert.True(printed[^1].Manual);
        Assert.DoesNotContain(printed, block => block.Key == ReportWordingComposition.VehicleHistoryCheck);
        Assert.Equal(
            "The vehicle has suffered moderate collision/impact damage to the right rear.",
            printed[0].Text);
    }

    /// <summary>
    /// A change to a frozen fact recomposes the block that reads it, so a
    /// snapshot never prints a sentence its own facts contradict.
    /// </summary>
    [Fact]
    public void AComposedBlockFollowsTheSnapshotsOwnFacts()
    {
        var snapshot = Repairable() with
        {
            Content = Repairable().Content with { IncludeValuationCommentary = true },
            ValuationCommentary = "Low mileage for its age.",
        };

        Assert.Equal(
            "Low mileage for its age.",
            snapshot.PrintedWording
                .Single(block => block.Key == ReportWordingComposition.ValuationCommentary).Text);
    }

    [Fact]
    public void AStoredChangeIsTrimmedToTheShapeTheReportPrints()
    {
        var validated = ReportWordingComposition.Validate(
            new(ReportWordingComposition.Salvage, "  Salvage  ", "  Sold as seen.  ", 3));

        Assert.Equal("Salvage", validated.Title);
        Assert.Equal("Sold as seen.", validated.Text);
    }

    [Fact]
    public void AnEmptyHeadingOrWordingIsHeldAsNoChange()
    {
        var validated = ReportWordingComposition.Validate(
            new(ReportWordingComposition.Salvage, "   ", "   ", null));

        Assert.Null(validated.Title);
        Assert.Null(validated.Text);
    }

    [Theory]
    [InlineData("nowhere", null, null, null, false)]
    [InlineData("manual:1", null, null, null, false)]
    [InlineData("salvage", null, null, null, true)]
    [InlineData("salvage", "A heading with a\nline break", null, null, false)]
    [InlineData("salvage", null, null, -1, false)]
    public void AChangeTheReportCouldNotPrintIsRefused(
        string key, string? title, string? text, int? order, bool manual) =>
        Assert.Throws<ArgumentException>(() =>
            ReportWordingComposition.Validate(new(key, title, text, order, Manual: manual)));

    [Fact]
    public void AHeadingOrWordingBeyondItsLengthIsRefused()
    {
        Assert.Throws<ArgumentException>(() => ReportWordingComposition.Validate(
            new(ReportWordingComposition.Salvage, new string('x', ReportWordingComposition.MaximumTitleLength + 1), null, null)));
        Assert.Throws<ArgumentException>(() => ReportWordingComposition.Validate(
            new(ReportWordingComposition.Salvage, null, new string('x', ReportWordingComposition.MaximumTextLength + 1), null)));
    }

    [Fact]
    public void AParagraphNeedsItsOwnKeyBeyondThePrefix() =>
        Assert.Throws<ArgumentException>(() => ReportWordingComposition.Validate(
            new(ReportWordingComposition.ManualKeyPrefix, null, "Something.", null, Manual: true)));

    [Fact]
    public void AnUnrecognisedMileageSourceIsRefused() =>
        Assert.Throws<ReportRenderRejectedException>(() =>
            ReportWordingComposition.MileageSentence("guessed"));

    private static AssessmentReportSnapshot Repairable() =>
        AssessmentReportRenderingTests.Snapshot(AssessmentReportOutcome.Repairable);

    private static AssessmentReportSnapshot TotalLoss() =>
        AssessmentReportRenderingTests.Snapshot(AssessmentReportOutcome.TotalLoss);
}
