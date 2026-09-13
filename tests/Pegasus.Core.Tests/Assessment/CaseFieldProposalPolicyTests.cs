using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// Settlement's Proposed column (Phase 5b): the proposal's status is derived
/// from what was recorded — an Automation value waits, the same value from
/// staff accepts it, anything else corrects it — never typed by a person.
/// </summary>
public sealed class CaseFieldProposalPolicyTests
{
    private static readonly DateTimeOffset Proposed = new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Proposed.AddHours(2);

    [Fact]
    public void AnAutomationValueOnADecisionFieldIsAnAwaitingProposal()
    {
        var proposal = CaseFieldProposalPolicy.Next(
            null, AssessmentVocabulary.Outcome, "total_loss", ActorKind.Automation, "pegasus-automation", Proposed);

        Assert.NotNull(proposal);
        Assert.Equal(CaseFieldProposalStatus.Awaiting, proposal.Status);
        Assert.Equal("total_loss", proposal.ProposedValue);
        Assert.Equal("pegasus-automation", proposal.ProposedBy);
        Assert.Equal(Proposed, proposal.ProposedAtUtc);
        Assert.Null(proposal.ResolvedAtUtc);
    }

    [Fact]
    public void StaffRecordingTheProposedValueAcceptsIt()
    {
        var awaiting = Awaiting(AssessmentVocabulary.SalvageCategory, "S");

        var accepted = CaseFieldProposalPolicy.Next(
            awaiting, AssessmentVocabulary.SalvageCategory, "S", ActorKind.Staff, "engineer-1", Later);

        Assert.Equal(CaseFieldProposalStatus.Accepted, accepted!.Status);
        Assert.Equal("S", accepted.ProposedValue);
        Assert.Equal("engineer-1", accepted.ResolvedBy);
        Assert.Equal(Later, accepted.ResolvedAtUtc);
    }

    [Theory]
    [InlineData("N")]
    [InlineData(null)]
    public void StaffRecordingAnotherValueOrClearingItCorrectsIt(string? value)
    {
        var awaiting = Awaiting(AssessmentVocabulary.SalvageCategory, "S");

        var corrected = CaseFieldProposalPolicy.Next(
            awaiting, AssessmentVocabulary.SalvageCategory, value, ActorKind.Staff, "engineer-1", Later);

        Assert.Equal(CaseFieldProposalStatus.Corrected, corrected!.Status);
        Assert.Equal("S", corrected.ProposedValue);
    }

    [Fact]
    public void AResolvedProposalStaysResolvedUntilTheNextAutomationProposal()
    {
        var accepted = Awaiting(AssessmentVocabulary.LegalStatus, "roadworthy") with
        {
            Status = CaseFieldProposalStatus.Accepted,
            ResolvedBy = "engineer-1",
            ResolvedAtUtc = Later
        };

        var staffAgain = CaseFieldProposalPolicy.Next(
            accepted, AssessmentVocabulary.LegalStatus, "unroadworthy", ActorKind.Staff, "engineer-2", Later.AddHours(1));
        Assert.Same(accepted, staffAgain);

        var reproposed = CaseFieldProposalPolicy.Next(
            accepted, AssessmentVocabulary.LegalStatus, "unroadworthy", ActorKind.Automation, "pegasus-automation", Later.AddHours(2));
        Assert.Equal(CaseFieldProposalStatus.Awaiting, reproposed!.Status);
        Assert.Equal("unroadworthy", reproposed.ProposedValue);
        Assert.Null(reproposed.ResolvedBy);
    }

    [Fact]
    public void NothingIsProposedOffTheDecisionFieldsOrByAStaffWriteAlone()
    {
        Assert.Null(CaseFieldProposalPolicy.Next(
            null, AssessmentVocabulary.SettlementExcess, "250.00", ActorKind.Automation, "pegasus-automation", Proposed));
        Assert.Null(CaseFieldProposalPolicy.Next(
            null, AssessmentVocabulary.Outcome, "repairable", ActorKind.Staff, "engineer-1", Proposed));
        Assert.Null(CaseFieldProposalPolicy.Next(
            null, AssessmentVocabulary.Outcome, null, ActorKind.Automation, "pegasus-automation", Proposed));
    }

    [Fact]
    public void TheDecisionFieldsAreTheSettlementStrip()
    {
        Assert.Equal(
            new[]
            {
                AssessmentVocabulary.Outcome,
                AssessmentVocabulary.ValueEngineer,
                AssessmentVocabulary.SalvageCategory,
                AssessmentVocabulary.SalvageValue,
                AssessmentVocabulary.LegalStatus,
                AssessmentVocabulary.UnroadworthyReason
            }.Order(StringComparer.Ordinal),
            CaseFieldProposalPolicy.DecisionPaths.Order(StringComparer.Ordinal));
    }

    private static CaseFieldProposal Awaiting(string path, string value) =>
        CaseFieldProposalPolicy.Next(null, path, value, ActorKind.Automation, "pegasus-automation", Proposed)!;
}
