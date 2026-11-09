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
        Assert.True(CaseCompletenessPolicy.Evaluate(facts, configuration, mileageRecorded: true).SatisfiesPolicy);
        Assert.False(facts.ImagesComplete);
        Assert.Empty(configuration.MissingRequirements(facts, mileageRecorded: true));
        Assert.Equal(["Images"], (configuration with { RequireImages = true }).MissingRequirements(facts, mileageRecorded: true));
    }
    private static readonly CaseWorkflowConfiguration Configuration =
        new("case-workflow", 1);

    [Fact]
    public void CompleteEvidenceIsReadyWithoutStaffConfirmation()
    {
        var completeness = new CaseCompleteness(true, true);

        Assert.True(completeness.IsReadyForReview(mileageRecorded: true));
        Assert.True(CaseCompletenessPolicy.Evaluate(
            completeness, Configuration, mileageRecorded: true).SatisfiesPolicy);
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

        Assert.False(completeness.IsReadyForReview(mileageRecorded: true));
        Assert.False(CaseCompletenessPolicy.Evaluate(
            completeness, Configuration, mileageRecorded: true).SatisfiesPolicy);
    }

    [Fact]
    public void NoMileageKeepsACompleteCaseOutOfReview()
    {
        // R759: no mileage keeps a Case Not ready on every route, whatever
        // the configured items; it is its own requirement.
        var completeness = new CaseCompleteness(true, true);
        var configuration = Configuration with { RequireInstructions = false, RequireImages = false };

        Assert.False(completeness.IsReadyForReview(mileageRecorded: false));
        var evaluation = CaseCompletenessPolicy.Evaluate(completeness, configuration, mileageRecorded: false);
        Assert.False(evaluation.SatisfiesPolicy);
        Assert.Equal([CaseWorkflowConfiguration.VehicleMileageRequirement], evaluation.MissingRequirements);
    }

    [Fact]
    public void AnIncompleteInstructionNamesTheMileageInItsOwnRow()
    {
        // The Instructions row lists the missing items, the mileage among
        // them, so the mileage is not a second row beside it.
        Assert.Equal(
            ["Instructions", "Images"],
            Configuration.MissingRequirements(new CaseCompleteness(false, false), mileageRecorded: false));
        Assert.Equal(
            [CaseWorkflowConfiguration.VehicleMileageRequirement, "Images"],
            Configuration.MissingRequirements(new CaseCompleteness(true, false), mileageRecorded: false));
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData(null, 41_000L, true)]
    [InlineData(CaseLifecycleState.NotReady, null, false)]
    [InlineData(CaseLifecycleState.NotReady, 41_000L, true)]
    [InlineData(CaseLifecycleState.Review, null, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, null, true)]
    public void TheMileageGateStandsOnlyOnTheWayIntoReview(CaseLifecycleState? state, long? mileage, bool open)
    {
        // A Case already past Not ready has passed the gate (R759, as R631).
        Assert.Equal(open, CaseCompletenessPolicy.MileageGateOpen(state, mileage));
    }
}
