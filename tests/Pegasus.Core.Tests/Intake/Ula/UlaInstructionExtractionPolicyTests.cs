using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Ula;

/// <summary>
/// United Legal Assistance's instruction letter (R1145): the main story of the
/// sample letter of 29 September 2026 as the Word reader returns it, tabs and
/// trailing spaces included. Every letter matching the ULA signature is a
/// standalone Audit (R1138, R1155).
/// </summary>
[Collection(ParserRegexBudgetSerialTests.Name)]
public sealed class UlaInstructionExtractionPolicyTests
{
    private static readonly DateTimeOffset ReadAt = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    private const string LetterLabel = "ula.eml, attachment 1: 257922.DOC";

    // The sample is addressed to another assessor; letters to Collision
    // Engineers carry a different addressee block.
    private static readonly string[] SampleAddressee =
        ["Laird Assessors", "188-200 Pensby Road", "Heswall", "CH60 7RJ"];

    private static readonly string[] CollisionEngineersAddressee =
        ["Collision Engineers Ltd", "1 Example Street", "Exampleton", "EX1 2MP"];

    private static readonly string[] EarlierMessage = ["<earlier@example.invalid>"];

    private static readonly string[] ExtractedFieldNames =
    [
        "Claimant name", "Claim reference", "Vehicle registration", "Vehicle model", "Incident date",
        "Accident circumstances", "VAT status", "Repairer name", "Repairer address"
    ];

    [Fact]
    public void TheSampleLetterSelectsUlaAndExtractsEveryField()
    {
        var read = Read(Letter(SampleAddressee));

        var (_, result) = SelectAndExtract(read);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Empty(result.MissingFields);
        Assert.Equal(
            ExtractedFieldNames.Order(StringComparer.Ordinal),
            result.Fields.Select(field => field.Name).Order(StringComparer.Ordinal));
        Assert.All(result.Fields, field => Assert.False(field.HasConflict));
        Assert.Equal("257922", Value(result, "Claim reference"));
        Assert.Equal("Motor Parts Direct Ltd", Value(result, "Claimant name"));
        Assert.Equal("FV25JOJ", Value(result, "Vehicle registration"));
        Assert.Equal("FORD TRANSIT COURIER TREND ECOBLUE", Value(result, "Vehicle model"));
        Assert.Equal("12th August 2026", Value(result, "Incident date"));
        Assert.Equal("Yes", Value(result, "VAT status"));
        Assert.Equal("Client hit in rear by TP", Value(result, "Accident circumstances"));
        Assert.Equal("Steer Weston", Value(result, "Repairer name"));
        Assert.Equal("5 Langford Road Weston-Super-Mare Avon BS23 3HP", Value(result, "Repairer address"));

        Assert.Equal("ULA", draft.SuggestedPrincipalCode);
        Assert.Equal("Motor Parts Direct Ltd", draft.ClaimantName);
        Assert.Equal("257922", draft.ClaimNumber);
        Assert.Equal("FV25JOJ", draft.VehicleRegistration);
        Assert.Null(draft.VehicleMake);
        Assert.Equal("FORD TRANSIT COURIER TREND ECOBLUE", draft.VehicleModel);
        Assert.Equal(new DateOnly(2026, 8, 12), draft.DateOfIncident);
        Assert.Equal("Yes", draft.VatStatus);
        Assert.Equal("Client hit in rear by TP", draft.AccidentCircumstances);
        Assert.Null(draft.InspectionAddress);
        Assert.Null(draft.InspectionDate);
        Assert.Null(draft.VehicleMileage);

        // The repairer reaches the Case's own repairer facts (R1027).
        Assert.Equal(CaseDataFieldNames.RepairerName, Field(result, "Repairer name").ToCaseDataFieldName());
        Assert.Equal(CaseDataFieldNames.RepairerAddress, Field(result, "Repairer address").ToCaseDataFieldName());
    }

