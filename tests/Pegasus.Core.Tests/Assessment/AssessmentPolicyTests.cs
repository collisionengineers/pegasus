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
    [InlineData(CaseLifecycleState.NotReady, 4L, 4L, false)]
    [InlineData(CaseLifecycleState.Review, 4L, 4L, false)]
    [InlineData(CaseLifecycleState.Held, 4L, 4L, false)]
    [InlineData(CaseLifecycleState.CreatedInError, 4L, 4L, false)]
    [InlineData(CaseLifecycleState.ReportPreparation, 4L, null, false)]
    [InlineData(CaseLifecycleState.ReportPreparation, 4L, 3L, false)]
    [InlineData(CaseLifecycleState.ReportPreparation, 4L, 4L, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, 4L, 5L, true)]
    [InlineData(CaseLifecycleState.PostReport, 4L, 4L, true)]
    [InlineData(CaseLifecycleState.PostReportComplete, 4L, 4L, true)]
    [InlineData(CaseLifecycleState.PostReportComplete, 4L, 3L, false)]
    public void AssessmentAccessRequiresWithEngineerOrOnwardsAndACurrentCycleExport(
        CaseLifecycleState state,
        long latestReviewVersion,
        long? latestExportVersion,
        bool expected)
    {
        var access = new AssessmentAccessState(
            state,
            latestReviewVersion,
            latestExportVersion);

        Assert.Equal(expected, access.CanOpen);
    }

    /// <summary>
    /// D11 (FRD-11): editable in Report preparation and Post report,
    /// read-only in Post-report complete; the opening states before
    /// Report preparation are refused outright so never reach the question.
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.ReportPreparation, false)]
    [InlineData(CaseLifecycleState.PostReport, false)]
    [InlineData(CaseLifecycleState.PostReportComplete, true)]
    public void AssessmentAccessIsReadOnlyOnlyOnceComplete(
        CaseLifecycleState state,
        bool expected)
    {
        var access = new AssessmentAccessState(state, 4L, 4L);

        Assert.Equal(expected, access.IsReadOnly);
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

    [Fact]
    public void EveryWritableVocabularyPathRoundTripsThroughItsCoreNormalizer()
    {
        foreach (var definition in AssessmentVocabulary.Definitions.Values
            .Where(definition => !AssessmentVocabulary.DerivedPaths.Contains(definition.Path)
                && !AssessmentVocabulary.AdoptedFindingPaths.Contains(definition.Path)))
        {
            var value = definition.Type switch
            {
                AssessmentFieldType.Text => "value",
                AssessmentFieldType.Enumerated => definition.Codes![0],
                AssessmentFieldType.WholeNumber => "1",
                AssessmentFieldType.Money => "1.00",
                AssessmentFieldType.Flag => "true",
                AssessmentFieldType.Date => "2026-09-03",
                AssessmentFieldType.Json => "[{\"zone\":\"front\",\"severity\":\"light\",\"note\":\"value\"}]",
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
        const string json = "[ { \"note\": \" Bonnet \" , \"severity\": \"light\", \"zone\": \"front\" }, { \"zone\": \"wheel_left_rear\", \"severity\": \"heavy\", \"note\": \"Wheel\" } ]";

        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { [AssessmentVocabulary.DamageImpacts] = json }));

        Assert.Equal("[{\"zone\":\"front\",\"severity\":\"light\",\"note\":\"Bonnet\"},{\"zone\":\"wheel_left_rear\",\"severity\":\"heavy\",\"note\":\"Wheel\"}]", normalized.Fields[AssessmentVocabulary.DamageImpacts]);
        Assert.Equal(("multiple", "heavy"), AssessmentPolicy.DeriveImpactValues(normalized.Fields[AssessmentVocabulary.DamageImpacts]));
        Assert.Equal(("wheel", "heavy"), AssessmentPolicy.DeriveImpactValues("[{\"zone\":\"wheel_left_rear\",\"severity\":\"heavy\",\"note\":\"\"}]"));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("[{\"zone\":\"front\",\"severity\":\"light\"}]")]
    [InlineData("[{\"zone\":\"unknown\",\"severity\":\"light\",\"note\":\"x\"}]")]
    [InlineData("[{\"zone\":\"front\",\"severity\":\"unknown\",\"note\":\"x\"}]")]
    [InlineData("[{\"zone\":\"front\",\"severity\":\"light\",\"note\":\"x\",\"extra\":\"unexpected\"}]")]
    [InlineData("[{\"zone\":\"front\",\"severity\":\"light\",\"note\":\"x\"},{\"zone\":\"front\",\"severity\":\"heavy\",\"note\":\"y\"}]")]
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
            [AssessmentVocabulary.DamageImpacts] = $"[{{\"zone\":\"front\",\"severity\":\"light\",\"note\":\"{longNote}\"}}]"
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
        // AUTO-015: the accepted Engineer's value is adopted only by the
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
    public void EveryDetailedZoneMapsToExactlyOneHeadlineParent()
    {
        Assert.Equal(23, AssessmentVocabulary.DetailedDamageZones.Count);
        Assert.Equal(8, AssessmentVocabulary.BroadDamageZones.Count);
        foreach (var zone in AssessmentVocabulary.DetailedDamageZones)
        {
            Assert.True(AssessmentVocabulary.DamageZones.ContainsKey(zone), zone);
            Assert.DoesNotContain(zone, AssessmentVocabulary.BroadDamageZones);
        }

        foreach (var broad in AssessmentVocabulary.BroadDamageZones)
        {
            // A broad region is its own headline.
            Assert.Equal(broad, AssessmentVocabulary.DamageZones[broad].ImpactLocation);
        }

        Assert.Equal("left_front", AssessmentVocabulary.DamageZones["front_left_corner"].ImpactLocation);
        Assert.Equal("front", AssessmentVocabulary.DamageZones["bonnet"].ImpactLocation);
        Assert.Equal("rear", AssessmentVocabulary.DamageZones["tailgate"].ImpactLocation);
        Assert.Equal("wheel", AssessmentVocabulary.DamageZones["wheel_left_rear"].ImpactLocation);
        Assert.All(
            AssessmentVocabulary.DamageZones.Values,
            zone => Assert.Contains(
                zone.ImpactLocation,
                AssessmentVocabulary.Definitions[AssessmentVocabulary.ImpactLocation].Codes!));
    }

    [Fact]
    public void BroadZonesAndTheirDetailedRegionsAreIndependentEntries()
    {
        // A broad impact recorded before the detailed diagram existed stays a
        // broad fact: nothing splits it into detailed regions, and a detailed
        // region recorded beside its broad parent is a second impact, not a
        // replacement for the first.
        const string json =
            "[{\"zone\":\"front\",\"severity\":\"light\",\"note\":\"Broad\"},"
            + "{\"zone\":\"front_centre\",\"severity\":\"heavy\",\"note\":\"Detailed\"}]";

        var normalized = AssessmentPolicy.ValidateAndNormalize(
            Request(new() { [AssessmentVocabulary.DamageImpacts] = json }));

        Assert.Equal(json, normalized.Fields[AssessmentVocabulary.DamageImpacts]);
        Assert.Equal(
            ("multiple", "heavy"),
            AssessmentPolicy.DeriveImpactValues(normalized.Fields[AssessmentVocabulary.DamageImpacts]));

        var broadAlone = AssessmentPolicy.DeriveImpactValues(
            "[{\"zone\":\"front\",\"severity\":\"light\",\"note\":\"Broad\"}]");
        Assert.Equal(("front", "light"), broadAlone);
    }

    [Fact]
    public void PostReviewReadinessNoLongerAsksForTheRetiredEngineerIdentityFields()
    {
        // ENG-038 / D18: the signing Engineer is the selected sign-off
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
        Assert.Contains(readiness, item => item.Requirement == "Odometer reading");
        Assert.Contains(readiness, item => item.Requirement == "Repairer VAT answer");

        var tbc = Projection(
        [
            Field("vehicle.mileage_source", "tbc")
        ]);
        Assert.DoesNotContain(
            AssessmentPolicy.EvaluateReadiness(tbc),
            item => item.Requirement == "Odometer reading");
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
        IReadOnlyList<AssessmentFieldValue> fields) => new(
        Guid.NewGuid(),
        "CE-QDOS-31-00001",
        0,
        CaseLifecycleState.Review,
        null,
        fields,
        [],
        new(null, null, null, null, null, null, null, null, null));
}
