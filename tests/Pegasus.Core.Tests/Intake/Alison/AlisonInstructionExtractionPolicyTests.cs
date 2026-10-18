using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Alison;

[Collection(ParserRegexBudgetSerialTests.Name)]
public sealed class AlisonInstructionExtractionPolicyTests
{
    private static readonly DateTimeOffset ReadAt = new(2031, 3, 12, 9, 0, 0, TimeSpan.Zero);

    // A null is a field the letter leaves blank or does not print.
    [ReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("14f74b8b2a76-ALISON_PDF_01.pdf.txt", "Nisa Dakhrun", "HA:MA:BFD/25/42518.001", "SJ12MRY", "2025-10-08")]
    [InlineData("9606e476f7fb-ALISON_PDF_02.pdf.txt", "Mohammed Tausif", null, "BD65XRZ", "2025-10-06")]
    [InlineData("36cb9c841623-ALISON_PDF_03.pdf.txt", "Everett Omar Henry", "HA:HH:BFD/25/42505.001", "LK75FHY", "2025-09-10")]
    [InlineData("4427e93ede02-ALISON_PDF_04.pdf.txt", "Patrick Doherty", "KM:HH:BFD/25/42502.001", "AF15UHS", "2025-09-02")]
    [InlineData("bc411df4ac7e-ALISON_PDF_05.pdf.txt", "Mohammed Uzair Azhar", "HA:HH:BFD/25/42498.001", "KE17ZSO", "2025-09-06")]
    [InlineData("e03b675ee296-ALISON_WORD_01.docx.txt", "Uzair Khan", null, "YH14AMK", "2025-09-12")]
    [InlineData("dbd2ac585c2a-ALISON_WORD_02.docx.txt", "Hamza Aziz", "MS:MA:BFD/24/42269.001", null, "2025-09-03")]
    [InlineData("64355cacaa88-ALISON_WORD_03.docx.txt", "Uzmaa Patel", "MS:MA:BFD/25/42474.001", "P8UHX", "2025-08-08")]
    [InlineData("e679f6409640-ALISON_WORD_04.docx.txt", "Asifah Nasur", "HA:HH:BFD/25/42486.001", "NJ10OXC", "2025-07-28")]
    [InlineData("31cf0b7eca63-ALISON_WORD_05.docx.txt", "Raja Khan", "MS:HH:BFD/25/42426.001", null, "2025-06-20")]
    public void RecordedLettersSelectExtractAndClassifyAnInspection(
        string extractedFile, string claimant, string? reference, string? registration, string accidentDate)
    {
        var text = File.ReadAllText(Path.Combine(
            ReferencePack.Root(), "astra_output", "extractions", "text", extractedFile));
        var read = new IntakeSourceReadResult(IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.DocumentContent, extractedFile, text)], [], [], RequiresOcr: false);

        var (selection, result) = SelectAndExtract(read);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Equal(claimant, draft.ClaimantName);
        Assert.Equal(reference, draft.ClaimNumber);
        Assert.Equal(registration, draft.VehicleRegistration);
        Assert.Equal(DateOnly.Parse(accidentDate, System.Globalization.CultureInfo.InvariantCulture), draft.DateOfIncident);
        var classification = new PrincipalMailClassificationPolicy("ALISON").Classify(read, selection.InstructionContent);
        Assert.Equal(MailCategory.NewInstruction, classification.Category);
        Assert.Equal(CaseType.Inspection, classification.CaseType);
    }

    [Fact]
    public void TheAttachedLetterSelectsExtractsAndClassifiesAnInspection()
    {
        // Invented values in Alison Law Solicitors' letter layout: the letter's
        // own date in words above the salutation, and the claimant's address
        // after their name on the Our Client line.
        var letter = string.Join('\n',
            "Your Reference:",
            "Our Ref: AB:CD:EXT/31/40001.001",
            "Our Email: handler@alison-law.co.uk",
            "11 March 2031",
            "Sent by email only to:",
            "Dear",
            "Our Client:         Ainsley Example of 1 Example Row, Exampleton, EX1 2PL",
            "Vehicle Reg:       AB12 CDE",
            "Accident Date:      03/03/2031",
            "Please arrange an inspection of our client’s vehicle as soon as possible.",
            "Address:    1 Example Row, Exampleton, EX1 2PL",
            "Yours faithfully",
            "ALISON LAW SOLICITORS");
        var read = new IntakeSourceReadResult(IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.EmailBody, "alison.eml, message body", "Please find our instructions attached."),
             new(IntakeEvidenceSource.DocumentContent, "alison.eml, attachment instruction.docx", letter)],
            [new(IntakeEvidenceSource.Sender, "handler@alison-law.co.uk"),
             new(IntakeEvidenceSource.Subject, "AB:CD:EXT/31/40001.001 Ainsley Example")],
            [], RequiresOcr: false);

        var (selection, result) = SelectAndExtract(read);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Empty(result.MissingFields);
        Assert.Equal("Ainsley Example", draft.ClaimantName);
        Assert.Equal("AB:CD:EXT/31/40001.001", draft.ClaimNumber);
        Assert.Equal("AB12CDE", draft.VehicleRegistration);
        Assert.Equal(new DateOnly(2031, 3, 3), draft.DateOfIncident);
        var classification = new PrincipalMailClassificationPolicy("ALISON").Classify(read, selection.InstructionContent);
        Assert.Equal(MailClassificationOutcome.Classified, classification.Outcome);
        Assert.Equal(MailCategory.NewInstruction, classification.Category);
        Assert.Equal(CaseType.Inspection, classification.CaseType);
    }

    private static (InstructionPolicySelection Selection, InstructionExtractionResult Result) SelectAndExtract(IntakeSourceReadResult read)
    {
        var policies = typeof(IInstructionExtractionPolicy).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IInstructionExtractionPolicy).IsAssignableFrom(type))
            .Select(type => (IInstructionExtractionPolicy)Activator.CreateInstance(type)!)
            .ToArray();
        var selection = new InstructionExtractionPolicySelector(policies).Select(read, InstructionDocumentSignature.InstructionRole);
        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal("ALISON", selection.Policy!.PrincipalCode);

        var result = selection.Policy.Extract(read with { Content = selection.InstructionContent }, ReadAt,
            new("ALISON", AlisonInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
        Assert.Equal(InstructionPolicyApplicability.Applicable, result.Applicability);
        return (selection, result);
    }
}
