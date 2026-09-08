using System.Security.Cryptography;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Dfd;

public sealed class DfdInstructionExtractionPolicyTests
{
    [DfdReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("3963d0e94ba1-DFD_01.pdf.txt", "DFD 01.pdf", "a8eebc6d0c4088cd8d7a1ccff8dc690a5f8a13c96457d1bc04b1aed7b4930782", "RJP/81517", "2026-05-01", "Samantha Wilson", "2026-04-28", "LS69OYW", "TBC", "TPV reverses into our o/s from a side road", "DFD")]
    [InlineData("7b34209b42ae-DFD_02.pdf.txt", "DFD 02.pdf", "10f47df9a02a19c111e0ad07d9f739e904b4665fbe8a452640f4f7a1814fb72d", "RJP/81516", "2026-04-30", "Henry Jones", "2026-04-29", "YS19OUA", "Car 2 Go", "TP pulls from side road into our path", "Car 2 Go")]
    [InlineData("1e59ede6a8e1-DFD_03.pdf.txt", "DFD 03.pdf", "bc276820f055a184e8ca0f977b59e5bcdeb703ed15ebff24772f24511d0760d1", "RJP/81513", "2026-04-27", "Farid Erfani", "2026-04-24", "DA17PGO", "SMC", "TP pulls onto roundabout into our rear n/s", "SMC")]
    [InlineData("dfb9201274d6-DFD_04.pdf.txt", "DFD 04.pdf", "6b27e492c18fbb4fde08ec059acbc3a79a18e2db4fa5036dfbae32224d96744b", "RJP/81509", "2026-05-16", "Ali Abdul", "2026-04-14", "KJ17HHR", "Whippendell Road, Watford", "Hit whilst parked", "DFD")]
    [InlineData("30d1d453e4fd-DFD_05.pdf.txt", "DFD 05.pdf", "ee4fab522c6f4bb5b3fbc693f405ac6282aa166d9af5bf9868f0f9a0d728019d", "RJP/81508", "2026-04-15", "Alan Joseph", "2026-04-01", "GV12RHF", "SMC", "Parked car we are passing opens door into out path", "SMC")]
    public void RecordedFormFieldsRemainGeometryBound(string extractedFile, string originalFile, string sha256,
        string reference, string instructionDate, string claimant, string incidentDate, string registration,
        string location, string circumstances, string source)
    {
        var root = ReferencePackRoot();
        _ = File.ReadAllText(Path.Combine(root, "astra_output", "extractions", "text", extractedFile));
        var original = File.ReadAllBytes(Path.Combine(root, "principal-docs", "original-mapper-instruction-corpus", originalFile));
        Assert.Equal(sha256, Convert.ToHexStringLower(SHA256.HashData(original)));
        var result = Extract(reference, instructionDate, claimant, incidentDate, registration, location, circumstances, source);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(reference, draft.ClaimNumber);
        Assert.Equal(instructionDate, Date(draft.InstructionDate));
        Assert.Equal(claimant, draft.ClaimantName);
        Assert.Equal(incidentDate, Date(draft.DateOfIncident));
        Assert.Equal(registration, draft.VehicleRegistration);
        Assert.Equal(location, draft.InspectionAddress);
        Assert.Equal(circumstances, draft.AccidentCircumstances);
        Assert.Equal(source, Field(result, "Claim source").SuggestedValue);
        Assert.All(result.Fields.SelectMany(field => field.Candidates), candidate => Assert.Equal(IntakeLocatorKind.FormField, candidate.Locator!.Kind));
        Assert.Equal("Text4", Assert.Single(Field(result, "Claim reference").Candidates).Locator!.FormField);
        Assert.Equal("Text5", Assert.Single(Field(result, "Instruction date").Candidates).Locator!.FormField);
        Assert.Null(draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
        Assert.Null(draft.InspectionDate);
        Assert.Null(draft.VatStatus);
    }

    private static InstructionExtractionResult Extract(params string[] values)
    {
        string[] names = ["Text4", "Text5", "Text6", "Text7", "Text8", "Text10", "Text11", "Text13"];
        var fields = names.Zip(values, (name, value) => new IntakeContentFragment(
            IntakeEvidenceSource.PdfContent, $"DFD form field {name}", value, IntakeSourceLocator.ForFormField(name, 1))).ToArray();
        return new DfdInstructionExtractionPolicy().Extract(
            new(IntakeSourceReadStatus.Readable, fields, [], [], RequiresOcr: false),
            new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero), new("DFD", DfdInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
    }
    private static InstructionReviewField Field(InstructionExtractionResult result, string name) => Assert.Single(result.Fields, field => field.Name == name);
    private static string? Date(DateOnly? value) => value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    private static string ReferencePackRoot() => Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT") ?? throw new InvalidOperationException("The reference-pack test should have been skipped.");
}

internal sealed class DfdReferencePackTheoryAttribute : TheoryAttribute
{
    public DfdReferencePackTheoryAttribute()
    {
        var root = Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) Skip = "PEGASUS_REFERENCE_PACK_ROOT is absent; the immutable reference pack differs per machine.";
    }
}
