using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// The Principal's own invoice (the ULA Invoice) is figured from the report's
/// own printed repair costs, computed here through the real
/// <see cref="EstimateTotals"/>, so its Sub Total, VAT and Total are the
/// report's to the penny (R1150, R1157, R1160).
/// </summary>
public sealed class UlaInvoiceProjectionTests
{
    private static readonly DateTimeOffset RecordedAtUtc = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The draft's own figures (ULA_Invoice_257922_DRAFT.docx): four rows at
    /// 20 per cent for a VAT registered repairer, whose VAT adds up to the
    /// report's with nothing to carry.
    /// </summary>
    [Fact]
    public void TheDraftsFiguresAreTheReportsOwn()
    {
        var costs = Costs(RepairerVatStatus.Registered, 1623.96m, 1073.73m, 796.04m, 597.47m);

        var invoice = UlaInvoiceCalculation.For(AssessmentReportOutcome.Repairable, costs, contractSum: null)!;

        UlaInvoiceRow[] expected =
            [
                new UlaInvoiceRow(1, UlaInvoiceCalculation.TotalLabour, 1m, 1623.96m, 20m, 324.79m, 1948.75m),
                new UlaInvoiceRow(2, UlaInvoiceCalculation.Parts, 1m, 1073.73m, 20m, 214.75m, 1288.48m),
                new UlaInvoiceRow(3, UlaInvoiceCalculation.PaintMaterials, 1m, 796.04m, 20m, 159.21m, 955.25m),
                new UlaInvoiceRow(4, UlaInvoiceCalculation.AdditionalOperations, 1m, 597.47m, 20m, 119.49m, 716.96m),
            ];
        Assert.Equal(expected, invoice.Rows);
        Assert.Equal(4091.20m, invoice.SubTotal);
        Assert.Equal(818.24m, invoice.Vat);
        Assert.Equal(4909.44m, invoice.Total);
        Assert.Equal(costs.Printed.Net, invoice.SubTotal);
        Assert.Equal(costs.Printed.Vat, invoice.Vat);
        Assert.Equal(costs.Printed.Gross, invoice.Total);
    }

    /// <summary>
    /// Each row's VAT rounds up a fraction of a penny that the report's one
    /// VAT figure does not, so the rows come to a penny more than the report.
    /// The largest taxable row gives the penny back.
    /// </summary>
    [Fact]
    public void TheLargestTaxableRowTakesThePennyTheRowsRoundAway()
    {
        var costs = Costs(RepairerVatStatus.Registered, 100.03m, 50.03m, 20.03m, 0m);
        Assert.Equal(34.02m, costs.Printed.Vat);

        var invoice = UlaInvoiceCalculation.For(AssessmentReportOutcome.Repairable, costs, contractSum: null)!;

        UlaInvoiceRow[] expected =
            [
                new UlaInvoiceRow(1, UlaInvoiceCalculation.TotalLabour, 1m, 100.03m, 20m, 20.00m, 120.03m),
                new UlaInvoiceRow(2, UlaInvoiceCalculation.Parts, 1m, 50.03m, 20m, 10.01m, 60.04m),
                new UlaInvoiceRow(3, UlaInvoiceCalculation.PaintMaterials, 1m, 20.03m, 20m, 4.01m, 24.04m),
            ];
        Assert.Equal(expected, invoice.Rows);
        Assert.Equal(costs.Printed.Vat, invoice.Rows.Sum(row => row.Vat));
        Assert.Equal(costs.Printed.Gross, invoice.Rows.Sum(row => row.Amount));
        Assert.Equal(costs.Printed.Gross, invoice.Total);
    }

