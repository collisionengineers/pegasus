using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Black;

/// <summary>
/// R796: an e-mail from the Blackstone Legal route whose attached letter says
/// "Please accept this correspondence as formal instructions to inspect our
/// clients vehicle and prepare a report" is a new instruction for an
/// Inspection, under either of the firm's subjects. The shape is the live
/// e-mails' (25 September to 15 October 2026): a one-line body and the letter,
/// selected from the letter alone; the subject plays no part except as reply
/// context.
/// </summary>
[Collection(ParserRegexBudgetSerialTests.Name)]
public sealed class BlackInstructionMailClassificationTests
{
    [Theory]
    [InlineData("info@blackstone-legal.co.uk", BlackLiveLetterFixture.NewInstructionsSubject)]
    [InlineData("Claims@blackstone-legal.co.uk", BlackLiveLetterFixture.OurClientSubject)]
    public void TheLetterAloneSelectsBlackAndClassifiesANewInspectionInstruction(string sender, string subject)
    {
        var read = Read(sender, subject, replyIdentities: []);
        var selection = Selector().Select(read, InstructionDocumentSignature.InstructionRole);

        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal(BlackInstructionExtractionPolicy.SupportedPrincipalCode, selection.Policy!.PrincipalCode);
        Assert.All(selection.InstructionContent, fragment =>
            Assert.StartsWith(BlackLiveLetterFixture.LetterLabel, fragment.SourceLabel, StringComparison.Ordinal));

        var route = new PrincipalMailRoutePolicy().Evaluate(read, selection);

        Assert.Equal(MailRouteDisposition.Accepted, route.Disposition);
        Assert.Equal(BlackInstructionExtractionPolicy.SupportedPrincipalCode, route.SelectedRoute?.PrincipalCode);

        var result = new PrincipalMailClassificationPolicy(BlackInstructionExtractionPolicy.SupportedPrincipalCode)
            .Classify(read, selection.InstructionContent);

        Assert.Equal(MailClassificationOutcome.Classified, result.Outcome);
        Assert.Equal(MailCategory.NewInstruction, result.Category);
        Assert.Equal(CaseType.Inspection, result.CaseType);
        Assert.True(Predicate(result, "instruction.explicit-inspection"));
        Assert.False(Predicate(result, "subject.reply-context"));
    }

    [Fact]
    public void AReplyCarryingTheLetterStaysReplyContext()
    {
        var read = Read("info@blackstone-legal.co.uk", "RE: " + BlackLiveLetterFixture.NewInstructionsSubject,
            replyIdentities: ["<earlier@example.invalid>"]);
        var selection = Selector().Select(read, InstructionDocumentSignature.InstructionRole);

        var result = new PrincipalMailClassificationPolicy(BlackInstructionExtractionPolicy.SupportedPrincipalCode)
            .Classify(read, selection.InstructionContent);

        Assert.Equal(MailClassificationOutcome.Unclassified, result.Outcome);
        Assert.True(Predicate(result, "subject.reply-context"));
    }

    private static bool Predicate(MailClassificationResult result, string key) =>
        Assert.Single(result.Predicates, predicate => predicate.Key == key).Matched;

    private static IntakeSourceReadResult Read(string sender, string subject, IReadOnlyList<string> replyIdentities) =>
        new(
            IntakeSourceReadStatus.Readable,
            [
                new(IntakeEvidenceSource.EmailBody, BlackLiveLetterFixture.EmailLabel + ", message body", BlackLiveLetterFixture.Body),
                BlackLiveLetterFixture.Page()
            ],
            [new(IntakeEvidenceSource.Sender, sender), new(IntakeEvidenceSource.Subject, subject)],
            [],
            RequiresOcr: false,
            RootEmail: new IntakeEmailSummary(sender, null, [], [], [], subject, BlackLiveLetterFixture.Body, [], null, null, replyIdentities));

    /// <summary>Every registered profile, so the letter must select BLACK against all of them.</summary>
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
