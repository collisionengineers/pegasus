using Pegasus.Core.Intake;
using Pegasus.Core.Cases;

namespace Pegasus.Core.Tests.Intake.Qdos;

public sealed class QdosMailClassificationPolicyTests
{
    [Fact]
    public void PolicyKeyAndVersionAreStable()
    {
        Assert.Equal("qdos_mail_classification", QdosMailClassificationPolicy.Key);
        Assert.Equal(4, QdosMailClassificationPolicy.Version);
    }

    [Fact]
    public void TriagePhraseInTheBodyClassifiesPreInstruction()
    {
        var result = Classify(body:
            "Triage Only Request. Please find attached our client's images.");

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        var category = Assert.IsType<MailCategory>(result.Category);
        Assert.Equal(ReceivedMailFamily.PreInstructionEmails, category.ReceivedFamily);
        Assert.Equal("triage-request", category.Subtype);
    }

    /// <summary>
    /// MAIL-012. QDOS's other triage template carries no body phrase at all —
    /// its tell is the generated subject line. U34 was one of these: a real
    /// triage request with a photograph that matched no predicate and fell
    /// through to Unclassified. The corpus holds five of them and seven of the
    /// body-phrase kind, with no message carrying both.
    /// </summary>
    [Fact]
    public void TheGeneratedTriageSubjectClassifiesPreInstruction()
    {
        var result = Classify(
            subject: "Engineer Triage - Our Claim Reference 47939/1, Vehicle registration GD65TVY",
            body: "Can you kindly advise if the vehicle would be considered repairable.");

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        var category = Assert.IsType<MailCategory>(result.Category);
        Assert.Equal(ReceivedMailFamily.PreInstructionEmails, category.ReceivedFamily);
        Assert.Equal("triage-request", category.Subtype);
    }

    /// <summary>
    /// Every QDOS message reaches the mailbox as a staff forward, so the tell
    /// sits behind a Fw: prefix in practice — as it did on U34.
    /// </summary>
    [Fact]
    public void TheTriageSubjectIsReadThroughAForwardPrefix()
    {
        var result = Classify(
            subject: "Fw: Engineer Triage - Our Claim Reference 47939/1",
            body: "Can you kindly advise if the vehicle would be considered repairable.");

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        Assert.Equal("triage-request", Assert.IsType<MailCategory>(result.Category).Subtype);
    }

    /// <summary>
    /// Both tells at once is one triage request, not two candidates. A second
    /// candidate for the same category would resolve to Ambiguous, leaving a
    /// message carrying more evidence classified worse than one carrying less.
    /// </summary>
    [Fact]
    public void BothTriageTellsTogetherStillClassifyAsOneTriageRequest()
    {
        var result = Classify(
            subject: "Engineer Triage - Our Claim Reference 47939/1",
            body: "Triage Only Request. Please advise on repairability.");

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        Assert.Equal("triage-request", Assert.IsType<MailCategory>(result.Category).Subtype);
    }

    /// <summary>
    /// The tell is a generated opening line, not the words appearing anywhere.
    /// A human writing about a triage is not an instruction to open one.
    /// </summary>
    [Theory]
    [InlineData("Chasing your Engineer Triage response for 47939/1")]
    [InlineData("engineer triage - our claim reference 47939/1")]
    public void ATriageMentionThatIsNotTheGeneratedLineIsNotTheTell(string subject)
    {
        var result = Classify(subject: subject, body: "Any update on this one?");

        Assert.Equal(MailClassificationOutcome.Unclassified, result.Outcome);
    }

    [Fact]
    public void AuditNotificationTitleInAnAttachmentClassifiesNewInstructionAudit()
    {
        var result = Classify(document:
            "AUDIT REPORT NOTIFICATION\nOur Ref: 12345/1\nPlease can you prepare an audit report.");

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        var category = Assert.IsType<MailCategory>(result.Category);
        Assert.Equal(ReceivedMailFamily.NewInstructionReceived, category.ReceivedFamily);
        Assert.Equal("audit", category.Subtype);
        Assert.Equal(CaseType.Audit, result.CaseType);
        Assert.Null(result.StandaloneAuditReport);
    }

    [Theory]
    [InlineData("The vehicle is repairable.", AuditAssessment.Repairable)]
    [InlineData("This vehicle is a total loss.", AuditAssessment.TotalLoss)]
    public void AuditInstructionAndSeparateOriginalReportProduceTheLiteralAssessment(
        string reportText,
        AuditAssessment expectedAssessment)
    {
        var result = new QdosMailClassificationPolicy().Classify(new(
            IntakeSourceReadStatus.Readable,
            [
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "message, attachment 1, audit-instructions.pdf",
                    "AUDIT REPORT NOTIFICATION\nOur Ref: 12345/1"),
                new(
                    IntakeEvidenceSource.PdfContent,
                    "message, attachment 2, original-report.pdf, page 1",
                    reportText)
            ],
            [],
            [],
            false));

