using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

public sealed class EstimateDocumentRenderingTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ForMapsEveryDocumentColumnAdjustmentAndCompletenessFact()
    {
        var estimate = Estimate(
            Details(
                rate: 50m,
                otherCosts: 8.76m,
                vat: EstimateVatPolicy.For(RepairerVatStatus.Unknown)),
            Line("new_part", 1, description: null, guideCode: "PANEL", partNumber: "PN-42",
                quantity: null, workUnits: 0.25m, materials: 3.21m, price: 20m),
            Line("specialist_fixed", 2, description: "Tyre", workUnits: 1m, price: null, unpriced: true));

        var snapshot = EstimateDocumentSnapshot.For(
            estimate, "QDOS26001", "CLAIM-1", new(2026, 9, 16),
            "Alex Example", "Ford Focus", "AB12 CDE");

        Assert.Equal(3.21m, snapshot.Totals.Printed.Materials);
        Assert.Equal(8.76m, snapshot.OtherCosts);
        Assert.True(snapshot.VatTreatmentPending);
        Assert.Equal(1, snapshot.UnpricedItemCount);
        Assert.Collection(snapshot.Lines,
            line =>
            {
                Assert.Equal(1, line.Quantity);
                Assert.Equal("PANEL", line.Description);
                Assert.Equal("PN-42", line.PartNumber);
                Assert.Equal(EstimateOperation.Replace, line.Operation);
                Assert.Equal(0.25m, line.Hours);
                Assert.Equal(3.21m, line.Materials);
                Assert.Equal(20m, line.UnitPrice);
            },
            line =>
            {
                Assert.Equal(EstimateOperation.Specialist, line.Operation);
                Assert.True(line.Unpriced);
            });
        snapshot.Validate();
    }

    [Fact]
    public void HoursSummaryUsesTheSinglePricedHoursClassifier()
    {
        var estimate = Estimate(
            Details(rate: 83.28m),
            Line("new_part", 1, workUnits: 0.2m),
            Line("repair", 2, workUnits: 10m),
            Line("rnr", 3, workUnits: 2.1m),
            Line("paint_repair", 4, workUnits: 0.3m, paintWorkUnits: 4m),
            Line("paint_blend", 5, workUnits: 0.2m, paintWorkUnits: 0.7m),
            Line("specialist_wu", 6, workUnits: 4m),
            Line("check_labour", 7, workUnits: 1m),
            Line("specialist_fixed", 8, workUnits: 5m, price: 180m));

        var snapshot = Snapshot(estimate);

        Assert.Equal(4.3m, snapshot.Hours.PaintTotal);
        Assert.Equal(0.9m, snapshot.Hours.BlendTotal);
        Assert.Equal(22.5m, snapshot.Hours.PricedTotal);
        Assert.Equal(5m, snapshot.Hours.UnpricedSpecialist);
        Assert.Equal(1_873.80m, snapshot.Hours.PricedTotal * snapshot.HourlyRate);
        Assert.Equal(
            snapshot.Totals.Raw.PanelLabour + snapshot.Totals.Raw.PaintLabour,
            snapshot.Hours.PricedTotal * snapshot.HourlyRate);
    }

    [Fact]
    public void WorkUnitSpecialistHoursReproduceTheEvaLabourFigure()
    {
        var estimate = Estimate(
            Details(rate: 83.28m),
            Line("repair", 1, workUnits: 16m),
            Line("specialist_wu", 2, workUnits: 6m));

        var snapshot = Snapshot(estimate);

        Assert.Equal(22m, snapshot.Hours.PricedTotal);
        Assert.Equal(1_832.16m, snapshot.Hours.PricedTotal * snapshot.HourlyRate);
        Assert.Equal(
            1_832.16m,
            snapshot.Totals.Raw.PanelLabour + snapshot.Totals.Raw.PaintLabour);
    }

    [Fact]
    public void GoldenFixedSpecialistFixturePinsTheCorrectNetArithmetic()
    {
        var estimate = Estimate(
            Details(rate: 83.28m),
            EvaEstimateLines("specialist_fixed"));

        var totals = EstimateTotals.Compute(estimate);
        var hours = EstimateHours.Of(estimate);

        Assert.Equal(1_415.76m, totals.Printed.PanelLabour + totals.Printed.PaintLabour);
        Assert.Equal(510.58m, totals.Printed.Materials);
        Assert.Equal(160.63m, totals.Printed.Parts);
        Assert.Equal(531.02m, totals.Printed.Specialist);
        Assert.Equal(2_617.99m, totals.Printed.Net);
        Assert.Equal(523.60m, totals.Printed.Vat);
        Assert.Equal(3_141.59m, totals.Printed.Gross);
        Assert.Equal(17m, hours.PricedTotal);
        Assert.Equal(5m, hours.UnpricedSpecialist);
        Assert.Equal(5, totals.OffPattern.Count(anomaly => anomaly.Field == "hours"));
    }

    [Fact]
    public void GoldenCheckLabourFixturePinsTheCorrectlyTypedDocumentTotals()
    {
        var snapshot = Snapshot(Estimate(
            Details(rate: 83.28m),
            EvaEstimateLines("check_labour")));

        Assert.Equal(1_832.16m, snapshot.Totals.Printed.PanelLabour + snapshot.Totals.Printed.PaintLabour);
        Assert.Equal(22m, snapshot.Hours.PricedTotal);
        Assert.Equal(3_034.39m, snapshot.Totals.Printed.Net);
    }

    [Fact]
    public void AcceptedAndDiscardedAcceptedEstimateUseFrozenRecordedTotals()
    {
        var original = Estimate(Details(rate: 40m), Line("repair", 1, workUnits: 2m));
        var recorded = EstimateTotals.Compute(original);
        var discarded = original with
        {
            State = RepairSpecificationState.Discarded,
            Details = Details(rate: 100m),
            RecordedTotals = recorded,
        };

        var snapshot = Snapshot(discarded);

        Assert.Same(recorded, snapshot.Totals);
        Assert.Equal("DISCARDED", snapshot.Status);
        Assert.NotEqual(EstimateTotals.Compute(discarded).Printed, snapshot.Totals.Printed);
    }

    [Fact]
    public void DraftAlwaysProjectsItsLiveCalculationEvenIfARecordedValueIsPresent()
    {
        var draft = Estimate(Details(rate: 40m), Line("repair", 1, workUnits: 2m));
        var obsolete = EstimateTotals.Compute(draft) with
        {
            Printed = EstimateTotals.Compute(draft).Printed with { Net = 999m },
        };

        var snapshot = Snapshot(draft with { RecordedTotals = obsolete });

        Assert.NotSame(obsolete, snapshot.Totals);
        Assert.Equal(80m, snapshot.Totals.Printed.Net);
    }

    [Fact]
    public void AcceptedNonCurrentHasAnExplicitStatus()
    {
        var draft = Estimate(Details(rate: 40m), Line("repair", 1, workUnits: 2m));
        var accepted = draft with
        {
            State = RepairSpecificationState.Accepted,
            IsCurrent = false,
            RecordedTotals = EstimateTotals.Compute(draft),
        };

        Assert.Equal("ACCEPTED", Snapshot(accepted).Status);
    }

    [Theory]
    [InlineData("lines")]
    [InlineData("rate")]
    [InlineData("description")]
    [InlineData("type")]
    [InlineData("reference")]
    [InlineData("registration")]
    [InlineData("reconciliation")]
    public void ValidateFailsClosedForEachDocumentRequirement(string fault)
    {
        var estimate = Estimate(Details(rate: 40m), Line("repair", 1, workUnits: 1m));
        var snapshot = Snapshot(estimate);
        snapshot = fault switch
        {
            "lines" => snapshot with { Lines = [] },
            "rate" => snapshot with { HourlyRate = 0m },
            "description" => snapshot with
            {
                Lines = [snapshot.Lines[0] with { Description = string.Empty }],
            },
            "type" => snapshot with
            {
                Lines = [snapshot.Lines[0] with { LineType = "unsupported" }],
            },
            "reference" => snapshot with { OurReference = string.Empty },
            "registration" => snapshot with { Registration = string.Empty },
            "reconciliation" => snapshot with
            {
                Totals = snapshot.Totals with
                {
                    Printed = snapshot.Totals.Printed with { Net = snapshot.Totals.Printed.Net + 0.01m },
                },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };

        Assert.Throws<ReportRenderRejectedException>(snapshot.Validate);
    }

    [Fact]
    public void PartsOnlyEstimateStillRequiresThePrintedLabourRate()
    {
        var snapshot = Snapshot(Estimate(Details(rate: 0m), Line("new_part", 1, price: 25m)));

        Assert.Throws<ReportRenderRejectedException>(snapshot.Validate);
    }

    private static EstimateDocumentSnapshot Snapshot(RepairSpecificationVersion estimate) =>
        EstimateDocumentSnapshot.For(
            estimate, "QDOS26001", "CLAIM-1", new(2026, 9, 16),
            "Alex Example", "Ford Focus", "AB12 CDE");

    private static EstimateDetails Details(
        decimal? rate,
        decimal? otherCosts = null,
        EstimateVatPolicy? vat = null) => new(
        "Estimate 1", rate, otherCosts, 20m,
        EstimateDiscounts.None,
        vat ?? EstimateVatPolicy.For(RepairerVatStatus.Registered));

    private static RepairSpecificationVersion Estimate(
        EstimateDetails details,
        params CaseEstimateLineRecord[] lines) => new(
        Guid.NewGuid(), CaseId, 1, RepairSpecificationState.Draft,
        new(RepairSpecificationSourceRoute.Manual, null, null, null),
        lines, null, "engineer", Now, null, null, null, null, details);

    private static CaseEstimateLineRecord[] EvaEstimateLines(string timedOperationType) =>
    [
        Line("new_part", 1, "Right Front Door Membrane", workUnits: 0.1m, price: 103.18m),
        Line("new_part", 2, "Right Front Door Protective Moulding", price: 43.19m),
        Line("rnr", 3, "Rear Bumper Lining", workUnits: 0.8m),
        Line("repair", 4, "Right Rear Side Panel", workUnits: 10m),
        Line("new_part", 5, "Right Side Panel Protective Moulding", workUnits: 0.1m, price: 14.26m),
        Line("rnr", 6, "Right Front Door Strip & Set-Up for Paint", workUnits: 1.3m),
        Line("specialist_fixed", 7, ".Assessment Damage Appraisal Charge", price: 176.96m),
        Line("specialist_fixed", 8, ".Environmental Charge", price: 31.23m),
        Line(timedOperationType, 9, ".QC & Road Test", workUnits: 1m),
        Line(timedOperationType, 10, ".Standard shutdown", workUnits: 1m),
        Line("specialist_fixed", 11, ".Sundries", price: 20m),
        Line(timedOperationType, 12, ".System Diagnostic Check (Post Repair)", workUnits: 1m),
        Line(timedOperationType, 13, ".System Diagnostic Check (Pre Repair)", workUnits: 1m),
        Line("specialist_fixed", 14, ".Vehicle Care Kit", price: 10.41m),
        Line(timedOperationType, 15, ".Wash/Clean", workUnits: 1m),
        Line("specialist_fixed", 16, "OSR tyre", price: 180m),
        Line("specialist_fixed", 17, "Wheel Alignment (check)", price: 112.42m),
        Line("paint_repair", 18, "Right Front Door, Complete", paintWorkUnits: 0.8m, materials: 191.20m),
        Line("paint_repair", 19, "Right Rear Side Panel, Complete", paintWorkUnits: 1.6m, materials: 187.60m),
        Line("paint_prep", 20, "Prep. metal (on vehicle without pre-painting)", paintWorkUnits: 1.7m, materials: 120.16m),
        Line("paint_repair", 21, "Colour mixing (1)", paintWorkUnits: 0.3m),
        Line("paint_repair", 22, "Sample colour creation (1)", paintWorkUnits: 0.3m, materials: 11.62m),
    ];

    private static CaseEstimateLineRecord Line(
        string type,
        int position,
        string? description = "Line",
        string? guideCode = null,
        string? partNumber = null,
        int? quantity = 1,
        decimal? workUnits = null,
        decimal? paintWorkUnits = null,
        decimal? materials = null,
        decimal? price = null,
        bool unpriced = false) => new(
        Guid.NewGuid(), position, type, guideCode, description, workUnits, price, unpriced,
        partNumber, null, null, null, null, ActorKind.Staff, "engineer", Now,
        paintWorkUnits, quantity, materials);
}
