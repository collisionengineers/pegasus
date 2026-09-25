using System.Globalization;
using System.Security.Cryptography;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Als;

public sealed class AlsInstructionExtractionPolicyTests
{
    [ReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("30ce8d251b5a", "ALS 01.DOC", "573a1db2e96f1c3d924fd04fceff5d24378d34a070f884b030a5f95a44de3f35", "Mrs Arleen Weatherall", "Mrs Catherine Carruthers", "158981", "KW18ZWE", "Vauxhall", "Grandland X Sport Nav T D Ss A 1.6", "2026-04-19", "no")]
    [InlineData("9f7e3469258d", "ALS 02.DOC", "1d27d037ad10f3ff84abe9b82b1389100a73f713d2ac4feade8db6ff20439578", "Mr Jonathan Binley", "Mr Jonathan Binley", "159035", "E8JNO", "Bmw", "M235i Auto 3.0", "2026-04-03", "no")]
    [InlineData("3c97eab27c6e", "ALS 03.DOC", "a70a4e486150780f7ea05c3776aeea696091c3b80cc369659786b8c38be94911", "Mrs Lisa Bell", "Peter Bell", "159133", "EF15AOS", "Ford", "Fiesta Titanium X Tdci", "2026-04-20", "no")]
    [InlineData("eb7ad819944f", "ALS 04.DOC", "ed85af2000f47314f4b0d3a6620b62e8a316b0de8dab98e9d50ce607bbf4c7be", "Mr Lee Kennedy", "Mr Lee Kennedy", "158920", "KE54DYS", "Ford", "Transit 85 T260m Fwd", "2026-04-19", "no")]
    [InlineData("34f80366f119", "ALS 05.DOC", "f53eaceac1c0b31b8052af04ecda4272be0e0030aac996d25ead38b471c0586b", "Mr David Bennett", "Miss Donna Cameron", "158804", "MM72GNY", "Volkswagen", "T-Roc R Tsi", "2026-04-08", "no")]
    public void PairedColumnsRemainRoleScoped(
        string key,
        string original,
        string sha,
        string claimant,
        string owner,
        string reference,
        string vrm,
        string make,
        string model,
        string incident,
        string vat)
    {
        var root = ReferencePack.Root();
        var text = File.ReadAllText(Path.Combine(root, "astra_output", "reports", "principals", "ALS", "sources", $"{key}.txt"));
        Assert.Equal(
            sha,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(
                Path.Combine(root, "principal-docs", "original-mapper-instruction-corpus", original)))));

        var result = new AlsInstructionExtractionPolicy().Extract(
            new(IntakeSourceReadStatus.Readable, [new(IntakeEvidenceSource.DocumentContent, "ALS instruction", text)], [], [], false),
            new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero),
            new("ALS", AlsInstructionExtractionPolicy.DocumentProfileKeyValue, 1));

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(claimant, draft.ClaimantName);
        Assert.Equal(owner, Field(result, "Vehicle owner"));
        Assert.Equal(reference, draft.ClaimNumber);
        Assert.Equal(vrm, draft.VehicleRegistration);
        Assert.Equal(make, draft.VehicleMake);
        Assert.Equal(model, draft.VehicleModel);
        Assert.Equal(incident, Date(draft.DateOfIncident));
        Assert.Equal(vat, draft.VatStatus);
        Assert.Null(draft.InspectionDate);
        Assert.DoesNotContain(result.Fields, field => field.Name.Contains("salvage", StringComparison.OrdinalIgnoreCase));
    }

    private static string? Field(InstructionExtractionResult result, string name) =>
        Assert.Single(result.Fields, field => field.Name == name).SuggestedValue;

    private static string? Date(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