    /// <summary>
    /// A repairer who is not VAT registered charges VAT on parts and paint
    /// only, as the report does: labour and additional operations print at
    /// 0.00 per cent, and the penny the two taxed rows round away comes off
    /// the larger of them.
    /// </summary>
    [Fact]
    public void ANotRegisteredRepairerTaxesPartsAndPaintOnly()
    {
        var costs = Costs(RepairerVatStatus.NotRegistered, 1623.96m, 1073.73m, 796.04m, 597.47m);
        Assert.Equal(373.95m, costs.Printed.Vat);

        var invoice = UlaInvoiceCalculation.For(AssessmentReportOutcome.Repairable, costs, contractSum: null)!;

        UlaInvoiceRow[] expected =
            [
                new UlaInvoiceRow(1, UlaInvoiceCalculation.TotalLabour, 1m, 1623.96m, 0m, 0m, 1623.96m),
                new UlaInvoiceRow(2, UlaInvoiceCalculation.Parts, 1m, 1073.73m, 20m, 214.74m, 1288.47m),
                new UlaInvoiceRow(3, UlaInvoiceCalculation.PaintMaterials, 1m, 796.04m, 20m, 159.21m, 955.25m),
                new UlaInvoiceRow(4, UlaInvoiceCalculation.AdditionalOperations, 1m, 597.47m, 0m, 0m, 597.47m),
            ];
        Assert.Equal(expected, invoice.Rows);
        Assert.Equal(4091.20m, invoice.SubTotal);
        Assert.Equal(373.95m, invoice.Vat);
        Assert.Equal(4465.15m, invoice.Total);
        Assert.Equal(costs.Printed.Gross, invoice.Total);
    }

    /// <summary>A row of 0.00 is left out and the rows after it are numbered on from 1.</summary>
    [Fact]
    public void ANoughtRowIsLeftOutAndTheRestRenumbered()
    {
        var costs = Costs(RepairerVatStatus.Registered, 1623.96m, 0m, 796.04m, 597.47m);

        var invoice = UlaInvoiceCalculation.For(AssessmentReportOutcome.Repairable, costs, contractSum: null)!;

        int[] numbers = [1, 2, 3];
        string[] descriptions =
            [UlaInvoiceCalculation.TotalLabour, UlaInvoiceCalculation.PaintMaterials, UlaInvoiceCalculation.AdditionalOperations];
        Assert.Equal(numbers, invoice.Rows.Select(row => row.Number));
        Assert.Equal(descriptions, invoice.Rows.Select(row => row.Description));
        Assert.Equal(costs.Printed.Net, invoice.SubTotal);
        Assert.Equal(costs.Printed.Vat, invoice.Rows.Sum(row => row.Vat));
        Assert.Equal(costs.Printed.Gross, invoice.Total);
    }

    /// <summary>
    /// A Contract repair prints one row for the agreed sum, VAT included. For
    /// a VAT registered repairer the sum is parted at the estimate's own
    /// percentage, the rate rounded to the penny and the VAT the rest.
    /// </summary>
    [Fact]
    public void AContractRepairForARegisteredRepairerPartsTheSumAtTheEstimatesPercentage()
    {
        var costs = Costs(RepairerVatStatus.Registered, 1623.96m, 1073.73m, 796.04m, 597.47m);

        var invoice = UlaInvoiceCalculation.For(AssessmentReportOutcome.ContractRepair, costs, 3000.01m)!;

        UlaInvoiceRow[] expected =
            [new UlaInvoiceRow(1, UlaInvoiceCalculation.ContractRepair, 1m, 2500.01m, 20m, 500.00m, 3000.01m)];
        Assert.Equal(expected, invoice.Rows);
        Assert.Equal(2500.01m, invoice.SubTotal);
        Assert.Equal(500.00m, invoice.Vat);
        Assert.Equal(3000.01m, invoice.Total);
    }

