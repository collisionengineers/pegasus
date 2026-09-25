using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Bc;

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

    private static string ReadSource(string key) =>
        File.ReadAllText(Path.Combine(ReferencePack.Root(), "astra_output", "reports", "principals", "BC", "sources", $"{key}.txt"));

    private static string? Date(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
