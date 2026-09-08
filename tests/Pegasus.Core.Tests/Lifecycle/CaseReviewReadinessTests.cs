using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

public sealed class CaseReviewReadinessTests
{
    /// <summary>
    /// CASE-046: a posted readiness claim is never authority. Core no longer
    /// refuses on the client's booleans; the Review gate reads the persisted
    /// instruction and image completeness inside the store transaction
    /// (CaseWorkflowPersistenceTests). Any posted combination passes here.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void PostedReadinessClaimsAreNotAuthorityForReview(
        bool instructionsComplete,
        bool imagesComplete)
    {
        var request = Request(new(
            instructionsComplete,
            imagesComplete,
            "readiness-evidence"));

        var exception = Record.Exception(() => CaseLifecycleRules.ValidateReturnToReview(request));

        Assert.Null(exception);
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