    /// <summary>
    /// For a repairer who is not VAT registered the one row carries VAT in
    /// the share the report's repair total does, the VAT % is the rate that
    /// results and the Amount is still the agreed sum (R1160).
    /// </summary>
    [Fact]
    public void AContractRepairForANotRegisteredRepairerCarriesTheReportsShareOfVat()
    {
        var costs = Costs(RepairerVatStatus.NotRegistered, 1623.96m, 1073.73m, 796.04m, 597.47m);
        Assert.Equal(373.95m, costs.Printed.Vat);
        Assert.Equal(4465.15m, costs.Printed.Gross);

        var invoice = UlaInvoiceCalculation.For(AssessmentReportOutcome.ContractRepair, costs, 4000.00m)!;

        // 4000.00 x 373.95 / 4465.15 = 334.994..., and 334.99 / 3665.01 = 9.140...%.
        UlaInvoiceRow[] expected =
            [new UlaInvoiceRow(1, UlaInvoiceCalculation.ContractRepair, 1m, 3665.01m, 9.14m, 334.99m, 4000.00m)];
        Assert.Equal(expected, invoice.Rows);
        Assert.Equal(3665.01m, invoice.SubTotal);
        Assert.Equal(334.99m, invoice.Vat);
        Assert.Equal(4000.00m, invoice.Total);
    }

    /// <summary>A Contract repair with no positive agreed sum has nothing to bill.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void AContractRepairWithNoAgreedSumHasNoInvoice(int? contractSum)
    {
        var costs = Costs(RepairerVatStatus.Registered, 100m, 50m, 20m, 5m);

        Assert.Null(UlaInvoiceCalculation.For(AssessmentReportOutcome.ContractRepair, costs, contractSum));
    }

    /// <summary>A Total loss and a Cash in lieu have no invoice (R1143).</summary>
    [Theory]
    [InlineData(AssessmentReportOutcome.TotalLoss)]
    [InlineData(AssessmentReportOutcome.CashInLieu)]
    public void ATotalLossOrCashInLieuHasNoInvoice(AssessmentReportOutcome outcome)
    {
        var costs = Costs(RepairerVatStatus.Registered, 100m, 50m, 20m, 5m);

        Assert.Null(UlaInvoiceCalculation.For(outcome, costs, contractSum: 1000m));
    }

    /// <summary>The Subject bar prints make and model in capitals, the make once (R1149).</summary>
    [Theory]
    [InlineData("Ford", "Focus", "FORD FOCUS")]
    [InlineData("Ford", "Ford Focus ST-Line", "FORD FOCUS ST-LINE")]
    [InlineData("ford", "FORD Focus", "FORD FOCUS")]
    [InlineData("Mini", "Minibus", "MINI MINIBUS")]
    [InlineData(" Ford ", " Focus ", "FORD FOCUS")]
    [InlineData(null, "Focus", "FOCUS")]
    [InlineData("Ford", null, "FORD")]
    [InlineData("Ford", "Ford", "FORD")]
    [InlineData(null, null, "")]
    public void TheSubjectVehicleIsMakeAndModelInCapitals(string? make, string? model, string expected)
    {
        Assert.Equal(expected, UlaInvoiceCalculation.SubjectVehicle(make, model));
    }

    /// <summary>
    /// The issuer block is the Principal's name, then its address lines with
    /// the postcode after the last, as the draft prints "Southport PR9 0AE";
    /// blank address lines are dropped and each line trimmed (R1147).
    /// </summary>
    [Fact]
    public void TheIssuerIsTheNameThenTheAddressWithThePostcodeOnItsLastLine()
    {
        string[] expected = ["United Legal Assistance", "130c Lord Street", "Southport PR9 0AE"];

        Assert.Equal(
            expected,
            UlaInvoiceSnapshot.IssuerLines(
                " United Legal Assistance ", "130c Lord Street\r\n\n  Southport  ", " PR9 0AE "));
    }

    /// <summary>
    /// With no address line the postcode is a line of its own; with neither,
    /// the name alone; with no name, no issuer at all.
    /// </summary>
    [Fact]
    public void TheIssuersPostcodeStandsAloneWithoutAnAddressAndTheNameAloneWithNeither()
    {
        string[] postcodeAlone = ["United Legal Assistance", "PR9 0AE"];
        string[] nameAlone = ["United Legal Assistance"];

        Assert.Equal(postcodeAlone, UlaInvoiceSnapshot.IssuerLines("United Legal Assistance", " \n ", "PR9 0AE"));
        Assert.Equal(nameAlone, UlaInvoiceSnapshot.IssuerLines("United Legal Assistance", null, " "));
        Assert.Empty(UlaInvoiceSnapshot.IssuerLines(" ", "130c Lord Street", "PR9 0AE"));
    }

