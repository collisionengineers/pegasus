using System.Globalization;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Phouse;

/// <summary>
/// Parkhouse Assist's instruction letter (R1191, R1195): the main story of
/// the four "Instructing_Repairer.docx" letters of 10 and 18 November 2026 as
/// the Word reader returns it, trailing spaces included. The letter proves an
/// Inspection by its sentence about the desktop inspection (R1193).
/// </summary>
[Collection(ParserRegexBudgetSerialTests.Name)]
public sealed class PhouseInstructionExtractionPolicyTests
{
    private static readonly DateTimeOffset ReadAt = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private const string LetterLabel = "phouse.eml, attachment 2: Instructing_Repairer.docx";

    private const string DesktopInspectionSentence =
        "Please be aware the engineers will be doing a DESKTOP INSPECTION. Send images and estimate to the engineers.  ";

    private static readonly string[] EarlierMessage = ["<earlier@example.invalid>"];

    private static readonly string[] ExtractedFieldNames =
    [
        "Claimant name", "Claim reference", "Vehicle registration", "Vehicle model", "Incident date",
        "VAT status", "Claimant address", "Claimant contact number", "Repairer name"
    ];

    [Theory]
    [InlineData("PHA 5028", "Simon Dani Glass", "16 Singleton Road M7 4LU", "07956857607", "VK63 SSZ - BMW 320d", "31/10/2026", "VK63SSZ", "BMW 320d", "2026-10-31")]
    [InlineData("PHA 5029", "Eliyahu David Benouaich", "50 Albert Ave", "07395431056", "ED13 BEN – Audi A7", "16/11/2026", "ED13BEN", "Audi A7", "2026-11-16")]
    [InlineData("PHA 5030", "Joseph Pollak", "82 Rigby St, M7 4BQ", "07494549992", "DG64 POH – Toyota Auris", "22/09/2026", "DG64POH", "Toyota Auris", "2026-09-22")]
    [InlineData("PHA 5031", "Eliezer Goldberg", "16 Whitehall Road NE8 4ES", "07975851306", "LN06 COA – Toyota Estima", "17/09/2026", "LN06COA", "Toyota Estima", "2026-09-17")]
    public void EachLetterSeenSelectsParkhouseAndExtractsEveryField(
        string reference,
        string client,
        string address,
        string telephone,
        string vehicle,
        string accidentDate,
        string registration,
        string model,
        string incidentDate)
    {
        var read = Read(Letter(reference, client, address, telephone, vehicle, accidentDate));

        var (_, result) = SelectAndExtract(read);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Empty(result.MissingFields);
        Assert.Equal(
            ExtractedFieldNames.Order(StringComparer.Ordinal),
            result.Fields.Select(field => field.Name).Order(StringComparer.Ordinal));
        Assert.All(result.Fields, field => Assert.False(field.HasConflict));
        Assert.Equal(reference, Value(result, "Claim reference"));
        Assert.Equal(client, Value(result, "Claimant name"));
        Assert.Equal(model, Value(result, "Vehicle model"));
        Assert.Equal("No", Value(result, "VAT status"));
        Assert.Equal(address, Value(result, "Claimant address"));
        Assert.Equal(telephone, Value(result, "Claimant contact number"));
        Assert.Equal("Parkhouse Bodyshop", Value(result, "Repairer name"));

        Assert.Equal("PHOUSE", draft.SuggestedPrincipalCode);
        Assert.Equal(client, draft.ClaimantName);
        Assert.Equal(reference, draft.ClaimNumber);
        Assert.Equal(registration, draft.VehicleRegistration);
        Assert.Null(draft.VehicleMake);
        Assert.Equal(model, draft.VehicleModel);
        Assert.Equal(DateOnly.ParseExact(incidentDate, "yyyy-MM-dd", CultureInfo.InvariantCulture), draft.DateOfIncident);
        Assert.Equal("No", draft.VatStatus);
        Assert.Equal(address, draft.ClaimantAddress);
        Assert.Equal(telephone, draft.ClaimantContactNumber);
        Assert.Null(draft.AccidentCircumstances);
        Assert.Null(draft.InspectionAddress);
        Assert.Null(draft.InspectionDate);
        Assert.Null(draft.VehicleMileage);

        // The repairer reaches the Case's own repairer fact (R1027, R1195).
        Assert.Equal(CaseDataFieldNames.RepairerName, Field(result, "Repairer name").ToCaseDataFieldName());
    }

