using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Classification;

/// <summary>
/// R713: a RE: subject is reply context for a selected instruction only when the
/// message carries In-Reply-To or References, or proves no work. Robert James
/// Solicitors write "Re:&lt;file number&gt;/&lt;client&gt;" on fresh instructions.
/// </summary>
[Collection(ParserRegexBudgetSerialTests.Name)]
public sealed class SelectedInstructionReplyContextTests
{
    private const string Subject = "Re:900001.001/Mr A Example";
    private const string Letter = "Please arrange an inspection of our client's vehicle, registration AB12 CDE.";

    [Fact]
    public void ARegardingSubjectWithoutReplyHeadersClassifiesTheProvedInstruction()
    {
        var result = Classify(Letter, replyIdentities: []);

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        Assert.Equal(MailCategory.NewInstruction, result.Category);
        Assert.Equal(CaseType.Inspection, result.CaseType);
        Assert.False(ReplyContext(result));
    }

    [Fact]
    public void AReSubjectWithReplyHeadersStaysReplyContext()
    {
        var result = Classify(Letter, replyIdentities: ["<earlier@example.invalid>"]);

        Assert.Equal(MailClassificationOutcome.Unclassified, result.Outcome);
        Assert.True(ReplyContext(result));
    }

    [Fact]
    public void AReSubjectStaysReplyContextWhenTheReaderCouldNotTell()
    {
        var result = Classify(Letter, replyIdentities: null, withRootEmail: false);

        Assert.Equal(MailClassificationOutcome.Unclassified, result.Outcome);
        Assert.True(ReplyContext(result));
    }

    [Fact]
    public void AReSubjectWithoutReplyHeadersAndWithoutProvedWorkStaysReplyContext()
    {
        var result = Classify("We can leave the report as it is.", replyIdentities: []);

        Assert.Equal(MailClassificationOutcome.Unclassified, result.Outcome);
        Assert.True(ReplyContext(result));
    }

    private static MailClassificationResult Classify(
        string instructionText, IReadOnlyList<string>? replyIdentities, bool withRootEmail = true)
    {
        IntakeContentFragment instruction = new(
            IntakeEvidenceSource.DocumentContent, "rjs.eml, attachment To Engineer with instructions.DOC", instructionText);
        var read = new IntakeSourceReadResult(
            IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.EmailBody, "rjs.eml, message body", "Please see the attached instructions."), instruction],
            [new(IntakeEvidenceSource.Sender, "handler@robertjameslaw.example"), new(IntakeEvidenceSource.Subject, Subject)],
            [],
            RequiresOcr: false,
            RootEmail: withRootEmail
                ? new IntakeEmailSummary(
                    "handler@robertjameslaw.example", null, [], [], [], Subject,
                    "Please see the attached instructions.", [], null, null, replyIdentities)
                : null);
        return new PrincipalMailClassificationPolicy("RJS").Classify(read, [instruction]);
    }

    private static bool ReplyContext(MailClassificationResult result) =>
        Assert.Single(result.Predicates, predicate => predicate.Key == "subject.reply-context").Matched;
}
