using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Dfd;

/// <summary>
/// R773: an e-mail from the DFD route whose attached form is titled "Engineer
/// instruction request" is a new instruction for an Inspection. The shape is
/// the live receipts' (6 and 9 November 2026): a signature-only body and the
/// form, selected from the document alone; the subject plays no part except
/// as reply context.
/// </summary>
[Collection(ParserRegexBudgetSerialTests.Name)]
public sealed class DfdInstructionMailClassificationTests
{
    private const string Subject = "New Instruction - A Example - RJP/00000";

    [Fact]
    public void TheFormAloneSelectsDfdAndClassifiesANewInspectionInstruction()
    {
        var read = Read(Subject, replyIdentities: []);
        var selection = Selector().Select(read, InstructionDocumentSignature.InstructionRole);

        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal(DfdInstructionExtractionPolicy.SupportedPrincipalCode, selection.Policy!.PrincipalCode);
        Assert.All(selection.InstructionContent, fragment =>
            Assert.StartsWith(DfdLiveFormFixture.FormLabel, fragment.SourceLabel, StringComparison.Ordinal));

        var result = new PrincipalMailClassificationPolicy(DfdInstructionExtractionPolicy.SupportedPrincipalCode)
            .Classify(read, selection.InstructionContent);

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        Assert.Equal(MailCategory.NewInstruction, result.Category);
        Assert.Equal(CaseType.Inspection, result.CaseType);
        Assert.True(Predicate(result, "instruction.explicit-inspection"));
        Assert.False(Predicate(result, "subject.reply-context"));
    }

    [Fact]
    public void AReplyCarryingTheFormStaysReplyContext()
    {
        var read = Read("RE: " + Subject, replyIdentities: ["<earlier@example.invalid>"]);
        var selection = Selector().Select(read, InstructionDocumentSignature.InstructionRole);

        var result = new PrincipalMailClassificationPolicy(DfdInstructionExtractionPolicy.SupportedPrincipalCode)
            .Classify(read, selection.InstructionContent);

        Assert.Equal(MailClassificationOutcome.Unclassified, result.Outcome);
        Assert.True(Predicate(result, "subject.reply-context"));
    }

    private static bool Predicate(MailClassificationResult result, string key) =>
        Assert.Single(result.Predicates, predicate => predicate.Key == key).Matched;

    private static IntakeSourceReadResult Read(string subject, IReadOnlyList<string> replyIdentities)
    {
        const string sender = "handler@dfd-solicitors.example";
        const string body = "Kind regards,\nA Handler\nDavison Flynn Duke Solicitors";
        return new(
            IntakeSourceReadStatus.Readable,
            [
                new(IntakeEvidenceSource.EmailBody, DfdLiveFormFixture.EmailLabel + ", message body", body),
                .. DfdLiveFormFixture.Content(("Text16", "5/11/26"), ("Text17", "6/11/26"))
            ],
            [new(IntakeEvidenceSource.Sender, sender), new(IntakeEvidenceSource.Subject, subject)],
            [],
            RequiresOcr: false,
            RootEmail: new IntakeEmailSummary(sender, null, [], [], [], subject, body, [], null, null, replyIdentities));
    }

    /// <summary>Every registered profile, so the form must select DFD against all of them.</summary>
    private static InstructionExtractionPolicySelector Selector() => new(
    [
        new QdosInstructionExtractionPolicy(),
        new PchInstructionExtractionPolicy(),
        new AxInstructionExtractionPolicy(),
        new FwInstructionExtractionPolicy(),
        new QclInstructionExtractionPolicy(),
        new OakInstructionExtractionPolicy(),
        new SblInstructionExtractionPolicy(),
        new BlackInstructionExtractionPolicy(),
        new RjsInstructionExtractionPolicy(),
        new DfdInstructionExtractionPolicy(),
        new KbsInstructionExtractionPolicy(),
        new KerrInstructionExtractionPolicy(),
        new MpInstructionExtractionPolicy(),
        new YmlInstructionExtractionPolicy(),
        new AlsInstructionExtractionPolicy(),
        new BcInstructionExtractionPolicy(),
        new SwanInstructionExtractionPolicy(),
        new TenInstructionExtractionPolicy(),
        new AmsInstructionExtractionPolicy(),
        new AlisonInstructionExtractionPolicy(),
        new KmrInstructionExtractionPolicy()
    ]);
}
