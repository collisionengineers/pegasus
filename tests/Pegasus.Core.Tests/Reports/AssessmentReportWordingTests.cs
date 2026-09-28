using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// Every word the layout prints outside the narrative blocks, pinned to the
/// template (reference/rendererref1). The fixture carries the facts of the
/// template's own sample job (sample_job_PK12TMZ.json), so each expected
/// string here is one the four sample reports print.
/// </summary>
public sealed class AssessmentReportWordingTests
{
    private static readonly DateTimeOffset RecordedAtUtc = new(2026, 7, 15, 9, 0, 0, TimeSpan.Zero);

    // The template's footer separator holds two no-break spaces.
    private const char NoBreakSpace = (char)0x00A0;

    /// <summary>Template: the top right of every page, and of the fee note page.</summary>
    [Fact]
    public void TheCompanyBlockIsTheTemplates()
    {
        Assert.Equal(
            [
                "Collision Engineers Ltd",
                "Independent Automotive Experts",
                "Engineers@CollisionEngineers.co.uk",
                "www.CollisionEngineers.co.uk",
            ],
            AssessmentReportWording.CompanyBlock(feeNote: false));
        // The VAT number appears on the fee note's page only (DESIGN_SPEC.md line 19).
        Assert.Equal(
            [
                "Collision Engineers Ltd",
                "Independent Automotive Experts",
                "VAT No: 262 0937 10",
                "Engineers@CollisionEngineers.co.uk",
                "www.CollisionEngineers.co.uk",
            ],
            AssessmentReportWording.CompanyBlock(feeNote: true));
    }

    /// <summary>Template: the foot of every page (DESIGN_SPEC.md lines 20 to 21).</summary>
    [Fact]
    public void TheFooterIsTheTemplates()
    {
        var snapshot = Sample(AssessmentReportOutcome.TotalLoss);
        var separator = $" {NoBreakSpace}|{NoBreakSpace} ";

        Assert.Equal(AssessmentReportWording.FooterSeparator, separator);
        Assert.Equal(
            $"PK12 TMZ · ap.qdos261789{separator}Collision Engineers Ltd{separator}www.CollisionEngineers.co.uk",
            AssessmentReportWording.Footer(snapshot, feeNote: false));
        Assert.Equal(
            $"PK12 TMZ · ap.qdos261789{separator}Collision Engineers Ltd{separator}VAT No: 262 0937 10",
            AssessmentReportWording.Footer(snapshot, feeNote: true));
        Assert.Equal(
            ["PK12 TMZ · ap.qdos261789", "Collision Engineers Ltd", "www.CollisionEngineers.co.uk"],
            AssessmentReportWording.FooterParts(snapshot, feeNote: false));
        // The Repair Spec printout carries the same footer.
        Assert.Equal(
            AssessmentReportWording.Footer(snapshot, feeNote: false),
            AssessmentReportWording.Footer("PK12 TMZ", "ap.qdos261789", feeNote: false));
        Assert.Equal("Page 1 of 7", AssessmentReportWording.PageNumber(1, 7));
        Assert.Equal(
            "Page 1 of 7",
            AssessmentReportWording.PageNumberLead + "1" + AssessmentReportWording.PageNumberJoin + "7");
    }

    /// <summary>Template: page 1, beneath the title.</summary>
    [Fact]
    public void TheHeaderLabelsAndTheMatterAreTheTemplates()
    {
        var snapshot = Sample(AssessmentReportOutcome.TotalLoss);

        Assert.Equal("Report For:", AssessmentReportWording.ReportForLabel);
        Assert.Equal("Matter:", AssessmentReportWording.MatterLabel);
        Assert.Equal(
            [
                new ReportRow("Date:", "16/07/2026"),
                new ReportRow("Our Ref:", "ap.qdos261789"),
                new ReportRow("Your Ref:", "MFI/ND/46885/1"),
            ],
            AssessmentReportWording.References(snapshot));
        // Composed, never typed (DESIGN_SPEC.md line 72).
        Assert.Equal(
            "Road Traffic Accident: Mrs Gurdip Kaur: 13/07/2026",
            AssessmentReportWording.Matter(snapshot));
    }

