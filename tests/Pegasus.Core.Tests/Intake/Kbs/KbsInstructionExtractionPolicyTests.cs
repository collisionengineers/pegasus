using System.Security.Cryptography;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Kbs;

public sealed class KbsInstructionExtractionPolicyTests
{
    [KbsReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("07dc1024aa79-KBS_01.docx.txt", "KBS 01.docx", "bcc96a21cb4418349f0a720c1b64545560b058d8ea7d19162bd2e8d1b2397730", "Mr Muzamal Hussain", "-.303671", "VX17GBU", "Toyota", "2026-05-02", "2026-05-06", "38 Highfield Avenue Stoke-on-Trent ST3 5LZ", "07595 531 859")]
    [InlineData("0e2f8b069759-KBS_02.pdf.txt", "KBS 02.pdf", "882c6ed6d2c78599b1d5bab4013a25288f1d9cc64b9c3ab0b58b4e68df5e7db8", "Mr Obaider Khan", "AA.506044", "KA58MMM", "Toyota", "2026-05-03", "2026-05-06", "7-8 Sydney Rd, Birmingham, B9 4QB", "0121 448 3037")]
    [InlineData("1727f4662ea0-KBS_03.pdf.txt", "KBS 03.pdf", "ab970f4d2b22e4665ba0240f26129f3247e7a091822e734c0a129b7c5b99f22f", "Mr Saeed Ahmed", "SK.506039", "DX18FCG", "Toyota", "2026-03-27", "2026-05-05", "85 Hamilton Road, Stoke-on-Trent, ST3 4RP", "07583115768")]
    [InlineData("bd8a5f25e061-KBS_04.pdf.txt", "KBS 04.pdf", "cbc01789c2808a572006880a02bc97962c3a57252e23b94061bc5a35d0c87b0e", "Mr Khayam Ahmed", "AA.303669", "GL18EVC", "Volkswagen", "2026-05-01", "2026-05-06", "295 Uttoxeter Road Stoke-On-Trent ST3 5LQ", "07595 531 859")]
    [InlineData("dfd09f1ccfaa-KBS_05.pdf.txt", "KBS 05.pdf", "7bdd91f82cb2877de5e231bdacf2335f65b8d2e3308c96f479c39eb1a8d4c3fe", "Mr Aasam Fareed", "AA.506036", "LO21GNN", "Audi", "2026-04-30", "2026-05-04", "85 Seddon Road, Stoke-on-Trent, ST3 5PA", "07583115768")]
    public void RecordedInstructionsPreserveExplicitRolesAndUnavailableFields(string extractedFile, string originalFile,
        string sha256, string claimant, string reference, string registration, string make, string incidentDate,
        string instructionDate, string location, string contact)
    {
        var root = ReferencePackRoot();
        var text = File.ReadAllText(Path.Combine(root, "astra_output", "extractions", "text", extractedFile));
        var original = File.ReadAllBytes(Path.Combine(root, "principal-docs", "original-mapper-instruction-corpus", originalFile));
        Assert.Equal(sha256, Convert.ToHexStringLower(SHA256.HashData(original)));
        var result = Extract(text);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(claimant, draft.ClaimantName);
        Assert.Equal(reference, draft.ClaimNumber);
        Assert.Equal(registration, draft.VehicleRegistration);
        Assert.Equal(make, draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
        Assert.Equal(incidentDate, Date(draft.DateOfIncident));
        Assert.Equal(instructionDate, Date(draft.InstructionDate));
        Assert.Equal(location, draft.InspectionAddress);
        Assert.Equal(contact, Field(result, "Inspection contact").SuggestedValue);
        Assert.NotNull(draft.AccidentCircumstances);
        Assert.Null(draft.InspectionDate);
        Assert.Null(draft.VatStatus);
    }
    private static InstructionExtractionResult Extract(string text) => new KbsInstructionExtractionPolicy().Extract(
        new(IntakeSourceReadStatus.Readable, [new(IntakeEvidenceSource.DocumentContent, "KBS instruction", text, IntakeSourceLocator.ForPage(1))], [], [], RequiresOcr: false),
        new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero), new("KBS", KbsInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
    private static InstructionReviewField Field(InstructionExtractionResult result, string name) => Assert.Single(result.Fields, field => field.Name == name);
    private static string? Date(DateOnly? value) => value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    private static string ReferencePackRoot() => Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT") ?? throw new InvalidOperationException("The reference-pack test should have been skipped.");
}
internal sealed class KbsReferencePackTheoryAttribute : TheoryAttribute
{
    public KbsReferencePackTheoryAttribute()
    {
        var root = Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) Skip = "PEGASUS_REFERENCE_PACK_ROOT is absent; the immutable reference pack differs per machine.";
    }
}
