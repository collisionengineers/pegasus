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
    /// The composed sentences are the report's own accepted wording, so a
    /// Case that has never been edited prints what the report printed before
    /// the blocks existed.
    /// </summary>
    [Fact]
    public void TheComposedSentencesAreTheReportsAcceptedWording()
    {
        var snapshot = Repairable();
        var blocks = ReportWordingComposition.Compose(snapshot, [])
            .ToDictionary(block => block.Key, StringComparer.Ordinal);

        Assert.Equal(
            "The vehicle has suffered Moderate collision/impact damage to the Right Rear.",
            blocks[ReportWordingComposition.NatureOfIncident].Text);
        Assert.Equal(
            "The mileage has been calculated from online data.",
            blocks[ReportWordingComposition.EngineersComments].Text);
        Assert.Equal(
            "The vehicle is considered to be in Good condition for its age and type.",
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
    [InlineData("A", "Category A (scrap only: the vehicle must be crushed in its entirety with no parts recovery)")]
    [InlineData("B", "Category B (break for spare parts: the body shell must be crushed)")]
    public void TheSalvageBlockFollowsTheRecordedCategory(string category, string expected)
    {
        var snapshot = TotalLoss() with { SalvageCategory = category };

        var salvage = ReportWordingComposition
            .Compose(snapshot, [])
            .Single(block => block.Key == ReportWordingComposition.Salvage);

        Assert.Contains(expected, salvage.Text, StringComparison.Ordinal);
        Assert.Contains("£500.00", salvage.Text, StringComparison.Ordinal);
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
            "The vehicle has suffered Moderate collision/impact damage to the Right Rear.",
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