    /// <summary>Template: the title and the two badges of each sample report.</summary>
    [Theory]
    [InlineData(AssessmentReportOutcome.TotalLoss, "TOTAL LOSS REPORT", "TOTAL LOSS — CATEGORY S")]
    [InlineData(AssessmentReportOutcome.Repairable, "REPAIRABLE REPORT", "REPAIRABLE")]
    [InlineData(AssessmentReportOutcome.CashInLieu, "CASH IN LIEU REPORT", "CASH IN LIEU")]
    [InlineData(AssessmentReportOutcome.ContractRepair, "CONTRACT REPAIR REPORT", "CONTRACT REPAIR")]
    public void TheTitleAndTheBadgesAreTheTemplates(
        AssessmentReportOutcome outcome, string title, string badge)
    {
        var snapshot = Sample(outcome);

        Assert.Equal(title, snapshot.Presentation().Title);
        Assert.Equal(badge, snapshot.Presentation().Badge);
        Assert.Equal("ROADWORTHY", AssessmentReportWording.RoadworthinessBadge(snapshot));
        Assert.Equal(
            "UNROADWORTHY",
            AssessmentReportWording.RoadworthinessBadge(snapshot with
            {
                LegalStatus = "unroadworthy",
                UnroadworthyReason = "the impacted wheel",
            }));
    }

    /// <summary>
    /// A total loss with no category keeps the badge the operator ruled on 26
    /// September 2026.
    /// </summary>
    [Fact]
    public void ATotalLossWithNoCategoryKeepsItsRuledBadge()
    {
        var snapshot = Sample(AssessmentReportOutcome.TotalLoss) with
        {
            SalvageCategory = AssessmentReportContract.NoSalvageCategory,
        };

        Assert.Equal("TOTAL LOSS", snapshot.Presentation().Badge);
    }

    /// <summary>
    /// Template: the tiles of the four sample reports, page 1
    /// (DESIGN_SPEC.md lines 84 to 85). The last tile is the red one.
    /// </summary>
    [Fact]
    public void TheTilesOfEachOutcomeAreTheTemplates()
    {
        Assert.Equal(
            [
                new ReportTile("PRE-ACCIDENT VALUE", "£4,011.00", false),
                new ReportTile("REPAIR COST INC VAT", "£4,405.73", false),
                new ReportTile("SALVAGE VALUE", "£320.88", false),
                new ReportTile("RECOMMENDED SETTLEMENT", "£3,690.12", true),
            ],
            AssessmentReportWording.Tiles(Sample(AssessmentReportOutcome.TotalLoss)));
        Assert.Equal(
            [
                new ReportTile("PRE-ACCIDENT VALUE", "£4,011.00", false),
                new ReportTile("LABOUR HOURS", "25.90", false),
                new ReportTile("REPAIR COST INC VAT", "£4,405.73", true),
            ],
            AssessmentReportWording.Tiles(Sample(AssessmentReportOutcome.Repairable)));
        Assert.Equal(
            [
                new ReportTile("PRE-ACCIDENT VALUE", "£4,011.00", false),
                new ReportTile("LABOUR HOURS", "25.90", false),
                new ReportTile("CASH IN LIEU SETTLEMENT", "£4,405.73", true),
            ],
            AssessmentReportWording.Tiles(Sample(AssessmentReportOutcome.CashInLieu)));
        // Sample - Contract Repair Report.pdf, page 1.
        Assert.Equal(
            [
                new ReportTile("PRE-ACCIDENT VALUE", "£4,011.00", false),
                new ReportTile("LABOUR HOURS", "25.90", false),
                new ReportTile("REPAIR COST INC VAT", "£4,405.73", true),
            ],
            AssessmentReportWording.Tiles(Sample(AssessmentReportOutcome.ContractRepair)));
    }

    /// <summary>Template: page 1 (DESIGN_SPEC.md lines 104 to 108).</summary>
    [Fact]
    public void TheIntroductionIsTheTemplates()
    {
        var snapshot = Sample(AssessmentReportOutcome.TotalLoss);

        Assert.Equal("Image Based Assessment", AssessmentReportWording.ImageBasedAssessment);
        Assert.Equal(
            "In accordance with your instructions received on 14/07/2026 requesting us to provide an independent accident damage report, we assessed the damage on 15/07/2026. Vehicle located at: Image Based Assessment. Our findings are as detailed below.",
            AssessmentReportWording.Introduction(snapshot));
        Assert.Equal(
            "In accordance with your instructions received on 14/07/2026 requesting us to provide an independent accident damage report, we assessed the damage on 15/07/2026. Vehicle located at: 1 Test Street, London. Our findings are as detailed below.",
            AssessmentReportWording.Introduction(snapshot with
            {
                AssessmentMethod = "physical",
                LocationAddress = "1 Test Street, London",
            }));
    }

