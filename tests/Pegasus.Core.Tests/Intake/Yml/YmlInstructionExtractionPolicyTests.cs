using System.Globalization;
using System.Security.Cryptography;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Yml;

public sealed class YmlInstructionExtractionPolicyTests
{
    [ReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("4a917d20cbc3", "HDUK 01.pdf", "37f70f707fa710d81c09abb3ccda02c61292a37fa3665d4dccc4ce20c6c8cfc6", "Ahmad Doshi", "HD4009", "OV20ZYT", "ToyotaCorolla", "2026-05-04", "8 Dale St, Burton-on-Trent DE14 3TE")]
    [InlineData("ccb45a5b27fc", "HDUK 02.pdf", "1567e1393f0a1d7c3cd609664b58fa4697cc30cfe57a8feb60ae741240f06e69", "Stacey Lewis", "HD4010", "RV61VBC", "BMW320", "2026-05-03", "6 Nant-Y-Gaer, Llay Wrexham, LL12 OSG")]
    [InlineData("6fa3e61e2392", "HDUK 03.pdf", "f1540a0c96e4d32d703b4faab7ed1ed2621e755d7395bd426da3288693688db2", "Lee Hornby", "HD4008", "ML16NGJ", "Land RoverRANGE ROVER SPORT SDV6 HSE", "2026-04-14", "126 Hatton Road, Chester CH1 5EF")]
    [InlineData("b46f113f3db4", "HDUK 04.pdf", "8dc9d4b59b85feb64eed9fc7a3dbc020170f002c82363372af5768411aa888b9", "Arshad Bibi", "HD3982", "CX15LKK", "VolkswagenGolf", "2026-03-20", "84 Sydney Street, Burton on Trent DE14 2QY")]
    [InlineData("8c907f0adfd0", "HDUK 05.pdf", "6708f0e9f46186a4ef7a5972271b05ac7ed8873d628b42940a5ef323bdf6d906", "Mohammed Haroon", "HD3992", "FG69DHZ", "BMW X3 M40I", "2026-04-27", "14 Outwoods Street, Burton on Trent DE14 2PJ")]
    public void HdukIssuerRemainsDistinctFromYmlPrincipal(
        string key,
        string original,
        string sha,
        string claimant,
        string reference,
        string vrm,
        string vehicle,
        string accident,
        string address)
    {
        var root = ReferencePack.Root();
        var text = File.ReadAllText(Path.Combine(root, "astra_output", "reports", "principals", "YML", "sources", $"{key}.txt"));
        Assert.Equal(
            sha,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(
                Path.Combine(root, "principal-docs", "original-mapper-instruction-corpus", original)))));

        var result = new YmlInstructionExtractionPolicy().Extract(
            new(
                IntakeSourceReadStatus.Readable,
                [new(IntakeEvidenceSource.DocumentContent, "HDUK instruction", text, IntakeSourceLocator.ForPage(1))],
                [],
                [],
                false),
            new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero),
            new("YML", YmlInstructionExtractionPolicy.DocumentProfileKeyValue, 1));

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("YML", draft.SuggestedPrincipalCode);
        Assert.Equal("HD UK Network", Assert.Single(result.Fields, field => field.Name == "Document issuer").SuggestedValue);
        Assert.Equal(claimant, draft.ClaimantName);
        Assert.Equal(reference, draft.ClaimNumber);
        Assert.Equal(vrm, draft.VehicleRegistration);
        Assert.Equal(vehicle, draft.VehicleMake);
        Assert.Equal(accident, Date(draft.DateOfIncident));
        Assert.Equal(address, draft.InspectionAddress);
        Assert.Null(draft.InspectionDate);
        Assert.Null(draft.VatStatus);
    }

    private static string? Date(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
