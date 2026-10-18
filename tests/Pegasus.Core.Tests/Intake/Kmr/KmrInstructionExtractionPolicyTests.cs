using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Kmr;

[Collection(ParserRegexBudgetSerialTests.Name)]
public sealed class KmrInstructionExtractionPolicyTests
{
    private static readonly DateTimeOffset ReadAt = new(2031, 3, 12, 9, 0, 0, TimeSpan.Zero);

    [ReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("5947593c6e4c-KMR_01.pdf.txt", "Aqeel Arshad", "01071-Arshad", "MJ25TPU", "2026-04-17")]
    [InlineData("3a7d203cdf17-KMR_02.pdf.txt", "Streetrax Ltd", "01068-Streetrax Ltd – PN22 THG", "PN22THG", "2026-04-24")]
    [InlineData("fdffcc8d627f-KMR_03.pdf.txt", "Streetrax Ltd", "01066-Streetrax Ltd – EX23 LKD", "EX23LKD", "2026-04-21")]
    [InlineData("c5b1a8f1416a-KMR_04.pdf.txt", "Streetrax Ltd", "00987-Streetrax Ltd – ET23 XJM", "ET23XJM", "2025-10-17")]
    [InlineData("f22211ec6ff2-KMR_05.pdf.txt", "Streetrax Ltd", "01058-Streetrax Ltd – MT69 UBW", "MT69UBW", "2026-03-31")]
    public void RecordedLettersSelectExtractAndClassifyAnInspection(
        string extractedFile, string claimant, string reference, string registration, string accidentDate)
    {
        var text = File.ReadAllText(Path.Combine(
            ReferencePack.Root(), "astra_output", "extractions", "text", extractedFile));
        var read = new IntakeSourceReadResult(IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.DocumentContent, extractedFile, text)], [], [], RequiresOcr: false);

        var (selection, result) = SelectAndExtract(read);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Empty(result.MissingFields);
        Assert.Equal(claimant, draft.ClaimantName);
        Assert.Equal(reference, draft.ClaimNumber);
        Assert.Equal(registration, draft.VehicleRegistration);
        Assert.Equal(DateOnly.Parse(accidentDate, System.Globalization.CultureInfo.InvariantCulture), draft.DateOfIncident);
        var classification = new PrincipalMailClassificationPolicy("KMR").Classify(read, selection.InstructionContent);
        Assert.Equal(MailCategory.NewInstruction, classification.Category);
        Assert.Equal(CaseType.Inspection, classification.CaseType);
    }

    [Fact]
    public void TheAttachedLetterSelectsExtractsAndClassifiesAnInspection()
    {
        // Invented values in KMR Law's letter layout: the letter's own date
        // above Our Reference, the registration after a dash on the Client
        // line, and the registration again in the prose.
        var letter = string.Join('\n',
            "Collision Engineers",
            "handler@kmrlaw.co.uk",
            "11 March, 2031",
            "Our Reference: 09001-Example Fleet Ltd – AB12 CDE",
            "Dear Sirs",
            "Client: Example Fleet Ltd – AB12 CDE",
            "Accident: 03/03/2031",
            "Please arrange an inspection of the damaged vehicle, an Example Coupe registration number AB12 CDE.",
            "Yours faithfully",
            "KMR Law Limited",
            "KMR Law Limited is a company registered in England and Wales Reg-00000000");
        var read = new IntakeSourceReadResult(IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.EmailBody, "kmr.eml, message body", "Please find our instructions attached."),
             new(IntakeEvidenceSource.PdfContent, "kmr.eml, attachment instruction.pdf", letter)],
            [new(IntakeEvidenceSource.Sender, "handler@kmrlaw.co.uk"),
             new(IntakeEvidenceSource.Subject, "09001-Example Fleet Ltd – AB12 CDE")],
            [], RequiresOcr: false);

        var (selection, result) = SelectAndExtract(read);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Empty(result.MissingFields);
        Assert.Equal("Example Fleet Ltd", draft.ClaimantName);
        Assert.Equal("09001-Example Fleet Ltd – AB12 CDE", draft.ClaimNumber);
        Assert.Equal("AB12CDE", draft.VehicleRegistration);
        Assert.Equal(new DateOnly(2031, 3, 3), draft.DateOfIncident);
        var classification = new PrincipalMailClassificationPolicy("KMR").Classify(read, selection.InstructionContent);
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
        Assert.Equal("KMR", selection.Policy!.PrincipalCode);

        var result = selection.Policy.Extract(read with { Content = selection.InstructionContent }, ReadAt,
            new("KMR", KmrInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
        Assert.Equal(InstructionPolicyApplicability.Applicable, result.Applicability);
        return (selection, result);
    }
}