    /// <summary>
    /// Template: Sample - Total Loss Report.pdf, page 1. Eight cells, in
    /// reading order across the grid, each value as the template formats it.
    /// </summary>
    [Fact]
    public void VehicleDetailsAreTheTemplatesEightCells()
    {
        Assert.Equal("Vehicle Details", AssessmentReportWording.VehicleDetailsHeading);
        Assert.Equal(
            [
                new ReportRow("Make", "KIA"),
                new ReportRow("Registration", "PK12 TMZ"),
                new ReportRow("Model", "PICANTO HALO ECODYNAMICS"),
                new ReportRow("VIN", "KNABX312LCT122784"),
                new ReportRow("Odometer", "50,500 miles"),
                new ReportRow("Engine / Fuel", "1,248 cc · Petrol"),
                new ReportRow("Pre-Incident Condition", "Good"),
                new ReportRow("Impact Magnitude", "Moderate — right rear"),
            ],
            AssessmentReportWording.VehicleDetails(Sample(AssessmentReportOutcome.TotalLoss)));
    }

    /// <summary>
    /// Template: DESIGN_SPEC.md line 90. A value that is not recorded prints
    /// a dash, and the odometer of a vehicle with no mileage prints TBC.
    /// </summary>
    [Fact]
    public void AVehicleDetailThatIsNotRecordedPrintsTheTemplatesMark()
    {
        var sample = Sample(AssessmentReportOutcome.Repairable);
        var snapshot = sample with
        {
            Vehicle = sample.Vehicle with
            {
                Vin = null,
                Engine = null,
                Fuel = " ",
                MileageDescription = AssessmentReportWording.NoMileage,
                MileageSource = "tbc",
            },
        };

        var rows = AssessmentReportWording.VehicleDetails(snapshot);

        Assert.Equal(8, rows.Count);
        Assert.Equal(new ReportRow("VIN", "—"), rows[3]);
        Assert.Equal(new ReportRow("Odometer", "TBC"), rows[4]);
        Assert.Equal(new ReportRow("Engine / Fuel", "—"), rows[5]);
    }

    /// <summary>
    /// Template: "1,248 cc · Petrol" and "1,248 cc". The Case records the
    /// engine size as a plain number; anything else prints as recorded.
    /// </summary>
    [Theory]
    [InlineData("1248", "1,248 cc")]
    [InlineData("1461", "1,461 cc")]
    [InlineData("998", "998 cc")]
    [InlineData(" 1598 ", "1,598 cc")]
    [InlineData("1600 cc", "1600 cc")]
    [InlineData("1.6", "1.6")]
    [InlineData("Electric", "Electric")]
    [InlineData("-1", "-1")]
    public void TheEngineSizeIsFormattedAsTheTemplateFormatsIt(string recorded, string printed)
    {
        Assert.Equal(printed, AssessmentReportWording.EngineSize(recorded));
    }

    [Fact]
    public void TheEngineAndFuelPrintWhicheverAreRecorded()
    {
        var vehicle = Sample(AssessmentReportOutcome.Repairable).Vehicle;

        Assert.Null(AssessmentReportWording.EngineSize(null));
        Assert.Null(AssessmentReportWording.EngineSize("  "));
        // The fuel prints as the Case records it.
        Assert.Equal(
            "1,461 cc · DIESEL",
            AssessmentReportWording.EngineAndFuel(vehicle with { Engine = "1461", Fuel = "DIESEL" }));
        Assert.Equal("1,461 cc", AssessmentReportWording.EngineAndFuel(vehicle with { Engine = "1461", Fuel = null }));
        Assert.Equal("Petrol", AssessmentReportWording.EngineAndFuel(vehicle with { Engine = null, Fuel = "Petrol" }));
    }

    /// <summary>
    /// The severity prints the same way everywhere in one report: in small
    /// letters inside the sentence, and with its first letter a capital where
    /// it opens the Impact Magnitude cell. No word after the first is
    /// capitalised.
    /// </summary>
    [Theory]
    [InlineData("light", "right_rear", "Light — right rear")]
    [InlineData("light_to_moderate", "left_front", "Light to moderate — left front")]
    [InlineData("moderate_to_heavy", "underside", "Moderate to heavy — underside")]
    [InlineData("heavy", "multiple", "Heavy — multiple")]
    public void ImpactMagnitudeOpensWithTheSeverityAndNamesTheLocationInSmallLetters(
        string severity, string location, string printed)
    {
        Assert.Equal(printed, AssessmentReportWording.ImpactMagnitude(severity, location));
        Assert.Equal(
            AssessmentVocabulary.DamageSeverities[severity].Display,
            ReportWordingComposition.LeadingWords(severity));
        Assert.Equal(
            ReportWordingComposition.LeadingWords(severity).ToLowerInvariant(),
            ReportWordingComposition.Words(severity));
    }

