using System.Globalization;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Assessment;

public sealed class AssessmentPolicyTests
{
    private static readonly ActionActor Automation = ActionActor.Automation("pegasus-automation");
    private static readonly ActionActor Engineer =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly ActionActor PlainStaff =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Theory]
    [InlineData(CaseLifecycleState.NotReady, true)]
    [InlineData(CaseLifecycleState.Review, true)]
    [InlineData(CaseLifecycleState.Held, false)]
    [InlineData(CaseLifecycleState.CreatedInError, false)]
    [InlineData(CaseLifecycleState.ReportPreparation, true)]
    [InlineData(CaseLifecycleState.PostReport, true)]
    [InlineData(CaseLifecycleState.PostReportComplete, true)]
    public void NativeAssessmentActionsRequireAWritableOrCompletedState(
        CaseLifecycleState state,
        bool expected)
    {
        var access = new AssessmentAccessState(state);

        Assert.Equal(expected, access.CanOpen);
    }

    [Theory]
    [InlineData(CaseLifecycleState.ReportPreparation, false)]
    [InlineData(CaseLifecycleState.PostReport, false)]
    [InlineData(CaseLifecycleState.PostReportComplete, true)]
    [InlineData(CaseLifecycleState.NotReady, false)]
    [InlineData(CaseLifecycleState.Review, false)]
    [InlineData(CaseLifecycleState.Held, true)]
    [InlineData(CaseLifecycleState.CreatedInError, true)]
    public void AssessmentAccessIsReadOnlyOutsideWritableStates(
        CaseLifecycleState state,
        bool expected)
    {
        var access = new AssessmentAccessState(state);

        Assert.Equal(expected, access.IsReadOnly);
    }

    [Theory]
    [InlineData(CaseLifecycleState.NotReady, true, false)]
    [InlineData(CaseLifecycleState.Held, false, true)]
    [InlineData(CaseLifecycleState.Review, true, false)]
    [InlineData(CaseLifecycleState.ReportPreparation, true, false)]
    [InlineData(CaseLifecycleState.PostReport, true, false)]
    [InlineData(CaseLifecycleState.PostReportComplete, true, true)]
    [InlineData(CaseLifecycleState.Query, false, true)]
    [InlineData(CaseLifecycleState.ProviderCancelled, false, true)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected, false, true)]
    [InlineData(CaseLifecycleState.CreatedInError, false, true)]
    [InlineData(CaseLifecycleState.SourceEmailUnlinked, false, true)]
    public void AssessmentAccessPinsOpenAndReadOnlyPerLifecycleState(
        CaseLifecycleState state,
        bool expectedCanOpen,
        bool expectedIsReadOnly)
    {
        var access = new AssessmentAccessState(state);

        Assert.Equal(expectedCanOpen, access.CanOpen);
        Assert.Equal(expectedIsReadOnly, access.IsReadOnly);
    }

    [Theory]
    [InlineData(CaseLifecycleState.NotReady, true)]
    [InlineData(CaseLifecycleState.Review, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, true)]
    [InlineData(CaseLifecycleState.PostReport, true)]
    [InlineData(CaseLifecycleState.Held, false)]
    [InlineData(CaseLifecycleState.PostReportComplete, false)]
    [InlineData(CaseLifecycleState.ProviderCancelled, false)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected, false)]
    [InlineData(CaseLifecycleState.CreatedInError, false)]
    [InlineData(CaseLifecycleState.SourceEmailUnlinked, false)]
    public void WorkspaceAssessmentWritesUseOnlyTheSupportedLifecycleStates(
        CaseLifecycleState state, bool expected)
    {
        Assert.Equal(expected, AssessmentPolicy.IsWritableState(state));
    }

    [Fact]
    public void SettlementCannotWriteASecondEstimateRepairDuration()
    {
        const string obsoletePath = "settlement.repair_duration";

        Assert.False(AssessmentVocabulary.Definitions.ContainsKey(obsoletePath));
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.NormalizeWritableField(obsoletePath, "3"));
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(Request(new() { [obsoletePath] = "3" })));
    }

    [Fact]
    public void UnknownFieldPathFailsClosed()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { ["vehicle.not_a_field"] = "red" })));
        Assert.Contains("vehicle.not_a_field", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CaseOwnedPathFailsClosedNamingTheCaseDetailEditPath()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { ["vehicle.registration"] = "AB12CDE" })));
        Assert.Contains("case-detail edit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryEnumeratedCodeFromTheScreenRoundTrips()
    {
        foreach (var definition in AssessmentVocabulary.Definitions.Values
            .Where(value => value.Type == AssessmentFieldType.Enumerated
                && !AssessmentVocabulary.DerivedPaths.Contains(value.Path)))
        {
            foreach (var code in definition.Codes!)
            {
                var normalized = AssessmentPolicy.ValidateAndNormalize(
                    Request(new() { [definition.Path] = code }, Engineer));
                Assert.Equal(code, normalized.Fields[definition.Path]);
            }

            Assert.ThrowsAny<ArgumentException>(() =>
                AssessmentPolicy.ValidateAndNormalize(
                    Request(new() { [definition.Path] = "unrecognized_code" }, Engineer)));
        }
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("12.345")]
    [InlineData("not-a-number")]
    public void MoneyValidationRefusesBadAmounts(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { ["costs.recovery_charge"] = value })));
    }

    [Fact]
    public void PositiveMoneyFieldsRefuseZero()
    {
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { ["assessment.values.retail"] = "0" }, Engineer)));
    }

    [Fact]
    public void MoneyIsCanonicalizedToTwoDecimalPlaces()
    {
        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { ["assessment.values.retail"] = "12500.5" }, Engineer));
        Assert.Equal("12500.50", normalized.Fields["assessment.values.retail"]);
    }

    [Fact]
    public void FlagsAcceptOnlyTrueOrFalse()
    {
        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { ["costs.repairer_vat_registered"] = "TRUE" }));
        Assert.Equal("true", normalized.Fields["costs.repairer_vat_registered"]);
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { ["costs.repairer_vat_registered"] = "yes" })));
    }

    [Fact]
    public void DatesAreExactIsoDates()
    {
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { ["incident.assessed"] = "03/08/2026" })));
    }

    [Theory]
    [InlineData("th-TH")]
    [InlineData("ar-SA")]
    [InlineData("en-GB")]
    public void DateValuesUseTheGregorianCalendarRegardlessOfCurrentCulture(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        try
        {
            var dateFields = AssessmentVocabulary.Definitions.Values
                .Where(definition => definition.Type == AssessmentFieldType.Date
                    && !AssessmentVocabulary.DerivedPaths.Contains(definition.Path)
                    && !AssessmentVocabulary.AdoptedFindingPaths.Contains(definition.Path))
                .ToArray();
            Assert.NotEmpty(dateFields);
            foreach (var definition in dateFields)
            {
                // Calendar probes, not new case evidence.
                foreach (var value in new[] { "2027-01-02", "2028-02-29" })
                {
                    var normalized = AssessmentPolicy.ValidateAndNormalize(
                        Request(new() { [definition.Path] = value }, Engineer));
                    Assert.Equal(value, normalized.Fields[definition.Path]);
                }

                foreach (var value in new[] { "2027-02-29", "02/01/2027", "0001-01-01" })
                {
                    Assert.Throws<ArgumentException>(() =>
                        AssessmentPolicy.ValidateAndNormalize(
                            Request(new() { [definition.Path] = value }, Engineer)));
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void EveryWritableVocabularyPathRoundTripsThroughItsCoreNormalizer()
    {
        foreach (var definition in AssessmentVocabulary.Definitions.Values
            .Where(definition => !AssessmentVocabulary.DerivedPaths.Contains(definition.Path)
                && !AssessmentVocabulary.AdoptedFindingPaths.Contains(definition.Path)))
        {
            var value = definition.Type switch
            {
                // A VIN is validated as ISO 3779 (Phase 5b), so it round-trips a real one.
                AssessmentFieldType.Text when definition.Path == AssessmentVocabulary.VehicleVin => "WVWZZZ1JZXW000001",
                AssessmentFieldType.Text => "value",
                AssessmentFieldType.Enumerated => definition.Codes![0],
                AssessmentFieldType.WholeNumber => "1",
                AssessmentFieldType.Money => "1.00",
                AssessmentFieldType.Flag => "true",
                AssessmentFieldType.Date => "2026-09-03",
                AssessmentFieldType.Json => "[{\"areas\":[\"front\"],\"severity\":\"light\",\"note\":\"value\"}]",
                _ => throw new ArgumentOutOfRangeException()
            };

            var normalized = AssessmentPolicy.ValidateAndNormalize(
                Request(new() { [definition.Path] = value }, Engineer));

            Assert.NotNull(normalized.Fields[definition.Path]);
        }
    }

    [Fact]
    public void DamageImpactsAreCanonicalAndDeriveHeadlineValues()
    {
        // The areas of a disc are written in the vocabulary's order, whatever
        // order the browser sent them in.
        const string json = "[ { \"note\": \" Bonnet \" , \"severity\": \"light\", \"areas\": [\"front\"] }, { \"areas\": [\"left_rear\", \"rear\"], \"severity\": \"heavy\", \"note\": \"Quarter\" } ]";

        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { [AssessmentVocabulary.DamageImpacts] = json }));

        Assert.Equal("[{\"areas\":[\"front\"],\"severity\":\"light\",\"note\":\"Bonnet\"},{\"areas\":[\"rear\",\"left_rear\"],\"severity\":\"heavy\",\"note\":\"Quarter\"}]", normalized.Fields[AssessmentVocabulary.DamageImpacts]);
        Assert.Equal(("multiple", "heavy"), AssessmentPolicy.DeriveImpactValues(normalized.Fields[AssessmentVocabulary.DamageImpacts]));
        Assert.Equal(("underside", "heavy"), AssessmentPolicy.DeriveImpactValues("[{\"areas\":[\"underside\"],\"severity\":\"heavy\",\"note\":\"\"}]"));
        // Two discs over the same area are one headline location.
        Assert.Equal(("rear", "moderate"), AssessmentPolicy.DeriveImpactValues("[{\"areas\":[\"rear\"],\"severity\":\"light\",\"note\":\"\"},{\"areas\":[\"rear\"],\"severity\":\"moderate\",\"note\":\"\"}]"));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("[{\"areas\":[\"front\"],\"severity\":\"light\"}]")]
    [InlineData("[{\"areas\":[\"unknown\"],\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"front\"],\"severity\":\"unknown\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"front\"],\"severity\":\"light\",\"note\":\"x\",\"extra\":\"unexpected\"}]")]
    [InlineData("[{\"areas\":[],\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"front\",\"front\"],\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"underside\",\"front\"],\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"areas\":[\"underside\"],\"severity\":\"light\",\"note\":\"x\"},{\"areas\":[\"underside\"],\"severity\":\"heavy\",\"note\":\"y\"}]")]
    [InlineData("[{\"zone\":\"front\",\"severity\":\"light\",\"note\":\"x\"}]")]
    public void DamageImpactsFailClosed(string json)
    {
        Assert.ThrowsAny<ArgumentException>(() => AssessmentPolicy.ValidateAndNormalize(
            Request(new() { [AssessmentVocabulary.DamageImpacts] = json })));
    }

    [Fact]
    public void DamageImpactNoteAndSerializedValueBoundsFailClosed()
    {
        var longNote = new string('x', 201);
        Assert.Throws<ArgumentException>(() => AssessmentPolicy.ValidateAndNormalize(Request(new()
        {
            [AssessmentVocabulary.DamageImpacts] = $"[{{\"areas\":[\"front\"],\"severity\":\"light\",\"note\":\"{longNote}\"}}]"
        })));
        Assert.Throws<ArgumentOutOfRangeException>(() => AssessmentPolicy.ValidateAndNormalize(Request(new()
        {
            [AssessmentVocabulary.DamageImpacts] = "[" + new string(' ', 4001) + "]"
        })));
    }

    [Theory]
    [InlineData(AssessmentVocabulary.ImpactLocation)]
    [InlineData(AssessmentVocabulary.ImpactSeverity)]
    public void DerivedImpactFieldsCannotBeWrittenDirectly(string path)
    {
        Assert.Throws<InvalidOperationException>(() => AssessmentPolicy.ValidateAndNormalize(
            Request(new() { [path] = "front" })));
    }

    [Fact]
    public void AGenericFieldSaveNeverWritesOrClearsTheAdoptedEngineerValue()
    {
        // The accepted Engineer's value is adopted only by the
        // valuation Apply command, which records the suggested and the chosen
        // amounts together. A Web or MCP field save that touched it would
        // rewrite a professional finding with no such evidence, so both a
        // value and a clearance fail closed — for an Engineer too.
        foreach (var actor in new[] { Engineer, Automation, PlainStaff })
        {
            foreach (var value in new string?[] { "4500.00", null })
            {
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    AssessmentPolicy.ValidateAndNormalize(
                        Request(new() { [AssessmentVocabulary.ValueEngineer] = value }, actor)));
                Assert.Contains("Apply", exception.Message, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void TheDamageAreasAreTheEightPlanAreasAndTheThreeOthers()
    {
        Assert.Equal(8, AssessmentVocabulary.DamagePlanAreas.Count);
        Assert.Equal(3, AssessmentVocabulary.DamageOtherAreas.Count);
        Assert.Equal(
            AssessmentVocabulary.DamagePlanAreas.Concat(AssessmentVocabulary.DamageOtherAreas).Order(StringComparer.Ordinal),
            AssessmentVocabulary.DamageAreas.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            [.. AssessmentVocabulary.DamagePlanAreas, .. AssessmentVocabulary.DamageOtherAreas, "multiple"],
            AssessmentVocabulary.Definitions[AssessmentVocabulary.ImpactLocation].Codes!);
        Assert.Equal("LH Rear", AssessmentVocabulary.DamageAreas["left_rear"]);
        Assert.True(AssessmentVocabulary.DamageAreaOrder("front") < AssessmentVocabulary.DamageAreaOrder("underside"));
        Assert.Equal(AssessmentVocabulary.DamagePlanAreas.Count + 3, AssessmentVocabulary.DamageAreaOrder("unknown"));

        // Each plan area's centre lies in its own band, and nothing lies off the plan.
        Assert.Equal(AssessmentVocabulary.DamagePlanAreas.Order(StringComparer.Ordinal), DamageAreaGeometry.Centres.Keys.Order(StringComparer.Ordinal));
        foreach (var (area, centre) in DamageAreaGeometry.Centres)
        {
            Assert.Equal(area, DamageAreaGeometry.AreaAt(centre.X, centre.Y));
        }
        Assert.Null(DamageAreaGeometry.AreaAt(-0.1, 0.5));
        Assert.Equal("left_side", DamageAreaGeometry.AreaAt(0.49, 0.5));
        Assert.Equal("right_side", DamageAreaGeometry.AreaAt(0.51, 0.5));

        // A one-area disc sits on the area's centre; a wider disc grows to
        // reach every area it names; the other areas draw nothing.
        var one = DamageAreaGeometry.Disc(["front"], 100, 200)!;
        Assert.Equal((50d, 20d, 12d), (one.CentreX, one.CentreY, one.Radius));
        var two = DamageAreaGeometry.Disc(["rear", "left_rear"], 100, 200)!;
        Assert.True(two.Radius > one.Radius);
        Assert.True(two.CentreX < 50);
        Assert.Null(DamageAreaGeometry.Disc(["underside"], 100, 200));
    }

    [Fact]
    public void OverlappingDiscsAreIndependentEntries()
    {
        // Two discs may cover the same area: each stays its own damage with
        // its own severity and note, and the headline location reads the
        // distinct areas across them.
        const string json =
            "[{\"areas\":[\"front\"],\"severity\":\"light\",\"note\":\"Small\"},"
            + "{\"areas\":[\"front\",\"right_front\"],\"severity\":\"heavy\",\"note\":\"Wide\"}]";

        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { [AssessmentVocabulary.DamageImpacts] = json }));

        Assert.Equal(json, normalized.Fields[AssessmentVocabulary.DamageImpacts]);
        Assert.Equal(
            ("multiple", "heavy"),
            AssessmentPolicy.DeriveImpactValues(normalized.Fields[AssessmentVocabulary.DamageImpacts]));

        var alone = AssessmentPolicy.DeriveImpactValues(
            "[{\"areas\":[\"front\"],\"severity\":\"light\",\"note\":\"Small\"}]");
        Assert.Equal(("front", "light"), alone);
    }

    [Fact]
    public void PostReviewReadinessNoLongerAsksForTheRetiredEngineerIdentityFields()
    {
        // Operator decision D18: the signing Engineer is the selected sign-off
        // account, so typed copies of that account's name, qualifications and
        // signature are no longer readiness items.
        var readiness = AssessmentPolicy.EvaluatePostReviewReadiness(Projection([]));
        var requirements = readiness.Select(item => item.Requirement).ToArray();

        Assert.DoesNotContain("Engineer name", requirements);
        Assert.DoesNotContain("Engineer qualifications", requirements);
        Assert.DoesNotContain("Signature", requirements);
        Assert.Contains("Agreed fee", requirements);
    }

    [Fact]
    public void SaveBoundCoversTheWholeVocabulary()
    {
        Assert.True(AssessmentPolicy.MaximumFieldsPerSave >= AssessmentVocabulary.Definitions.Count);
    }

    [Fact]
    public void AutomationMayRecordFindingFields()
    {
        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { ["assessment.legal_status"] = "roadworthy" }));
        Assert.Equal("roadworthy", normalized.Fields["assessment.legal_status"]);
    }

    [Fact]
    public void NonEngineerStaffCannotRecordFindingFields()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { ["assessment.outcome"] = "repairable" }, PlainStaff)));
        Assert.Contains("Engineer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NonEngineerStaffMayRecordOrdinaryFields()
    {
        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { ["vehicle.condition"] = "good" }, PlainStaff));
        Assert.Equal("good", normalized.Fields["vehicle.condition"]);
    }

    [Fact]
    public void SystemWorkerActorsAreRefused()
    {
        Assert.ThrowsAny<Exception>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(
                    new() { ["vehicle.condition"] = "good" },
                    ActionActor.SystemWorker("worker"))));
    }

    [Fact]
    public void UnroadworthyRequiresAReasonInTheMergedState()
    {
        var saved = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["assessment.legal_status"] = "unroadworthy"
        };
        Assert.Throws<InvalidOperationException>(() =>
            AssessmentPolicy.ValidateMergedState(
                saved,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["assessment.legal_status"] = "unroadworthy"
                }));
        AssessmentPolicy.ValidateMergedState(
            saved,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["assessment.legal_status"] = "unroadworthy",
                ["assessment.unroadworthy_reason"] = "Suspension damage"
            });
    }

    [Fact]
    public void TotalLossRequiresCategoryAndSalvageValueInTheMergedState()
    {
        var saved = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["assessment.outcome"] = "total_loss"
        };
        Assert.Throws<InvalidOperationException>(() =>
            AssessmentPolicy.ValidateMergedState(
                saved,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["assessment.outcome"] = "total_loss",
                    ["assessment.category"] = "S"
                }));
        AssessmentPolicy.ValidateMergedState(
            saved,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["assessment.outcome"] = "total_loss",
                ["assessment.category"] = "S",
                ["assessment.salvage_value"] = "1500.00"
            });
    }

    [Fact]
    public void EstimateLinesValidateTypePrecisionAndUnpricedRules()
    {
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(lines: [Line("unknown_type")])));
        // Hours are kept at the provider's own precision (B04): a quarter of
        // an hour is a real time, a seventh decimal place is not.
        AssessmentPolicy.ValidateAndNormalize(
            Request(lines: [Line("repair") with { WorkUnits = 1.25m }]));
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(lines: [Line("repair") with { WorkUnits = 1.2345678m }])));
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(lines: [Line("new_part") with { Unpriced = true, Price = 10m }])));

        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(lines:
            [
                Line("repair") with { WorkUnits = 2.5m, Status = "estimated" },
                Line("new_part") with { Price = 120.50m, EvidenceLabel = "official" }
            ]));
        Assert.Equal(2, normalized.EstimateLines!.Count);
    }

    [Fact]
    public void AnEmptySaveIsRefusedAndAnEmptyLineCollectionClears()
    {
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(Request(new())));
        var normalized = AssessmentPolicy.ValidateAndNormalize(Request(lines: []));
        Assert.Empty(normalized.EstimateLines!);
    }

    [Fact]
    public void ReadinessNamesMissingRequirementsAndHonoursTheTbcMileageRule()
    {
        var empty = Projection([]);
        var readiness = AssessmentPolicy.EvaluateReadiness(empty);
        Assert.Contains(readiness, item => item.Requirement == "Vehicle type");
        Assert.Contains(readiness, item => item.Requirement == "Repairer VAT answer");

        // The year is the case record's own fact now, named beside the make
        // and the model rather than asked for again on the Vehicle section.
        Assert.Contains(
            readiness,
            item => item.Requirement == "Vehicle year" && item.Source == "Case record");
        Assert.DoesNotContain(readiness, item => item.Requirement == "Mileage source");

        // The report's mileage source is derived from the mileage's own
        // provenance, so a case with no mileage reads To be confirmed and the
        // odometer requirement is waived exactly as it was when staff typed it.
        Assert.DoesNotContain(readiness, item => item.Requirement == "Odometer reading");

        var recorded = Projection(
            [],
            new(null, null, null, "2012", 80_000, "miles", "owner", null, null, null, null));
        Assert.DoesNotContain(
            AssessmentPolicy.EvaluateReadiness(recorded),
            item => item.Requirement is "Odometer reading" or "Vehicle year");
    }

    [Fact]
    public void ContractRepairRequiresAConfirmedAgreedSum()
    {
        var missing = Projection([Field(AssessmentVocabulary.Outcome, "contract_repair")]);
        Assert.Contains(
            AssessmentPolicy.EvaluatePostReviewReadiness(missing),
            item => item.Requirement == "Agreed contract sum");

        var unconfirmed = Projection(
            [
                Field(AssessmentVocabulary.Outcome, "contract_repair"),
                Field(AssessmentVocabulary.SettlementContractSum, "4500.00")
                    with { ConfirmedBy = null, ConfirmedAtUtc = null },
            ]);
        Assert.Contains(
            AssessmentPolicy.EvaluatePostReviewReadiness(unconfirmed),
            item => item.Requirement == "Agreed contract sum");

        var confirmed = Projection(
            [
                Field(AssessmentVocabulary.Outcome, "contract_repair"),
                Field(AssessmentVocabulary.SettlementContractSum, "4500.00"),
            ]);
        Assert.DoesNotContain(
            AssessmentPolicy.EvaluatePostReviewReadiness(confirmed),
            item => item.Requirement == "Agreed contract sum");
    }

    [Fact]
    public void ReadinessNamesEachUnconfirmedValueIndividually()
    {
        var projection = Projection(
        [
            Field("vehicle.condition", "good") with { ConfirmedBy = null, ConfirmedAtUtc = null },
            Field("assessment.outcome", "repair") with { ConfirmedBy = null, ConfirmedAtUtc = null }
        ]);
        var readiness = AssessmentPolicy.EvaluateReadiness(projection);

        // One blocker per unconfirmed value naming its own field and
        // provenance — never a single aggregate count.
        Assert.Contains(
            readiness,
            item => item.Requirement == "vehicle.condition awaits review"
                && item.Source.StartsWith("Recorded by ", StringComparison.Ordinal));
        Assert.Contains(
            readiness,
            item => item.Requirement == "assessment.outcome awaits review");
        Assert.DoesNotContain(
            readiness,
            item => item.Requirement.Contains("values await review", StringComparison.Ordinal));
    }

    private static SaveAssessmentRequest Request(
        Dictionary<string, string?>? fields = null,
        ActionActor? actor = null,
        IReadOnlyList<EstimateLineInput>? lines = null) => new(
        Guid.NewGuid(),
        0,
        actor ?? Automation,
        "mcp:test-operation",
        "Test save",
        "lease-token",
        fields ?? new Dictionary<string, string?>(StringComparer.Ordinal),
        lines);

    private static EstimateLineInput Line(string type) =>
        new(type, null, "Test line", null, null, false, null, null, null, null, null);

    private static AssessmentFieldValue Field(string path, string value) => new(
        path,
        value,
        ActorKind.Staff,
        "staff",
        DateTimeOffset.UtcNow,
        "staff",
        DateTimeOffset.UtcNow);

    private static CaseAssessmentProjection Projection(
        IReadOnlyList<AssessmentFieldValue> fields,
        AssessmentCaseOwnedData? caseOwned = null) => new(
        Guid.NewGuid(),
        "CE-QDOS-31-00001",
        0,
        CaseLifecycleState.Review,
        null,
        fields,
        [],
        caseOwned ?? new(null, null, null, null, null, null, "tbc", null, null, null, null));
}
