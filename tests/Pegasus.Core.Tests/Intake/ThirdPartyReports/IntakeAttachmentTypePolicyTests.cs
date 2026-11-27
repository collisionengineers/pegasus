using Pegasus.Core.Documents;
using Pegasus.Core.Intake.ThirdPartyReports;

namespace Pegasus.Core.Tests.Intake.ThirdPartyReports;

/// <summary>
/// A new Case's mailed attachment is an Instruction unless what it prints
/// says otherwise: an Audatex costing with no assessment number is an
/// External estimate, a photo sheet is Other (operator, 27 November 2026;
/// R1336, R1337). The texts are the retained text of the files a.PCH26168-p
/// and its neighbours carried, cut to what the rule reads.
/// </summary>
[Collection(ParserRegexBudgetSerialTests.Name)]
public sealed class IntakeAttachmentTypePolicyTests
{
    private const string ExclusiveAudatexInvoice = """
        Exclusive Repair Network
        St. George's House, 56 Peter Street
        Manchester, M2 3NQ
        Email Address:info@exclusive-assessors.co.uk
        Invoice
        Claim Reference: EHR110660 Created: 25/11/2026
        Audatex System Using Manufacturer Times INVOICE - PAGE 1 OF 3
        Vehicle reg : DF23NMA
        Calculation 25/11/2026
        Total Labour £1,401.62
        Grand Total Incl VAT £3,069.79
        Total Due £3,069.79
        LABOUR Time Basis 10 WU=1 HR.
        63 69 19 00 R + R REAR BUMPER COVER 9.0
        """;

    private const string GarageAudatexReport = """
        TEL: SYNTHETIC COACHWORKS 0151 000 0000 1 SYNTHETIC ROAD
        Assessment Number: JK012544 Bodyshop Full Report
        Version: VBR041NG/1 Printed: 17/11/2026
        Audatex System Using Manufacturer Times
        LABOUR Time Basis 10 WU=1 HR.
        """;

    private const string ExclusivePhotoSheet = """
        Our Ref: Page 1Your Ref: EHR110660
        PM000030-15.jpg PM000030-1.jpg
        PM000030-4.jpg PM000030-13.jpg
        Our Ref: Page 2Your Ref: EHR110660
        PM000030-12.jpg PM000030-7.jpg
        """;

    private const string LairdPhotoSheet = """
        Our Reference
        26-1989072/2648052
        Your Reference
        AKH/JMO/50435/1
        Date
        6th Nov 2026
        Vehicle Images
        Page 1 of 2
        Email: enquiries@laird-assessors.com
        Web: www.laird-assessors.com
        """;

    private const string LairdReportWithPhotographs = """
        F.A.O. The Court
        Dear Sirs
        In accordance with your written instructions asking us to provide an accident damage report
        Repairable Damage Assessment Report
        Vehicle Images
        Page 9 of 10
        Email: enquiries@laird-assessors.com
        """;

    private const string ExclusiveEngineerReport = """
        Date: 22/11/2026
        Our Ref: 00047771/DAVIDR
        Your Ref: EHR110660
        REPAIRABLE REPORT
        The repairers have compiled an estimate on the Audatex System.
        Exclusive Vehicle Assessors
        """;

    private const string AuditInstruction = """
        URGENT NEW INSTRUCTION (Connexus Audit Report)
        Claim Number: 584752
        Registration No: DF23NMA
        Audit Report needed – please find original engineers report
        """;

    [Fact]
    public void AnAudatexCostingWithNoAssessmentNumberIsAnExternalEstimate() =>
        Assert.Equal(
            DocumentSemanticRole.ExternalEstimate,
            IntakeAttachmentTypePolicy.FilingType(ExclusiveAudatexInvoice));

    [Fact]
    public void AnAudatexReportWithItsAssessmentNumberIsLeftForTheWorkersHeaderRule() =>
        Assert.Null(IntakeAttachmentTypePolicy.FilingType(GarageAudatexReport));

    [Fact]
    public void APhotoSheetOfFileNamesIsOther() =>
        Assert.Equal(DocumentSemanticRole.Other, IntakeAttachmentTypePolicy.FilingType(ExclusivePhotoSheet));

    [Fact]
    public void LairdsVehicleImagesSheetIsOther() =>
        Assert.Equal(DocumentSemanticRole.Other, IntakeAttachmentTypePolicy.FilingType(LairdPhotoSheet));

    [Fact]
    public void ALairdReportCarryingItsPhotographsIsNotAPhotoSheet() =>
        Assert.Null(IntakeAttachmentTypePolicy.FilingType(LairdReportWithPhotographs));

    [Fact]
    public void AnEngineerReportNamingAudatexIsNotAnEstimate() =>
        Assert.Null(IntakeAttachmentTypePolicy.FilingType(ExclusiveEngineerReport));

    [Fact]
    public void TheInstructionItselfHasNoOtherType() =>
        Assert.Null(IntakeAttachmentTypePolicy.FilingType(AuditInstruction));

    [Fact]
    public void AnAttachmentWithNoTextHasNoOtherType()
    {
        Assert.Null(IntakeAttachmentTypePolicy.FilingType(null));
        Assert.Null(IntakeAttachmentTypePolicy.FilingType("  "));
    }
}