    [Theory]
    [InlineData("poor", "Poor")]
    [InlineData("below_average", "Below average")]
    [InlineData("average", "Average")]
    [InlineData("good", "Good")]
    [InlineData("excellent", "Excellent")]
    public void TheConditionCellOpensWithACapitalAndNoOther(string condition, string printed)
    {
        var sample = Sample(AssessmentReportOutcome.Repairable);
        var snapshot = sample with { Vehicle = sample.Vehicle with { Condition = condition } };

        Assert.Equal(printed, AssessmentReportWording.VehicleDetails(snapshot)[6].Value);
        Assert.Equal(printed, AssessmentReportWording.VehicleData(snapshot)[^1].Value);
    }

    /// <summary>Template: Sample - Total Loss Report.pdf, page 2.</summary>
    [Fact]
    public void TheDesktopAssessmentIsTheTemplates()
    {
        Assert.Equal("Desktop Assessment", AssessmentReportWording.DesktopAssessmentHeading);
        Assert.Equal(
            "This report has been compiled from a desktop review of the information available relating to this claim.",
            AssessmentReportWording.DesktopAssessment);
    }

    /// <summary>Template: the value box of each sample report, page 2.</summary>
    [Theory]
    [InlineData(AssessmentReportOutcome.TotalLoss, "Recommended equitable settlement (pre-accident value less salvage)", "£3,690.12")]
    [InlineData(AssessmentReportOutcome.Repairable, "Recommended settlement (calculated repair cost)", "£4,405.73")]
    [InlineData(AssessmentReportOutcome.CashInLieu, "Recommended cash in lieu settlement (estimated repair cost)", "£4,405.73")]
    [InlineData(AssessmentReportOutcome.ContractRepair, "Agreed contract repair (including VAT)", "£4,405.73")]
    public void TheValueBoxIsTheTemplates(AssessmentReportOutcome outcome, string label, string figure)
    {
        Assert.Equal(new ReportRow(label, figure), AssessmentReportWording.ValueBox(Sample(outcome)));
    }

    /// <summary>
    /// Template: the value box of each sample report, page 2. The total loss
    /// sample sets its label in two cells, the second beginning 47.4 mm to the
    /// right of the first; the other three set theirs in one.
    /// </summary>
    [Fact]
    public void TheValueBoxLabelStandsInTheTemplatesCells()
    {
        Assert.Equal(
            ["Recommended equitable settlement", "(pre-accident value less salvage)"],
            AssessmentReportWording.ValueBoxLabelCells(Sample(AssessmentReportOutcome.TotalLoss)));
        Assert.Equal(
            ["Recommended settlement (calculated repair cost)"],
            AssessmentReportWording.ValueBoxLabelCells(Sample(AssessmentReportOutcome.Repairable)));
        Assert.Equal(
            ["Recommended cash in lieu settlement (estimated repair cost)"],
            AssessmentReportWording.ValueBoxLabelCells(Sample(AssessmentReportOutcome.CashInLieu)));
        Assert.Equal(
            ["Agreed contract repair (including VAT)"],
            AssessmentReportWording.ValueBoxLabelCells(Sample(AssessmentReportOutcome.ContractRepair)));
    }

    /// <summary>Template: Sample - Total Loss Report.pdf, page 3.</summary>
    [Fact]
    public void VehicleDataIsTheTemplatesNineRows()
    {
        Assert.Equal("Vehicle Data", AssessmentReportWording.VehicleDataHeading);
        Assert.Equal(
            [
                new ReportRow("Retail Value", "£4,011.00"),
                new ReportRow("Trade Value", "£2,848.00"),
                new ReportRow("Engineer's Value", "£4,011.00"),
                new ReportRow("VIN", "KNABX312LCT122784"),
                new ReportRow("Year", "2012 (12 reg)"),
                new ReportRow("Odometer", "50,500 miles"),
                new ReportRow("Engine", "1,248 cc"),
                new ReportRow("Fuel", "Petrol"),
                new ReportRow("Condition", "Good"),
            ],
            AssessmentReportWording.VehicleData(Sample(AssessmentReportOutcome.TotalLoss)));
    }

    /// <summary>
    /// Template: DESIGN_SPEC.md line 18. A Vehicle Data row with nothing
    /// recorded is left out; the odometer of a vehicle with no mileage still
    /// prints TBC.
    /// </summary>
    [Fact]
    public void AVehicleDataRowWithNothingRecordedIsLeftOut()
    {
        var sample = Sample(AssessmentReportOutcome.Repairable);
        var snapshot = sample with
        {
            Vehicle = sample.Vehicle with
            {
                Vin = null,
                Year = string.Empty,
                Engine = null,
                Fuel = null,
                MileageDescription = AssessmentReportWording.NoMileage,
            },
        };

        Assert.Equal(
            ["Retail Value", "Trade Value", "Engineer's Value", "Odometer", "Condition"],
            AssessmentReportWording.VehicleData(snapshot).Select(row => row.Label));
        Assert.Equal("TBC", AssessmentReportWording.VehicleData(snapshot)[3].Value);
    }

