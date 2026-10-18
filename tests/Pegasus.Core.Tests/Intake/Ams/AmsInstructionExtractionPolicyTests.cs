using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Ams;

[Collection(ParserRegexBudgetSerialTests.Name)]
public sealed class AmsInstructionExtractionPolicyTests
{
    private static readonly DateTimeOffset ReadAt = new(2031, 3, 12, 9, 0, 0, TimeSpan.Zero);

    [ReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("2fc0dfd3e948-AMS_01.DOC.txt", "Mr Bilal Ali", "100085.002/RSY/SQ", "AV59DXR", "2026-04-29")]
    [InlineData("c709c6460da3-AMS_02.DOC.txt", "Mr Intikhab Alam", "100627.001/AU/SWA", "ME17DWM", "2026-04-24")]
    [InlineData("85aba646dd4f-AMS_03.DOC.txt", "Mr Ahmed Mansour A Daleel", "100623.001/AU/SWA", "MX16SYJ", "2026-04-23")]
    [InlineData("c8ba68ab82ec-AMS_04.DOC.txt", "Mr Ahmed Younis Bashir", "100621.001/RSY/SWA", "MX67RFN", "2026-04-18")]
    [InlineData("aa5e539e1bbf-AMS_05.DOC.txt", "Mr Tekleab Angosom Araya", "100615.001/RSY/SWA", "BT69BRF", "2026-04-21")]
    public void RecordedLettersSelectExtractAndClassifyAnInspection(
        string extractedFile, string claimant, string reference, string registration, string accidentDate)
    {
        var text = File.ReadAllText(Path.Combine(
            ReferencePack.Root(), "astra_output", "extractions", "text", extractedFile));
        var read = new IntakeSourceReadResult(IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.DocumentContent, extractedFile, text)], [], [], RequiresOcr: false);

        var (selection, draft) = SelectAndExtract(read);

        Assert.Equal(claimant, draft.ClaimantName);
        Assert.Equal(reference, draft.ClaimNumber);
        Assert.Equal(registration, draft.VehicleRegistration);
        Assert.Equal(DateOnly.Parse(accidentDate, System.Globalization.CultureInfo.InvariantCulture), draft.DateOfIncident);
        var classification = new PrincipalMailClassificationPolicy("AMS").Classify(read, selection.InstructionContent);
        Assert.Equal(MailCategory.NewInstruction, classification.Category);
        Assert.Equal(CaseType.Inspection, classification.CaseType);
    }

    [Fact]
    public void TheAttachedLetterSelectsExtractsAndClassifiesAnInspection()
    {
        // Invented values in AMS Solicitors' letter layout: the letter's own
        // date in words above the salutation, tab-aligned values below it.
        var letter = string.Join('\n',
            "Your Ref:  ",
            "Our Ref: 104321.001/AB/CD",
            "Date: 11th March 2031 ",
            "To the Engineer",
            "E-MAIL: handler@amssolicitors.co.uk ",
            "URGENT VEHICLE INSPECTION REQUIRED",
            "Dear Sirs",
            "Our client: \t\tMr Ainsley Example ",
            "Client's Address: \t1 Example Row, Exampleton, EX1 2PL",
            "Mobile Tel:\t             0700 000 0000  ",
            "Vehicle Reg:\t\tAB12 CDE \t\t \t\t",
            "Vehicle Location: \tExample Yard, Exampleton EX3 4YD\t",
            "Accident Date:\t\t03/03/2031 ",
            "Please inspect our client's vehicle and e-mail your report to us.",
            "Yours faithfully",
            "A Handler",
            "AMS Solicitors");
        var read = new IntakeSourceReadResult(IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.EmailBody, "ams.eml, message body", "Please find our instructions attached."),
             new(IntakeEvidenceSource.DocumentContent, "ams.eml, attachment instruction.doc", letter)],
            [new(IntakeEvidenceSource.Sender, "handler@amssolicitors.co.uk"),
             new(IntakeEvidenceSource.Subject, "104321.001/AB/CD Mr Ainsley Example")],
            [], RequiresOcr: false);

        var (selection, draft) = SelectAndExtract(read);

        Assert.Equal("Mr Ainsley Example", draft.ClaimantName);
        Assert.Equal("104321.001/AB/CD", draft.ClaimNumber);
        Assert.Equal("AB12CDE", draft.VehicleRegistration);
        Assert.Equal(new DateOnly(2031, 3, 3), draft.DateOfIncident);
        var classification = new PrincipalMailClassificationPolicy("AMS").Classify(read, selection.InstructionContent);
        Assert.Equal(MailClassificationOutcome.Classified, classification.Outcome);
        Assert.Equal(MailCategory.NewInstruction, classification.Category);
        Assert.Equal(CaseType.Inspection, classification.CaseType);
    }

    private static (InstructionPolicySelection Selection, InstructionDraft Draft) SelectAndExtract(IntakeSourceReadResult read)
    {
        var policies = typeof(IInstructionExtractionPolicy).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IInstructionExtractionPolicy).IsAssignableFrom(type))
            .Select(type => (IInstructionExtractionPolicy)Activator.CreateInstance(type)!)
            .ToArray();
        var selection = new InstructionExtractionPolicySelector(policies).Select(read, InstructionDocumentSignature.InstructionRole);
        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal("AMS", selection.Policy!.PrincipalCode);

        var result = selection.Policy.Extract(read with { Content = selection.InstructionContent }, ReadAt,
            new("AMS", AmsInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
        Assert.Equal(InstructionPolicyApplicability.Applicable, result.Applicability);
        Assert.Empty(result.MissingFields);
        return (selection, Assert.IsType<InstructionDraft>(result.InstructionDraft));
    }
}
