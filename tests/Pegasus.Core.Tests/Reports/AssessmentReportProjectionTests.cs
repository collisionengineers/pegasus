using System.Globalization;
using System.Security.Cryptography;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Reports;

public sealed class AssessmentReportProjectionTests
{
    private static readonly DateTimeOffset RecordedAtUtc = new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CompleteInputProjectsToARenderableSnapshot()
    {
        var result = AssessmentReportProjection.Project(ReadyInput());

        Assert.True(result.IsReady);
        Assert.Empty(result.Reasons);
        var snapshot = result.Snapshot!;
        Assert.Equal("CE-100", snapshot.OurReference);
        Assert.Equal("P-100", snapshot.YourReference);
        Assert.Equal("Alex Example", snapshot.ClaimantName);
        Assert.Equal(["Approved Principal"], snapshot.ReportFor);
        Assert.Equal("PK12TMZ", snapshot.Vehicle.Registration);
        Assert.Equal("image_based", snapshot.AssessmentMethod);
        Assert.Equal(["Door skin"], snapshot.NewParts);
        Assert.Equal(["Nearside door"], snapshot.Repairs);
        Assert.Equal(["Blend nearside wing"], snapshot.Operations);
        Assert.True(snapshot.Vehicle.VinChecked);
        Assert.Equal("Manual", snapshot.Vehicle.Transmission);
        Assert.Equal("Blue", snapshot.Vehicle.Colour);
        Assert.Equal("Hatchback", snapshot.Vehicle.Body);
        Assert.Equal(new DateOnly(2027, 1, 2), snapshot.Vehicle.TaxExpiry);
        Assert.Equal(new DateOnly(2027, 3, 4), snapshot.Vehicle.MotExpiry);
        Assert.Equal("None", snapshot.Vehicle.AirbagsDeployed);
        Assert.Equal("P0001", snapshot.Vehicle.FaultCodes);
        Assert.True(snapshot.Vehicle.TemporaryRepairsPossible);
        Assert.Equal("Secure bumper", snapshot.Vehicle.TemporaryRepairMethod);
        Assert.Equal(25m, snapshot.Vehicle.TemporaryRepairCost);
        var impact = Assert.Single(snapshot.Damage.Impacts);
        Assert.Equal(new ReportImpact("Right rear", "Moderate", "Quarter panel"), impact);
        Assert.Equal("OK", snapshot.Damage.RightFrontTyre);
        Assert.Equal("Worn", snapshot.Damage.LeftFrontTyre);
        Assert.Equal("Damaged", snapshot.Damage.RightRearTyre);
        Assert.Equal("Illegal", snapshot.Damage.LeftRearTyre);
        Assert.Equal("OK", snapshot.Damage.RightFrontBelt);
        Assert.Equal("Locked", snapshot.Damage.LeftFrontBelt);
        Assert.Equal("Deployed", snapshot.Damage.RightRearBelt);
        Assert.Equal("Not fitted", snapshot.Damage.LeftRearBelt);
        Assert.Equal("Repair kit", snapshot.Damage.SpareTyre);
        Assert.Equal("Not fitted", snapshot.Damage.CentreBelt);
        Assert.Equal("Door scratch", snapshot.Damage.Unrelated);
        Assert.Equal(75m, snapshot.Damage.UnrelatedDeduction);
        Assert.Equal("Red paint", snapshot.Damage.MaterialTransfer);
        Assert.Equal(250m, snapshot.Settlement.Excess);
        Assert.Equal(100m, snapshot.Settlement.Betterment);
        Assert.True(snapshot.Settlement.ClaimantVatRegistered);
        Assert.Equal(6_000m, snapshot.Settlement.Reserve);
        Assert.Equal(4_830m, snapshot.Settlement.Equity);
        Assert.Equal("Parts delay", snapshot.Settlement.RepairDelays);
        Assert.Equal("None", snapshot.Settlement.ReportDelay);
        Assert.Equal(20m, snapshot.Settlement.StoragePerDay);
        Assert.Equal(80m, snapshot.Settlement.Recovery);
        Assert.Equal(new DateOnly(2026, 8, 4), snapshot.Settlement.HireStart);
        Assert.Equal(35m, snapshot.Settlement.HireDailyCost);
        Assert.Equal(200m, snapshot.Settlement.Diminution);
        Assert.Equal("Repairer", snapshot.Settlement.SalvageAt);
        Assert.Equal("Salvage Co", snapshot.Settlement.SalvageAgent);
        Assert.Equal("SAL-1", snapshot.Settlement.SalvageAgentReference);
        Assert.True(snapshot.Settlement.SalvageMoved);
        Assert.False(snapshot.Settlement.SalvageOwnerRetains);
        Assert.True(snapshot.Settlement.SalvageValueAgreed);
        Assert.Equal(new DateOnly(2026, 8, 20), snapshot.Settlement.SalvageSettled);
        Assert.Equal("Ed Mawdsley", snapshot.Signatory.PrintedName);
        Assert.Equal("ATA VDA AQP", snapshot.Signatory.Qualifications);
        Assert.Single(snapshot.Photos);
        Assert.Single(snapshot.Sources);

        // A ready snapshot must also satisfy the renderer's own gate.
        snapshot.Validate();
    }

