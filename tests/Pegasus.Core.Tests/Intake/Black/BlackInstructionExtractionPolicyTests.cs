using System.Security.Cryptography;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Black;

public sealed class BlackInstructionExtractionPolicyTests
{
    [BlackReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("82495b41afe2-BLACK_01.pdf.txt", "BLACK 01.pdf", "bf9f91b87f71dec6ecbea3f77d54c0522867c9a446599a1ebf5f03d859fa4385", "261436SA", "2026-05-06", "Mr Abraham Huruy", "2026-04-30", "Flat 1-2, 24 Potter Street, Newport Gwent, NP20 2DB", "07405340061", "Toyota Prius - BF16 FOT", "Toyota Prius", "BF16FOT")]
    [InlineData("558b9c4efaa4-BLACK_02.pdf.txt", "BLACK 02.pdf", "7deb35748a1bff7f8804f312b1cee297c0abfdc4323474ab9e3142cf909fb834", "261435SA", "2026-05-06", "Mr Osman Ifow", "2026-05-03", "Brewery Street, Aston, Birmingham, B6 4JB", "07847577303", "Toyota Prius - BX66 SZV", "Toyota Prius", "BX66SZV")]
    [InlineData("e80d16c3a841-BLACK_03.pdf.txt", "BLACK 03.pdf", "201fb07ae667c1300ca773f24c6dee1e66e8eb63364e30932ff626e660e98071", "261434SA", "2026-05-05", "Mr Hassan Butt", "2026-04-30", "Brewery Street, Aston, Birmingham, B6 4JB", "07936853974", "Toyota Prius - BR19 SRX", "Toyota Prius", "BR19SRX")]
    [InlineData("83baa82fde88-BLACK_04.pdf.txt", "BLACK 04.pdf", "89aaec4f52eb417ed7207319a8e7d2171b70d364f5f944b494b3e222de60afcd", "261425SA", "2026-04-30", "Mr Jamal Khan", "2026-04-22", "13 Oregon Avenue, London, E12 5JE", "+44 7935 990386", "Toyota Yaris - CB11 DCB", "Toyota Yaris", "CB11DCB")]
    [InlineData("d5b6f09ae070-BLACK_05.pdf.txt", "BLACK 05.pdf", "a0a1ae68d7f000d2d0fcf0e2db9e8185c53edc9a9a67b30c37466e95ec855419", "261422SA", "2026-04-30", "Mr Afjol Hussain", "2026-04-26", "Brewery Street, Aston, Birmingham, B6 4JB", "07875687320", "Toyota Voxy - BX67 RZU", "Toyota Voxy", "BX67RZU")]
    public void RecordedInstructionsUseLabelAndTerminalRegistrationBoundaries(
        string extractedFile, string originalFile, string sha256, string reference,
        string instructionDate, string claimant, string incidentDate, string address,
        string mobile, string rawVehicle, string vehicle, string registration)
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
        Assert.Equal(vehicle, draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
        Assert.Equal(instructionDate, Date(draft.InstructionDate));
        Assert.Equal(incidentDate, Date(draft.DateOfIncident));
        Assert.Equal(address, Field(result, "Claimant address").SuggestedValue);
        Assert.Equal(mobile, Field(result, "Claimant mobile").SuggestedValue);
        Assert.Equal(rawVehicle, Field(result, "Vehicle description").SuggestedValue);
        Assert.Null(draft.InspectionAddress);
        Assert.Null(draft.InspectionDate);
        Assert.Null(draft.AccidentCircumstances);
        Assert.Null(draft.VatStatus);
        Assert.All(result.Fields.SelectMany(field => field.Candidates), candidate =>
            Assert.Equal(IntakeSourceLocator.ForPage(1), candidate.Locator));

        var malformed = Extract(text.Replace(rawVehicle, $"{rawVehicle} - INVALID", StringComparison.Ordinal));
        Assert.Null(Assert.IsType<InstructionDraft>(malformed.InstructionDraft).VehicleRegistration);
        Assert.Null(Assert.IsType<InstructionDraft>(malformed.InstructionDraft).VehicleMake);

        var nonTerminal = Extract(text.Replace(rawVehicle, $"{rawVehicle} - vehicle details", StringComparison.Ordinal));
        Assert.Null(Assert.IsType<InstructionDraft>(nonTerminal.InstructionDraft).VehicleRegistration);
        Assert.Null(Assert.IsType<InstructionDraft>(nonTerminal.InstructionDraft).VehicleMake);
    }

    private static InstructionExtractionResult Extract(string text) =>
        new BlackInstructionExtractionPolicy().Extract(
            new(IntakeSourceReadStatus.Readable,
                [new(IntakeEvidenceSource.DocumentContent, "BLACK instruction.pdf", text, IntakeSourceLocator.ForPage(1))],
                [], [], RequiresOcr: false),
            new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero),
            new("BLACK", BlackInstructionExtractionPolicy.DocumentProfileKeyValue, 1));

    private static InstructionReviewField Field(InstructionExtractionResult result, string name) =>
        Assert.Single(result.Fields, field => field.Name == name);

    private static string? Date(DateOnly? value) => value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string ReferencePackRoot() =>
        Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT")
        ?? throw new InvalidOperationException("The reference-pack test should have been skipped.");
}

internal sealed class BlackReferencePackTheoryAttribute : TheoryAttribute
{
    public BlackReferencePackTheoryAttribute()
    {
        var root = Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            Skip = "PEGASUS_REFERENCE_PACK_ROOT is absent; the immutable reference pack differs per machine.";
    }
}
