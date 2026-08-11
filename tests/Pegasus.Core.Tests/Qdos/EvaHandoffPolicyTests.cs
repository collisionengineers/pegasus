using Pegasus.Core.Eva;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Qdos;

public sealed class EvaHandoffPolicyTests
{
    [Fact]
    public void ReviewConfirmedCustodyAcceptedMappingCurrentEvidenceAndEligibleImagesAreRequired()
    {
        var eligible = new EvaHandoffEligibility(
            CaseLifecycleState.Review,
            IsArchived: false,
            RenderedWorkflowVersion: 9,
            AcceptedEvidenceVersion: 9,
            CaseCustodyConfirmed: true,
            AuditRequired: true,
            AuditCustodyConfirmed: true,
            MappingAccepted: true,
            EligibleImageCount: 2);

        Assert.Empty(EvaHandoffPolicy.Evaluate(eligible));
        foreach (var state in Enum.GetValues<CaseLifecycleState>().Where(value => value != CaseLifecycleState.Review))
        {
            Assert.Contains(
                EvaHandoffPolicy.Evaluate(eligible with { State = state }),
                reason => reason.Contains("only while the case is in Review", StringComparison.Ordinal));
        }
        Assert.NotEmpty(EvaHandoffPolicy.Evaluate(eligible with { IsArchived = true }));
        Assert.NotEmpty(EvaHandoffPolicy.Evaluate(eligible with { AcceptedEvidenceVersion = 8 }));
        Assert.NotEmpty(EvaHandoffPolicy.Evaluate(eligible with { CaseCustodyConfirmed = false }));
        Assert.NotEmpty(EvaHandoffPolicy.Evaluate(eligible with { AuditCustodyConfirmed = false }));
        Assert.NotEmpty(EvaHandoffPolicy.Evaluate(eligible with { MappingAccepted = false }));
        Assert.NotEmpty(EvaHandoffPolicy.Evaluate(eligible with { EligibleImageCount = 0 }));
        Assert.Empty(EvaHandoffPolicy.Evaluate(eligible with
        {
            AuditRequired = false,
            AuditCustodyConfirmed = false
        }));
    }
}
