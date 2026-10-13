using Pegasus.Core.Intake;

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
        var derived = CaseStateMailClassification.Derive(Unclassified, reportIsSent: true);

        Assert.NotNull(derived);
        Assert.Equal(MailClassificationOutcome.Classified, derived.Outcome);
        Assert.Equal(MailCategory.Query, derived.Category);
        Assert.Equal(CaseStateMailClassification.Key, derived.PolicyKey);
        Assert.Same(Unclassified.Predicates, derived.Predicates);
    }

    [Fact]
    public void MailJoiningACaseWhoseReportIsNotSentIsACaseUpdate()
    {
        var derived = CaseStateMailClassification.Derive(null, reportIsSent: false);

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
        var derived = CaseStateMailClassification.Derive(Unclassified, reportIsSent: false);

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

        Assert.Null(CaseStateMailClassification.Derive(classified, reportIsSent: true));
    }
}
