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

        var result = AssessmentReportProjection.Prepare(assessment, input.Costs);

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
    public void MismatchedEngineerSignatureIsNotReady()
    {
        var input = ReadyInput();
        var mutatedFields = ReplaceField(input.Assessment.Fields, AssessmentVocabulary.EngineerQualifications, "Wrong");
        var result = AssessmentReportProjection.Project(
            input with { Assessment = input.Assessment with { Fields = mutatedFields } });

        AssertNotReady(result, "Accepted engineer signature");
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
        // Production never supplies Costs today: no accepted formula exists
        // to convert estimate lines and a rate card into a labour rate
        // (EXT-09, open decision D2). This is the honest, permanent state of
        // the capability until that formula is accepted.
        var result = AssessmentReportProjection.Project(ReadyInput() with { Costs = null });

        var reason = AssertNotReady(result, AssessmentReportProjection.RepairCostRequirement);
        Assert.Contains("EXT-09", reason.WhyOutstanding, StringComparison.Ordinal);
    }

    private static AssessmentReadinessItem AssertNotReady(
        AssessmentReportProjectionResult result, string requirement)
    {
        Assert.False(result.IsReady);
        Assert.Null(result.Snapshot);
        var reason = Assert.Single(result.Reasons, item => item.Requirement == requirement);
        return reason;
    }

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
            Field(AssessmentVocabulary.IncidentAssessed, "2026-08-03"),
            Field(AssessmentVocabulary.ImpactSeverity, "moderate"),
            Field(AssessmentVocabulary.ImpactLocation, "right_rear"),
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
            Costs: new ReportRepairCosts(5m, 30m, 50m, 20m, 5m, true));
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
