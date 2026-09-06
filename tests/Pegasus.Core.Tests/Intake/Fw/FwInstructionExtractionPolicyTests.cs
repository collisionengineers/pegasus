using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Fw;

public sealed class FwInstructionExtractionPolicyTests
{
    [Fact]
    public void CurrentInstructionExcludesQuotedEarlierIdentityAndVehicle()
    {
        var current = Instruction(
            "29679-01", "Mr Dan-gabriel Ilie", "BA69UMG", "Toyota PRIUS",
            "05 May 2026", "06 May 2026", "Somstar Recovery & Storage, Birmingham B5 6JX",
            "Our Client Was Travelling Along The Main Road.", "MrMartin", "FG19VFL Ford TRANSIT CONNECT");
        var quoted = Instruction(
            "29674-01", "Mr Yunus Mohammed Abdul Amin", "RE05XEX", "Honda CIVIC TYPE R",
            "04 May 2026", "05 May 2026", "Somstar Recovery & Storage, Birmingham B5 6JX",
            "Our Clients Vehicle Was Parked.", "Asaad", "AP10FBF Toyota VERSO");

        var result = Extract(current + "\n-----Original Message-----\n" + quoted);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Equal("Mr Dan-gabriel Ilie", draft.ClaimantName);
        Assert.Equal("29679-01", draft.ClaimNumber);
        Assert.Equal("BA69UMG", draft.VehicleRegistration);
        Assert.Equal("Toyota PRIUS", draft.VehicleMake);
        Assert.DoesNotContain(result.Fields.SelectMany(field => field.Candidates), candidate =>
            candidate.Value.Contains("Yunus", StringComparison.Ordinal)
            || candidate.Value.Contains("RE05XEX", StringComparison.Ordinal));
    }

    [Fact]
    public void ThirdPartyAndInspectionLocationRemainSeparateRoles()
    {
        var result = Extract(Instruction(
            "29679-01", "Mr Dan-gabriel Ilie", "BA69UMG", "Toyota PRIUS",
            "05 May 2026", "06 May 2026", "Somstar Recovery & Storage, Birmingham B5 6JX",
            "Our Client Was Travelling Along The Main Road.", "MrMartin", "FG19VFL Ford TRANSIT CONNECT"));
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Equal("Somstar Recovery & Storage, Birmingham B5 6JX", draft.InspectionAddress);
        Assert.Equal("MrMartin", Field(result, "Third party name").SuggestedValue);
        Assert.Equal("FG19VFL", Field(result, "Third party registration").SuggestedValue);
        Assert.Equal("Ford TRANSIT CONNECT", Field(result, "Third party vehicle").SuggestedValue);
        var roles = new FwInstructionExtractionPolicy().FieldRoles;
        Assert.Equal("claimant", roles["Claimant name"].PartyRole);
        Assert.Equal("third-party", roles["Third party name"].PartyRole);
        Assert.Equal("inspection-location", roles["Inspection address"].PartyRole);
    }

    [Fact]
    public void ConflictingCurrentInstructionsAreAmbiguousRatherThanOrdered()
    {
        var first = Instruction(
            "29679-01", "Mr Dan-gabriel Ilie", "BA69UMG", "Toyota PRIUS",
            "05 May 2026", "06 May 2026", "", "First current account.", "MrMartin",
            "FG19VFL Ford TRANSIT CONNECT");
        var second = Instruction(
            "29626-01", "Catalin Anghelache", "KS21JUW", "Mercedes-Benz E 220 AMG LNE NGT ED PRM + D A",
            "15 April 2026", "06 May 2026", "", "Second current account.", "",
            "RE71KFD Ford TRANSIT CONNECT");

        var result = Extract(first, second);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Null(draft.ClaimantName);
        Assert.Null(draft.ClaimNumber);
        Assert.Null(draft.VehicleRegistration);
        Assert.True(Field(result, "Claimant name").HasConflict);
        Assert.True(Field(result, "Claim reference").HasConflict);
        Assert.True(Field(result, "Vehicle registration").HasConflict);
    }

    [Fact]
    public void SelectorUsesDocumentEvidenceWithoutSenderRouteActivation()
    {
        var read = Read(Instruction(
            "29679-01", "Mr Dan-gabriel Ilie", "BA69UMG", "Toyota PRIUS",
            "05 May 2026", "06 May 2026", "", "Current account.", "MrMartin",
            "FG19VFL Ford TRANSIT CONNECT") + "\nwww.fairwaylegal.co.uk");

        var selection = new InstructionExtractionPolicySelector([new FwInstructionExtractionPolicy()])
            .Select(read, InstructionDocumentSignature.InstructionRole);

        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal("FW", selection.Policy!.PrincipalCode);
    }

    private static InstructionExtractionResult Extract(params string[] fragments) =>
        new FwInstructionExtractionPolicy().Extract(
            Read(fragments),
            new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero),
            new("FW", FwInstructionExtractionPolicy.DocumentProfileKeyValue, 1));

    private static IntakeSourceReadResult Read(params string[] fragments) => new(
        IntakeSourceReadStatus.Readable,
        fragments.Select((text, index) => new IntakeContentFragment(
            IntakeEvidenceSource.EmailBody,
            $"FW message part {index + 1}",
            text)).ToArray(),
        [],
        [],
        RequiresOcr: false);

    private static InstructionReviewField Field(InstructionExtractionResult result, string name) =>
        Assert.Single(result.Fields, field => field.Name == name);

    private static string Instruction(
        string reference,
        string insured,
        string registration,
        string vehicle,
        string accidentDate,
        string instructionDate,
        string inspectionLocation,
        string circumstances,
        string thirdPartyName,
        string thirdPartyVehicle) => $"""
        New INSTRUCTIONS:
        Date: {instructionDate}
        Our Ref: {reference}
        Our Insured: Name: {insured}
        Address: 44 Piccadilly Crescent, Tamworth B78 2EL
        Accident Date: {accidentDate} Time: 17:00
        Vehicle Registration Number: {registration}
        Make/Model: {vehicle}
        Damage:
        Accident Location: Ashby Rd B5493 Near Kings Ln
        Circumstance: {circumstances}
        Third Party Name {thirdPartyName}
        Third Party Reg: {thirdPartyVehicle}
        Inspection Location:
        {inspectionLocation}
        Should you have any query relating to the attached please kindly respond to Info@fairwaylegal.co.uk
        """;
}