        Assert.Equal(CaseType.Audit, result.CaseType);
        var report = Assert.IsType<StandaloneAuditReportEvaluation>(result.StandaloneAuditReport);
        Assert.Equal(expectedAssessment, report.Assessment);
        Assert.Equal("message, attachment 2, original-report.pdf", report.AssetSourceLabel);
    }

    [Fact]
    public void AuditInstructionWithoutASeparateOriginalReportCannotProduceAnAssessment()
    {
        var result = new QdosMailClassificationPolicy().Classify(new(
            IntakeSourceReadStatus.Readable,
            [
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "message, attachment 1, audit-instructions.pdf",
                    "AUDIT REPORT NOTIFICATION\nThis assessment is repairable.")
            ],
            [],
            [],
            false));

        Assert.Equal(CaseType.Audit, result.CaseType);
        Assert.Null(result.StandaloneAuditReport);
    }

    [Fact]
    public void OriginalReportWithBothOutcomesCannotProduceAnAssessment()
    {
        var result = new QdosMailClassificationPolicy().Classify(new(
            IntakeSourceReadStatus.Readable,
            [
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "message, attachment 1, audit-instructions.pdf",
                    "AUDIT REPORT NOTIFICATION"),
                new(
                    IntakeEvidenceSource.PdfContent,
                    "message, attachment 2, original-report.pdf, page 1",
                    "The vehicle is repairable and also a total loss.")
            ],
            [],
            [],
            false));

        Assert.Null(result.StandaloneAuditReport);
    }

    [Theory]
    [InlineData("The vehicle is unrepairable.")]
    [InlineData("The vehicle is not repairable.")]
    [InlineData("The vehicle is not a total loss.")]
    public void NegatedOrSubwordOutcomeCannotProduceAnAssessment(string reportText)
    {
        var result = new QdosMailClassificationPolicy().Classify(new(
            IntakeSourceReadStatus.Readable,
            [
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "message, attachment 1, audit-instructions.pdf",
                    "AUDIT REPORT NOTIFICATION"),
                new(
                    IntakeEvidenceSource.PdfContent,
                    "message, attachment 2, original-report.pdf, page 1",
                    reportText)
            ],
            [],
            [],
            false));

        Assert.Equal(CaseType.Audit, result.CaseType);
        Assert.Null(result.StandaloneAuditReport);
    }

    [Theory]
    [InlineData("ENGINEER NOTIFICATION (REPORT + AUDIT REPORT)\nOur Ref: 23456/1")]
    [InlineData("ENGINEER NOTIFICATION\nOur Ref: 23456/1")]
    public void EngineerNotificationTitleClassifiesNewInstructionInspection(string document)
    {
        var result = Classify(document: document);

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        var category = Assert.IsType<MailCategory>(result.Category);
        Assert.Equal(ReceivedMailFamily.NewInstructionReceived, category.ReceivedFamily);
        Assert.Equal("inspection", category.Subtype);
        Assert.Equal(
            document.Contains("REPORT + AUDIT REPORT", StringComparison.Ordinal)
                ? CaseType.InspectionAndAudit
                : CaseType.Inspection,
            result.CaseType);
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
            subject: "RE: (EREF9) RTA on 18/06/2026 : Mrs Jane Example",
            document: "AUDIT REPORT NOTIFICATION\nOur Ref: 12345/1");

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        var category = Assert.IsType<MailCategory>(result.Category);
        Assert.Equal("audit", category.Subtype);
        Assert.True(category.IsReplyContext);
    }

    [Fact]
    public void ForwardPrefixDoesNotSetReplyContext()
    {
        var result = Classify(
            subject: "FW: (EREF9) RTA on 18/06/2026 : Mrs Jane Example",
            document: "AUDIT REPORT NOTIFICATION\nOur Ref: 12345/1");

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        Assert.False(Assert.IsType<MailCategory>(result.Category).IsReplyContext);
    }

    [Fact]
    public void AuditChaserBodyWithoutAnInstructionLetterFailsClosed()
    {
        var result = Classify(
            subject: "(EREF26) RTA on 20/05/2026 : Mrs Jane Example (Our Ref: AB/98765/1)",
            body: "Please can you forward your final audit report as soon as possible.");

        Assert.Equal(MailClassificationOutcome.Unclassified, result.Outcome);
        Assert.Null(result.Category);
    }

    [Fact]
    public void SimultaneousCategoryPredicatesProduceAmbiguityWithNoInventedWinner()
    {
        var result = Classify(
            body: "Triage Only Request. Please provide an initial assessment.",
            document: "AUDIT REPORT NOTIFICATION\nOur Ref: 12345/1");

        Assert.Equal(MailClassificationOutcome.Ambiguous, result.Outcome);
        Assert.Null(result.Category);
        Assert.Equal(2, result.AmbiguousCandidates.Count);
        Assert.Null(result.CaseType);
        Assert.Contains("pre-instruction-emails/triage-request", result.AmbiguousCandidates);
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

    [Theory]
    [InlineData("this was a triage only request originally", null)]
    [InlineData(null, "Please see our audit report notification below.")]
    [InlineData(null, "The engineer notification was sent last week.")]
    public void TellsAreCaseExactSoHumanProseNeverMatchesThem(string? body, string? document)
    {
        var result = Classify(body: body, document: document);

        Assert.Equal(MailClassificationOutcome.Unclassified, result.Outcome);
        Assert.Null(result.Category);
    }

    [Fact]
    public void TellsInsideAnAttachedMessageNeverClassifyTheCarryingMessage()
    {
        // A chaser that carries the original instruction email as an
        // attachment must classify on its own content, not the original's.
        var result = new QdosMailClassificationPolicy().Classify(new(
            IntakeSourceReadStatus.Readable,
            [
                new(
                    IntakeEvidenceSource.EmailBody,
                    "message body",
                    "Please can you provide an update on this instruction."),
                new(
                    IntakeEvidenceSource.EmailBody,
                    "message body, attached email 1",
                    "Triage Only Request. Please find attached our client's images."),
                new(
                    IntakeEvidenceSource.DocumentContent,
                    "message body, attached email 1, attached letter",
                    "AUDIT REPORT NOTIFICATION\nOur Ref: 12345/1")
            ],
            [new(IntakeEvidenceSource.Subject, "RE: (EREF9) RTA on 18/06/2026")],
            [],
            false));

        Assert.Equal(MailClassificationOutcome.Unclassified, result.Outcome);
        Assert.Null(result.Category);
        Assert.Null(result.CaseType);
    }

    [Fact]
    public void CombinedMarkerInADifferentDocumentDoesNotUpgradeInspection()
    {
        var result = new QdosMailClassificationPolicy().Classify(new(
            IntakeSourceReadStatus.Readable,
            [
                new(IntakeEvidenceSource.DocumentContent, "instruction letter", "ENGINEER NOTIFICATION\nOur Ref: 23456/1"),
                new(IntakeEvidenceSource.DocumentContent, "unrelated attachment", "REPORT + AUDIT REPORT")
            ],
            [],
            [],
            false));

        Assert.Equal(CaseType.Inspection, result.CaseType);
    }

    [Fact]
    public void CombinedMarkerInsideNestedEmailDoesNotUpgradeInspection()
    {
        var result = new QdosMailClassificationPolicy().Classify(new(
            IntakeSourceReadStatus.Readable,
            [
                new(IntakeEvidenceSource.DocumentContent, "instruction letter", "ENGINEER NOTIFICATION\nOur Ref: 23456/1"),
                new(IntakeEvidenceSource.DocumentContent, "message body, attached email 1, attached letter", "ENGINEER NOTIFICATION (REPORT + AUDIT REPORT)")
            ],
            [],
            [],
            false));

        Assert.Equal(CaseType.Inspection, result.CaseType);
    }

    [Fact]
    public void CombinedMarkerWithoutEngineerTitleDoesNotCreateACaseType()
    {
        var result = Classify(document: "REPORT + AUDIT REPORT");

        Assert.Equal(MailClassificationOutcome.Unclassified, result.Outcome);
        Assert.Null(result.CaseType);
    }

    [Fact]
    public void SimultaneousAuditAndEngineerTitlesAreAmbiguousWithoutACaseType()
    {
        var result = new QdosMailClassificationPolicy().Classify(new(
            IntakeSourceReadStatus.Readable,
            [
                new(IntakeEvidenceSource.DocumentContent, "audit instruction", "AUDIT REPORT NOTIFICATION"),
                new(IntakeEvidenceSource.DocumentContent, "engineer instruction", "ENGINEER NOTIFICATION")
            ],
            [],
            [],
            false));

        Assert.Equal(MailClassificationOutcome.Ambiguous, result.Outcome);
        Assert.Null(result.CaseType);
        Assert.Contains("new-instruction-received/audit", result.AmbiguousCandidates);
        Assert.Contains("new-instruction-received/inspection", result.AmbiguousCandidates);
    }

    [Fact]
    public void EveryPredicateIsAlwaysRecordedWithAUniqueKey()
    {
        var result = Classify(body: "Anything at all.");

        Assert.Equal(6, result.Predicates.Count);
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
