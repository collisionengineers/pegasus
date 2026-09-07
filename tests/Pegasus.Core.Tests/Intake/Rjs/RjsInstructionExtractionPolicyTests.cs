using System.Security.Cryptography;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Rjs;

public sealed class RjsInstructionExtractionPolicyTests
{
    [RjsReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("6dd2cdadd7f6-RJS_01.DOC.txt", "RJS 01.DOC", "2df5be4f88b9830b456169601b01baf73cd8a3a514304507db4d87f09989f467", "Mr Maheswaran Ratnam", "119986AA.001/LDB/LDB", "BU16AYT", "none", null, "2026-04-28", "2026-05-06", "35 Leicester Road Luton Bedfordshire LU4 8SF", "07846769280", "The claimant was stationary in traffic on Dunstable Road when the defendant has failed to maintain proper braking distance, negligently colliding with the rear a third party vehicle. Following this, the third-party vehicle has been shunted into the rear of the claimant's vehicle..")]
    [InlineData("7ce9c3843ab1-RJS_02.DOC.txt", "RJS 02.DOC", "20a755f5fa2d5b8229665e3bee136b0148bf1aa218eda5c976403cd669cb3e8d", "Mr Haroon Ahmed Mahroof", "101805.002/LDB/BN", "YH15CVF", "none", "GOLF MATCH TDI BLUEMOTION TECHNOLOGY", "2026-04-30", "2026-05-01", "27 Dale Road Luton LU1 1LJ", "07568766640", "The claimant was stationary at traffic lights on Old Bedford Road in Luton when the defendant has failed to maintain proper braking distance, negligently collding with the rear of the claimant's vehicle..")]
    [InlineData("04338a09a3b6-RJS_03.DOC.txt", "RJS 03.DOC", "a8718e95523dd99230583153d03236984d95a5b12cd63f02b1cc78320ccd215d", "Mr Mohamad Rizaf Mohamad Ilyas", "126170.001/LG/LG", "SF16EFM", "none", "HORIZON BLUE HDI S/S TEPEE ALLURE", "2026-04-29", "2026-04-30", "14 St. Edmunds Close Crawley RH11 7SR", "07706831403", "The claimant was stationary in traffic when the defendant has failed to maintain proper braking distance and has negligently collided with the rear of the claimant's vehicle. .")]
    [InlineData("b034b397ca18-RJS_04.DOC.txt", "RJS 04.DOC", "bd1b55ea9ea8861665e252697a78fd04113f5032efc5c2127e372ebf51c4d118", "Mr Abdul Malik", "126068.001/LG/LDB", "EA21YFN", "none", "IONIQ PREMIUM", "2026-04-21", "2026-04-28", "113 Dallow Road Luton LU1 1NP", "07956824499", "The claimant was correctly proceeding on Biscot Road when the defendant has failed to give way from Cavindislh Road, coming out of a one way street the wrong way whilst being chased by the police, pulling out when unsafe to do so and causing a collision with the claimant's vehicle. .")]
    [InlineData("395afa8acaf3-RJS_05.DOC.txt", "RJS 05.DOC", "4e789343422d0d79c4fa1b021b06ba2acdf318e72018e2732384bef31a220b26", "Mrs Amna Ali", "125950.001/BN/BN", "YG67SZR", "none", null, "2026-04-21", "2026-04-27", "250 Selbourne Road LU4 8LU", "07360664921", "The claimant was correctly braking on the M1 due to traffic when the defendant has failed to maintain the correct braking distance and has negligently collided with the rear of the claimants vehicle..")]
    public void RecordedInstructionsPreserveIndependentMakeModelAndContactRoles(
        string extractedFile, string originalFile, string sha256, string claimant, string reference,
        string registration, string make, string? model, string incidentDate, string instructionDate,
        string address, string mobile, string circumstances)
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
        Assert.Equal(model, draft.VehicleModel);
        Assert.Equal(incidentDate, Date(draft.DateOfIncident));
        Assert.Equal(instructionDate, Date(draft.InstructionDate));
        Assert.Equal(address, Field(result, "Claimant address").SuggestedValue);
        Assert.Equal(mobile, Field(result, "Claimant mobile").SuggestedValue);
        Assert.Equal(circumstances, draft.AccidentCircumstances);
        Assert.Null(draft.InspectionAddress);
        Assert.Null(draft.InspectionDate);
        Assert.Null(draft.VatStatus);
    }

    private static InstructionExtractionResult Extract(string text) => new RjsInstructionExtractionPolicy().Extract(
        new(IntakeSourceReadStatus.Readable, [new(IntakeEvidenceSource.DocumentContent, "RJS instruction.DOC", text)], [], [], RequiresOcr: false),
        new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero), new("RJS", RjsInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
    private static InstructionReviewField Field(InstructionExtractionResult result, string name) => Assert.Single(result.Fields, field => field.Name == name);
    private static string? Date(DateOnly? value) => value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    private static string ReferencePackRoot() => Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT") ?? throw new InvalidOperationException("The reference-pack test should have been skipped.");
}

internal sealed class RjsReferencePackTheoryAttribute : TheoryAttribute
{
    public RjsReferencePackTheoryAttribute()
    {
        var root = Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) Skip = "PEGASUS_REFERENCE_PACK_ROOT is absent; the immutable reference pack differs per machine.";
    }
}
