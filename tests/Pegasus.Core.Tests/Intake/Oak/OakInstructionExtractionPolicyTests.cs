using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Oak;

public sealed partial class OakInstructionExtractionPolicyTests
{
    [OakReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("1a271f062d95-OAK_01.DOC.txt", "OAK 01.DOC", "2253a09ce674ef3e52548694f14d9b00e989789212acb210f4766ecd35979da7", "TJD/GRAHAM/S486562.001", "2026-05-05", "Mr Sam Graham", "B24SRG", null, "17 Powdermill Brae, Gorebridge, EH23 4HX", "CL in the left lane of a roundabout then TP moved into CL's lane and hit CL's vehicle..", "O'malley Recovery")]
    [InlineData("e06eeacbd66a-OAK_02.DOC.txt", "OAK 02.DOC", "22395559092263e89dd7440e61e26521a0f693e87355ccdaf5f64bae77b06d4e", "TJD/PACHLA/S486035.001", "2026-05-05", "Ms Anna Pachla", "EN18KEJ", null, "19 J Annandale Street, Edinburgh, EH21 7AH", "Client was driving her Taxi in the left lane. TP was going in same direction in right hand lane. Suddenly TP changed into the clients lane and collided with the clients vehicle. She thinks his intention was to turn left..", "Hfdrz Ltd Taxi")]
    [InlineData("542cc4a722c6-OAK_03.DOC.txt", "OAK 03.DOC", "c48c8702830036066d21ddacc9f3d224a0bfe5db15d5d68ad45d76a42a46f19a", "JAA/MORGAN/S486439.001", "2026-05-05", "Mr Lewis Morgan", "CV68OVM", null, "41 Moffat Crescent, Lochgelly, KY5 9NY", "CL stationary on the road due to traffic when the oncoming TP hit CL's vehicle and proceed down the road.", "Wilson Breakdown Recovery")]
    [InlineData("4b7a89910814-OAK_04.DOC.txt", "OAK 04.DOC", "191ac025ab19d0174375e8bf831ea6083f1ed2ef61be182e4055fe01cd5cfaa2", "GHE/BUTT/S486424.001", "2026-05-05", "Mr Mohammad Butt", "SG12BLS", "TOYOTA YARIS VVT-I SR", "15 Greenacres Drive, Glasgow, G53 7BB", "that our client was proceeding correctly through a green light at a cross road when the defendant ran a red light, cutting across them to turn right, colliding with our client’s vehicle.", "Undent It")]
    [InlineData("acb244aa252f-OAK_05.DOC.txt", "OAK 05.DOC", "70424671cf11e236e570db5bf0f806a23499d7d663f857f0a2c73c67e3c89b41", "JPS/O'DONNELL/S486079.001", "2026-05-01", "Mr James O'Donnell", "MF17WYH", null, "99 Littleton Park, Barrhead, Glasgow, G78 2FA", "Clients was progressing down the narrow road as the tp initially was stationary in the passing place (photos attached). Tp all of a sudden pulled out of the passing place giving the client no where to go and colliding with the side of clients vehicle also pushing the vehicle into the hedges..", "Spray Tek Accident Repair Centre Ltd")]
    public void RecordedHeadersAndInstructionBlocksRemainRoleAligned(
        string extractedFile,
        string originalFile,
        string originalSha256,
        string reference,
        string instructionDate,
        string claimant,
        string registration,
        string? model,
        string address,
        string circumstances,
        string source)
    {
        var root = ReferencePackRoot();
        var text = File.ReadAllText(Path.Combine(root, "astra_output", "extractions", "text", extractedFile));
        var original = File.ReadAllBytes(Path.Combine(
            root, "principal-docs", "original-mapper-instruction-corpus", originalFile));
        Assert.Equal(originalSha256, Convert.ToHexStringLower(SHA256.HashData(original)));

        var result = Extract(StructuredRead(text));
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(reference, draft.ClaimNumber);
        Assert.Equal(
            instructionDate,
            draft.InstructionDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(claimant, draft.ClaimantName);
        Assert.Equal(registration, draft.VehicleRegistration);
        Assert.Null(draft.VehicleMake);
        Assert.Equal(model, draft.VehicleModel);
        Assert.Equal(address, draft.InspectionAddress);
        Assert.Equal(circumstances, draft.AccidentCircumstances);
        Assert.Equal(source, Field(result, "Source").SuggestedValue);
        Assert.Equal(source, Field(result, "Introducer").SuggestedValue);
        Assert.Equal("T1R1C3", Assert.Single(Field(result, "Claim reference").Candidates).Locator!.Cell);
        Assert.Equal("T1R1C3", Assert.Single(Field(result, "Instruction date").Candidates).Locator!.Cell);
        Assert.Equal(IntakeLocatorKind.TableCell,
            Assert.Single(Field(result, "Claim reference").Candidates).Locator!.Kind);
        Assert.Null(draft.InspectionDate);
        Assert.Null(draft.VatStatus);
    }

    [Fact]
    public void SequentialFlattenedHeaderValuesFailClosed()
    {
        var text = """
            Our Ref:
            Your Ref:
            Date:
            TJD/GRAHAM/S486562.001
            05/05/26
            URGENT VEHICLE INSPECTION REQUIRED
            Dear Sirs
            Our Client: Mr Sam Graham
            Accident: 5th May 2026
            Client reg: B24 SRG
            Client model: ,
            Yours sincerely
            Oakwood Scotland Solicitors Limited
            """;

        var result = Extract(Read(text));
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Null(draft.ClaimNumber);
        Assert.Null(draft.InstructionDate);
        Assert.Empty(Field(result, "Claim reference").Candidates);
        Assert.Empty(Field(result, "Instruction date").Candidates);
        Assert.Equal("Mr Sam Graham", draft.ClaimantName);
        Assert.Equal("B24SRG", draft.VehicleRegistration);
        Assert.Null(draft.VehicleModel);
    }

    [Fact]
    public void GenericTotalLossLanguageRemainsRequestedWorkNotOutcome()
    {
        var text = """
            Dear Sirs
            Our Client: Mr Mohammad Butt
            Accident: 3rd May 2026
            Client reg: SG12 BLS
            Client model: TOYOTA YARIS VVT-I SR
            Source: Undent It
            Please arrange an inspection of my client’s vehicle as soon as possible and provide a report detailing the damage sustained, costs of repair or cost of replacement if beyond repair.
            The introducer is called Undent It.
            Yours sincerely
            Oakwood Scotland Solicitors Limited
            """;

        var result = Extract(Read(text));

        Assert.Contains("cost of replacement if beyond repair", Field(result, "Requested work").SuggestedValue);
        Assert.DoesNotContain(result.Fields, field => field.Name.Contains("outcome", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Evidence, item =>
            item.Finding == IntakeEvidenceFinding.ExtractedField
            && item.Signal.Contains("outcome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SelectorUsesDocumentEvidenceWithoutSenderRouteActivation()
    {
        var read = Read("""
            Dear Sirs
            Our Client: Mr Sam Graham
            Client reg: B24 SRG
            Client model: ,
            Oakwood Scotland Solicitors Limited
            """);

        var selection = new InstructionExtractionPolicySelector([new OakInstructionExtractionPolicy()])
            .Select(read, InstructionDocumentSignature.InstructionRole);

        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal("OAK", selection.Policy!.PrincipalCode);
    }

    private static InstructionExtractionResult Extract(IntakeSourceReadResult read) =>
        new OakInstructionExtractionPolicy().Extract(
            read,
            new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero),
            new("OAK", OakInstructionExtractionPolicy.DocumentProfileKeyValue, 1));

    private static IntakeSourceReadResult StructuredRead(string text)
    {
        var header = HeaderTableRegex().Match(text);
        Assert.True(header.Success, "The recorded OAK extraction no longer contains its header table.");
        return Read(
            text,
            new(IntakeEvidenceSource.DocumentContent, "OAK header labels", "Our Ref:\nYour Ref:\nDate:",
                IntakeSourceLocator.ForCell(1, 1, 2)),
            new(IntakeEvidenceSource.DocumentContent, "OAK header values", header.Groups["values"].Value,
                IntakeSourceLocator.ForCell(1, 1, 3)));
    }

    private static IntakeSourceReadResult Read(string text, params IntakeContentFragment[] structured) => new(
        IntakeSourceReadStatus.Readable,
        [new(IntakeEvidenceSource.DocumentContent, "OAK instruction.DOC", text), .. structured],
        [],
        [],
        RequiresOcr: false);

    private static InstructionReviewField Field(InstructionExtractionResult result, string name) =>
        Assert.Single(result.Fields, field => field.Name == name);

    private static string ReferencePackRoot() =>
        Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT")
        ?? throw new InvalidOperationException("The reference-pack test should have been skipped.");

    [GeneratedRegex(@"(?ms)^\|\s*Our Ref:\s*Your Ref:\s*Date:\s*\|\s*(?<values>.+?)\s*\|\s*^URGENT VEHICLE", RegexOptions.CultureInvariant, 100)]
    private static partial Regex HeaderTableRegex();
}

internal sealed class OakReferencePackTheoryAttribute : TheoryAttribute
{
    public OakReferencePackTheoryAttribute()
    {
        var root = Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            Skip = "PEGASUS_REFERENCE_PACK_ROOT is absent; the immutable reference pack differs per machine.";
    }
}
