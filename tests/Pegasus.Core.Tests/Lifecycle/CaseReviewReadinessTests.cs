using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

public sealed class CaseReviewReadinessTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void ReviewAlwaysRequiresCompleteInstructionsAndImages(
        bool instructionsComplete,
        bool imagesComplete)
    {
        var request = Request(new(
            instructionsComplete,
            imagesComplete,
            "readiness-evidence"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => CaseLifecycleRules.ValidateReturnToReview(request));

        Assert.Equal("Review requires complete instructions and images.", exception.Message);
    }

    [Fact]
    public void CompleteInstructionsAndImagesSatisfyReviewReadiness()
    {
        CaseLifecycleRules.ValidateReturnToReview(Request(new(
            InstructionsComplete: true,
            ImagesComplete: true,
            "readiness-evidence")));
    }

    private static ReturnCaseToReviewRequest Request(CaseReadinessEvidence readiness) => new(
        Guid.NewGuid(),
        3,
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
        "review-operation",
        "Ready to send",
        new string('l', CaseEditAuthority.LeaseTokenLength),
        readiness);
}
