using System.Globalization;
using System.Security.Cryptography;
using Pegasus.Core.Intake;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Intake.Als;

[Collection(ParserRegexBudgetSerialTests.Name)]
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

        var result = Extract(text);

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

    [Fact]
    public void APartyBlockWithoutTheVatRowStillGivesTheClaimant()
    {
        var result = Extract(Instruction(clientRows: true, vatRow: false));

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("Mr Example", draft.ClaimantName);
        Assert.Equal("1 Example Road, Exampletown, AB1 2CD", Field(result, "Claimant address"));
        Assert.DoesNotContain("Claimant name", result.MissingFields);
        Assert.Null(Field(result, "VAT status"));
        Assert.Null(draft.VatStatus);
        Assert.Equal("Mrs Owner", Field(result, "Vehicle owner"));
    }

    [Fact]
    public void APartyBlockWithTheVatRowReadsAsBefore()
    {
        var result = Extract(Instruction(clientRows: true, vatRow: true));

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal("Mr Example", draft.ClaimantName);
        Assert.Equal("1 Example Road, Exampletown, AB1 2CD", Field(result, "Claimant address"));
        Assert.Equal("no", draft.VatStatus);
        Assert.Equal("Mrs Owner", Field(result, "Vehicle owner"));
    }

    [Fact]
    public void AnInstructionWithoutClientRowsStillReportsTheClaimantMissing()
    {
        var result = Extract(Instruction(clientRows: false, vatRow: true));

        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Null(draft.ClaimantName);
        Assert.Null(Field(result, "Claimant address"));
        Assert.Contains("Claimant name", result.MissingFields);
    }

    // Invented text in the layout the Party regex describes: the tab-separated Client/Owner header, the
    // Name, Address and Tel rows with the owner's Name across a tab on the client's Tel row, then the vehicle table.
    private static string Instruction(bool clientRows, bool vatRow)
    {
        List<string> lines =
        [
            "Our Reference:\tREF-0001", "", "Dear Sirs", "",
            "Accident Date: 05/05/2026", "Accident Circumstances: Invented circumstances.", "",
            "Client Details\tVehicle Owner Details (if applicable)\t"
        ];
        if (clientRows)
            lines.AddRange(
            [
                "Name: Mr Example ",
                "Address: 1 Example Road, Exampletown, AB1 2CD ",
                "Tel: 01234 567890 \tName: Mrs Owner",
                "Address: 2 Example Road, Exampletown, AB1 2CE",
                "Tel: 01234 567891 "
            ]);
        if (vatRow) lines.Add("VAT Registered: no \t");
        lines.AddRange(
        [
            "Clients Vehicle\tThird Party Vehicle\t", "Vehicle Reg:\tAB12CDE \tVehicle Reg:\tZZ99ZZZ\t", "",
            "Yours faithfully", "Auto Logistic Solutions Ltd"
        ]);
        return string.Join('\n', lines);
    }

    private static InstructionExtractionResult Extract(string text) => new AlsInstructionExtractionPolicy().Extract(
        new(IntakeSourceReadStatus.Readable, [new(IntakeEvidenceSource.DocumentContent, "ALS instruction", text)], [], [], false),
        new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero),
        new("ALS", AlsInstructionExtractionPolicy.DocumentProfileKeyValue, 1));

    private static string? Field(InstructionExtractionResult result, string name) =>
        Assert.Single(result.Fields, field => field.Name == name).SuggestedValue;

    private static string? Date(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
