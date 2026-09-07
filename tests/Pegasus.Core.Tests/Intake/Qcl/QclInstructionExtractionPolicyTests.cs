using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Qcl;

public sealed class QclInstructionExtractionPolicyTests
{
    [Fact]
    public void ConcatenatedLabelsKeepClaimantFieldsAndDateRolesBounded()
    {
        var result = Extract("""
            Address: Complex Reports
            Date: 06 May 2026
            Our Ref: 225880.TA
            Box Reference: QCL24257
            Report Due on: 12 May 2026
            Dear Sirs
            Re: Mr Hamza Ahmad
            Acc date04-May-2026
            Vehicle regAY19 LTW
            MakeBMW X3
            Location54 Street Austell Drive Heald Green Cheadle SK8 3EG
            Contact no07384958598
            Yours Faithfully
            QC Law
            records@qc-law.co.uk
            """);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Equal("Mr Hamza Ahmad", draft.ClaimantName);
        Assert.Equal("225880.TA", draft.ClaimNumber);
        Assert.Equal("AY19LTW", draft.VehicleRegistration);
        Assert.Equal("BMW X3", draft.VehicleMake);
        Assert.Null(draft.VehicleModel);
        Assert.Equal(new DateOnly(2026, 5, 4), draft.DateOfIncident);
        Assert.Equal(new DateOnly(2026, 5, 6), draft.InstructionDate);
        Assert.Equal("54 Street Austell Drive Heald Green Cheadle SK8 3EG", draft.InspectionAddress);
        Assert.Null(draft.InspectionDate);
        Assert.Equal("QCL24257", Field(result, "Box reference").SuggestedValue);
        Assert.Equal("12 May 2026", Field(result, "Report deadline").SuggestedValue);
    }

    [Fact]
    public void TabSeparatedExplicitModelSurvivesWithoutBleedingIntoLocation()
    {
        var result = Extract("""
            Address:	Complex Reports	Date:	01 May 2026
            Our Ref:	225873.TA
            Dear Sirs
            Re:	Mr Syed Azhar Hussain
            Acc date:	30-Apr-2026
            Vehicle reg:	LY63 XKP
            Make:	Toyota
            Model:	Prius Hybrid
            Location:	Flat 5 Dale House 204 London Road Hazel Grove Stockport SK7 4DF
            Contact no:	07497222239
            Yours Faithfully
            QC Law
            claims@qc-law.co.uk
            """);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Equal("Toyota", draft.VehicleMake);
        Assert.Equal("Prius Hybrid", draft.VehicleModel);
        Assert.Equal(
            "Flat 5 Dale House 204 London Road Hazel Grove Stockport SK7 4DF",
            draft.InspectionAddress);
    }

    [Fact]
    public void MissingReferenceAndModelAreNotBorrowedFromBoxOrMetadata()
    {
        var result = Extract("""
            Address: Complex Reports
            Date: 01 May 2026
            Box Reference: QCL24257
            Report Due on: 12 May 2026
            Dear Sirs
            Re: Mr Chaudhary Ameer
            Acc date29-Apr-2026
            Vehicle regMX67 PXS
            MakeToyota Prius
            Location34 Avon Way Colchester CO4 3TP
            Contact no07462777478
            Yours Faithfully
            QC Law
            records@qc-law.co.uk
            """);
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);

        Assert.Null(draft.ClaimNumber);
        Assert.Null(draft.VehicleModel);
        Assert.Null(draft.InspectionDate);
        Assert.Equal("QCL24257", Field(result, "Box reference").SuggestedValue);
        Assert.Equal("12 May 2026", Field(result, "Report deadline").SuggestedValue);
    }

    [Fact]
    public void QcLawAndComplexReportsRemainIssuerAndIntermediary()
    {
        var result = Extract("""
            Address: Complex Reports
            Date: 01 May 2026
            Our Ref: 225871.TA
            Dear Sirs
            Re: Mr Bilal Hussain
            Acc date30-Apr-2026
            Vehicle regFG70 UJS
            MakeToyota Corolla Icon
            Location333 Brinnington Road Stockport SK5 8AF
            Contact no07400348623
            Yours Faithfully
            QC Law
            claims@qc-law.co.uk
            """);
        var roles = new QclInstructionExtractionPolicy().FieldRoles;

        Assert.Equal("QC Law", Field(result, "Document issuer").SuggestedValue);
        Assert.Equal("issuer", roles["Document issuer"].PartyRole);
        Assert.Equal("Complex Reports", Field(result, "Intermediary").SuggestedValue);
        Assert.Equal("intermediary", roles["Intermediary"].PartyRole);
        Assert.Equal("claimant", roles["Claimant name"].PartyRole);
        Assert.Equal("principal", roles["Claim reference"].ReferenceRole);
    }

    [Fact]
    public void SelectorUsesDocumentEvidenceWithoutSenderRouteActivation()
    {
        var read = Read("""
            Dear Sirs
            Re: Mr Hamza Ahmad
            Vehicle regAY19 LTW
            MakeBMW X3
            Yours Faithfully
            QC Law
            records@qc-law.co.uk
            """);

        var selection = new InstructionExtractionPolicySelector([new QclInstructionExtractionPolicy()])
            .Select(read, InstructionDocumentSignature.InstructionRole);

        Assert.Equal(InstructionPolicySelectionOutcome.Selected, selection.Outcome);
        Assert.Equal("QCL", selection.Policy!.PrincipalCode);
    }

    private static InstructionExtractionResult Extract(string text) =>
        new QclInstructionExtractionPolicy().Extract(
            Read(text),
            new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero),
            new("QCL", QclInstructionExtractionPolicy.DocumentProfileKeyValue, 1));

    private static IntakeSourceReadResult Read(string text) => new(
        IntakeSourceReadStatus.Readable,
        [new(IntakeEvidenceSource.DocumentContent, "QCL instruction.docx", text)],
        [],
        [],
        RequiresOcr: false);

    private static InstructionReviewField Field(InstructionExtractionResult result, string name) =>
        Assert.Single(result.Fields, field => field.Name == name);
}