    /// <summary>An address that already ends with its postcode does not print it twice.</summary>
    [Fact]
    public void AnAddressAlreadyEndingWithItsPostcodeKeepsItsLastLine()
    {
        string[] expected = ["United Legal Assistance", "130c Lord Street", "Southport pr90ae"];

        Assert.Equal(
            expected,
            UlaInvoiceSnapshot.IssuerLines("United Legal Assistance", "130c Lord Street\nSouthport pr90ae", "PR9 0AE"));
    }

    /// <summary>
    /// A frozen report has an invoice to make only with the Principal's
    /// invoice facts, a claim reference to number it by and an outcome with
    /// something to bill (R1143, R1147, R1148). The answer reads stored facts
    /// alone.
    /// </summary>
    [Theory]
    [InlineData(AssessmentReportOutcome.Repairable, true)]
    [InlineData(AssessmentReportOutcome.ContractRepair, true)]
    [InlineData(AssessmentReportOutcome.TotalLoss, false)]
    [InlineData(AssessmentReportOutcome.CashInLieu, false)]
    public void AnInvoiceIsDueOnlyForAnOutcomeWithSomethingToBill(AssessmentReportOutcome outcome, bool due)
    {
        var facts = new UlaInvoiceSnapshot(["United Legal Assistance"], ["Alex Example"], "FORD FOCUS");
        var frozen = AssessmentReportRenderingTests.Snapshot(outcome) with { PrincipalInvoice = facts };

        Assert.Equal(due, UlaInvoiceCalculation.IsDue(frozen));
        // A Principal with no such invoice froze no facts, and an invoice
        // cannot be numbered without a claim reference.
        Assert.False(UlaInvoiceCalculation.IsDue(frozen with { PrincipalInvoice = null }));
        Assert.False(UlaInvoiceCalculation.IsDue(frozen with { YourReference = null }));
        Assert.False(UlaInvoiceCalculation.IsDue(frozen with { YourReference = " " }));
        Assert.False(UlaInvoiceCalculation.IsDue(
            frozen with { Settlement = new ReportSettlement(null), Outcome = AssessmentReportOutcome.ContractRepair }));
    }

    /// <summary>
    /// A Current estimate whose printed components are exactly the figures
    /// given: one panel hour at the labour figure, one part at the parts
    /// figure carrying the materials, and the specialist figure as the
    /// header's other costs, at 20 per cent VAT.
    /// </summary>
    private static ReportRepairCosts Costs(
        RepairerVatStatus status, decimal labour, decimal parts, decimal materials, decimal specialist)
    {
        var estimate = new RepairSpecificationVersion(
            Guid.NewGuid(), Guid.NewGuid(), 1, RepairSpecificationState.Draft,
            new(RepairSpecificationSourceRoute.Manual, null, null, null),
            [
                Line(1, "repair", "Labour", workUnits: 1m, price: null),
                Line(2, "new_part", "Part", workUnits: null, price: parts) with { Materials = materials },
            ],
            "engineer-1", RecordedAtUtc,
            new EstimateDetails("Repairer", labour, specialist, 20m, Vat: EstimateVatPolicy.For(status)),
            IsCurrent: true);
        var costs = ReportRepairCosts.For(estimate);
        Assert.Equal(labour, costs.TotalLabour);
        Assert.Equal(parts, costs.Printed.Parts);
        Assert.Equal(materials, costs.Printed.Materials);
        Assert.Equal(specialist, costs.Printed.Specialist);
        return costs;
    }

    private static CaseEstimateLineRecord Line(
        int position, string type, string description, decimal? workUnits, decimal? price) => new(
            Guid.NewGuid(), position, type, null, description, workUnits, price, false, null, null,
            "case", "Test evidence",
            ActorKind.Staff, "engineer-1", RecordedAtUtc, Quantity: 1);
}
