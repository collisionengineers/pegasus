using System.Security.Cryptography;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Mp;

public sealed class MpInstructionExtractionPolicyTests
{
    [MpReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("6ca905773ea2", "MP PDF 01.pdf", "79097baeec1eac46bb9a34afe67945d398df93a621857179c793f2cff5d5d3f4", "Mr Ali Ahmed Qurban", "RA6458832", "FG21DGV", "Toyota Prius (Private Hire)", "2026-04-15", "2026-04-20", "Lewisham Park London SE13")]
    [InlineData("ddbac0aec529", "MP PDF 02.pdf", "bf8092e4bd7e47407590a20784173bc185b5d68b265451a7b71d4a6214eafe82", "Mr Ali Mohamed Sharif", "RA6458834", "LB18GZE", "Toyota Prius (Private Hire)", "2026-04-18", "2026-04-21", "Aldrington Road London SW16 1TA")]
    [InlineData("5993eedfe5f3", "MP PDF 03.pdf", "b33f7e55b179a8a12b2f1a59d2360f12f8b1e49fd65b673e8b7544ce913967f5", "Mr Nukadin Ali Adan", "RA6458839", "GV70OVZ", "Mercedes E300 (Private Hire)", "2026-04-20", "2026-04-22", "Aldrington Road London SW16 1TA")]
    [InlineData("038b9beb1564", "MP PDF 04.pdf", "6f799851135947ec05c90d464eb6266bad570f0fe1a7e13ae9992b1b6071e917", "Mr David Busuioc", "RA6458838", null, "BMW 520D", "2026-04-20", "2026-04-22", "RHS Recovery 9-11 Bradbury Street Dewsbury WF13 3AU TEL: 07850 027964 Engineer must contact the storage yard 30 minutes prior to arrival.")]
    [InlineData("c58cbb14e3d8", "MP Weird 01.pdf", "277b0531273e350ebd60f5820024e9a40e45b71d7d5e0157d11c84c1ac9a0541", "Mr Mohamed Patel", "RA6458842", "OW75EHD", "Mercedes E300", "2026-04-23", null, "Bridgeman Street Bolton BL3")]
    [InlineData("491d36680055", "MP Weird 02.pdf", "89413ad58022e75f3592c377d0c0f572d8ba8325722090775334178ca94be098", "Mr Ali Nuuh", "RA6458827", "JAV8R", "Mercedes E Class", "2026-04-07", null, "Horns Road liford 1G6")]
    [InlineData("6216cf347e7a", "MP Word 01.doc", "fcd72fbe7d3058f27c1552c2e91d910b055a15834e22177e27c1c67e1fda83d3", "Miss Shakila Hussain", "RA6458825", "GF14RUC", "Volkswagen Sharan", "2026-04-07", "2026-04-21", "RHS Recovery 9-11 Bradbury Street Dewsbury WF13 3AU TEL: 07850 027964 Engineer must contact the storage yard 30 minutes prior to arrival.")]
    [InlineData("587fef3fcd2f", "MP Word 02.doc", "cd2b104f88a4674ba99251e1940268c709ff1c78a90eba4a7dfdb4ff5e66e0f3", "Mr Dubow Ali", "RA6458845", "EN70EAE", "Hyundai Ioniq", "2026-04-29", "2026-04-30", "Unit B3, London Street Trading Estate, High Street, Bolton, BL3 6SR")]
    [InlineData("bc5b8ff69073", "MP Word 03.doc", "18fa98723d35429de6ff75e955beaee59cdd63c87b3169092566178b2f43b654", "MONTREAL PRESTIGE LTD", "RA6458812", "MA22CHZ", "Toyota Corolla (Private Hire)", "2026-03-22", "2026-03-25", "Gertrude Road Norwich NR3 4RW")]
    [InlineData("94a31052a844", "MP Word 04.doc", "b494d5e063beb991b4ab4239f11465dc189a3d8ec29afcebb7976490b357ad56", "Miss Waseema Kola", "RA6458668", "WA55KYK", "BMW I4", "2025-09-22", null, "Rose Hill Works Nelson Street Bolton BL3 2RW")]
    [InlineData("f185fa954f48", "MP Word 05.doc", "c0aa299d0affab6ef2696ac11b7e5edb83d504d4ffcb5893874161c9159d53b5", "Mr Sukhchain Singh", "RA6458806", "SF15GXT", "Peugeot E7 (Hackney Carriage)", "2026-03-06", "2026-03-17", "Whitton Close Doncaster DN4 7RD")]
    public void RecordedOcrAndDocumentInstructionsKeepDateRoles(string sourceKey, string originalFile, string sha256,
        string claimant, string reference, string? registration, string vehicle, string incidentDate,
        string? instructionDate, string location)
    {
        var root = ReferencePackRoot();
        var text = File.ReadAllText(Path.Combine(root, "astra_output", "reports", "principals", "MP", "sources", $"{sourceKey}.txt"));
        var original = File.ReadAllBytes(Path.Combine(root, "principal-docs", "original-mapper-instruction-corpus", originalFile));
        Assert.Equal(sha256, Convert.ToHexStringLower(SHA256.HashData(original)));
        var result = Extract(text); var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(claimant, draft.ClaimantName); Assert.Equal(reference, draft.ClaimNumber); Assert.Equal(registration, draft.VehicleRegistration);
        Assert.Equal(vehicle, draft.VehicleMake); Assert.Equal(incidentDate, Date(draft.DateOfIncident)); Assert.Equal(instructionDate, Date(draft.InstructionDate));
        Assert.Equal(location, draft.InspectionAddress); Assert.Null(draft.InspectionDate); Assert.Null(draft.VatStatus);
    }
    private static InstructionExtractionResult Extract(string text) => new MpInstructionExtractionPolicy().Extract(new(IntakeSourceReadStatus.Readable, [new(IntakeEvidenceSource.DocumentContent, "MP instruction", text, IntakeSourceLocator.ForPage(1))], [], [], RequiresOcr: false), new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero), new("MP", MpInstructionExtractionPolicy.DocumentProfileKeyValue, 1));
    private static string? Date(DateOnly? value) => value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    private static string ReferencePackRoot() => Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT") ?? throw new InvalidOperationException("The reference-pack test should have been skipped.");
}
internal sealed class MpReferencePackTheoryAttribute : TheoryAttribute
{
    public MpReferencePackTheoryAttribute() { var root = Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT"); if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) Skip = "PEGASUS_REFERENCE_PACK_ROOT is absent; the immutable reference pack differs per machine."; }
}
