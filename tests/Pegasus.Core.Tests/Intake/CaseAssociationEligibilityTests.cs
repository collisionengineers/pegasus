using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Intake;

public sealed class CaseAssociationEligibilityTests
{
    public static TheoryData<CaseLifecycleState, bool, bool> FormalCases()
    {
        var data = new TheoryData<CaseLifecycleState, bool, bool>();
        foreach (var state in Enum.GetValues<CaseLifecycleState>())
        {
            var eligibleState = state is CaseLifecycleState.NotReady
                or CaseLifecycleState.Held
                or CaseLifecycleState.Review
                or CaseLifecycleState.ReportPreparation;
            data.Add(state, false, eligibleState);
            data.Add(state, true, false);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(FormalCases))]
    public void AFormalCaseIsEligibleOnlyBeforeItsReportIsSent(
        CaseLifecycleState state,
        bool hasReportSentEvidence,
        bool expected) =>
        Assert.Equal(expected, CaseAssociationEligibility.IsFormalCaseEligible(state, hasReportSentEvidence));

    [Theory]
    [InlineData(TriageState.Open, false, true)]
    [InlineData(TriageState.AwaitingInformation, false, true)]
    [InlineData(TriageState.FindingRecorded, false, true)]
    [InlineData(TriageState.Completed, false, false)]
    [InlineData(TriageState.Cancelled, false, false)]
    [InlineData(TriageState.Open, true, false)]
    public void ATriageCaseTakesImagesWhileOpenAndUnlinked(TriageState state, bool linked, bool expected) =>
        Assert.Equal(expected, CaseAssociationEligibility.IsTriageCaseEligible(state, linked));

    [Fact]
    public void AFormalCaseTakesPrecedenceOverATriageCase()
    {
        var formal = Candidate("QDOS26119", isTriage: false);
        var triage = Candidate("t.QDOS26118", isTriage: true);

        Assert.Equal([formal], CaseAssociationEligibility.PreferFormalCases([triage, formal]));
        Assert.Equal([triage], CaseAssociationEligibility.PreferFormalCases([triage]));
        Assert.Equal([formal], CaseAssociationEligibility.FormalCases([triage, formal]));
        Assert.Empty(CaseAssociationEligibility.FormalCases([triage]));
    }

    private static ImageIntakeCaseCandidate Candidate(string reference, bool isTriage) =>
        new(Guid.NewGuid(), reference, 1, "DN63WGG", DateTimeOffset.UnixEpoch,
            Guid.NewGuid(), "QDOS", isTriage);
}
