using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Bc;

[Collection(ParserRegexBudgetSerialTests.Name)]
public sealed class BcInstructionExtractionPolicyTests
{
    [ReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("42b02fa3992b", "BC 01.DOC", "a917a59c8ac78c4f5887f14544497899612b8503f443b7841601e66567f65544", "Mr Sabih Tariq", "RTA135646.001/NE/Usayd Ibrahim", "FM18UUS", "2026-04-29", "266 Gayfield Avenue Brierley Hill Dudley DY5 2SU", "266 Gayfield Avenue Brierley Hill Dudley  DY5 2SU")]
    [InlineData("7e335673e863", "BC 02.DOC", "44b4f5665b21723befa2e13712a553d236e23d862ed2061f964212477eb33adf", "Mr Arslan Tariq", "RTA135650.001/NE/Usayd Ibrahim", "BR19AYT", "2026-04-20", "6 Laxey Close Chadderton Oldham OL9 8NX", "6 Laxey Close Chadderton Oldham  OL9 8NX")]
    [InlineData("60278b711ba0", "BC 03.DOC", "b031745bb46d740cad472cc4be29705a338e819ebf3c7981033fad447cbd3809", "Mr Humayun Tufail", "RTA135633.001/NE/Usayd Ibrahim", "NL22HNZ", "2026-04-30", "50 Abbey Road Huddersfield HD2 1BB", "50 Abbey Road Huddersfield   HD2 1BB")]
    [InlineData("6b882806cfda", "BC 04.DOC", "4240c22b2de79ba94dbbe87aeab675e329b9c8507af864bd2250ce35b664a87c", "Mr Muhammad Aon", "RTA135598.001/NE/Usayd Ibrahim", "DE61CYJ", "2026-04-24", "1 Tyndall Street Oldham OL4 5LA", "1 Tyndall Street Oldham   OL4 5LA")]
    [InlineData("af26a496d0cf", "BC 05.DOC", "1363d914b187c57d6a75f26da31c183c61ce30862c160b9120926153aae1efe5", "Mr Muhammad Shahid Ramzan", "RTA135610.001/NE/Usayd Ibrahim", "LH12BKD", "2026-04-28", "55 Arnold Street Huddersfield HD2 2TA", "55 Arnold Street Huddersfield   HD2 2TA")]
    public void ContextualHeaderAndTerminalRegistrationRemainBounded(
        string key,
        string original,
        string sha,
        string claimant,
        string reference,
        string vrm,
        string incident,
        string address,
        string printedAddress)
    {
        var root = ReferencePack.Root();
        var text = ReadSource(key);
        Assert.Equal(
            sha,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(
                Path.Combine(root, "principal-docs", "original-mapper-instruction-corpus", original)))));

        var result = new BcInstructionExtractionPolicy().Extract(
            new(IntakeSourceReadStatus.Readable, [new(IntakeEvidenceSource.DocumentContent, "BC instruction", text)], [], [], false),
            new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero),
            new("BC", BcInstructionExtractionPolicy.DocumentProfileKeyValue, 1));

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(claimant, draft.ClaimantName);
        Assert.Equal(reference, draft.ClaimNumber);
        Assert.Equal(vrm, draft.VehicleRegistration);
        Assert.Equal(incident, Date(draft.DateOfIncident));
        Assert.Equal(address, draft.InspectionAddress);
        Assert.Equal(
            printedAddress,
            Assert.Single(Assert.Single(result.Fields, field => field.Name == "Inspection address").Candidates).SourceValue);
        Assert.Equal(
            "Physical vehicle inspection",
            Assert.Single(result.Fields, field => field.Name == "Requested inspection method").SuggestedValue);
        Assert.Null(draft.InspectionDate);
        Assert.Null(draft.VatStatus);
        Assert.Null(draft.AccidentCircumstances);
    }

    [ReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("42b02fa3992b")]
    public void ReversedMarkersAndInvalidContextFailClosed(string key)
    {
        var text = ReadSource(key);
        var policy = new BcInstructionExtractionPolicy();

        var reversed = $"Baker & Coleman Solicitors Limited\n{text.Replace("Baker & Coleman Solicitors Limited", string.Empty, StringComparison.Ordinal)}";
        var reversedResult = policy.Extract(
            new(IntakeSourceReadStatus.Readable, [new(IntakeEvidenceSource.DocumentContent, "BC instruction", reversed)], [], [], false),
            DateTimeOffset.UtcNow,
            new("BC", BcInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
        Assert.All(reversedResult.Fields, field => Assert.Null(field.SuggestedValue));

        var selector = new InstructionExtractionPolicySelector([policy]);
        var wrongPrincipal = Regex.Replace(
            text,
            @"Baker\s*&\s*Coleman",
            "Auto Logistic Solutions",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        Assert.NotEqual(text, wrongPrincipal);
        Assert.NotEqual(
            InstructionPolicySelectionOutcome.Selected,
            selector.Select(
                new(IntakeSourceReadStatus.Readable, [new(IntakeEvidenceSource.DocumentContent, "wrong principal", wrongPrincipal)], [], [], false),
                InstructionDocumentSignature.InstructionRole).Outcome);

        Assert.Throws<ArgumentException>(() => policy.Extract(
            new(IntakeSourceReadStatus.Readable, [new(IntakeEvidenceSource.DocumentContent, "incomplete", text)], [], [], RequiresOcr: false, IsIncomplete: true),
            DateTimeOffset.UtcNow,
            new("BC", BcInstructionExtractionPolicy.DocumentProfileKeyValue, 1)));
    }

    private static readonly DateTimeOffset ReadAt = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

    // Baker & Coleman's request sentences as their letters print them, around
    // invented parties: the physical wording of the April 2026 letters and the
    // desktop wording of the 6 November 2026 letter (R793).
    private const string DesktopRequest = "Please would you prepare a desktop report of your findings.";
    private const string PhysicalRequest = "Please arrange to inspect our client's vehicle and prepare a report of your findings.";

    [Fact]
    public void TheDesktopReportLetterSelectsExtractsAndClassifiesAnInspection()
    {
        var read = Read(Letter(DesktopRequest));

        var (selection, result) = SelectAndExtract(read);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Empty(result.MissingFields);
        Assert.Equal("Mr A Example", draft.ClaimantName);
        Assert.Equal("RTA900001.001/EX/Example Handler", draft.ClaimNumber);
        Assert.Equal("AB12CDE", draft.VehicleRegistration);
        Assert.Equal(new DateOnly(2026, 11, 1), draft.DateOfIncident);
        Assert.Null(Assert.Single(result.Fields, field => field.Name == "Requested inspection method").SuggestedValue);
        var classification = new PrincipalMailClassificationPolicy("BC").Classify(read, selection.InstructionContent);
        Assert.Equal(MailClassificationOutcome.Classified, classification.Outcome);
        Assert.Equal(MailCategory.NewInstruction, classification.Category);
        Assert.Equal(CaseType.Inspection, classification.CaseType);
    }

    [Fact]
    public void ThePhysicalInspectionLetterStillClassifiesAnInspection()
    {
        var read = Read(Letter(PhysicalRequest));

        var (selection, result) = SelectAndExtract(read);

        Assert.Equal(
            "Physical vehicle inspection",
            Assert.Single(result.Fields, field => field.Name == "Requested inspection method").SuggestedValue);
        var classification = new PrincipalMailClassificationPolicy("BC").Classify(read, selection.InstructionContent);
        Assert.Equal(MailClassificationOutcome.Classified, classification.Outcome);
        Assert.Equal(MailCategory.NewInstruction, classification.Category);
        Assert.Equal(CaseType.Inspection, classification.CaseType);
    }

    [Fact]
    public void ALetterWithoutARequestSentenceFailsClosed()
    {
        var read = Read(Letter(request: null));

        var (selection, _) = SelectAndExtract(read);
        var classification = new PrincipalMailClassificationPolicy("BC").Classify(read, selection.InstructionContent);

        Assert.Equal(MailClassificationOutcome.Unclassified, classification.Outcome);
        Assert.False(Assert.Single(classification.Predicates, predicate => predicate.Key == "instruction.explicit-inspection").Matched);
    }

    [Fact]
    public void AReplyCarryingTheDesktopReportLetterStaysReplyContext()
    {
        var read = Read(Letter(DesktopRequest)) with
        {
            TransportEvidence =
            [
                new(IntakeEvidenceSource.Sender, "handler@bakercoleman.co.uk"),
                new(IntakeEvidenceSource.Subject, "RE: RTA900001.001  -  Mr A Example")
            ]
        };

        var (selection, _) = SelectAndExtract(read);
        var classification = new PrincipalMailClassificationPolicy("BC").Classify(read, selection.InstructionContent);

        Assert.Equal(MailClassificationOutcome.Unclassified, classification.Outcome);
        Assert.True(Assert.Single(classification.Predicates, predicate => predicate.Key == "subject.reply-context").Matched);
    }

    private static string Letter(string? request)
    {
        string?[] lines =
        [
            "RTA900001.001/EX/Example Handler",
            "9th November 2026",
            "Collision Engineers",
            "Dear Sirs",
            "Re:\tClient: Mr A Example",
            "Accident:\t1st November 2026",
            "Address:\t1 Example Street, Exampleton EX1 2MP",
            "We act on behalf of the above named in connection with a road traffic accident which occurred on 1st November 2026.",
            "Our client's vehicle, registered number AB12 CDE, was damaged in the accident.",
            request,
            "Please also request the engineer to comment on whether temporary repairs would be viable and if not the reasons why.",
            "Yours faithfully",
            "Baker & Coleman Solicitors Limited"
        ];
        return string.Join('\n', lines.Where(line => line is not null));
    }

    private static IntakeSourceReadResult Read(string letter) => new(
        IntakeSourceReadStatus.Readable,
        [
            new(IntakeEvidenceSource.EmailBody, "bc.eml, message body", "Please find attached our letter of instructions."),
            new(IntakeEvidenceSource.DocumentContent, "bc.eml, attachment 1: Engineer Ltr RTA Initial Instructions.DOC", letter)
        ],
        [
            new(IntakeEvidenceSource.Sender, "handler@bakercoleman.co.uk"),
            new(IntakeEvidenceSource.Subject, "RTA900001.001  -  Mr A Example")
        ],
        [],
        RequiresOcr: false);

    private static (InstructionPolicySelection Selection, InstructionExtractionResult Result) SelectAndExtract(IntakeSourceReadResult read)
    {
        var policies = typeof(IInstructionExtractionPolicy).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IInstructionExtractionPolicy).IsAssignableFrom(type))
            .Select(type => (IInstructionExtractionPolicy)Activator.CreateInstance(type)!)
            .ToArray();
        var selection = new InstructionExtractionPolicySelector(policies).Select(read, InstructionDocumentSignature.InstructionRole);
        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal("BC", selection.Policy!.PrincipalCode);

        var result = selection.Policy.Extract(read with { Content = selection.InstructionContent }, ReadAt,
            new("BC", BcInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
        Assert.Equal(InstructionPolicyApplicability.Applicable, result.Applicability);
        return (selection, result);
    }

    private static string ReadSource(string key) =>
        File.ReadAllText(Path.Combine(ReferencePack.Root(), "astra_output", "reports", "principals", "BC", "sources", $"{key}.txt"));

    private static string? Date(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
