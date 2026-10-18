using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Swan;

[Collection(ParserRegexBudgetSerialTests.Name)]
public sealed class SwanInstructionExtractionPolicyTests
{
    private static readonly DateTimeOffset ReadAt = new(2031, 3, 12, 9, 0, 0, TimeSpan.Zero);

    [ReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("05d924776d14-SWAN_01.docx.txt", "Mr Muhammed Tahmid Nadim Ahmed", "01239- Muhammed.Ahmed", "PN19VUA", "2026-04-10")]
    [InlineData("0d75d5d58634-SWAN_02.docx.txt", "Mr Shahid Yaseen", "01214- Shahid.Yaseen", "BH17PKC", "2026-03-26")]
    [InlineData("49c7e25fcc51-SWAN_03.docx.txt", "Miss Anna Jammeh", "01200- Anna.Jammeh", "KY70RLZ", "2026-03-18")]
    [InlineData("3a15788ab795-SWAN_04.docx.txt", "Mr Souhail Elhamdanhy", "01186- Souhail.Elhamdanhy", "LX20HUH", "2026-03-10")]
    [InlineData("c5096f17c507-SWAN_05.docx.txt", "Mr Washim Saddik Mirza", "01169- Washim.Mirza", "GX64VEM", "2026-02-27")]
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
        var classification = new PrincipalMailClassificationPolicy("SWAN").Classify(read, selection.InstructionContent);
        Assert.Equal(MailCategory.NewInstruction, classification.Category);
        Assert.Equal(CaseType.Inspection, classification.CaseType);
    }

    [Fact]
    public void TheAttachedLetterSelectsExtractsAndClassifiesAnInspection()
    {
        // Invented values in Swan Solicitors' letter layout, padded with the
        // no-break spaces the template prints between a label and its value.
        const string pad = "       ";
        var letter = string.Join('\n',
            "Our Ref: 04321- Ainsley.Example ",
            "Date: 11/03/2031",
            "To the Engineer",
            "Vehicle Inspection Instructions ",
            "Dear All",
            $"Our Client:{pad}Mr Ainsley Example{pad}",
            $"Client Address:{pad}1 Example Row, Exampleton EX1 2PL",
            $"Client Contact Tel:{pad}0700 000 0000",
            $"Vehicle Reg:{pad}AB12 CDE",
            $"Vehicle Location:{pad}Example Garage, Exampleton",
            $"Accident Date:{pad}RTA - 03/03/2031 ",
            "Please arrange inspection of the vehicle and send us the report.",
            "Kind regards",
            "Swan Solicitors",
            "www.swansolicitors.co.uk");
        var read = new IntakeSourceReadResult(IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.EmailBody, "swan.eml, message body", "Please find our instructions attached."),
             new(IntakeEvidenceSource.DocumentContent, "swan.eml, attachment instruction.docx", letter)],
            [new(IntakeEvidenceSource.Sender, "handler@swansolicitors.co.uk"),
             new(IntakeEvidenceSource.Subject, "04321- Ainsley.Example")],
            [], RequiresOcr: false);

        var (selection, draft) = SelectAndExtract(read);

        Assert.Equal("Mr Ainsley Example", draft.ClaimantName);
        Assert.Equal("04321- Ainsley.Example", draft.ClaimNumber);
        Assert.Equal("AB12CDE", draft.VehicleRegistration);
        Assert.Equal(new DateOnly(2031, 3, 3), draft.DateOfIncident);
        var classification = new PrincipalMailClassificationPolicy("SWAN").Classify(read, selection.InstructionContent);
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
        Assert.Equal("SWAN", selection.Policy!.PrincipalCode);

        var result = selection.Policy.Extract(read with { Content = selection.InstructionContent }, ReadAt,
            new("SWAN", SwanInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
        Assert.Equal(InstructionPolicyApplicability.Applicable, result.Applicability);
        Assert.Empty(result.MissingFields);
        return (selection, Assert.IsType<InstructionDraft>(result.InstructionDraft));
    }
}
