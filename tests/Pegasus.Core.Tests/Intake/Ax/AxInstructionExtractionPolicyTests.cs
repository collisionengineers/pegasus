using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Ax;

public sealed class AxInstructionExtractionPolicyTests
{
    [Theory]
    [InlineData("1063856", "Ms Stephanie Scouller", "S90LLR", "BMW 220D M SPORT AUTO", "05 May 2026")]
    [InlineData("1061903", "Mr Muhammad A Qadri", "SG75CEU", "JAECOO 7 LUXURY PHEV AUTO", "21 Apr 2026")]
    [InlineData("1063495", "Mr Callum McGregor", "DA67YKH", "MITSUBISHI ECLIPSE CROSS 4 4X4 CVT", "30 Apr 2026")]
    [InlineData("1063506", "Mr slawomir Kluska", "SH20ZFM", "RENAULT CLIO RS LINE TCE", "02 May 2026")]
    [InlineData("1063118", "Miss Stevie-Leigh Reynolds", "PN64WBO", "RENAULT CLIO DYNAMIQUE MEDIANAV", "29 Apr 2026")]
    public void RecordedClientBlocksRemainDistinctFromBodyshopAndThirdParty(
        string reference, string claimant, string registration, string vehicle, string accidentDate)
    {
        var text = $"05 May 2026\nAX Reference: {reference}\nReport Due on: 07/05/2026\n"
            + $"Bodyshop Details\nName: Wrong Repairer\nAddress: Repairer Street\n"
            + $"Client Details\nName: {claimant}\nVRM: {registration}\nVehicle: {vehicle}\n"
            + $"Accident Date: {accidentDate}\nAccident Circumstances: Client stopped safely.\nVAT Registered: No\n"
            + "Bodyshop Details\nName: Correct Repairer\nAddress: 10 Repair Road\n"
            + "Third Party Details\nVRM: FL10 UCU\nMake/Model: VAUXHALL/CORSA";

        var result = Extract(text);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(claimant, draft.ClaimantName);
        Assert.Equal(reference, draft.ClaimNumber);
        Assert.Equal(registration, draft.VehicleRegistration);
        Assert.Equal(vehicle, draft.VehicleMake);
        Assert.DoesNotContain(result.Fields, field => field.Name == "Vehicle registration" && field.SuggestedValue == "FL10UCU");
        Assert.Equal("Correct Repairer", Field(result, "Repairer name"));
        Assert.Equal("10 Repair Road", Field(result, "Repairer address"));
        Assert.Null(draft.InspectionAddress);
        Assert.Null(draft.InspectionDate);
        Assert.Equal("07/05/2026", Field(result, "Report deadline"));
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
}
