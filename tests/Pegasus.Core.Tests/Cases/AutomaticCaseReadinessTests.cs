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
        Assert.True(CaseCompletenessPolicy.Evaluate(facts, configuration).SatisfiesPolicy);
        Assert.False(facts.ImagesComplete);
        Assert.Empty(configuration.MissingRequirements(facts));
        Assert.Equal(["Images"], (configuration with { RequireImages = true }).MissingRequirements(facts));
    }
    private static readonly CaseWorkflowConfiguration Configuration =
        new("case-workflow", 1);

    [Fact]
    public void CompleteEvidenceIsReadyWithoutStaffConfirmation()
    {
        var completeness = new CaseCompleteness(true, true);

        Assert.True(completeness.IsReadyForReview());
        Assert.True(CaseCompletenessPolicy.Evaluate(
            completeness, Configuration).SatisfiesPolicy);
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

        Assert.False(completeness.IsReadyForReview());
        Assert.False(CaseCompletenessPolicy.Evaluate(
            completeness, Configuration).SatisfiesPolicy);
    }
}