    [Fact]
    public void TheSignatureDoesNotDependOnTheAddressee()
    {
        var sample = SelectAndExtract(Read(Letter(SampleAddressee))).Result;
        var addressedToUs = SelectAndExtract(Read(Letter(CollisionEngineersAddressee))).Result;

        Assert.Equal(sample.InstructionDraft, addressedToUs.InstructionDraft);
        Assert.Equal(
            sample.Fields.Select(field => (field.Name, field.SuggestedValue)),
            addressedToUs.Fields.Select(field => (field.Name, field.SuggestedValue)));
    }

    [Theory]
    [InlineData("united-legal.co.uk")]
    [InlineData("Our Reference:")]
    [InlineData("Our Client")]
    [InlineData("Vehicle:")]
    [InlineData("Accident:")]
    public void ALetterMissingARequiredSignalIsNotSelected(string signal)
    {
        // Signals match case-insensitively, so every printed line carrying the
        // signal goes ("Your Reference:" carries "our Reference:").
        var letter = string.Join('\n', Letter(SampleAddressee)
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
        var selection = SelectPolicy(Read($"{Letter(SampleAddressee)}\n{firm}"));

        Assert.NotEqual(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.DoesNotContain(selection.Matches, policy => policy.PrincipalCode == "ULA");
    }

    [Fact]
    public void ExtractionRefusesAnotherPrincipalAndIncompleteContent()
    {
        var policy = new UlaInstructionExtractionPolicy();
        var letter = Letter(SampleAddressee);

        Assert.Throws<ArgumentException>(() => policy.Extract(
            new(IntakeSourceReadStatus.Readable, [new(IntakeEvidenceSource.DocumentContent, LetterLabel, letter)], [], [], false),
            ReadAt,
            new("PCH", UlaInstructionExtractionPolicy.DocumentProfileKeyValue, 1)));
        Assert.Throws<ArgumentException>(() => policy.Extract(
            new(IntakeSourceReadStatus.Readable, [new(IntakeEvidenceSource.DocumentContent, LetterLabel, letter)], [], [], RequiresOcr: false, IsIncomplete: true),
            ReadAt,
            new("ULA", UlaInstructionExtractionPolicy.DocumentProfileKeyValue, 1)));
    }

    [Fact]
    public void TheSelectedLetterClassifiesAsAStandaloneAuditWithoutAnOriginalReport()
    {
        var read = Read(Letter(SampleAddressee));
        var (selection, _) = SelectAndExtract(read);

        var classification = new PrincipalMailClassificationPolicy("ULA").Classify(read, selection.InstructionContent);

        Assert.Equal(MailClassificationOutcome.Classified, classification.Outcome);
        Assert.Equal(MailCategory.NewInstruction, classification.Category);
        Assert.Equal(CaseType.Audit, classification.CaseType);
        Assert.Null(classification.StandaloneAuditReport);
        Assert.True(Predicate(classification, "instruction.explicit-audit"));
        Assert.False(Predicate(classification, "instruction.explicit-inspection"));
    }

    [Fact]
    public void InspectionWordingBesideTheLetterDoesNotCompeteWithTheAudit()
    {
        var read = Read(Letter(SampleAddressee));
        var (selection, _) = SelectAndExtract(read);
        IntakeContentFragment covering = new(
            IntakeEvidenceSource.EmailBody, "ula.eml, message body", "Please find attached our inspection request.");

        var classification = new PrincipalMailClassificationPolicy("ULA").Classify(
            read, [.. selection.InstructionContent, covering]);

        Assert.Equal(MailClassificationOutcome.Classified, classification.Outcome);
        Assert.Equal(MailCategory.NewInstruction, classification.Category);
        Assert.Equal(CaseType.Audit, classification.CaseType);
        Assert.Null(classification.StandaloneAuditReport);
    }

    [Fact]
    public void AUlaMessageWithNoSelectedLetterProvesNoWork()
    {
        var read = Read(Letter(SampleAddressee));

        var classification = new PrincipalMailClassificationPolicy("ULA").Classify(read, []);

        Assert.Equal(MailClassificationOutcome.Unclassified, classification.Outcome);
        Assert.False(Predicate(classification, "instruction.explicit-audit"));
    }

    [Fact]
    public void AReplyCarryingTheLetterStaysReplyContext()
    {
        const string subject = "RE: 257922 Motor Parts Direct Ltd";
        var read = Read(Letter(SampleAddressee)) with
        {
            TransportEvidence =
            [
                new(IntakeEvidenceSource.Sender, "claims@united-legal.co.uk"),
                new(IntakeEvidenceSource.Subject, subject)
            ],
            RootEmail = new IntakeEmailSummary(
                "claims@united-legal.co.uk", null, [], [], [], subject,
                "Please find attached our instructions.", [], null, null, EarlierMessage)
        };
        var (selection, _) = SelectAndExtract(read);

        var classification = new PrincipalMailClassificationPolicy("ULA").Classify(read, selection.InstructionContent);

        Assert.Equal(MailClassificationOutcome.Unclassified, classification.Outcome);
        Assert.True(Predicate(classification, "subject.reply-context"));
    }

    private static string Letter(string[] addressee)
    {
        string[] lines =
        [
            "29th September 2026",
            "Our Reference:\t257922 ",
            "Your Reference:\t",
            .. addressee,
            "\tContact: Shannon Jones",
            "\tPhone: 01704 337644",
            "\tFax: 01704 468 031",
            "\tclaims@united-legal.co.uk",
            "Dear Sirs ",
            "Our Client\tMotor Parts Direct Ltd ",
            "Vehicle: \tFV25JOJ FORD TRANSIT COURIER TREND ECOBLUE  ",
            "Accident:\t12th August 2026 ",
            "VAT Registered:\tYes ",
            "Please authorise up to the rates indicated below. This is to be handled under the Rerate scheme.",
            "The circumstances of the accident are Client hit in rear by TP",
            "Please note we have instructed the following Repairer, and they will supply you with an estimate for repairs in due course. Please send the authorisation to the garage at Trade rates and send United Legal the retail report.",
            "Steer Weston ",
            "5 Langford Road Weston-Super-Mare Avon   BS23 3HP ",
            "01934628873 01173745800 notify@steer.co.uk ",
            "I have advised my client of your instructions. Please arrange the desktop inspection once the estimate is received from the Repairer.",
            "Yours faithfully",
            "Shannon Jones",
            "United Legal Assistance Ltd "
        ];
        return string.Join('\n', lines);
    }

    private static IntakeSourceReadResult Read(string letter) => new(
        IntakeSourceReadStatus.Readable,
        [
            new(IntakeEvidenceSource.EmailBody, "ula.eml, message body", "Please find attached our instructions."),
            new(IntakeEvidenceSource.DocumentContent, LetterLabel, letter)
        ],
        [
            new(IntakeEvidenceSource.Sender, "claims@united-legal.co.uk"),
            new(IntakeEvidenceSource.Subject, "257922 Motor Parts Direct Ltd")
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
        Assert.Equal("ULA", selection.Policy!.PrincipalCode);
        Assert.Equal(LetterLabel, Assert.Single(selection.InstructionContent).SourceLabel);

        var result = selection.Policy.Extract(read with { Content = selection.InstructionContent }, ReadAt,
            new("ULA", UlaInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
        Assert.Equal(InstructionPolicyApplicability.Applicable, result.Applicability);
        return (selection, result);
    }

    private static InstructionReviewField Field(InstructionExtractionResult result, string name) =>
        Assert.Single(result.Fields, field => field.Name == name);

    private static string? Value(InstructionExtractionResult result, string name) => Field(result, name).SuggestedValue;

    private static bool Predicate(MailClassificationResult result, string key) =>
        Assert.Single(result.Predicates, predicate => predicate.Key == key).Matched;
}
