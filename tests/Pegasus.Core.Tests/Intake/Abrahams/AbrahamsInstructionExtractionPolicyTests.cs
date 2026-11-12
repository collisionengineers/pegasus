using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Abrahams;

/// <summary>
/// Invented values in the layout of Abrahams Solicitors' letter, as the real
/// reader projects the four originals seen (21 April, 30 October, 9 and 10
/// November 2026; R979).
/// </summary>
[Collection(ParserRegexBudgetSerialTests.Name)]
public sealed class AbrahamsInstructionExtractionPolicyTests
{
    private static readonly DateTimeOffset ReadAt = new(2031, 3, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheAttachedLetterSelectsExtractsAndClassifiesAnInspection()
    {
        var read = Read(Letter("Our Client: Mr Ainsley Example", "Accident: 03/03/2031"));

        var (selection, draft, missing) = SelectAndExtract(read);

        Assert.Equal("Mr Ainsley Example", draft.ClaimantName);
        Assert.Equal("104321.001", draft.ClaimNumber);
        Assert.Equal(new DateOnly(2031, 3, 3), draft.DateOfIncident);
        Assert.Empty(missing);
        var classification = new PrincipalMailClassificationPolicy("ABRAHAMS").Classify(read, selection.InstructionContent);
        Assert.Equal(MailClassificationOutcome.Classified, classification.Outcome);
        Assert.Equal(MailCategory.NewInstruction, classification.Category);
        Assert.Equal(CaseType.Inspection, classification.CaseType);
    }

    [Fact]
    public void TheLetterStatesNoRegistrationSoTheDraftHasNone()
    {
        // R981: the Case is still created, and starts Not ready.
        var (_, draft, _) = SelectAndExtract(Read(Letter("Our Client: Mr Ainsley Example", "Accident: 03/03/2031")));

        Assert.Null(draft.VehicleRegistration);
        Assert.Contains("Vehicle registration", InstructionDraftCompleteness.MissingFieldNames(draft));
    }

    [Fact]
    public void ALetterWithNoAccidentLineIsReadWithTheDateMissing()
    {
        // One original prints the client's address and telephone and another
        // no Accident line; the sender's own note in the e-mail is not read.
        var read = Read(
            Letter("Our Client: Mr Ainsley Example", "Address: 1 Example Row", "Exampleton", "EX1 2PL", "Mobile Tel: 07000000000"),
            body: "Hi,\n\nPlease see attached instruction.\n\nDOA - 3/3/2031");

        var (_, draft, missing) = SelectAndExtract(read);

        Assert.Equal("Mr Ainsley Example", draft.ClaimantName);
        Assert.Equal("104321.001", draft.ClaimNumber);
        Assert.Null(draft.DateOfIncident);
        Assert.Equal("Incident date", Assert.Single(missing));
    }

    [Fact]
    public void AnotherFirmsLetterWithTheSameHeadingAndLabelsIsNotAbrahams()
    {
        var letter = Letter("Our Client: Mr Ainsley Example", "Accident: 03/03/2031")
            .Replace("Abrahams Solicitors", "Robert James Solicitors", StringComparison.Ordinal);

        var selection = new InstructionExtractionPolicySelector(Policies())
            .Select(Read(letter), InstructionDocumentSignature.InstructionRole);

        Assert.DoesNotContain(selection.Matches, policy => policy.PrincipalCode == "ABRAHAMS");
    }

    private static string Letter(params string[] clientLines)
    {
        string[] lines =
        [
            "Your Reference:",
            "Our Reference:\t104321.001",
            "",
            "Collision Engineers Ltd",
            "77 - 79 Hoylake Road",
            "Wirral",
            "CH46 9PY",
            "",
            "11th March 2031",
            "",
            "URGENT VEHICLE INSPECTION REQUIRED",
            "",
            "Dear Sirs",
            "",
            .. clientLines,
            "",
            "I act on behalf of my above named client in the recovery of damages resulting from a  accident which occurred on the above date.",
            "",
            "Please arrange an inspection of my client’s vehicle as soon as possible and provide a report detailing the damage sustained, costs of repair or cost of replacement if beyond repair.",
            "",
            "Yours faithfully",
            "",
            "Abrahams Solicitors"
        ];
        return string.Join('\n', lines);
    }

    private static IntakeSourceReadResult Read(string letter, string body = "Hi,\n\nPlease see attached instruction.") =>
        new(IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.EmailBody, "abrahams.eml, message body", body),
             new(IntakeEvidenceSource.DocumentContent, "abrahams.eml, attachment 1: To Engineer with instructions.DOC", letter)],
            [new(IntakeEvidenceSource.Sender, "handler@abrahamssolicitors.co.uk"),
             new(IntakeEvidenceSource.Subject, "PCR:104321.001/Mr A Example")],
            [], RequiresOcr: false);

    private static IInstructionExtractionPolicy[] Policies() =>
        typeof(IInstructionExtractionPolicy).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IInstructionExtractionPolicy).IsAssignableFrom(type))
            .Select(type => (IInstructionExtractionPolicy)Activator.CreateInstance(type)!)
            .ToArray();

    private static (InstructionPolicySelection Selection, InstructionDraft Draft, IReadOnlyList<string> Missing) SelectAndExtract(IntakeSourceReadResult read)
    {
        var selection = new InstructionExtractionPolicySelector(Policies()).Select(read, InstructionDocumentSignature.InstructionRole);
        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal("ABRAHAMS", selection.Policy!.PrincipalCode);

        var result = selection.Policy.Extract(read with { Content = selection.InstructionContent }, ReadAt,
            new("ABRAHAMS", AbrahamsInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
        Assert.Equal(InstructionPolicyApplicability.Applicable, result.Applicability);
        return (selection, Assert.IsType<InstructionDraft>(result.InstructionDraft), result.MissingFields);
    }
}
