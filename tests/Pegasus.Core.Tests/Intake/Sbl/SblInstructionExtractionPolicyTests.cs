using System.Security.Cryptography;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Sbl;

public sealed class SblInstructionExtractionPolicyTests
{
    [SblReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("e34f948c31ef-SBL_01.pdf.txt", "SBL 01.pdf", "fa2d7e6abe04830ac29bd5faa7b9452212a6bc91d636cfddf10510c821780fc8", "Mr Craig Motorhome Escapes", "SBL-B0470099", "SK24KYF", "FORD SWIFT VOYAGER 494 AUTO", "2026-04-06", "2026-05-06", "Hit whilst parked occupied", "Not VAT Registered", "Royston Lodge, Bathgate, EH48 1JX", "Home address", "C.A.R.S Collision Accident Recovery Service Ltd", "Mr Craig Motorhome Escapes", "Level Up Bodyshop", "Block 1 Whiteside Industrial Estate, Bathgate EH48 2RX", "07720315785", "levelupautobody60@gmail.com")]
    [InlineData("4da6b68702ad-SBL_02.pdf.txt", "SBL 02.pdf", "7cd71550bb2d0d782885928036c23818b6db877cac97db30d55e76ca47d62866", "Mr EDSB Ltd EDSB Ltd", "SBL-B0558371", "DA75JCU", "VOLKSWAGEN ID4 PRO MATCH", "2026-04-26", "2026-05-06", "Our client was driving down the road when a driver came out of their driveway into the side of my vehicle at low speed. The driveway was on the left of our vehicle and hit the rear nearside. The third-party accepted fault. There was no emergency services", "VAT Registered", "E D S B Ltd Unit 2 Meadow Court 124 Millshaw Leeds, LS11 8LZ", "In use", "MAGNA ACCIDENT SERVICES LIMITED", "Mr EDSB Ltd EDSB Ltd", "Paynes Of Hinkley Arc", "Watling St Hinckley", null, "arc@paynesgarages.co.uk")]
    [InlineData("d335835d3e63-SBL_03.pdf.txt", "SBL 03.pdf", "d5106d2067f7576c6873527db59f8868ed8617f2f1334faf22f5a65caf36adee", "Ms Jacklyn Gurney", "SBL-B0427818", "L777GUR", "AUDI A1 TFSI S LINE", "2026-02-12", "2026-04-28", "Hit whilst parked unoccupied", "Not VAT Registered", "75 Auckland Street Glasgow, G22 5NY", "At home location", "C.A.R.S Collision Accident Recovery Service Ltd", "Ms Jacklyn Gurney", "Motherwell Accident Repair Centre / Scottish Accident", "36 Speirs Wharf, Glasgow G4 9TG", "07792089990", "motherwellarc@outlook.com")]
    [InlineData("c238111c150c-SBL_04.pdf.txt", "SBL 04.pdf", "3fd4d9cd2f7895579f51afcaf055f43e465038125543f198046109fd313dd99b", "Miss Arabella Christie", "SBL-B0423796", "AJ17FNL", "Peugeot 308", "2026-04-25", "2026-04-28", "Client has been stopped at some traffic lights with 2 vehicles in front. Lights have turned amber and then the client has been hit from behind by the other vehicle.", "Not VAT Registered", "Spring Hill Compton Abdale Cheltenham , GL54 4DU", "With Repairer", "Fleet Mitigation Solutions", "Miss Arabella Christie", "OH Works Accident and Paintwork Repair Ltd,", "Unit C2, Rhymes Lane, Fairford, GL7 4BU", "01285 238153", "oakley@oh-works.co.uk")]
    [InlineData("9c397a4dbd70-SBL_05.pdf.txt", "SBL 05.pdf", "436db268cf7cb824ef089e08399879b4f7f78a65bf3c2b0f515d043c44bb3e00", "Mr Yoni Sherer", "SBL-B0484837", "VX71YDO", "Audi A5 S LN ED 1 45 TSFSI MHEV Q SA", "2026-04-13", "2026-04-28", "Client’s vehicle was parked up, third party vehicle pulled out from parking spot behind and collided with parked vehicle causing damage to front right of vehicle. Client was not in the vehicle at the time.", "Not VAT Registered", "1 East Meade, Prestwich, , M25 0JJ", "Parkhouse Bodyshop, Unit 7, Parkhouse Bridge Estate Langley Road, M6 6JQ", "Parkhouse Assist", "Mr Yoni Sherer", "Parkway Prestige", "Unit 1, Leo Industrial Estate, Mosley Rd, Trafford Park, Stretford, Manchester M17 1JS", "0161 872 5335", "info@parkwayprestige.co.uk")]
    public void RecordedSectionsRemainRoleScoped(
        string extractedFile, string originalFile, string sha256, string claimant, string reference,
        string registration, string vehicle, string incidentDate, string instructionDate,
        string circumstances, string vat, string claimantAddress, string vehicleLocation,
        string introducer, string driver, string repairer, string repairerAddress,
        string? repairerTelephone, string repairerEmail)
    {
        var root = ReferencePackRoot();
        var text = File.ReadAllText(Path.Combine(root, "astra_output", "extractions", "text", extractedFile));
        var original = File.ReadAllBytes(Path.Combine(
            root, "principal-docs", "original-mapper-instruction-corpus", originalFile));
        Assert.Equal(sha256, Convert.ToHexStringLower(SHA256.HashData(original)));

        var result = Extract(text);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(claimant, draft.ClaimantName);
        Assert.Equal(reference, draft.ClaimNumber);
        Assert.Equal(registration, draft.VehicleRegistration);
        Assert.Equal(vehicle, draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
        Assert.Equal(incidentDate, Date(draft.DateOfIncident));
        Assert.Equal(instructionDate, Date(draft.InstructionDate));
        Assert.Equal(circumstances, draft.AccidentCircumstances);
        Assert.Equal(vat, draft.VatStatus);
        Assert.Equal(claimantAddress, Field(result, "Claimant address").SuggestedValue);
        Assert.Equal(vehicleLocation, draft.InspectionAddress);
        Assert.Equal(introducer, Field(result, "Introducer").SuggestedValue);
        Assert.Equal(driver, Field(result, "Driver").SuggestedValue);
        Assert.Equal(repairer, Field(result, "Repairer name").SuggestedValue);
        Assert.Equal(repairerAddress, Field(result, "Repairer address").SuggestedValue);
        Assert.Equal(repairerTelephone, Field(result, "Repairer telephone").SuggestedValue);
        Assert.Equal(repairerEmail, Field(result, "Repairer email").SuggestedValue);
        Assert.All(result.Fields.SelectMany(field => field.Candidates), candidate =>
            Assert.Equal(IntakeSourceLocator.ForPage(1), candidate.Locator));
        Assert.Null(draft.InspectionDate);
        Assert.Null(Field(result, "Hire company").SuggestedValue);
        Assert.Null(Field(result, "Hire out date").SuggestedValue);
        Assert.Null(Field(result, "Agreed labour rate").SuggestedValue);
    }

    [Fact]
    public void BlankPlaceholdersStayUnavailableAndInternationalValuesKeepTheirPrintedForm()
    {
        var result = Extract(Template("""
            Vehicle Make: Toyota Prius
            Model: -
            Registration: 12-D-34567
            Mileage: N/A
            Current Vehicle Location: 7 Rue de Lyon, 75012 Paris, France
            Incident Circumstances: Stationary when struck
            Agreed Value:
            """));
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Equal("12-D-34567", draft.VehicleRegistration);
        Assert.Null(draft.VehicleModel);
        Assert.Null(draft.VehicleMileage);
        Assert.Equal("7 Rue de Lyon, 75012 Paris, France", draft.InspectionAddress);
        Assert.Null(Field(result, "Hire out date").SuggestedValue);
    }

    [Fact]
    public void ConflictingCurrentPolicyholderValuesAreAmbiguousAndNeverBorrowDriverOrRepairerAddress()
    {
        var text = Template("""
            Vehicle Make: Toyota Prius
            Registration: AB12 CDE
            Incident Circumstances: Stationary when struck
            Agreed Value:
            """).Replace(
                "Policyholder Name: Alex One",
                "Policyholder Name: Alex One\nPolicyholder Name: Alex Two",
                StringComparison.Ordinal);

        var result = Extract(text);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Null(draft.ClaimantName);
        Assert.True(Field(result, "Claimant name").HasConflict);
        Assert.DoesNotContain("Driver Person", Field(result, "Claimant name").Candidates.Select(item => item.Value));
        Assert.Equal("Claimant Place", Field(result, "Claimant address").SuggestedValue);
        Assert.Equal("Repairer Place", Field(result, "Repairer address").SuggestedValue);
    }

    [Fact]
    public void EqualDriverAndPolicyholderRemainTwoExplicitRoles()
    {
        var text = Template("""
            Vehicle Make: Toyota Prius
            Registration: AB12 CDE
            Incident Circumstances: Stationary when struck
            Agreed Value:
            """).Replace("Driver Person", "Alex One", StringComparison.Ordinal);

        var result = Extract(text);

        Assert.Equal("Alex One", Field(result, "Claimant name").SuggestedValue);
        Assert.Equal("Alex One", Field(result, "Driver").SuggestedValue);
        Assert.Equal("claimant", new SblInstructionExtractionPolicy().FieldRoles["Claimant name"].PartyRole);
        Assert.Equal("driver", new SblInstructionExtractionPolicy().FieldRoles["Driver"].PartyRole);
    }

    [Fact]
    public void CircumstancesRetainTheRecordedCurlyApostrophe()
    {
        var result = Extract(Template("""
            Vehicle Make: Toyota Prius
            Registration: AB12 CDE
            Incident Circumstances: Client’s vehicle was parked.
            Agreed Value:
            """));

        Assert.Equal("Client’s vehicle was parked.",
            Assert.IsType<InstructionDraft>(result.InstructionDraft).AccidentCircumstances);
    }

    [Fact]
    public void SelectorUsesDocumentFingerprintWithoutActivatingASenderRoute()
    {
        var read = Read(Template("""
            Vehicle Make: Toyota Prius
            Registration: AB12 CDE
            Incident Circumstances: Stationary when struck
            Agreed Value:
            """));

        var selection = new InstructionExtractionPolicySelector([new SblInstructionExtractionPolicy()])
            .Select(read, InstructionDocumentSignature.InstructionRole);

        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal("SBL", selection.Policy!.PrincipalCode);
    }

    private static string Template(string vehicle) => $$"""
        URGENT NEW INSTRUCTION
        Instruction Details
        To: Collision Engineers
        Date: 06/05/2026
        From: Smart Business Link
        Introducer: Introducer Company
        Claim & Policyholder
        Claim Number: SBL-1
        Incident Date: 05/05/2026
        Driver: Driver Person
        Policyholder Name: Alex One
        Policyholder VAT Status:
        Address: Claimant Place
        Vehicle & Damage
        {{vehicle}}
        Repairer Details
        Repairer Name: Repairer Company
        Repairer Address: Repairer Place
        Repairer Tel:
        Repairer Email:
        Agreed Labour Rate:
        Insurance & Hire
        Hire Supplied:
        Hire Company:
        Hire Out Date:
        Important Notes
        """;

    private static InstructionExtractionResult Extract(string text) =>
        new SblInstructionExtractionPolicy().Extract(
            Read(text),
            new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero),
            new("SBL", SblInstructionExtractionPolicy.DocumentProfileKeyValue, 1));

    private static IntakeSourceReadResult Read(string text) => new(
        IntakeSourceReadStatus.Readable,
        [new(IntakeEvidenceSource.DocumentContent, "SBL instruction.pdf", text, IntakeSourceLocator.ForPage(1))],
        [], [], RequiresOcr: false);

    private static InstructionReviewField Field(InstructionExtractionResult result, string name) =>
        Assert.Single(result.Fields, field => field.Name == name);

    private static string? Date(DateOnly? value) => value?.ToString(
        "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string ReferencePackRoot() =>
        Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT")
        ?? throw new InvalidOperationException("The reference-pack test should have been skipped.");
}

internal sealed class SblReferencePackTheoryAttribute : TheoryAttribute
{
    public SblReferencePackTheoryAttribute()
    {
        var root = Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            Skip = "PEGASUS_REFERENCE_PACK_ROOT is absent; the immutable reference pack differs per machine.";
    }
}