    /// <summary>
    /// Template: Sample - Total Loss Report.pdf, page 3. The header cells are
    /// "Repair Cost Calculation" and "Amount"; the nine rows and their figures
    /// are the sample's own. Sub Total and the VAT row print in bold and the
    /// total is shaded.
    /// </summary>
    [Fact]
    public void TheRepairCostTableIsTheTemplatesNineRows()
    {
        Assert.Equal("Repair Cost Calculation", AssessmentReportWording.RepairCostHeading);
        Assert.Equal("Amount", AssessmentReportWording.AmountHeading);
        Assert.Equal(
            [
                new ReportCostRow("Labour Hours", "25.90", ReportCostRowKind.Figure),
                new ReportCostRow("Hourly Rate", "£83.28", ReportCostRowKind.Figure),
                new ReportCostRow("Total Labour", "£2,156.95", ReportCostRowKind.Figure),
                new ReportCostRow("Parts", "£278.44", ReportCostRowKind.Figure),
                new ReportCostRow("Paint / Materials", "£769.42", ReportCostRowKind.Figure),
                new ReportCostRow("Specialist / Other", "£466.63", ReportCostRowKind.Figure),
                new ReportCostRow("Sub Total", "£3,671.44", ReportCostRowKind.Subtotal),
                new ReportCostRow("VAT (20%)", "£734.29", ReportCostRowKind.Subtotal),
                new ReportCostRow("Total Estimated Repair Cost", "£4,405.73", ReportCostRowKind.Total),
            ],
            AssessmentReportWording.RepairCosts(SampleCosts(RepairerVatStatus.Registered)));
    }

    /// <summary>
    /// Template: DESIGN_SPEC.md lines 126 to 129. A repairer who is not VAT
    /// registered charges VAT on parts and paint only, and the row says so.
    /// </summary>
    [Fact]
    public void ARepairerWhoIsNotRegisteredPrintsTheTemplatesPartsAndPaintRow()
    {
        var rows = AssessmentReportWording.RepairCosts(SampleCosts(RepairerVatStatus.NotRegistered));

        // 20 per cent of the parts and the paint and materials: 278.44 + 769.42.
        Assert.Equal(
            new ReportCostRow("VAT (20% — parts & paint only)", "£209.57", ReportCostRowKind.Subtotal),
            rows[7]);
        Assert.Equal(
            new ReportCostRow("Total Estimated Repair Cost", "£3,881.01", ReportCostRowKind.Total),
            rows[8]);
    }

    /// <summary>
    /// The template words no other VAT row, so a repair spec whose VAT it
    /// cannot word prints no table at all. Readiness names the gap first.
    /// </summary>
    [Fact]
    public void TheRepairCostTableRefusesAVatTreatmentTheTemplateCannotWord()
    {
        Assert.Throws<ReportRenderRejectedException>(
            () => AssessmentReportWording.RepairCosts(SampleCosts(RepairerVatStatus.Unknown)));
    }

    /// <summary>Template: Sample - Total Loss Report.pdf, pages 4 and 5.</summary>
    [Fact]
    public void TheWorkListsAndTheImagesHeadingAreTheTemplates()
    {
        var snapshot = Sample(AssessmentReportOutcome.TotalLoss);

        Assert.Equal(
            ["Main New Parts Required", "Repairs Required", "Additional Operations"],
            AssessmentReportWording.WorkLists(snapshot).Select(list => list.Title));
        Assert.Equal(snapshot.NewParts, AssessmentReportWording.WorkLists(snapshot)[0].Items);
        // A list with no items is not printed.
        Assert.Equal(
            ["Main New Parts Required", "Additional Operations"],
            AssessmentReportWording.WorkLists(snapshot with { Repairs = [] }).Select(list => list.Title));
        Assert.Equal("Vehicle Images", AssessmentReportWording.VehicleImagesHeading);
        Assert.Equal("VEHICLE IMAGES", AssessmentReportWording.ImagePackTitle);
    }