    [Fact]
    public void UnconfirmedEstimateLineBlocksTheWholeDraftViaTheSharedReadinessRail()
    {
        // The estimate-line grouping never has to filter by confirmation
        // itself: AssessmentPolicy.EvaluatePostReviewReadiness already blocks the
        // whole draft on the first unconfirmed line, of any type.
        var input = ReadyInput();
        var unconfirmed = input.Assessment.EstimateLines[0] with { ConfirmedBy = null, ConfirmedAtUtc = null };
        var withUnconfirmedLine = input with
        {
            Assessment = input.Assessment with
            {
                EstimateLines = [.. input.Assessment.EstimateLines.Skip(1), unconfirmed]
            }
        };

        var result = AssessmentReportProjection.Project(withUnconfirmedLine);

        AssertNotReady(result, $"Estimate line {unconfirmed.Position} ({unconfirmed.Type}) awaits review");
    }

    [Fact]
    public void ReviewTransitionRequirementsAreNotRecalculatedByReportReadiness()
    {
        var input = ReadyInput();
        var assessment = input.Assessment with
        {
            CaseOwned = input.Assessment.CaseOwned with
            {
                Registration = null,
                Make = null,
                Model = null,
                Mileage = null,
                MileageUnit = null,
                IncidentDate = null,
                InstructionDate = null,
                InspectionMode = null,
                InspectionAddress = null
            }
        };

        var result = AssessmentReportProjection.Prepare(
            assessment,
            input.CurrentEstimate,
            input.Signatory);

        Assert.True(result.CanGenerate);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void MissingReviewTransitionDataAtGenerationIsAnInvalidState()
    {
        Assert.Throws<InvalidDataException>(() =>
            AssessmentReportProjection.Project(ReadyInput() with { ClaimantName = null }));
    }

    [Fact]
    public void UnrecognizedInspectionModeAtGenerationIsAnInvalidState()
    {
        var input = ReadyInput();
        Assert.Throws<InvalidDataException>(() => AssessmentReportProjection.Project(
            input with { Assessment = input.Assessment with { CaseOwned = input.Assessment.CaseOwned with { InspectionMode = "Unknown" } } }));
    }

    [Fact]
    public void MissingSignOffEngineerIsNotReady()
    {
        var result = AssessmentReportProjection.Project(ReadyInput() with { Signatory = null });

        AssertNotReady(result, "Sign-off Engineer");
    }

    [Theory]
    [InlineData("", true, "image/png")]
    [InlineData("Ed Mawdsley", false, "image/png")]
    [InlineData("Ed Mawdsley", true, "image/gif")]
    public void IncompleteSignOffEngineerIsNotReady(
        string printedName,
        bool hasSignature,
        string contentType)
    {
        var result = AssessmentReportProjection.Project(ReadyInput() with
        {
            Signatory = new ReportSignatory(
                printedName,
                "ATA VDA AQP",
                hasSignature ? [1, 2, 3] : [],
                contentType),
        });

        AssertNotReady(result, "Sign-off Engineer");
    }

    [Fact]
    public void BlankQualificationsAreRetainedAsAbsent()
    {
        var result = AssessmentReportProjection.Project(ReadyInput() with
        {
            Signatory = new ReportSignatory("Neil O'Reilly", " ", [1, 2, 3], "image/png"),
        });

        Assert.True(result.IsReady);
        Assert.Equal("Neil O'Reilly", result.Snapshot!.Signatory.PrintedName);
        Assert.Null(result.Snapshot.Signatory.Qualifications);
    }

    [Fact]
    public void UnconfirmedAssessmentFieldSurfacesFromTheSharedReadinessRail()
    {
        var input = ReadyInput();
        var mutatedFields = input.Assessment.Fields
            .Select(field => field.Path == AssessmentVocabulary.Outcome
                ? field with { ConfirmedBy = null, ConfirmedAtUtc = null }
                : field)
            .ToArray();
        var result = AssessmentReportProjection.Project(
            input with { Assessment = input.Assessment with { Fields = mutatedFields } });

        AssertNotReady(result, $"{AssessmentVocabulary.Outcome} awaits review");
    }

    [Fact]
    public void MissingRepairCostsIsNotReadyNamingTheAcceptedFormulaGap()
    {
        // There is no hand-typed cost path: without a Current estimate the
        // draft fails closed naming the missing estimate (EXT-09).
        var result = AssessmentReportProjection.Project(ReadyInput() with { CurrentEstimate = null });

        var reason = AssertNotReady(result, AssessmentReportProjection.RepairCostRequirement);
        Assert.Contains("EXT-09", reason.WhyOutstanding, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCurrentEstimateSuppliesTheCanonicalBreakdownAndTheLists()
    {
        var estimate = CurrentEstimate(new("Repairer", 2, 45m, 60m, 15m, 5m, null, Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)));
        var input = ReadyInput() with { CurrentEstimate = estimate };

        var result = AssessmentReportProjection.Project(input);

        Assert.Empty(result.Reasons);
        var totals = EstimateTotals.Compute(estimate);
        var costs = result.Snapshot!.Costs;
        Assert.Equal(45m, costs.HourlyRate);
        Assert.Equal(3m, costs.LabourHours);
        Assert.Equal(2.5m, costs.PaintHours);
        // The one owner of estimate money is carried, never re-derived.
        Assert.Equal(totals.Printed, costs.Printed);
        Assert.Equal(totals.Printed.Gross, costs.Total);
        // 5 % from the estimate, not a built-in 20 % rule, and charged on the
        // unrounded taxable base rather than on the printed net (B04).
        Assert.Equal(5m, costs.VatPercent);
        Assert.Equal("VAT (5%)", costs.VatLabel);
        Assert.Equal(
            decimal.Round(totals.Raw.Taxable * 0.05m, 2, MidpointRounding.AwayFromZero),
            costs.Printed.Vat);
        Assert.Equal(["Bonnet"], result.Snapshot.NewParts);
        Assert.Equal(["Repair wing"], result.Snapshot.Repairs);
        Assert.Equal(["Paint wing"], result.Snapshot.Operations);
        Assert.Equal(2, result.Snapshot.Settlement.RepairDays);
        result.Snapshot.Validate();
    }

    /// <summary>
    /// ENG-039: the report's cost block is the printed breakdown itself, row
    /// for row, even when the estimate carries discounts and charges VAT on
    /// only some of its categories. The report never re-derives a figure and
    /// never reads a flattened projection of one.
    /// </summary>
    [Fact]
    public void TheReportsCostsAreThePrintedBreakdownRowForRow()
    {
        var estimate = CurrentEstimate(new EstimateDetails(
            "Repairer", 2, 45m, 60m, 15m, 20m, null,
            new EstimateDiscounts(0.1m, 0.05m, 0.125m, 0.025m),
            new EstimateVatPolicy(
                RepairerVatStatus.NotRegistered,
                EstimateVatCategories.Parts | EstimateVatCategories.Materials,
                false),
            null));

        var costs = AssessmentReportProjection
            .Project(ReadyInput() with { CurrentEstimate = estimate })
            .Snapshot!.Costs;

        var totals = EstimateTotals.Compute(estimate);
        var printed = totals.Printed;
        Assert.Equal(printed.Parts, costs.Printed.Parts);
        Assert.Equal(printed.PanelLabour, costs.Printed.PanelLabour);
        Assert.Equal(printed.PaintLabour, costs.Printed.PaintLabour);
        Assert.Equal(printed.Materials, costs.Printed.Materials);
        Assert.Equal(printed.Specialist, costs.Printed.Specialist);
        Assert.Equal(printed.Net, costs.Printed.Net);
        Assert.Equal(printed.Vat, costs.Printed.Vat);
        Assert.Equal(printed.Gross, costs.Total);
        // The categories the repairer's position charges are the ones taxed:
        // the panel and paint labour this estimate carries is not.
        Assert.True(printed.PanelLabour + printed.PaintLabour > 0m);
        Assert.Equal(
            decimal.Round(
                (totals.Raw.Parts + totals.Raw.Materials) * 20m / 100m,
                2,
                MidpointRounding.AwayFromZero),
            costs.Printed.Vat);
        costs.Validate();
    }

    [Fact]
    public void ThePrintedComponentsReconcileToThePrintedTotal()
    {
        var costs = AssessmentReportProjection
            .Project(ReadyInput() with
            {
                CurrentEstimate = CurrentEstimate(
                    new("Repairer", 2, 45m, 60m, 15m, 20m, null, Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered))),
            })
            .Snapshot!.Costs;

        Assert.Equal(
            costs.Printed.Net,
            costs.Printed.Parts + costs.Printed.PanelLabour + costs.Printed.PaintLabour
                + costs.Printed.Materials + costs.Printed.Specialist);
        Assert.Equal(costs.Printed.Gross, costs.Printed.Net + costs.Printed.Vat);
        costs.Validate();
    }

    [Fact]
    public void TheRetiredD18EngineerFieldsAreNoLongerReadinessItems()
    {
        // B02 removed the D18 name/qualifications/signature items: the
        // selected sign-off account owns those facts now.
        var input = ReadyInput();
        var withoutD18 = input.Assessment.Fields
            .Where(field => field.Path is not (AssessmentVocabulary.EngineerName
                or AssessmentVocabulary.EngineerQualifications
                or AssessmentVocabulary.EngineerSignature))
            .ToArray();

        var result = AssessmentReportProjection.Project(
            input with { Assessment = input.Assessment with { Fields = withoutD18 } });

        Assert.True(result.IsReady);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void AReportDateIsSetOnlyWhenOneIsStated()
    {
        Assert.Throws<InvalidDataException>(() =>
            AssessmentReportProjection.Project(ReadyInput() with { ReportDate = null }));
    }

    [Fact]
    public void ARecordedOverrideWinsOverTheGenerationDate()
    {
        var input = ReadyInput();
        var fields = input.Assessment.Fields
            .Append(Field(AssessmentVocabulary.ReportDateOverride, "true"))
            .Append(Field(AssessmentVocabulary.ReportDate, "2026-07-04"))
            .ToArray();

        var result = AssessmentReportProjection.Project(
            input with { Assessment = input.Assessment with { Fields = fields } });

        Assert.Equal(new DateOnly(2026, 7, 4), result.Snapshot!.ReportDate);
        Assert.True(result.Snapshot.ReportDateOverridden);
    }

    [Fact]
    public void PersistedDatesParseUnderANonGregorianCulture()
    {
        // ENG-037: a th-TH workstation reads a Buddhist-calendar year unless
        // the invariant culture is stated at every persisted-date parse.
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");
        try
        {
            var snapshot = AssessmentReportProjection.Project(ReadyInput()).Snapshot!;

            Assert.Equal(new DateOnly(2027, 1, 2), snapshot.Vehicle.TaxExpiry);
            Assert.Equal(new DateOnly(2027, 3, 4), snapshot.Vehicle.MotExpiry);
            Assert.Equal(new DateOnly(2026, 8, 3), snapshot.Assessed);
            Assert.Equal(new DateOnly(2026, 8, 20), snapshot.Settlement.SalvageSettled);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void TheThreeReportContentChoicesAreIndependentAndDefaultOff(
        bool discloseGuideSource, bool includeUnrelatedDamage)
    {
        var input = ReadyInput();
        var fields = input.Assessment.Fields
            .Append(Field(
                AssessmentVocabulary.ReportDiscloseGuideSource,
                discloseGuideSource ? "true" : "false"))
            .Append(Field(
                AssessmentVocabulary.ReportIncludeUnrelatedDamage,
                includeUnrelatedDamage ? "true" : "false"))
            .ToArray();

        var snapshot = AssessmentReportProjection.Project(input with
        {
            Assessment = input.Assessment with { Fields = fields },
            Guides = new ReportGuideSources([ValuationSource.Glasses]),
        }).Snapshot!;

        Assert.Equal(discloseGuideSource, snapshot.Content.DiscloseGuideSource);
        Assert.Equal(includeUnrelatedDamage, snapshot.Content.IncludeUnrelatedDamage);
        Assert.False(snapshot.Content.IncludeValuationCommentary);
        Assert.Equal(discloseGuideSource, snapshot.PrintsGuideDisclosure);
    }

    [Fact]
    public void TheGuideSentenceIsOmittedWhenNoGlassesGuideWasUsed()
    {
        var input = ReadyInput();
        var fields = input.Assessment.Fields
            .Append(Field(AssessmentVocabulary.ReportDiscloseGuideSource, "true"))
            .ToArray();

        var snapshot = AssessmentReportProjection.Project(input with
        {
            Assessment = input.Assessment with { Fields = fields },
            Guides = new ReportGuideSources([ValuationSource.Cazana]),
        }).Snapshot!;

        Assert.True(snapshot.Content.DiscloseGuideSource);
        Assert.False(snapshot.PrintsGuideDisclosure);
    }

    [Fact]
    public void EquitySubtractsRepairAfterBettermentAndSalvageButNotExcess()
    {
        var input = ReadyInput();
        var fields = input.Assessment.Fields
            .Append(Field(AssessmentVocabulary.SalvageValue, "500.00"))
            .ToArray();

        var result = AssessmentReportProjection.Project(
            input with { Assessment = input.Assessment with { Fields = fields } });

        Assert.Equal(4_330m, result.Snapshot!.Settlement.Equity);
        Assert.Equal(250m, result.Snapshot.Settlement.Excess);
    }

    [Fact]
    public void ACurrentEstimateWithoutALabourRateIsNotReady()
    {
        var estimate = CurrentEstimate(new("Repairer", null, null, null, null, 20m, null));

        var result = AssessmentReportProjection.Project(
            ReadyInput() with { CurrentEstimate = estimate });

        AssertNotReady(result, AssessmentReportProjection.LabourRateRequirement);
    }

    /// <summary>
    /// The Current estimate every ready input carries: 50 parts, five panel
    /// hours at 30, 20 materials and 5 specialist, giving a printed net of
    /// 225, 20 per cent VAT of 45 and a printed gross of 270.
    /// </summary>
    private static RepairSpecificationVersion DefaultCurrentEstimate() => new(
        Guid.NewGuid(), Guid.NewGuid(), 2, RepairSpecificationState.Accepted,
        new(RepairSpecificationSourceRoute.Manual, null, null, null),
        [
            Line(1, "repair", "Nearside door") with { WorkUnits = 5m, Price = null },
            Line(2, "new_part", "Door skin") with { WorkUnits = null, Price = 50m, Quantity = 1 },
            Line(3, "paint_blend", "Blend nearside wing") with { WorkUnits = null, Price = null },
        ],
        null, "engineer-1", RecordedAtUtc, "engineer-1", RecordedAtUtc, null, null,
        new EstimateDetails("Repairer", null, 30m, 20m, 5m, 20m, null, Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
        IsCurrent: true);

    private static RepairSpecificationVersion CurrentEstimate(EstimateDetails details) => new(
        Guid.NewGuid(), Guid.NewGuid(), 2, RepairSpecificationState.Accepted,
        new(RepairSpecificationSourceRoute.Manual, null, null, null),
        [
            Line(1, "new_part", "Bonnet") with { WorkUnits = null, Price = 310m, Quantity = 1 },
            Line(2, "repair", "Repair wing") with { WorkUnits = 3m },
            Line(3, "paint_repair", "Paint wing") with { WorkUnits = null, PaintWorkUnits = 2.5m },
        ],
        null, "engineer-1", RecordedAtUtc, "engineer-1", RecordedAtUtc, null, null, details, IsCurrent: true);

    private static AssessmentReadinessItem AssertNotReady(
        AssessmentReportProjectionResult result, string requirement)
    {
        Assert.False(result.IsReady);
        Assert.Null(result.Snapshot);
        var reason = Assert.Single(result.Reasons, item => item.Requirement == requirement);
        return reason;
    }

    /// <summary>The complete Review assessment the report fixtures share.</summary>
    internal static CaseAssessmentProjection ReadyAssessment() => ReadyInput().Assessment;

    /// <summary>The Current estimate the report fixtures share.</summary>
    internal static RepairSpecificationVersion ReadyCurrentEstimate() => DefaultCurrentEstimate();

    private static AssessmentReportProjectionInput ReadyInput()
    {
        var image = new byte[] { 137, 80, 78, 71, 1, 2, 3, 4 };
        var photo = new ReportImageEvidence(
            "site.jpg", "image/jpeg", image, Convert.ToHexStringLower(SHA256.HashData(image)));
        var source = new AcceptedReportSource("instruction.pdf", "1", new string('a', 64));

        var fields = new[]
        {
            Field(AssessmentVocabulary.VehicleType, "car"),
            Field(AssessmentVocabulary.VehicleYear, "2012"),
            Field(AssessmentVocabulary.VehicleMileageSource, "online_data"),
            Field(AssessmentVocabulary.VehicleCondition, "good"),
            Field(AssessmentVocabulary.VehicleVin, "VIN12345"),
            Field(AssessmentVocabulary.VehicleEngineCc, "1600"),
            Field(AssessmentVocabulary.VehicleFuel, "Petrol"),
            Field(AssessmentVocabulary.VehicleVinChecked, "true"),
            Field(AssessmentVocabulary.VehicleTransmission, "manual"),
            Field(AssessmentVocabulary.VehicleColour, "Blue"),
            Field(AssessmentVocabulary.VehicleBody, "Hatchback"),
            Field(AssessmentVocabulary.VehicleTaxExpiry, "2027-01-02"),
            Field(AssessmentVocabulary.VehicleMotExpiry, "2027-03-04"),
            Field(AssessmentVocabulary.VehicleAirbagsDeployed, "None"),
            Field(AssessmentVocabulary.VehicleFaultCodes, "P0001"),
            Field(AssessmentVocabulary.VehicleTemporaryRepairsPossible, "true"),
            Field(AssessmentVocabulary.VehicleTemporaryRepairMethod, "Secure bumper"),
            Field(AssessmentVocabulary.VehicleTemporaryRepairCost, "25.00"),
            Field(AssessmentVocabulary.IncidentAssessed, "2026-08-03"),
            Field(AssessmentVocabulary.ImpactSeverity, "moderate"),
            Field(AssessmentVocabulary.ImpactLocation, "right_rear"),
            Field(AssessmentVocabulary.DamageImpacts, "[{\"zone\":\"right_rear\",\"severity\":\"moderate\",\"note\":\"Quarter panel\"}]"),
            Field(AssessmentVocabulary.DamageTyreRightFront, "ok"),
            Field(AssessmentVocabulary.DamageTyreLeftFront, "worn"),
            Field(AssessmentVocabulary.DamageTyreRightRear, "damaged"),
            Field(AssessmentVocabulary.DamageTyreLeftRear, "illegal"),
            Field(AssessmentVocabulary.DamageBeltRightFront, "ok"),
            Field(AssessmentVocabulary.DamageBeltLeftFront, "locked"),
            Field(AssessmentVocabulary.DamageBeltRightRear, "deployed"),
            Field(AssessmentVocabulary.DamageBeltLeftRear, "not_fitted"),
            Field(AssessmentVocabulary.DamageSpareTyre, "repair_kit"),
            Field(AssessmentVocabulary.DamageCentreBelt, "not_fitted"),
            Field(AssessmentVocabulary.DamageUnrelated, "Door scratch"),
            Field(AssessmentVocabulary.DamageUnrelatedDeduction, "75.00"),
            Field(AssessmentVocabulary.DamageMaterialTransfer, "Red paint"),
            Field(AssessmentVocabulary.ValueRetail, "5000.00"),
            Field(AssessmentVocabulary.ValueTrade, "4000.00"),
            Field(AssessmentVocabulary.ValueEngineer, "5000.00"),
            Field(AssessmentVocabulary.CostRepairerVatRegistered, "true"),
            Field(AssessmentVocabulary.Outcome, "repairable"),
            Field(AssessmentVocabulary.LegalStatus, "roadworthy"),
            Field(AssessmentVocabulary.HistoryCheck, "History clear"),
            Field(AssessmentVocabulary.EngineersComments, "No further comments"),
            Field(AssessmentVocabulary.EngineerName, "A Patterson"),
            Field(AssessmentVocabulary.EngineerQualifications, "M.Inst.IAEA"),
            Field(AssessmentVocabulary.EngineerSignature, "andy_patterson"),
            Field(AssessmentVocabulary.AgreedFee, "120.00"),
            Field(AssessmentVocabulary.FeeDescriptionLines, "Engineering assessment"),
            Field(AssessmentVocabulary.SettlementExcess, "250.00"),
            Field(AssessmentVocabulary.SettlementBetterment, "100.00"),
            Field(AssessmentVocabulary.SettlementClaimantVatRegistered, "true"),
            Field(AssessmentVocabulary.SettlementReserve, "6000.00"),
            Field(AssessmentVocabulary.SettlementRepairDelays, "Parts delay"),
            Field(AssessmentVocabulary.SettlementReportDelay, "None"),
            Field(AssessmentVocabulary.SettlementStoragePerDay, "20.00"),
            Field(AssessmentVocabulary.CostRecoveryCharge, "80.00"),
            Field(AssessmentVocabulary.SettlementHireStart, "2026-08-04"),
            Field(AssessmentVocabulary.SettlementHireDailyCost, "35.00"),
            Field(AssessmentVocabulary.SettlementDiminution, "200.00"),
            Field(AssessmentVocabulary.SettlementSalvageAt, "Repairer"),
            Field(AssessmentVocabulary.SettlementSalvageAgent, "Salvage Co"),
            Field(AssessmentVocabulary.SettlementSalvageAgentReference, "SAL-1"),
            Field(AssessmentVocabulary.SettlementSalvageMoved, "true"),
            Field(AssessmentVocabulary.SettlementSalvageOwnerRetains, "false"),
            Field(AssessmentVocabulary.SettlementSalvageValueAgreed, "true"),
            Field(AssessmentVocabulary.SettlementSalvageSettled, "2026-08-20"),
        };

        var estimateLines = new[]
        {
            Line(1, "repair", "Nearside door"),
            Line(2, "new_part", "Door skin"),
            Line(3, "paint_blend", "Blend nearside wing"),
        };

        var caseOwned = new AssessmentCaseOwnedData(
            Registration: "PK12TMZ",
            Make: "Ford",
            Model: "Focus",
            Mileage: 80_000,
            MileageUnit: "miles",
            IncidentDate: new DateOnly(2026, 8, 1),
            InstructionDate: new DateOnly(2026, 8, 2),
            InspectionMode: "ImageBasedAssessment",
            InspectionAddress: null);

        var assessment = new CaseAssessmentProjection(
            Guid.NewGuid(),
            "CE-100",
            0,
            CaseLifecycleState.Review,
            Guid.NewGuid(),
            fields,
            estimateLines,
            caseOwned);

        return new AssessmentReportProjectionInput(
            assessment,
            ClaimantName: "Alex Example",
            OurReference: "CE-100",
            YourReference: "P-100",
            ReportFor: ["Approved Principal"],
            ReportDate: new DateOnly(2026, 8, 19),
            Photos: [photo],
            Sources: [source],
            CurrentEstimate: DefaultCurrentEstimate(),
            Signatory: new ReportSignatory("Ed Mawdsley", "ATA VDA AQP", [1, 2, 3], "image/png"));
    }

    private static AssessmentFieldValue[] ReplaceField(
        IReadOnlyList<AssessmentFieldValue> fields, string path, string value) =>
        fields.Select(field => field.Path == path ? field with { Value = value } : field).ToArray();

    private static AssessmentFieldValue Field(string path, string value) => new(
        path, value, ActorKind.Staff, "engineer-1", RecordedAtUtc, "engineer-1", RecordedAtUtc);

    private static CaseEstimateLineRecord Line(int position, string type, string description) => new(
        Guid.NewGuid(), position, type, null, description, 2.5m, null, false, null, null,
        "confirmed", "case", "Test evidence",
        ActorKind.Staff, "engineer-1", RecordedAtUtc, "engineer-1", RecordedAtUtc);
}
