using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Ax;

public sealed class AxInstructionExtractionPolicyTests
{
    [AxReferencePackTheory]
    [Trait("Category", "Corpus")]
    [InlineData("4fa79c00fd18-AX_01.pdf.txt", "1063856", "Ms Stephanie Scouller", "S90LLR", "BMW 220D M SPORT AUTO", "2026-05-05", "2026-05-06", "2026-05-11", "Client was stationary on Station road giving way to the vehicles waiting to join the roundabout when the third party failed to stop and collided with clients rear end. The third party was apologetic and accepted liability at the scene, details exchanged at the scene", "G A Mann", "10-16 Glenfarg Street, Glasgow, G20 7QF", "01415768865")]
    [InlineData("63c08984c1f7-AX_02.pdf.txt", "1061903", "Mr Muhammad A Qadri", "SG75CEU", "JAECOO 7 LUXURY PHEV AUTO", "2026-04-21", "2026-05-05", "2026-05-08", "Our client was Parked Attended and was returning back to his vehicle - Third Party has reversed and collided into the Offside of our clients vehicle. Our client witnessed the accident and Details were Exchanged.", "RTA Storage & Recovery", "365 Aikenhead Rd, Glasgow, G42 0GG", "07724 541096")]
    [InlineData("d0c7563ecd23-AX_03.pdf.txt", "1063495", "Mr Callum McGregor", "DA67YKH", "MITSUBISHI ECLIPSE CROSS 4 4X4 CVT", "2026-04-30", "2026-05-05", "2026-05-07", "Client proceeding correctly on a country road B769 on a blind bend and the tp overtaking on the corner and as he is overtaking the tp is oncoming and the tp realised late and attempted to going back onto the client side of the road and collided with the tp driver side and then collided with the clients driver side which then caused the tp to drive off the road into a field. Details exhcanged.", "Lanarkshire Bodyworx Ltd", "Unit 8C, Powerline Ind Est, Yieldshields Road, Carluke, ML8 4FR", "07985606399")]
    [InlineData("13509b46340a-AX_04.pdf.txt", "1063506", "Mr slawomir Kluska", "SH20ZFM", "RENAULT CLIO RS LINE TCE", "2026-05-02", "2026-05-05", "2026-05-07", "Client stationary in a queue of traffic and the tp behind our client failed to stop and shunted our client in the rear. Details exchanged.", "Ultimate Car Body Repairs Ltd", "184 Clydesdale Street, Bellshill, Coatbridge, ML4 2RS", "07564 090038/07935473921")]
    [InlineData("fe98b1e7532c-AX_05.pdf.txt", "1063118", "Miss Stevie-Leigh Reynolds", "PN64WBO", "RENAULT CLIO DYNAMIQUE MEDIANAV", "2026-04-29", "2026-05-05", "2026-05-07", "Our client was proceeding down Crammond Ave - Third Party has failed to giveway and pulled out of Aitkenhead Ave and collided into the Nearside of our clients vehicle. Details Exchanged", "Lanarkshire ARC", "108a Park Street, Motherwell, ML1 1PF", "07827446300")]
    public void RecordedClientBlocksRemainDistinctFromBodyshopAndThirdParty(
        string sourceFile, string reference, string claimant, string registration, string vehicle,
        string accidentDate, string instructionDate, string deadline, string circumstances,
        string repairer, string repairerAddress, string repairerTelephone)
    {
        var text = File.ReadAllText(Path.Combine(
            ReferencePackRoot(), "astra_output", "extractions", "text", sourceFile));

        var result = Extract(text);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(claimant, draft.ClaimantName);
        Assert.Equal(reference, draft.ClaimNumber);
        Assert.Equal(registration, draft.VehicleRegistration);
        Assert.Equal(vehicle, draft.VehicleMake);
        Assert.DoesNotContain(result.Fields, field => field.Name == "Vehicle registration" && field.SuggestedValue == "FL10UCU");
        Assert.Equal(repairer, Field(result, "Repairer name"));
        Assert.Equal(repairerAddress, Field(result, "Repairer address"));
        Assert.Equal(repairerTelephone, Field(result, "Repairer telephone"));
        Assert.Equal(
            accidentDate,
            draft.DateOfIncident?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(circumstances, draft.AccidentCircumstances);
        Assert.Equal(instructionDate, draft.InstructionDate?.ToString(
            "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("No", draft.VatStatus);
        Assert.All(result.Fields.SelectMany(field => field.Candidates), candidate =>
            Assert.Equal(IntakeSourceLocator.ForPage(1), candidate.Locator));
        Assert.Null(draft.InspectionAddress);
        Assert.Null(draft.InspectionDate);
        Assert.Equal(deadline, DateOnly.ParseExact(
            Field(result, "Report deadline")!, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)
            .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("deadline", new AxInstructionExtractionPolicy().FieldRoles["Report deadline"].PartyRole);
    }

    [Fact]
    public void SelectorUsesDocumentSignalsAndDoesNotNeedASenderRoute()
    {
        var read = Read("AX Reference: 1\nClient Details\nName: A Person\nVRM: S90LLR\nVehicle: BMW 220D");
        var selection = new InstructionExtractionPolicySelector([new AxInstructionExtractionPolicy()])
            .Select(read, InstructionDocumentSignature.InstructionRole);
        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal("AX", selection.Policy!.PrincipalCode);
    }

    private static InstructionExtractionResult Extract(string text) => new AxInstructionExtractionPolicy().Extract(
        Read(text), new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero),
        new("AX", AxInstructionExtractionPolicy.DocumentProfileKeyValue, 1));

    private static IntakeSourceReadResult Read(string text) => new(IntakeSourceReadStatus.Readable,
        [new(IntakeEvidenceSource.DocumentContent, "AX.pdf", text, IntakeSourceLocator.ForPage(1))],
        [], [], RequiresOcr: false);

    private static string? Field(InstructionExtractionResult result, string name) =>
        Assert.Single(result.Fields, field => field.Name == name).SuggestedValue;

    private static string ReferencePackRoot() =>
        Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT")
        ?? throw new InvalidOperationException("The reference-pack test should have been skipped.");
}

internal sealed class AxReferencePackTheoryAttribute : TheoryAttribute
{
    public AxReferencePackTheoryAttribute()
    {
        var root = Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            Skip = "PEGASUS_REFERENCE_PACK_ROOT is absent; the immutable reference pack differs per machine.";
    }
}
