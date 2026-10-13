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

    [Fact]
    public void MailJoiningACaseWhoseReportIsSentIsAQuery()
    {
        var derived = CaseStateMailClassification.Derive(Unclassified, CaseLifecycleState.ReportSent);

        Assert.NotNull(derived);
        Assert.Equal(MailClassificationOutcome.Classified, derived.Outcome);
        Assert.Equal(MailCategory.Query, derived.Category);
        Assert.Equal(CaseStateMailClassification.Key, derived.PolicyKey);
        Assert.Same(Unclassified.Predicates, derived.Predicates);
    }

    [Theory]
    [InlineData(CaseLifecycleState.NotReady)]
    [InlineData(CaseLifecycleState.Held)]
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    [InlineData(CaseLifecycleState.PrincipalCancelled)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected)]
    [InlineData(CaseLifecycleState.CreatedInError)]
    [InlineData(CaseLifecycleState.SourceEmailUnlinked)]
    public void MailJoiningACaseWhoseReportIsNotSentIsACaseUpdate(CaseLifecycleState state)
    {
        var derived = CaseStateMailClassification.Derive(null, state);

        Assert.NotNull(derived);
        Assert.Equal(MailClassificationOutcome.Classified, derived.Outcome);
        Assert.Equal(MailCategory.CaseUpdate, derived.Category);
        Assert.Equal(CaseStateMailClassification.Key, derived.PolicyKey);
        Assert.Equal("The message joined the linked Case without a category of its own.", derived.Reason);
        Assert.Empty(derived.Predicates);
    }

    [Fact]
    public void MailJoiningATriageCaseIsACaseUpdate()
    {
        var derived = CaseStateMailClassification.Derive(Unclassified, null);

        Assert.NotNull(derived);
        Assert.Equal(MailCategory.CaseUpdate, derived.Category);
        Assert.Same(Unclassified.Predicates, derived.Predicates);
    }

    [Fact]
    public void AClassifiedDecisionIsNeverOverridden()
    {
        var classified = MailClassificationResult.Classified(
            MailCategory.Billing,
            [],
            "Staff correction.",
            PrincipalMailClassificationPolicy.Key,
            PrincipalMailClassificationPolicy.Version);

        Assert.Null(CaseStateMailClassification.Derive(classified, CaseLifecycleState.ReportSent));
    }
}
