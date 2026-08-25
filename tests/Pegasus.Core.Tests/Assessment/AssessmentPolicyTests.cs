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
    [InlineData(CaseLifecycleState.Review, 4L, null, false)]
    [InlineData(CaseLifecycleState.Review, 4L, 3L, false)]
    [InlineData(CaseLifecycleState.Review, 4L, 4L, true)]
    [InlineData(CaseLifecycleState.Review, 4L, 5L, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, 4L, 4L, true)]
    [InlineData(CaseLifecycleState.PostReport, 4L, 4L, false)]
    public void AssessmentAccessRequiresAnExportInTheCurrentReviewCycle(
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

    [Fact]
    public void UnknownFieldPathFailsClosed()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(new() { ["vehicle.colour"] = "red" })));
        Assert.Contains("vehicle.colour", exception.Message, StringComparison.Ordinal);
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
            .Where(value => value.Type == AssessmentFieldType.Enumerated))
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
    public void EstimateLinesValidateTypeStepAndUnpricedRules()
    {
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(lines: [Line("unknown_type")])));
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.ValidateAndNormalize(
                Request(lines: [Line("repair") with { WorkUnits = 1.25m }])));
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
