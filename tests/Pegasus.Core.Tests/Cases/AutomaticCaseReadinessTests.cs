using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

public sealed class AutomaticCaseReadinessTests
{
    [Fact]
    public void ReadinessEvaluationHasValueEqualityAcrossIndependentReads()
    {
        var first = new CaseCompletenessEvaluation(false, "case-workflow", 1)
        {
            MissingRequirements = new List<string> { "Images" }
        };
        var second = first with { MissingRequirements = new List<string> { "Images" } };
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, second with { MissingRequirements = ["Instructions"] });
    }

    [Fact]
    public void ConfiguredRequirementsChangeReadinessWithoutChangingEvidence()
    {
        var facts = new CaseCompleteness(true, false);
        var configuration = new CaseWorkflowConfiguration("case-workflow", 2) { RequireImages = false };
        Assert.True(CaseCompletenessPolicy.Evaluate(facts, configuration, CaseVehicleGate.Open).SatisfiesPolicy);
        Assert.False(facts.ImagesComplete);
        Assert.Empty(configuration.MissingRequirements(facts, CaseVehicleGate.Open));
        Assert.Equal(["Images"], (configuration with { RequireImages = true }).MissingRequirements(facts, CaseVehicleGate.Open));
    }
    private static readonly CaseWorkflowConfiguration Configuration =
        new("case-workflow", 1);

    [Fact]
    public void CompleteEvidenceIsReadyWithoutStaffConfirmation()
    {
        var completeness = new CaseCompleteness(true, true);

        Assert.True(completeness.IsReadyForReview(CaseVehicleGate.Open));
        Assert.True(CaseCompletenessPolicy.Evaluate(
            completeness, Configuration, CaseVehicleGate.Open).SatisfiesPolicy);
        Assert.Equal(
            ["ImagesComplete", "InstructionComplete"],
            typeof(CaseCompleteness).GetProperties()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void MissingEvidenceIsNotReady(
        bool instructionComplete,
        bool imagesComplete)
    {
        var completeness = new CaseCompleteness(instructionComplete, imagesComplete);

        Assert.False(completeness.IsReadyForReview(CaseVehicleGate.Open));
        Assert.False(CaseCompletenessPolicy.Evaluate(
            completeness, Configuration, CaseVehicleGate.Open).SatisfiesPolicy);
    }

    [Fact]
    public void NoMileageKeepsACompleteCaseOutOfReview()
    {
        // R759: no mileage keeps a Case Not ready on every route, whatever
        // the configured items; it is its own requirement.
        var completeness = new CaseCompleteness(true, true);
        var configuration = Configuration with { RequireInstructions = false, RequireImages = false };

        var noMileage = new CaseVehicleGate(RegistrationRecorded: true, MileageRecorded: false);
        Assert.False(completeness.IsReadyForReview(noMileage));
        var evaluation = CaseCompletenessPolicy.Evaluate(completeness, configuration, noMileage);
        Assert.False(evaluation.SatisfiesPolicy);
        Assert.Equal([CaseWorkflowConfiguration.VehicleMileageRequirement], evaluation.MissingRequirements);
    }

    [Fact]
    public void NoRegistrationKeepsACaseWithItsMileageOutOfReview()
    {
        // R1081: a Case with no registration stays Not ready even when its
        // mileage is recorded, the one item the automatic route's waiver
        // does not cover, and the registration is named as what it waits for.
        var completeness = new CaseCompleteness(true, true);
        var noRegistration = new CaseVehicleGate(RegistrationRecorded: false, MileageRecorded: true);

        Assert.False(completeness.IsReadyForReview(noRegistration));
        var evaluation = CaseCompletenessPolicy.Evaluate(completeness, Configuration, noRegistration);
        Assert.False(evaluation.SatisfiesPolicy);
        Assert.Equal([CaseWorkflowConfiguration.VehicleRegistrationRequirement], evaluation.MissingRequirements);
        // The name is the instruction item's own label, as the mileage's is.
        Assert.Contains(
            Pegasus.Core.Intake.InstructionDraftFields.All,
            field => field.CaseDataFieldName == CaseDataFieldNames.VehicleRegistration
                && field.Label == CaseWorkflowConfiguration.VehicleRegistrationRequirement);
    }

    [Fact]
    public void NeitherVehicleFactNamesTheRegistrationFirst()
    {
        // In the order they are asked for (R759: the mileage after the registration).
        Assert.Equal(
            [
                CaseWorkflowConfiguration.VehicleRegistrationRequirement,
                CaseWorkflowConfiguration.VehicleMileageRequirement,
                "Images"
            ],
            Configuration.MissingRequirements(new CaseCompleteness(true, false), new CaseVehicleGate(false, false)));
    }

    [Fact]
    public void AnIncompleteInstructionNamesTheMileageInItsOwnRow()
    {
        // The Instructions row lists the missing items, the mileage among
        // them, so the mileage is not a second row beside it.
        Assert.Equal(
            ["Instructions", "Images"],
            Configuration.MissingRequirements(new CaseCompleteness(false, false), new CaseVehicleGate(false, false)));
        Assert.Equal(
            [CaseWorkflowConfiguration.VehicleMileageRequirement, "Images"],
            Configuration.MissingRequirements(new CaseCompleteness(true, false), new CaseVehicleGate(true, false)));
    }

    [Theory]
    [InlineData(null, "AB12CDE", null, true, false)]
    [InlineData(null, "AB12CDE", 41_000L, true, true)]
    [InlineData(null, null, 41_000L, false, true)]
    [InlineData(null, " ", 41_000L, false, true)]
    [InlineData(CaseLifecycleState.NotReady, "AB12CDE", null, true, false)]
    [InlineData(CaseLifecycleState.NotReady, "AB12CDE", 41_000L, true, true)]
    [InlineData(CaseLifecycleState.NotReady, null, 41_000L, false, true)]
    [InlineData(CaseLifecycleState.NotReady, null, null, false, false)]
    [InlineData(CaseLifecycleState.Review, null, null, true, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, null, null, true, true)]
    public void TheVehicleGateStandsOnlyOnTheWayIntoReview(
        CaseLifecycleState? state,
        string? registration,
        long? mileage,
        bool registrationRecorded,
        bool mileageRecorded)
    {
        // A Case already past Not ready has passed the gate (R759, R1081,
        // as R631): neither fact sends it back.
        var gate = CaseCompletenessPolicy.VehicleGate(state, registration, mileage);
        Assert.Equal(new CaseVehicleGate(registrationRecorded, mileageRecorded), gate);
        Assert.Equal(registrationRecorded && mileageRecorded, gate.IsOpen);
    }
}
