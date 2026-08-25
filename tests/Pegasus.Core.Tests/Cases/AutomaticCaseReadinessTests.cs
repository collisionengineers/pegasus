using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// An automatically created case used to be born with every completeness flag
/// false and a policy that demanded staff confirmation nobody would ever give,
/// so it could never leave Not ready however complete it was. QDOS26009 held
/// claimant, claim number, incident date, make, model and registration and
/// still read "details incomplete" (CASE-013).
/// </summary>
public sealed class AutomaticCaseReadinessTests
{
    private static readonly CaseWorkflowConfiguration RequiresEverything = new(
        RequireStaffInstructionReviewBeforeEngineerAssignment: true,
        RequireStaffImageReviewBeforeEngineerAssignment: true,
        PolicyKey: "case-workflow",
        PolicyVersion: 1);

    [Fact]
    public void AnAutomaticallyDefinitiveIntakeIsReadyWithoutStaffConfirmation() =>
        Assert.True(
            CaseCompletenessPolicy
                .Evaluate(Complete(staffConfirmed: false), RequiresEverything, automaticallyDefinitive: true)
                .SatisfiesPolicy);

    [Fact]
    public void StaffAcceptanceIsNotExemptFromTheStaffReviewRequirement() =>
        Assert.False(
            CaseCompletenessPolicy
                .Evaluate(Complete(staffConfirmed: false), RequiresEverything, automaticallyDefinitive: false)
                .SatisfiesPolicy);

    [Fact]
    public void TheWaiverCoversStaffReviewOnlyAndNotMissingEvidence() =>
        Assert.False(
            CaseCompletenessPolicy
                .Evaluate(
                    new CaseCompleteness(false, false, false, false),
                    RequiresEverything,
                    automaticallyDefinitive: true)
                .SatisfiesPolicy);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CompletenessCannotBeWaivedByStaffReviewConfiguration(
        bool requireInstructionReview,
        bool requireImageReview)
    {
        var configuration = new CaseWorkflowConfiguration(
            requireInstructionReview,
            requireImageReview,
            "case-workflow",
            1);

        Assert.False(CaseCompletenessPolicy.Evaluate(
            new(false, true, false, true), configuration).SatisfiesPolicy);
        Assert.False(CaseCompletenessPolicy.Evaluate(
            new(true, false, true, false), configuration).SatisfiesPolicy);
    }

    [Fact]
    public void TheReadinessRuleAndTheAcceptancePolicyAgreeOnTheWaiver()
    {
        var completeness = Complete(staffConfirmed: false);

        Assert.Equal(
            completeness.IsReadyForReview(automaticallyDefinitive: true),
            CaseCompletenessPolicy
                .Evaluate(completeness, RequiresEverything, automaticallyDefinitive: true)
                .SatisfiesPolicy);
        Assert.Equal(
            completeness.IsReadyForReview(automaticallyDefinitive: false),
            CaseCompletenessPolicy
                .Evaluate(completeness, RequiresEverything, automaticallyDefinitive: false)
                .SatisfiesPolicy);
    }

    private static CaseCompleteness Complete(bool staffConfirmed) =>
        new(InstructionComplete: true,
            ImagesComplete: true,
            InstructionConfirmedByStaff: staffConfirmed,
            ImagesConfirmedByStaff: staffConfirmed);
}