    /// <summary>Template: Sample - Total Loss Report.pdf, page 6.</summary>
    [Fact]
    public void TheSignOffIsTheTemplates()
    {
        Assert.Equal("Statement of Truth", AssessmentReportWording.StatementOfTruthHeading);
        Assert.Equal("Yours faithfully,", AssessmentReportWording.Valediction);
        Assert.Equal(
            "A Patterson — M.Inst.IAEA",
            AssessmentReportWording.SignatoryLine(new ReportSignatory("A Patterson", "M.Inst.IAEA", [1], "image/png")));
        Assert.Equal(
            "N O'Reilly",
            AssessmentReportWording.SignatoryLine(new ReportSignatory("N O'Reilly", " ", [1], "image/png")));
        Assert.Equal(
            "Independent Automotive Engineer, Collision Engineers Ltd",
            AssessmentReportWording.SignatoryRole);
        Assert.Equal("Engineers@CollisionEngineers.co.uk", AssessmentReportWording.CompanyEmail);
    }

    /// <summary>
    /// Template: Sample - Total Loss Report.pdf, page 6. Its third paragraph
    /// opens with the guide sentence and runs on.
    /// </summary>
    [Fact]
    public void TheStatementOfTruthStandsInTheTemplatesFourParagraphs()
    {
        var sample = Sample(AssessmentReportOutcome.TotalLoss);
        var disclosed = sample with
        {
            Content = sample.Content with { DiscloseGuideSource = true },
            Guides = new ReportGuideSources([ValuationSource.Glasses]),
        };

        Assert.Equal(
            [
                "I declare that I understand my duty in providing this report to the court and I confirm that I have complied with that duty. I understand that this duty overrides any other obligation. The report is based upon instructions received.",
                "I confirm that I have made clear which facts and matters referred to in this report are within my own knowledge and which are not. Those that are within my own knowledge I confirm to be true. The opinions I have expressed represent my true and complete professional opinion on the matters to which they refer.",
                "We have used Glass's Evaluator to assist with the valuation of the vehicle and Thatcham and/or manufacturer's data to compile the repair specification. Parts prices are subject to fluctuation and further damage may be found upon dismantling the vehicle. Our valuation is based on the mileage information provided and assuming that the vehicle has a valid MOT certificate (where applicable) to support such.",
                "We appreciate your instructions and enclose our fee note for your kind attention, which we confirm remains payable irrespective of the outcome of this case. Please ensure this is passed to your accounts department.",
            ],
            AssessmentReportWording.StatementOfTruthParagraphs(disclosed));
        // A report that does not print the guide sentence prints the statement as it holds it.
        Assert.Equal(sample.StatementOfTruth, AssessmentReportWording.StatementOfTruthParagraphs(sample));
        Assert.Equal(4, sample.StatementOfTruth.Count);
    }

    /// <summary>Template: Sample - Total Loss Report.pdf, page 7.</summary>
    [Fact]
    public void TheFeeNoteIsTheTemplates()
    {
        var snapshot = Sample(AssessmentReportOutcome.TotalLoss);

        Assert.Equal("FEE NOTE", AssessmentReportWording.FeeNoteTitle);
        Assert.Equal("Bill To:", AssessmentReportWording.BillToLabel);
        Assert.Equal("Description", AssessmentReportWording.FeeDescriptionHeading);
        Assert.Equal("Amount (£)", AssessmentReportWording.FeeAmountHeading);
        Assert.Equal("Vehicle Damage Assessment Report — PK12 TMZ", AssessmentReportWording.FeeTitle(snapshot));
        Assert.Equal("100.00", AssessmentReportWording.FeeAmount(snapshot));
        Assert.Equal(
            [
                new ReportCostRow("Subtotal (Net)", "100.00", ReportCostRowKind.Subtotal),
                new ReportCostRow("VAT @ 20%", "20.00", ReportCostRowKind.Subtotal),
                new ReportCostRow("TOTAL DUE", "£120.00", ReportCostRowKind.Total),
            ],
            AssessmentReportWording.FeeTotals(snapshot));
        Assert.Equal("Payment Details", AssessmentReportWording.PaymentDetailsHeading);
        Assert.Equal(
            [
                new ReportRow("Account Name", "Collision Engineers Ltd"),
                new ReportRow("Bank", "Lloyds Bank"),
                new ReportRow("Sort Code", "30-12-80"),
                new ReportRow("Account Number", "50858868"),
                new ReportRow("Payment Reference", "ap.qdos261789"),
                new ReportRow("Remittance Email", "accounts@collisionengineers.co.uk"),
            ],
            AssessmentReportWording.PaymentDetails(snapshot));
        Assert.Equal("Terms", AssessmentReportWording.TermsHeading);
        Assert.Equal(
            [AssessmentReportContract.FeeTerms, AssessmentReportContract.AdditionalFeeTerms],
            AssessmentReportWording.Terms);
        Assert.Equal("Thank you for your business.", AssessmentReportWording.ThankYou);
    }

