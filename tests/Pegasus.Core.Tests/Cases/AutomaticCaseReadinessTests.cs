using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

public sealed class AutomaticCaseReadinessTests
{
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