    [Theory]
    [InlineData("Instructing repairer:")]
    [InlineData("Our Ref:")]
    [InlineData("Our Client:")]
    [InlineData("Vehicle:")]
    [InlineData("Accident Date:")]
    [InlineData("Parkhouse Assist")]
    public void ALetterMissingARequiredSignalIsNotSelected(string signal)
    {
        // Signals match case-insensitively, so every printed line carrying the
        // signal goes.
        var letter = string.Join('\n', SampleLetter()
            .Split('\n')
            .Where(line => !line.Contains(signal, StringComparison.OrdinalIgnoreCase)));

        var selection = SelectPolicy(Read(letter));

        Assert.Equal(InstructionPolicySelectionOutcome.NotApplicable, selection.Outcome);
    }

    [Theory]
    [InlineData("Connexus Vehicle Assessors")]
    [InlineData("Exclusive Vehicle Assessors")]
    public void ANegativeSignalRefusesTheLetter(string firm)
    {
        var selection = SelectPolicy(Read($"{SampleLetter()}\n{firm}"));

        Assert.NotEqual(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.DoesNotContain(selection.Matches, policy => policy.PrincipalCode == "PHOUSE");
    }

    [Fact]
    public void ExtractionRefusesAnotherPrincipalAndIncompleteContent()
    {
        var policy = new PhouseInstructionExtractionPolicy();
        var letter = SampleLetter();

        Assert.Throws<ArgumentException>(() => policy.Extract(
            new(IntakeSourceReadStatus.Readable, [new(IntakeEvidenceSource.DocumentContent, LetterLabel, letter)], [], [], false),
            ReadAt,
            new("PCH", PhouseInstructionExtractionPolicy.DocumentProfileKeyValue, 1)));
        Assert.Throws<ArgumentException>(() => policy.Extract(
            new(IntakeSourceReadStatus.Readable, [new(IntakeEvidenceSource.DocumentContent, LetterLabel, letter)], [], [], RequiresOcr: false, IsIncomplete: true),
            ReadAt,
            new("PHOUSE", PhouseInstructionExtractionPolicy.DocumentProfileKeyValue, 1)));
    }

    [Fact]
    public void TheDesktopInspectionSentenceClassifiesANewInspectionInstruction()
    {
        var read = Read(SampleLetter());
        var (selection, _) = SelectAndExtract(read);

        var classification = new PrincipalMailClassificationPolicy("PHOUSE").Classify(read, selection.InstructionContent);

        Assert.Equal(MailClassificationOutcome.Classified, classification.Outcome);
        Assert.Equal(MailCategory.NewInstruction, classification.Category);
        Assert.Equal(CaseType.Inspection, classification.CaseType);
        Assert.True(Predicate(classification, "instruction.explicit-inspection"));
        Assert.False(Predicate(classification, "instruction.explicit-audit"));
    }

    [Fact]
    public void ALetterWithoutTheSentenceProvesNoWork()
    {
        var letter = string.Join('\n', SampleLetter()
            .Split('\n')
            .Where(line => !line.Contains("DESKTOP INSPECTION", StringComparison.Ordinal)));
        var read = Read(letter);
        var (selection, _) = SelectAndExtract(read);

        var classification = new PrincipalMailClassificationPolicy("PHOUSE").Classify(read, selection.InstructionContent);

        Assert.Equal(MailClassificationOutcome.Unclassified, classification.Outcome);
        Assert.False(Predicate(classification, "instruction.explicit-inspection"));
    }

    [Fact]
    public void TheSentenceIsParkhousesTellAlone()
    {
        var read = Read(SampleLetter());
        var (selection, _) = SelectAndExtract(read);

        var classification = new PrincipalMailClassificationPolicy("PCH").Classify(read, selection.InstructionContent);

        Assert.False(Predicate(classification, "instruction.explicit-inspection"));
    }

    [Fact]
    public void AReplyCarryingTheLetterStaysReplyContext()
    {
        const string subject = "Re: New repair instruction - PHA 5030 - DG64 POH";
        var read = Read(SampleLetter()) with
        {
            TransportEvidence =
            [
                new(IntakeEvidenceSource.Sender, "office@parkhousebodyshop.com"),
                new(IntakeEvidenceSource.Subject, subject)
            ],
            RootEmail = new IntakeEmailSummary(
                "office@parkhousebodyshop.com", null, [], [], [], subject,
                "See the attached repair estimate", [], null, null, EarlierMessage)
        };
        var (selection, _) = SelectAndExtract(read);

        var classification = new PrincipalMailClassificationPolicy("PHOUSE").Classify(read, selection.InstructionContent);

        Assert.Equal(MailClassificationOutcome.Unclassified, classification.Outcome);
        Assert.True(Predicate(classification, "subject.reply-context"));
    }

    private static string SampleLetter() => Letter(
        "PHA 5030", "Joseph Pollak", "82 Rigby St, M7 4BQ", "07494549992", "DG64 POH – Toyota Auris", "22/09/2026");

    private static string Letter(
        string reference,
        string client,
        string address,
        string telephone,
        string vehicle,
        string accidentDate)
    {
        string[] lines =
        [
            "Instructing repairer:",
            "Parkhouse Bodyshop ",
            "Unit 7, Park House Bridge Estate, 7 Langley Rd, Manchester, Pendlebury, Salford ",
            "M6 6JQ ",
            $"Our Ref: {reference}",
            "18 November 2026 ",
            "Dear Sirs",
            $"Our Client: {client}",
            $"Address:{address}",
            $"Telephone:{telephone}",
            $"Vehicle:{vehicle}",
            $"Accident Date: {accidentDate} ",
            "Vat Registered: No",
            "Await booking in date.",
            "Our clients details are provided above.  ",
            "Please liaise directly with the instructed engineer to provide repair estimates and images of the vehicle.",
            DesktopInspectionSentence,
            "Engineers details: engineers@collisionengineers.co.uk",
            "Our client has advised that the damage caused to the vehicle is:",
            "Damage to front bumper.",
            "Our client has also advised that the issues since impact are:",
            "NA ",
            "They also advised that the following areas of damage are unrelated to this accident:",
            "None",
            "Please do not attempt to involve any other Agent/Claims Management Company to deal with this incident. We have already obtained our clients instructions to deal with this matter. ",
            "Please note if the vehicle turns out to be a total loss you must submit a total loss estimate & storage dates along with your invoices in order to receive payment.",
            "Yours faithfully",
            "Parkhouse Assist"
        ];
        return string.Join('\n', lines);
    }

    private static IntakeSourceReadResult Read(string letter) => new(
        IntakeSourceReadStatus.Readable,
        [
            new(IntakeEvidenceSource.EmailBody, "phouse.eml, message body", "See attached\nRegards,\nYidi\nParkhouse Assist\nAddress:     Brulimar House, Jubilee Road, Middleton M24 2LX\nOffice:        0161 524 0000"),
            new(IntakeEvidenceSource.DocumentContent, LetterLabel, letter)
        ],
        [
            new(IntakeEvidenceSource.Sender, "office@parkhouseassist.com"),
            new(IntakeEvidenceSource.Subject, "New repair instruction - PHA 5030 - DG64 POH")
        ],
        [],
        RequiresOcr: false);

    private static InstructionPolicySelection SelectPolicy(IntakeSourceReadResult read)
    {
        var policies = typeof(IInstructionExtractionPolicy).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IInstructionExtractionPolicy).IsAssignableFrom(type))
            .Select(type => (IInstructionExtractionPolicy)Activator.CreateInstance(type)!)
            .ToArray();
        return new InstructionExtractionPolicySelector(policies).Select(read, InstructionDocumentSignature.InstructionRole);
    }

    private static (InstructionPolicySelection Selection, InstructionExtractionResult Result) SelectAndExtract(IntakeSourceReadResult read)
    {
        var selection = SelectPolicy(read);
        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal("PHOUSE", selection.Policy!.PrincipalCode);
        Assert.Equal(LetterLabel, Assert.Single(selection.InstructionContent).SourceLabel);

        var result = selection.Policy.Extract(read with { Content = selection.InstructionContent }, ReadAt,
            new("PHOUSE", PhouseInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
        Assert.Equal(InstructionPolicyApplicability.Applicable, result.Applicability);
        return (selection, result);
    }

    private static InstructionReviewField Field(InstructionExtractionResult result, string name) =>
        Assert.Single(result.Fields, field => field.Name == name);

    private static string? Value(InstructionExtractionResult result, string name) => Field(result, name).SuggestedValue;

    private static bool Predicate(MailClassificationResult result, string key) =>
        Assert.Single(result.Predicates, predicate => predicate.Key == key).Matched;
}