    /// <summary>
    /// The fee's own line is the template's and so are the description lines
    /// the Case records. Neither source words a fee with no description, so a
    /// Case that records none prints the fee's own line alone.
    /// </summary>
    [Fact]
    public void AFeeWithNoRecordedDescriptionPrintsNoInventedLine()
    {
        var snapshot = Sample(AssessmentReportOutcome.Repairable) with { FeeDescriptionLines = [] };

        snapshot.Validate();

        Assert.Empty(snapshot.FeeDescriptionLines);
        Assert.Equal("Vehicle Damage Assessment Report — PK12 TMZ", AssessmentReportWording.FeeTitle(snapshot));
    }

    /// <summary>
    /// Report For is the principal's name, each line of its address and its
    /// postcode (DESIGN_SPEC.md lines 69 to 70: the principal's address
    /// block).
    /// </summary>
    [Fact]
    public void ReportForIsTheNameTheAddressLineByLineAndThePostcode()
    {
        Assert.Equal(
            ["QDOS Assistance", "3rd Floor, Crown House", "Manchester Road", "Wilmslow", "SK9 1BH"],
            AssessmentReportWording.ReportFor(
                " QDOS Assistance ",
                "3rd Floor, Crown House\r\nManchester Road\r\n\r\n  Wilmslow  \r\n",
                " SK9 1BH "));
    }

    [Theory]
    [InlineData("3rd Floor, Crown House\nWilmslow\nSK9 1BH", "SK9 1BH")]
    [InlineData("3rd Floor, Crown House\nWilmslow\nsk9 1bh", "SK9 1BH")]
    [InlineData("3rd Floor, Crown House\nWilmslow\nSK91BH", "SK9 1BH")]
    [InlineData("3rd Floor, Crown House\nWilmslow SK9 1BH", "SK9 1BH")]
    [InlineData("3rd Floor, Crown House\nWilmslow, SK9 1BH", "sk91bh")]
    public void ThePostcodeIsNotPrintedTwiceWhenTheAddressEndsWithIt(string address, string postcode)
    {
        var lines = AssessmentReportWording.ReportFor("QDOS Assistance", address, postcode);

        Assert.Equal(["QDOS Assistance", .. address.Split('\n')], lines);
    }

    [Fact]
    public void APostcodeTheAddressOnlyMentionsEarlierIsStillPrinted()
    {
        Assert.Equal(
            ["QDOS Assistance", "SK9 1BH House", "Wilmslow", "SK9 1BH"],
            AssessmentReportWording.ReportFor("QDOS Assistance", "SK9 1BH House\nWilmslow", "SK9 1BH"));
    }

