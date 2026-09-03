using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

public sealed class AutomaticCaseReadinessTests
{
    private static readonly CaseWorkflowConfiguration Configuration =
        new("case-workflow", 1);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompleteEvidenceIsReadyRegardlessOfIntakeConfirmation(bool staffConfirmed)
    {
        var completeness = Complete(staffConfirmed);

        Assert.True(completeness.IsReadyForReview(automaticallyDefinitive: false));
        Assert.True(CaseCompletenessPolicy.Evaluate(
            completeness,
            Configuration,
            automaticallyDefinitive: false).SatisfiesPolicy);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void MissingEvidenceIsNotReady(
        bool instructionComplete,
        bool imagesComplete)
    {
        var completeness = new CaseCompleteness(
            instructionComplete,
            imagesComplete,
            InstructionConfirmedByStaff: instructionComplete,
            ImagesConfirmedByStaff: imagesComplete);

        Assert.False(completeness.IsReadyForReview(automaticallyDefinitive: true));
        Assert.False(CaseCompletenessPolicy.Evaluate(
            completeness,
            Configuration,
            automaticallyDefinitive: true).SatisfiesPolicy);
    }

    private static CaseCompleteness Complete(bool staffConfirmed) =>
        new(InstructionComplete: true,
            ImagesComplete: true,
            InstructionConfirmedByStaff: staffConfirmed,
            ImagesConfirmedByStaff: staffConfirmed);
}
