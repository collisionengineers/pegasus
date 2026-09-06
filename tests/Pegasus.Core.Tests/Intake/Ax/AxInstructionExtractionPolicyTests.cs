using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Ax;

public sealed class AxInstructionExtractionPolicyTests
{
    [Theory]
    [InlineData("4fa79c00fd18-AX_01.pdf.txt", "1063856", "Ms Stephanie Scouller", "S90LLR", "BMW 220D M SPORT AUTO", "2026-05-05", "G A Mann", "G20 7QF", "01415768865")]
    [InlineData("63c08984c1f7-AX_02.pdf.txt", "1061903", "Mr Muhammad A Qadri", "SG75CEU", "JAECOO 7 LUXURY PHEV AUTO", "2026-04-21", "RTA Storage & Recovery", "G42 0GG", "07724 541096")]
    [InlineData("d0c7563ecd23-AX_03.pdf.txt", "1063495", "Mr Callum McGregor", "DA67YKH", "MITSUBISHI ECLIPSE CROSS 4 4X4 CVT", "2026-04-30", "Lanarkshire Bodyworx Ltd", "ML8 4FR", "07985606399")]
    [InlineData("13509b46340a-AX_04.pdf.txt", "1063506", "Mr slawomir Kluska", "SH20ZFM", "RENAULT CLIO RS LINE TCE", "2026-05-02", "Ultimate Car Body Repairs Ltd", "ML4 2RS", "07564 090038/07935473921")]
    [InlineData("fe98b1e7532c-AX_05.pdf.txt", "1063118", "Miss Stevie-Leigh Reynolds", "PN64WBO", "RENAULT CLIO DYNAMIQUE MEDIANAV", "2026-04-29", "Lanarkshire ARC", "ML1 1PF", "07827446300")]
    public void RecordedClientBlocksRemainDistinctFromBodyshopAndThirdParty(
        string sourceFile, string reference, string claimant, string registration, string vehicle,
        string accidentDate, string repairer, string repairerPostcode, string repairerTelephone)
    {
        var text = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "pegasus_pack", "astra_output", "extractions", "text", sourceFile));

        var result = Extract(text);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        Assert.Equal(claimant, draft.ClaimantName);
        Assert.Equal(reference, draft.ClaimNumber);
        Assert.Equal(registration, draft.VehicleRegistration);
        Assert.Equal(vehicle, draft.VehicleMake);
        Assert.DoesNotContain(result.Fields, field => field.Name == "Vehicle registration" && field.SuggestedValue == "FL10UCU");
        Assert.Equal(repairer, Field(result, "Repairer name"));
        Assert.Contains(repairerPostcode, Field(result, "Repairer address"), StringComparison.Ordinal);
        Assert.Equal(repairerTelephone, Field(result, "Repairer telephone"));
        Assert.Equal(
            accidentDate,
            draft.DateOfIncident?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        Assert.False(string.IsNullOrWhiteSpace(draft.AccidentCircumstances));
        Assert.All(result.Fields.SelectMany(field => field.Candidates), candidate =>
            Assert.Equal(IntakeSourceLocator.ForPage(1), candidate.Locator));
        Assert.Null(draft.InspectionAddress);
        Assert.Null(draft.InspectionDate);
        Assert.NotNull(Field(result, "Report deadline"));
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

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pegasus.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