    /// <summary>
    /// A principal with no address prints its name alone (operator, 27
    /// September 2026).
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("  \r\n  ", "  ")]
    public void APrincipalWithNoAddressPrintsItsNameAlone(string? address, string? postcode)
    {
        Assert.Equal(["QDOS Assistance"], AssessmentReportWording.ReportFor("QDOS Assistance", address, postcode));
    }

    [Fact]
    public void APrincipalWithAPostcodeAndNoAddressPrintsBoth()
    {
        Assert.Equal(
            ["QDOS Assistance", "SK9 1BH"],
            AssessmentReportWording.ReportFor("QDOS Assistance", null, "SK9 1BH"));
    }

    /// <summary>
    /// A report needs an addressee: a principal with no name yields none, and
    /// the snapshot is refused for want of one.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void APrincipalWithNoNameHasNoAddressee(string? name)
    {
        var lines = AssessmentReportWording.ReportFor(name, "3rd Floor, Crown House", "SK9 1BH");

        Assert.Empty(lines);
        Assert.Throws<ReportRenderRejectedException>(
            (Sample(AssessmentReportOutcome.Repairable) with { ReportFor = lines }).Validate);
    }

    /// <summary>
    /// Template: Sample - Total Loss Report.pdf, page 7. The fee note sets the
    /// address two lines to a row: "3rd Floor, Crown House, Manchester Road"
    /// and "Wilmslow, SK9 1BH".
    /// </summary>
    [Fact]
    public void BillToSetsTheAddressTwoLinesToARowAsTheTemplateDoes()
    {
        Assert.Equal(
            ["QDOS Assistance", "3rd Floor, Crown House, Manchester Road", "Wilmslow, SK9 1BH"],
            AssessmentReportWording.BillTo(
                ["QDOS Assistance", "3rd Floor, Crown House", "Manchester Road", "Wilmslow", "SK9 1BH"]));
        // Template: Sample - Total Loss Report.pdf, page 7, sets the sample
        // job's six lines in these four rows.
        Assert.Equal(
            [
                "FAO The Court",
                "C/o QDOS Assistance",
                "3rd Floor, Crown House, Manchester Road",
                "Wilmslow, SK9 1BH",
            ],
            AssessmentReportWording.BillTo(
            [
                "FAO The Court", "C/o QDOS Assistance", "3rd Floor, Crown House",
                "Manchester Road", "Wilmslow", "SK9 1BH",
            ]));
        Assert.Equal(
            ["QDOS Assistance", "3rd Floor, Crown House", "Wilmslow, SK9 1BH"],
            AssessmentReportWording.BillTo(["QDOS Assistance", "3rd Floor, Crown House", "Wilmslow", "SK9 1BH"]));
        Assert.Equal(
            ["QDOS Assistance", "SK9 1BH"],
            AssessmentReportWording.BillTo(["QDOS Assistance", "SK9 1BH"]));
        Assert.Equal(["QDOS Assistance"], AssessmentReportWording.BillTo(["QDOS Assistance"]));
        Assert.Empty(AssessmentReportWording.BillTo([]));
    }

    [Fact]
    public void ValuesPrintAsTheTemplatePrintsThem()
    {
        Assert.Equal("16/07/2026", AssessmentReportWording.Date(new DateOnly(2026, 7, 16)));
        Assert.Equal("£4,405.73", AssessmentReportWording.Money(4_405.73m));
        Assert.Equal("4,405.73", AssessmentReportWording.Number(4_405.73m));
        Assert.Equal("25.90", AssessmentReportWording.Hours(25.9m));
    }

    /// <summary>
    /// The facts of the template's sample job, as a snapshot of the outcome
    /// asked for.
    /// </summary>
    private static AssessmentReportSnapshot Sample(AssessmentReportOutcome outcome)
    {
        var totalLoss = outcome == AssessmentReportOutcome.TotalLoss;
        var costs = SampleCosts(RepairerVatStatus.Registered);
        return AssessmentReportRenderingTests.Snapshot(outcome) with
        {
            OurReference = "ap.qdos261789",
            YourReference = "MFI/ND/46885/1",
            ReportDate = new DateOnly(2026, 7, 16),
            ClaimantName = "Mrs Gurdip Kaur",
            IncidentDate = new DateOnly(2026, 7, 13),
            InstructionsReceived = new DateOnly(2026, 7, 14),
            Assessed = new DateOnly(2026, 7, 15),
            Vehicle = new ReportVehicle(
                "PK12 TMZ", "KIA", "PICANTO HALO ECODYNAMICS", "2012 (12 reg)", "good",
                "50,500 miles", "online_data", "KNABX312LCT122784", "1248", "Petrol"),
            EngineerValue = 4_011m,
            RetailValue = 4_011m,
            TradeValue = 2_848m,
            SalvageCategory = totalLoss ? "S" : null,
            SalvageValue = totalLoss ? 320.88m : null,
            Costs = costs,
            Settlement = outcome == AssessmentReportOutcome.ContractRepair
                ? new ReportSettlement(costs.Total)
                : new ReportSettlement(),
            AgreedFee = 100m,
            FeeDescriptionLines =
            [
                "Collate data, compile repair specification, calculate estimate, research valuation.",
                "Produce Vehicle Damage Assessment report.",
                "Storage of all material case files and images.",
            ],
        };
    }

    /// <summary>
    /// The sample job's costs: 25.9 labour hours at 83.28 (20.4 panel and 5.5
    /// paint), 278.44 parts, 769.42 paint and materials and 466.63 specialist.
    /// </summary>
    private static ReportRepairCosts SampleCosts(RepairerVatStatus status) => ReportRepairCosts.For(
        new RepairSpecificationVersion(
            Guid.NewGuid(), Guid.NewGuid(), 1, RepairSpecificationState.Draft,
            new(RepairSpecificationSourceRoute.Manual, null, null, null),
            [
                Line(1, "repair", "Right Rear Side Panel with Sill Panel") with { WorkUnits = 20.4m },
                Line(2, "paint_repair", "Paint Right Rear Side Panel, Complete")
                    with { PaintWorkUnits = 5.5m, Materials = 769.42m },
                Line(3, "new_part", "Rear Bumper Lining") with { Price = 278.44m, Quantity = 1 },
            ],
            "engineer-1", RecordedAtUtc,
            new EstimateDetails("Repairer", 83.28m, 466.63m, 20m, Vat: EstimateVatPolicy.For(status)),
            IsCurrent: true));

    private static CaseEstimateLineRecord Line(int position, string type, string description) => new(
        Guid.NewGuid(), position, type, null, description, null, null, false, null, null,
        "case", "Test evidence",
        ActorKind.Staff, "engineer-1", RecordedAtUtc);
}
