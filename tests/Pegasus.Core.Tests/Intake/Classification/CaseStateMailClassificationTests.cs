using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Intake.Classification;

public sealed class CaseStateMailClassificationTests
{
    private static readonly MailClassificationResult Unclassified = MailClassificationResult.Unclassified(
        [new("subject.reply-prefix", false, "No reply prefix.")],
        "No accepted classification predicate matched.",
        PrincipalMailClassificationPolicy.Key,
        PrincipalMailClassificationPolicy.Version);

    [Theory]
    [InlineData(CaseLifecycleState.PostReport)]
    [InlineData(CaseLifecycleState.PostReportComplete)]
    [InlineData(CaseLifecycleState.Query)]
    public void MailJoiningACaseWhoseReportIsOutIsAPostReportQuery(CaseLifecycleState state)
    {
        var derived = CaseStateMailClassification.Derive(Unclassified, state);

        Assert.NotNull(derived);
        Assert.Equal(MailClassificationOutcome.Classified, derived.Outcome);
        Assert.Equal(MailCategory.Received(ReceivedMailFamily.PostReportEmails, "query"), derived.Category);
        Assert.Equal(CaseStateMailClassification.Key, derived.PolicyKey);
        Assert.Same(Unclassified.Predicates, derived.Predicates);
    }

    [Theory]
    [InlineData(CaseLifecycleState.NotReady)]
    [InlineData(CaseLifecycleState.Held)]
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    public void MailJoiningACaseStillInProgressIsOngoingCorrespondence(CaseLifecycleState state)
    {
        var derived = CaseStateMailClassification.Derive(null, state);

        Assert.NotNull(derived);
        Assert.Equal(
            MailCategory.Received(ReceivedMailFamily.InProgressCases, "ongoing-correspondence"),
            derived.Category);
        Assert.Empty(derived.Predicates);
    }

    [Theory]
    [InlineData(CaseLifecycleState.PrincipalCancelled)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected)]
    [InlineData(CaseLifecycleState.CreatedInError)]
    [InlineData(CaseLifecycleState.SourceEmailUnlinked)]
    public void AClosedCaseDerivesNothing(CaseLifecycleState state) =>
        Assert.Null(CaseStateMailClassification.Derive(Unclassified, state));

    [Fact]
    public void AClassifiedDecisionIsNeverOverridden()
    {
        var classified = MailClassificationResult.Classified(
            MailCategory.Received(ReceivedMailFamily.Billing, "remittance"),
            [],
            "Staff correction.",
            PrincipalMailClassificationPolicy.Key,
            PrincipalMailClassificationPolicy.Version);

        Assert.Null(CaseStateMailClassification.Derive(classified, CaseLifecycleState.PostReportComplete));
    }
}
