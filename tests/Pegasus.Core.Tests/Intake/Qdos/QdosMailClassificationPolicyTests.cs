using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Qdos;

public sealed class QdosMailClassificationPolicyTests
{
    [Fact]
    public void PolicyKeyAndVersionAreStable()
    {
        Assert.Equal("qdos_mail_classification", QdosMailClassificationPolicy.Key);
        Assert.Equal(1, QdosMailClassificationPolicy.Version);
    }

    [Fact]
    public void TriagePhraseInTheBodyClassifiesPreInstruction()
    {
        var result = Classify(body:
            "Triage Only Request. Please find attached our client's images.");

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        var category = Assert.IsType<MailCategory>(result.Category);
        Assert.Equal(ReceivedMailFamily.PreInstructionEmails, category.ReceivedFamily);
        Assert.Null(category.Subtype);
    }

    [Fact]
    public void AuditNotificationTitleInAnAttachmentClassifiesNewInstructionAudit()
    {
        var result = Classify(document:
            "AUDIT REPORT NOTIFICATION\nOur Ref: 46553/1\nPlease can you prepare an audit report.");

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        var category = Assert.IsType<MailCategory>(result.Category);
        Assert.Equal(ReceivedMailFamily.NewInstructionReceived, category.ReceivedFamily);
        Assert.Equal("audit", category.Subtype);
    }

    [Theory]
    [InlineData("ENGINEER NOTIFICATION (REPORT + AUDIT REPORT)\nOur Ref: 46913/1")]
    [InlineData("ENGINEER NOTIFICATION\nOur Ref: 46913/1")]
    public void EngineerNotificationTitleClassifiesNewInstructionInspection(string document)
    {
        var result = Classify(document: document);

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        var category = Assert.IsType<MailCategory>(result.Category);
        Assert.Equal(ReceivedMailFamily.NewInstructionReceived, category.ReceivedFamily);
        Assert.Equal("inspection", category.Subtype);
    }

    [Fact]
    public void AutomaticReplySubjectClassifiesGeneralAutoreply()
    {
        var result = Classify(subject: "Automatic reply: (EREF8) RTA on 18/06/2026");

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        var category = Assert.IsType<MailCategory>(result.Category);
        Assert.Equal(ReceivedMailFamily.General, category.ReceivedFamily);
        Assert.Equal("autoreply", category.Subtype);
    }

    [Fact]
    public void ReplyPrefixMirrorsTheUnderlyingCategoryWithReplyContext()
    {
        var result = Classify(
            subject: "RE: (EREF9) RTA on 18/06/2026 : Mr Nick Jones",
            document: "AUDIT REPORT NOTIFICATION\nOur Ref: 46553/1");

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        var category = Assert.IsType<MailCategory>(result.Category);
        Assert.Equal("audit", category.Subtype);
        Assert.True(category.IsReplyContext);
    }

    [Fact]
    public void ForwardPrefixDoesNotSetReplyContext()
    {
        var result = Classify(
            subject: "FW: (EREF9) RTA on 18/06/2026 : Mr Nick Jones",
            document: "AUDIT REPORT NOTIFICATION\nOur Ref: 46553/1");

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        Assert.False(Assert.IsType<MailCategory>(result.Category).IsReplyContext);
    }

    [Fact]
    public void AuditChaserBodyWithoutAnInstructionLetterFailsClosed()
    {
        var result = Classify(
            subject: "(EREF26) RTA on 20/05/2026 : Mrs Vivien Healey (Our Ref: TG/45497/1)",
            body: "Please can you forward your final audit report as soon as possible.");

        Assert.Equal(MailClassificationOutcome.Unclassified, result.Outcome);
        Assert.Null(result.Category);
    }

    [Fact]
    public void SimultaneousCategoryPredicatesProduceAmbiguityWithNoInventedWinner()
    {
        var result = Classify(
            body: "Triage Only Request. Please provide an initial assessment.",
            document: "AUDIT REPORT NOTIFICATION\nOur Ref: 46553/1");

        Assert.Equal(MailClassificationOutcome.Ambiguous, result.Outcome);
        Assert.Null(result.Category);
        Assert.Equal(2, result.AmbiguousCandidates.Count);
        Assert.Contains("pre-instruction-emails", result.AmbiguousCandidates);
        Assert.Contains("new-instruction-received/audit", result.AmbiguousCandidates);
    }

    [Fact]
    public void NothingMatchingFailsClosedAsUnclassified()
    {
        var result = Classify(body: "General correspondence with no generated tell.");

        Assert.Equal(MailClassificationOutcome.Unclassified, result.Outcome);
        Assert.Null(result.Category);
        Assert.Empty(result.AmbiguousCandidates);
    }

    [Fact]
    public void EveryPredicateIsAlwaysRecordedWithAUniqueKey()
    {
        var result = Classify(body: "Anything at all.");

        Assert.Equal(5, result.Predicates.Count);
        Assert.Equal(
            result.Predicates.Count,
            result.Predicates.Select(predicate => predicate.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.All(result.Predicates, predicate => Assert.False(string.IsNullOrWhiteSpace(predicate.Detail)));
    }

    private static MailClassificationResult Classify(
        string? subject = null,
        string? body = null,
        string? document = null)
    {
        var content = new List<IntakeContentFragment>();
        if (body is not null)
        {
            content.Add(new(IntakeEvidenceSource.EmailBody, "message body", body));
        }

        if (document is not null)
        {
            content.Add(new(IntakeEvidenceSource.DocumentContent, "attached letter", document));
        }

        return new QdosMailClassificationPolicy().Classify(new(
            IntakeSourceReadStatus.Readable,
            content,
            subject is null ? [] : [new(IntakeEvidenceSource.Subject, subject)],
            [],
            false));
    }
}
