using System.Globalization;
using System.Security.Cryptography;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
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
        // The claimant, the claim reference and the dates are the Case's own
        // facts: instructions were received on the Case's received date, and
        // the damage was assessed on its Inspection date.
        Assert.Equal("P-100", snapshot.YourReference);
        Assert.Equal("Alex Example", snapshot.ClaimantName);
        Assert.Equal(new DateOnly(2026, 8, 2), snapshot.InstructionsReceived);
        Assert.Equal(new DateOnly(2026, 8, 3), snapshot.Assessed);
        // The adoption's basis retail and trade.
        Assert.Equal(5000m, snapshot.RetailValue);
        Assert.Equal(4000m, snapshot.TradeValue);
        Assert.Equal(["Approved Principal"], snapshot.ReportFor);
        Assert.Equal("PK12TMZ", snapshot.Vehicle.Registration);
        Assert.Equal("image_based", snapshot.AssessmentMethod);
        Assert.Equal(["Door skin"], snapshot.NewParts);
        Assert.Equal(["Nearside door"], snapshot.Repairs);
        Assert.Equal(["Blend nearside wing"], snapshot.Operations);
        // The vehicle facts the template prints, as recorded. The Case also
        // records a transmission, colour, body, expiry dates, airbags, tyres,
        // belts and settlement facts, and the report carries none of them.
        Assert.Equal(
            new ReportVehicle(
                "PK12TMZ", "Ford", "Focus", "2012", "good", "80,000 miles", "online_data",
                "VIN12345", "1600", "Petrol"),
            snapshot.Vehicle);
        var impact = Assert.Single(snapshot.Damage.Impacts);
        Assert.Equal(["right_rear"], impact.Codes);
        // Each damage keeps its own severity, which shades its disc.
        Assert.Equal("moderate", impact.Severity);
        // The disc the operator drew travels to the report as drawn.
        Assert.Equal(new DamageDisc(0.86, 0.86, 0.1), impact.Disc);
        Assert.Equal("Door scratch", snapshot.Damage.Unrelated);
        Assert.Equal(DamagePlanGeometry.Car, snapshot.Damage.Profile);
        // Only a contract repair carries an agreed sum.
        Assert.Equal(new ReportSettlement(), snapshot.Settlement);
        Assert.Equal("Ed Mawdsley", snapshot.Signatory.PrintedName);
        Assert.Equal("ATA VDA AQP", snapshot.Signatory.Qualifications);
        Assert.Single(snapshot.Photos);
        Assert.Single(snapshot.Sources);

        // A ready snapshot must also satisfy the renderer's own gate.
        snapshot.Validate();
    }

    /// <summary>
    /// An unroadworthy vehicle's report carries the reason and says the
    /// vehicle is unroadworthy. Its temporary repairs are recorded on the Case
    /// and printed nowhere.
    /// </summary>
    /// <summary>
    /// The report marks the damage on the recorded vehicle's drawing (28
    /// September 2026): a van on the van, a motorcycle or scooter on the
    /// motorbike, anything else on the car.
    /// </summary>
    [Theory]
    [InlineData("van", DamagePlanGeometry.Van)]
    [InlineData("motorcycle", DamagePlanGeometry.Motorbike)]
    [InlineData("scooter", DamagePlanGeometry.Motorbike)]
    [InlineData("caravan", DamagePlanGeometry.Car)]
    public void TheDamageIsDrawnOnTheRecordedVehicle(string vehicleType, string profile)
    {
        var input = ReadyInput();
        var fields = ReplaceField(input.Assessment.Fields, AssessmentVocabulary.VehicleType, vehicleType);

        var snapshot = AssessmentReportProjection.Project(
            input with { Assessment = input.Assessment with { Fields = fields } }).Snapshot!;

        Assert.Equal(profile, snapshot.Damage.Profile);
    }

    [Fact]
    public void AnUnroadworthyVehicleCarriesItsReasonAndNoTemporaryRepair()
    {
        var input = ReadyInput();
        AssessmentFieldValue[] fields =
        [
            .. ReplaceField(input.Assessment.Fields, AssessmentVocabulary.LegalStatus, "unroadworthy"),
            Field(AssessmentVocabulary.UnroadworthyReason, "Brake line severed"),
        ];

        var result = AssessmentReportProjection.Project(
            input with { Assessment = input.Assessment with { Fields = fields } });

        Assert.True(result.IsReady);
        var snapshot = result.Snapshot!;
        Assert.True(snapshot.IsUnroadworthy);
        Assert.Equal("Brake line severed", snapshot.UnroadworthyReason);
        Assert.Equal(AssessmentReportProjection.Project(input).Snapshot!.Vehicle, snapshot.Vehicle);
        snapshot.Validate();
    }

    /// <summary>
    /// The Damage section's Incident narrative is the Nature of Incident block
    /// the report prints (operator, 24 September 2026): the sentence composed
    /// from the headline impact, or the Engineer's own wording, whether or not
    /// the report can yet be projected.
    /// </summary>
    [Fact]
    public void TheIncidentNarrativeIsTheNatureOfIncidentBlockTheReportPrints()
    {
        // The template's sentence (Sample - Total Loss Report.pdf, page 2).
        const string composed = "The vehicle has suffered moderate collision/impact damage to the right rear.";
        const string worded = "The vehicle was struck from behind.";
        var input = ReadyInput();

        Assert.Equal(composed, ReportWordingComposition.NatureOfIncidentOf(input));
        Assert.Equal(composed, PrintedNatureOfIncident(input));

        var engineerWording = input with
        {
            Wording = [new(ReportWordingComposition.NatureOfIncident, null, worded, null)]
        };
        Assert.Equal(worded, ReportWordingComposition.NatureOfIncidentOf(engineerWording));
        Assert.Equal(worded, PrintedNatureOfIncident(engineerWording));

        // A Case the report cannot yet be projected for reads the same narrative.
        var unsigned = input with { Signatory = null };
        Assert.False(AssessmentReportProjection.Project(unsigned).IsReady);
        Assert.Equal(composed, ReportWordingComposition.NatureOfIncidentOf(unsigned));

        // No headline impact and no wording of the Engineer's: no narrative.
        var noImpact = input with
        {
            Assessment = input.Assessment with
            {
                Fields =
                [
                    .. input.Assessment.Fields.Where(field =>
                        field.Path is not (AssessmentVocabulary.ImpactSeverity or AssessmentVocabulary.ImpactLocation))
                ]
            }
        };
        Assert.Null(ReportWordingComposition.NatureOfIncidentOf(noImpact));
    }

    /// <summary>
    /// Damage to several areas reads the same on the Damage section and on the
    /// report: the manager's case page's sentence
    /// (pegasus_case_dashboard_2026-09-15.html, lines 1161 to 1163), with the
    /// areas the recorded damages name.
    /// </summary>
    [Fact]
    public void DamageToSeveralAreasReadsTheSameOnTheCaseAndOnTheReport()
    {
        const string composed =
            "The vehicle has suffered heavy collision/impact damage to the following areas:"
            + "\n— Front"
            + "\n— Right rear";
        var ready = ReadyInput();
        AssessmentFieldValue[] fields =
        [
            .. ready.Assessment.Fields.Where(field => field.Path is not (
                AssessmentVocabulary.ImpactSeverity
                or AssessmentVocabulary.ImpactLocation
                or AssessmentVocabulary.DamageImpacts)),
            Field(AssessmentVocabulary.ImpactSeverity, "heavy"),
            Field(AssessmentVocabulary.ImpactLocation, "multiple"),
            Field(
                AssessmentVocabulary.DamageImpacts,
                "[{\"areas\":[\"front\"],\"severity\":\"light\",\"note\":\"Bonnet\"},{\"areas\":[\"right_rear\"],\"severity\":\"heavy\",\"note\":\"Quarter\"}]"),
        ];
        var input = ready with { Assessment = ready.Assessment with { Fields = fields } };

        Assert.Equal(composed, ReportWordingComposition.NatureOfIncidentOf(input));
        Assert.Equal(composed, PrintedNatureOfIncident(input));
        Assert.Equal(composed, ReportWordingComposition.NatureOfIncidentOf(input with { Signatory = null }));
    }

    /// <summary>
    /// A Case with no mileage prints the template's word for it
    /// (DESIGN_SPEC.md line 90: a missing value renders as "TBC").
    /// </summary>
    [Fact]
    public void AVehicleWithNoMileagePrintsTheTemplatesWordForIt()
    {
        var input = ReadyInput();

        var snapshot = AssessmentReportProjection.Project(input with
        {
            Assessment = input.Assessment with
            {
                CaseOwned = input.Assessment.CaseOwned with
                {
                    Mileage = null,
                    MileageUnit = null,
                    MileageSource = "tbc",
                }
            }
        }).Snapshot!;

        Assert.Equal("TBC", snapshot.Vehicle.MileageDescription);
        Assert.Equal("tbc", snapshot.Vehicle.MileageSource);
    }

    /// <summary>
    /// "Engineer's Value applied." is what the Case save records when a
    /// calculated value is applied. It is a message for the screen, in neither
    /// the template nor the manager's case page, so it is never the report's
    /// valuation commentary; the Engineer's own commentary is.
    /// </summary>
    [Fact]
    public void TheCaseSavesOwnReasonIsNeverTheValuationCommentary()
    {
        var assessment = ReadyInput().Assessment;

        Assert.Equal("Engineer's Value applied.", ValuationCalculationPolicy.AppliedReason);
        Assert.Null(AssessmentReportProjection.ValuationCommentaryOf(
            assessment, ValuationCalculationPolicy.AppliedReason));
        Assert.Null(AssessmentReportProjection.ValuationCommentaryOf(
            assessment, "  " + ValuationCalculationPolicy.AppliedReason + " "));

        var written = assessment with
        {
            Fields =
            [
                .. assessment.Fields,
                Field(AssessmentVocabulary.ReportValuationCommentaryText, "Low mileage for its age."),
            ]
        };
        Assert.Equal(
            "Low mileage for its age.",
            AssessmentReportProjection.ValuationCommentaryOf(written, ValuationCalculationPolicy.AppliedReason));
    }

    [Theory]
    [InlineData(true, ValuationSource.Glasses, true)]
    [InlineData(true, ValuationSource.Cazana, false)]
    [InlineData(false, ValuationSource.Glasses, false)]
    public void TheCaseReadsTheStatementOfTruthTheReportPrints(
        bool disclose, ValuationSource guide, bool namesGlasses)
    {
        var ready = ReadyInput();
        var input = ready with
        {
            Assessment = ready.Assessment with
            {
                Fields =
                [
                    .. ready.Assessment.Fields,
                    Field(AssessmentVocabulary.ReportDiscloseGuideSource, disclose ? "true" : "false"),
                ]
            },
            Guides = new ReportGuideSources([guide]),
        };

        var statement = AssessmentReportContract.StatementOfTruthOf(input);

        Assert.Equal(AssessmentReportProjection.Project(input).Snapshot!.StatementOfTruth, statement);
        Assert.Equal(namesGlasses, statement.Contains(AssessmentReportContract.StatementOfTruthGuide));
        Assert.Equal(
            [AssessmentReportContract.StatementOfTruth1, AssessmentReportContract.StatementOfTruth2],
            statement.Take(2));
        Assert.Equal(
            [AssessmentReportContract.StatementOfTruth3, AssessmentReportContract.StatementOfTruth4],
            statement.TakeLast(2));
        // The Case shows it before a report can be projected.
        Assert.Equal(statement, AssessmentReportContract.StatementOfTruthOf(input with { Signatory = null }));
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("de-DE")]
    [InlineData("ar-SA")]
    public void MileageUsesEnGbFormattingRegardlessOfAmbientCulture(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

            var snapshot = AssessmentReportProjection.Project(ReadyInput()).Snapshot!;

            Assert.Equal("80,000 miles", snapshot.Vehicle.MileageDescription);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    /// <summary>
    /// Entry to Review proves only instruction and image completeness, so each
    /// Case fact the report prints is named before anything is projected. The
    /// vehicle make, model and year are left to the Review-entry rail, and a
    /// missing mileage prints as TBC, so none of them is named.
    /// </summary>
    [Fact]
    public void EveryCaseFactTheReportPrintsIsANamedBlocker()
    {
        var input = ReadyInput();
        var assessment = input.Assessment with
        {
            CaseOwned = input.Assessment.CaseOwned with
            {
                Registration = null,
                Make = null,
                Model = null,
                Year = null,
                Mileage = null,
                MileageUnit = null,
                MileageSource = "tbc",
                IncidentDate = null,
                InspectionMode = null,
                InspectionAddress = null,
                InspectionDate = null
            }
        };

        var result = AssessmentReportProjection.Prepare(
            assessment,
            input.CurrentEstimate,
            input.Signatory);

        string[] printed = ["Vehicle registration", "Incident date", "Inspection type", "Inspection date"];
        Assert.False(result.CanGenerate);
        Assert.Equal(
            printed.Order(StringComparer.Ordinal),
            result.Reasons.Select(reason => reason.Requirement).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// A Review Case can lack a fact the report prints, so the projection
    /// names it as ordinary casework rather than throwing for the error page.
    /// </summary>
    [Theory]
    [InlineData(CaseDataFieldNames.ClaimantName, "Claimant name")]
    [InlineData(CaseDataFieldNames.ClaimNumber, "Claim reference")]
    [InlineData(CaseDataFieldNames.IncidentDate, "Incident date")]
    [InlineData(CaseDataFieldNames.VehicleRegistration, "Vehicle registration")]
    [InlineData(CaseDataFieldNames.InspectionMode, "Inspection type")]
    [InlineData(CaseDataFieldNames.InspectionDate, "Inspection date")]
    [InlineData(CaseDataFieldNames.InspectionAddress, "Inspection address")]
    public void AMissingPrintedCaseFactIsNotReadyAndNeverThrows(string field, string requirement)
    {
        var input = ReadyInput();

        var result = AssessmentReportProjection.Project(input with
        {
            Assessment = input.Assessment with
            {
                CaseOwned = WithoutCaseFact(input.Assessment.CaseOwned, field)
            }
        });

        var reason = AssertNotReady(result, requirement);
        Assert.Equal(field, reason.Field);
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
    public void ACurrentRepairSpecWithNoLinesIsNotReady()
    {
        // Taking a spec into use no longer needs lines, so report readiness
        // names the empty Current spec instead.
        var result = AssessmentReportProjection.Project(
            ReadyInput() with { CurrentEstimate = ReadyCurrentEstimate() with { Lines = [] } });

        var reason = AssertNotReady(result, AssessmentReportProjection.RepairCostRequirement);
        Assert.Equal(CaseReportReadiness.CurrentEstimateEmpty, reason);
    }

    [Fact]
    public void MissingRepairCostsIsNotReadyNamingTheAcceptedFormulaGap()
    {
        // There is no hand-typed cost path: without a Current repair spec the
        // draft fails closed with generation's own item for it.
        var result = AssessmentReportProjection.Project(ReadyInput() with { CurrentEstimate = null });

        var reason = AssertNotReady(result, AssessmentReportProjection.RepairCostRequirement);
        Assert.Equal(CaseReportReadiness.CurrentEstimateMissing, reason);
    }

    [Fact]
    public void ContractRepairWithoutASumIsNotReady()
    {
        var input = ReadyInput();
        var contractFields = input.Assessment.Fields
            .Select(field => field.Path == AssessmentVocabulary.Outcome
                ? field with { Value = "contract_repair" }
                : field)
            .ToArray();

        var missing = AssessmentReportProjection.Project(
            input with { Assessment = input.Assessment with { Fields = contractFields } });
        AssertNotReady(missing, "Agreed contract sum");

        var recorded = AssessmentReportProjection.Project(
            input with
            {
                Assessment = input.Assessment with
                {
                    Fields = contractFields.Append(
                        Field(AssessmentVocabulary.SettlementContractSum, "4500.00")).ToArray(),
                },
            });
        Assert.True(recorded.IsReady);
        Assert.Equal(4500m, recorded.Snapshot!.Settlement.ContractSum);
    }

    [Fact]
    public void TheCurrentEstimateSuppliesTheCanonicalBreakdownAndTheLists()
    {
        var estimate = CurrentEstimate(new("Repairer", 45m, 15m, 5m, Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)), materials: 60m);
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
        result.Snapshot.Validate();
    }

    /// <summary>
    /// The report's cost block is the printed breakdown itself, row
    /// for row, even when the estimate carries discounts and charges VAT on
    /// only some of its categories. The report never re-derives a figure and
    /// never reads a flattened projection of one.
    /// </summary>
    [Fact]
    public void TheReportsCostsAreThePrintedBreakdownRowForRow()
    {
        var estimate = CurrentEstimate(new EstimateDetails(
            "Repairer", 45m, 15m, 20m,
            new EstimateDiscounts(0.1m, 0.05m, 0.125m, 0.025m),
            new EstimateVatPolicy(
                RepairerVatStatus.NotRegistered,
                EstimateVatCategories.Parts | EstimateVatCategories.Materials,
                false),
            null), materials: 60m);

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
        // The template's own row for a repairer who is not VAT registered
        // (DESIGN_SPEC.md line 129).
        Assert.Equal("VAT (20% — parts & paint only)", costs.VatLabel);
        costs.Validate();
    }

    /// <summary>
    /// A repair spec whose repairer VAT status is Unknown charges VAT on
    /// nothing, so its report would understate the repair cost: it blocks the
    /// report (operator, 27 September 2026). The preview and generation name
    /// the one item, and its resolution says where to record the status.
    /// </summary>
    [Fact]
    public void AnUnknownRepairerVatStatusIsNotReady()
    {
        var estimate = CurrentEstimate(new("Repairer", 45m, 15m, 20m), materials: 60m);

        var result = AssessmentReportProjection.Project(ReadyInput() with { CurrentEstimate = estimate });

        var reason = AssertNotReady(result, CaseReportReadiness.RepairerVatRequirement);
        Assert.Equal(CaseReportReadiness.RepairerVatStatusUnknown, reason);
        Assert.Equal("Repairer VAT status", reason.Requirement);
        Assert.Contains("on the Repair Spec section", reason.HowToResolve, StringComparison.Ordinal);
        Assert.Equal(reason, Assert.Single(result.Reasons));
    }

    /// <summary>
    /// Recording the status does not by itself word a hand-picked set of
    /// costs: the template words VAT on everything and VAT on parts and paint
    /// only, so any other set blocks the report too.
    /// </summary>
    [Theory]
    [InlineData(RepairerVatStatus.Registered, EstimateVatCategories.Labour | EstimateVatCategories.Parts)]
    [InlineData(RepairerVatStatus.NotRegistered, EstimateVatCategories.Parts)]
    [InlineData(RepairerVatStatus.NotRegistered, EstimateVatCategories.None)]
    public void AHandPickedSetOfVatCostsTheTemplateCannotWordIsNotReady(
        RepairerVatStatus status, EstimateVatCategories categories)
    {
        var estimate = CurrentEstimate(
            new("Repairer", 45m, 15m, 20m, Vat: new EstimateVatPolicy(status, categories, CategoriesOverridden: true)),
            materials: 60m);

        var result = AssessmentReportProjection.Project(ReadyInput() with { CurrentEstimate = estimate });

        var reason = AssertNotReady(result, CaseReportReadiness.RepairerVatRequirement);
        Assert.Equal(CaseReportReadiness.RepairerVatHandPicked, reason);
        Assert.Contains("on the Repair Spec section", reason.HowToResolve, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RepairerVatStatus.Registered, "VAT (20%)")]
    [InlineData(RepairerVatStatus.NotRegistered, "VAT (20% — parts & paint only)")]
    public void ARecordedRepairerVatStatusIsReadyAndWordsItsRow(RepairerVatStatus status, string row)
    {
        var estimate = CurrentEstimate(
            new("Repairer", 45m, 15m, 20m, Vat: EstimateVatPolicy.For(status)), materials: 60m);

        var result = AssessmentReportProjection.Project(ReadyInput() with { CurrentEstimate = estimate });

        Assert.True(result.IsReady, string.Join("; ", result.Reasons.Select(reason => reason.Requirement)));
        Assert.Equal(row, result.Snapshot!.Costs.VatLabel);
        result.Snapshot.Validate();
    }

    [Fact]
    public void ThePrintedComponentsReconcileToThePrintedTotal()
    {
        var costs = AssessmentReportProjection
            .Project(ReadyInput() with
            {
                CurrentEstimate = CurrentEstimate(
                    new("Repairer", 45m, 15m, 20m, Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)), materials: 60m),
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

    /// <summary>
    /// The report prints the recorded category's accepted wording (operator,
    /// 26 September 2026), so a total loss projects whichever category is
    /// recorded, N/A included.
    /// </summary>
    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("S")]
    [InlineData("N")]
    [InlineData("N/A")]
    public void ATotalLossProjectsItsRecordedCategory(string category)
    {
        var result = AssessmentReportProjection.Project(TotalLossInput(category));

        Assert.True(result.IsReady);
        Assert.Equal(category, result.Snapshot!.SalvageCategory);
    }

    [Fact]
    public void AnOverriddenReportDateWithoutADateIsNotReady()
    {
        var input = ReadyInput();
        var fields = input.Assessment.Fields
            .Append(Field(AssessmentVocabulary.ReportDateOverride, "true"))
            .ToArray();

        var result = AssessmentReportProjection.Project(
            input with { Assessment = input.Assessment with { Fields = fields } });

        var reason = AssertNotReady(result, "Report date");
        Assert.Equal(AssessmentVocabulary.ReportDate, reason.Field);
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
        // A th-TH workstation reads a Buddhist-calendar year unless
        // the invariant culture is stated at every persisted-date parse.
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("th-TH");
        try
        {
            var input = ReadyInput();
            var fields = input.Assessment.Fields
                .Append(Field(AssessmentVocabulary.ReportDateOverride, "true"))
                .Append(Field(AssessmentVocabulary.ReportDate, "2027-01-02"))
                .ToArray();

            var snapshot = AssessmentReportProjection.Project(
                input with { Assessment = input.Assessment with { Fields = fields } }).Snapshot!;

            Assert.Equal(new DateOnly(2027, 1, 2), snapshot.ReportDate);
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

    /// <summary>
    /// The settlement facts the Case records beyond the agreed contract sum
    /// (excess, betterment, reserve, hire, storage and the salvage
    /// arrangements) are no part of the report, and a sum left on a Case that
    /// is not a contract repair is not carried either.
    /// </summary>
    [Theory]
    [InlineData("repairable")]
    [InlineData("cash_in_lieu")]
    public void OnlyAContractRepairCarriesAnAgreedSum(string outcome)
    {
        var input = ReadyInput();
        AssessmentFieldValue[] fields =
        [
            .. ReplaceField(input.Assessment.Fields, AssessmentVocabulary.Outcome, outcome),
            Field(AssessmentVocabulary.SettlementContractSum, "4500.00"),
        ];

        var result = AssessmentReportProjection.Project(
            input with { Assessment = input.Assessment with { Fields = fields } });

        Assert.True(result.IsReady);
        Assert.Null(result.Snapshot!.Settlement.ContractSum);
        Assert.Null(AssessmentReportProjection.Project(TotalLossInput()).Snapshot!.Settlement.ContractSum);
    }

    [Fact]
    public void ACurrentEstimateWithoutALabourRateIsNotReady()
    {
        var estimate = CurrentEstimate(new("Repairer", null, null, 20m));

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
        Guid.NewGuid(), Guid.NewGuid(), 2, RepairSpecificationState.Draft,
        new(RepairSpecificationSourceRoute.Manual, null, null, null),
        [
            Line(1, "repair", "Nearside door") with { WorkUnits = 5m, Price = null },
            Line(2, "new_part", "Door skin") with { WorkUnits = null, Price = 50m, Quantity = 1 },
            Line(3, "paint_blend", "Blend nearside wing") with { WorkUnits = null, Price = null, Materials = 20m },
        ],
        "engineer-1", RecordedAtUtc,
        new EstimateDetails("Repairer", 30m, 5m, 20m, Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
        IsCurrent: true);

    private static RepairSpecificationVersion CurrentEstimate(EstimateDetails details, decimal? materials = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), 2, RepairSpecificationState.Draft,
        new(RepairSpecificationSourceRoute.Manual, null, null, null),
        [
            Line(1, "new_part", "Bonnet") with { WorkUnits = null, Price = 310m, Quantity = 1 },
            Line(2, "repair", "Repair wing") with { WorkUnits = 3m },
            Line(3, "paint_repair", "Paint wing") with { WorkUnits = null, PaintWorkUnits = 2.5m, Materials = materials },
        ],
        "engineer-1", RecordedAtUtc, details, IsCurrent: true);

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
            Field(AssessmentVocabulary.VehicleCondition, "good"),
            Field(AssessmentVocabulary.VehicleVin, "VIN12345"),
            Field(AssessmentVocabulary.VehicleEngineCc, "1600"),
            Field(AssessmentVocabulary.VehicleFuel, "Petrol"),
            Field(AssessmentVocabulary.VehicleTransmission, "manual"),
            Field(AssessmentVocabulary.VehicleColour, "Blue"),
            Field(AssessmentVocabulary.VehicleBody, "Hatchback"),
            Field(AssessmentVocabulary.VehicleTaxExpiry, "2027-01-02"),
            Field(AssessmentVocabulary.VehicleMotExpiry, "2027-03-04"),
            Field(AssessmentVocabulary.VehicleAirbagsDeployed, "None"),
            Field(AssessmentVocabulary.VehicleTemporaryRepairsPossible, "true"),
            Field(AssessmentVocabulary.VehicleTemporaryRepairMethod, "Secure bumper"),
            Field(AssessmentVocabulary.VehicleTemporaryRepairCost, "25.00"),
            Field(AssessmentVocabulary.ImpactSeverity, "moderate"),
            Field(AssessmentVocabulary.ImpactLocation, "right_rear"),
            Field(AssessmentVocabulary.DamageImpacts, "[{\"areas\":[\"right_rear\"],\"disc\":{\"x\":0.86,\"y\":0.86,\"r\":0.1},\"severity\":\"moderate\",\"note\":\"Quarter panel\"}]"),
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
            Year: "2012",
            Mileage: 80_000,
            MileageUnit: "miles",
            MileageSource: "online_data",
            IncidentDate: new DateOnly(2026, 8, 1),
            ReceivedDate: new DateOnly(2026, 8, 2),
            InspectionMode: "ImageBasedAssessment",
            InspectionAddress: null,
            InspectionDate: new DateOnly(2026, 8, 3),
            ClaimantName: "Alex Example",
            ClaimNumber: "P-100");

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
            OurReference: "CE-100",
            ReportFor: ["Approved Principal"],
            ReportDate: new DateOnly(2026, 8, 19),
            Photos: [photo],
            Sources: [source],
            CurrentEstimate: DefaultCurrentEstimate(),
            Signatory: new ReportSignatory("Ed Mawdsley", "ATA VDA AQP", [1, 2, 3], "image/png"));
    }

    private static AssessmentReportProjectionInput TotalLossInput(string category = "S")
    {
        var input = ReadyInput();
        return input with
        {
            Assessment = input.Assessment with
            {
                Fields =
                [
                    .. ReplaceField(input.Assessment.Fields, AssessmentVocabulary.Outcome, "total_loss"),
                    Field(AssessmentVocabulary.SalvageCategory, category),
                    Field(AssessmentVocabulary.SalvageValue, "500.00"),
                ],
            },
        };
    }

    /// <summary>
    /// <paramref name="owned"/> without the one printed Case fact named by its
    /// Case field. An address is printed only for a vehicle inspected at a
    /// physical location, so the address is removed from such an inspection.
    /// </summary>
    private static AssessmentCaseOwnedData WithoutCaseFact(AssessmentCaseOwnedData owned, string field) => field switch
    {
        CaseDataFieldNames.ClaimantName => owned with { ClaimantName = null },
        CaseDataFieldNames.ClaimNumber => owned with { ClaimNumber = null },
        CaseDataFieldNames.IncidentDate => owned with { IncidentDate = null },
        CaseDataFieldNames.VehicleRegistration => owned with { Registration = null },
        CaseDataFieldNames.InspectionMode => owned with { InspectionMode = null },
        CaseDataFieldNames.InspectionDate => owned with { InspectionDate = null },
        CaseDataFieldNames.InspectionAddress => owned with
        {
            InspectionMode = nameof(CaseInspectionMode.PhysicalAddress),
            InspectionAddress = null
        },
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Not a Case fact the report prints.")
    };

    /// <summary>The text of the Nature of Incident block the projected report prints.</summary>
    private static string PrintedNatureOfIncident(AssessmentReportProjectionInput input) =>
        Assert.Single(
            AssessmentReportProjection.Project(input).Snapshot!.PrintedWording,
            block => block.Key == ReportWordingComposition.NatureOfIncident).Text;

    private static AssessmentFieldValue[] ReplaceField(
        IReadOnlyList<AssessmentFieldValue> fields, string path, string value) =>
        fields.Select(field => field.Path == path ? field with { Value = value } : field).ToArray();

    private static AssessmentFieldValue Field(string path, string value) => new(
        path, value, ActorKind.Staff, "engineer-1", RecordedAtUtc);

    private static CaseEstimateLineRecord Line(int position, string type, string description) => new(
        Guid.NewGuid(), position, type, null, description, 2.5m, null, false, null, null,
        "case", "Test evidence",
        ActorKind.Staff, "engineer-1", RecordedAtUtc);
}
