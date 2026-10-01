using Pegasus.Core.Assessment;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;

namespace Pegasus.Core.Tests.Intake.ThirdPartyReports;

/// <summary>
/// John R Bell's report is a printed form that always arrives as a scan, so
/// what the reader sees is OCR text in reading order: a row of labels, then
/// the row of their values, each on its own line. These pages are synthetic
/// — the form's own labels with made-up values — in exactly that shape.
/// </summary>
public sealed partial class ThirdPartyReportExtractionTests
{
    private const string JohnRBellPageOne = """
        JOHN R. BELL & ASSOCIATES
        JOHN R. BELL M.Inst.A.E.A. M.F.I.E.A.
        Motor Claims Assessor, Loss Adjuster & Negotiator
        Telephone 01000 000000 e-mail info@example.test
        DATE : 30/09/2026 MY REF: 1234 CLAIM NO : XX/YY/12345/1 CLIENT: EXAMPLE MOTORS LTD
        Repairers
        Inspected on
        At
        EXAMPLE AUTOS LTD
        30/09/26
        Via Images
        Year
        09/24
        VEHICLE DETAILS
        Make
        EXAMPLE MAKE
        CC
        1950
        Model
        EXAMPLE MODEL 300
        Reg.No.
        AB12 CDE
        Extras/Modifications
        Mileage
        Tax Expiry
        Tread on tyres - mms.
        Chassis No.
        123456
        08/27
        NSF
        5
        NSR
        5
        WVWZZZ1KZAW123456
        OSF
        5
        Paintwork Colour
        BLACK
        Overall Pre-Accident Condition
        VERY GOOD
        Guide Values
        Glass's
        My estimated retail value
        £
        Retail £ 12345
        Dealer Retail
        £
        Retail
        £
        12345
        Trade £
        Dealer Trade
        £
        Trade
        £
        Direction of Impact
        Degree of Damage
        Moderate
        Roadworthy/Unroadworthy?
        Roadworthy
        Legal to drive ? YES
        COST OF REPAIRS - DAMAGE DETAILS OVERLEAF
        Discussed in great detail and agreed at £ 1000.50 labour, parts approx £ 2000 incl auxiliary work, Software
        £85, Paint materials £ 500.25 DISCOUNTED 10%, Anti corrosion £15. AUDATEX computer
        generated estimate.
        Labour rate £99, Approx repair time 6-8 days
        Provisional reserve figure for repairs. Subject to MLP & further checking. Inc. VAT
        £ 2345
        GENERAL REMARKS
        This report relates to the damage sustained to vehicle (AB12 CDE) in a road
        traffic accident.
        John R/Bell M.Inst.A.E.A. M.F.I.E.A.
        This engineer's report is addressed TO THE COURT and prepared in accordance with the requirements of the Civil Procedure rules.
        """;

    private const string JohnRBellPageTwo = """
        DATE: 30/09/26 MY REF: 1234 CLAIM NO : XX/YY/12345/1 CLIENT: EXAMPLE MOTORS LTD
        Vehicle component
        Damage sustained to
        Action - RN - RP, etc.
        Bumper Rear
        Complete cover assembly
        Renew
        """;

    private const string JohnRBellPageThree = """
        JOHN R. BELL & ASSOCIATES
        JOHN R. BELL M.Inst.A.E.A. M.F.I.E.A.
        Motor Claims Assessor, Loss Adjuster & Negotiator
        DATE: 30/09/2026 MY REF: 1234 CLAIM NO: XX/YY/12345/1 CLIENT : EXAMPLE MOTORS LTD
        To taking your instructions; inspecting vehicle; discussing repairs and agreeing figures; advising
        you our findings, as per our report of today's date.
        Fee:
        £110.00
        Please make cheques payable to John R. Bell
        """;

    [Fact]
    public void TheJohnRBellOcrTextSelectsItsFamilyAlone()
    {
        var result = ThirdPartyReportExtraction.Extract(JohnRBellOcr(), Context());

        Assert.Equal(ThirdPartySelectionOutcome.Selected, result.Selection.Outcome);
        Assert.Equal(ThirdPartyReportFamily.JohnRBell, result.Selection.Family);
        Assert.Single(result.Selection.Matches);
        Assert.Equal("John R Bell", result.Candidate!.Identity.Issuer!.Value);
        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Code is ThirdPartyFindingCodes.SourceRequiresOcr
                or ThirdPartyFindingCodes.ReportFieldsUnavailableWithoutOcr
                or ThirdPartyFindingCodes.DocumentSignatureAmbiguous);
    }

    [Fact]
    public void TheJohnRBellFormFillsEachLabelledField()
    {
        var result = ThirdPartyReportExtraction.Extract(JohnRBellOcr(), Context());

        // The header row repeats on every page in two date spellings; one
        // normalised date, one reference, one claimant, not a conflict.
        Assert.Equal("2026-09-30", Value(result, ThirdPartyReportFields.ReportDate, string.Empty));
        Assert.Equal("1234", Value(result, ThirdPartyReportFields.ReportReference, "our-ref"));
        Assert.Equal("XX/YY/12345/1", Value(result, ThirdPartyReportFields.ClaimReference, "your-ref"));
        Assert.Equal("EXAMPLE MOTORS LTD", PartyValue(result, ThirdPartyReportFields.Claimant, "claimant"));
        Assert.Equal("JOHN R. BELL", Value(result, ThirdPartyReportFields.EngineerName, string.Empty));
        Assert.Equal("M.Inst.A.E.A. M.F.I.E.A.", Value(result, ThirdPartyReportFields.EngineerQualifications, string.Empty));

        // The "Repairers / Inspected on / At" row prints three labels then
        // three values; the two-digit year is the form's own spelling.
        Assert.Equal("EXAMPLE AUTOS LTD", PartyValue(result, ThirdPartyReportFields.Repairer, "repairer"));
        Assert.Equal("2026-09-30", Value(result, ThirdPartyReportFields.InspectionDate, string.Empty));
        Assert.Equal("Via Images", Value(result, ThirdPartyReportFields.ObservedInspectionMethod, string.Empty));

        Assert.Equal("EXAMPLE MAKE", Value(result, ThirdPartyReportFields.Make, string.Empty));
        Assert.Equal("EXAMPLE MODEL 300", Value(result, ThirdPartyReportFields.Model, string.Empty));
        Assert.Equal("AB12CDE", Value(result, ThirdPartyReportFields.Registration, string.Empty));
        Assert.Equal("123456", Value(result, ThirdPartyReportFields.Mileage, string.Empty));
        Assert.Equal("WVWZZZ1KZAW123456", Value(result, ThirdPartyReportFields.Vin, string.Empty));

        Assert.Equal("Moderate", Value(result, ThirdPartyReportFields.Severity, string.Empty));
        Assert.Equal("Roadworthy", Value(result, ThirdPartyReportFields.Roadworthiness, string.Empty));
        Assert.Equal("12345", Value(result, ThirdPartyReportFields.Retail, string.Empty));
        // The trade cell is printed blank: a Missing row, never an invented value.
        Assert.Equal(SourceCandidateDisposition.Missing, Row(result, ThirdPartyReportFields.Trade, string.Empty)!.Disposition);

        Assert.Equal("1000.50", Value(result, ThirdPartyReportFields.LabourAmount, ThirdPartyEstimateRoles.Agreed));
        Assert.Equal("2000", Value(result, ThirdPartyReportFields.Parts, ThirdPartyEstimateRoles.Agreed));
        Assert.Equal("500.25", Value(result, ThirdPartyReportFields.PaintMaterials, ThirdPartyEstimateRoles.Agreed));
        Assert.Equal("99", Value(result, ThirdPartyReportFields.LabourRate, ThirdPartyEstimateRoles.Agreed));
        Assert.Equal("2345", Value(result, ThirdPartyReportFields.Gross, ThirdPartyEstimateRoles.Agreed));
        Assert.Equal("6", Value(result, ThirdPartyReportFields.MinimumRepairDays, string.Empty));
        Assert.Equal("8", Value(result, ThirdPartyReportFields.MaximumRepairDays, string.Empty));
        Assert.StartsWith("This engineer's report is addressed TO THE COURT", Value(result, ThirdPartyReportFields.Declaration, string.Empty));

        // The form prints no repairable or total-loss word, so nothing is invented.
        Assert.Null(Row(result, ThirdPartyReportFields.Outcome, string.Empty));
        Assert.Null(Row(result, ThirdPartyReportFields.Repairability, string.Empty));
    }

    [Fact]
    public void TheJohnRBellFormFillsAssessorDateAndRoadworthinessAndLeavesRepairableStatusBlank()
    {
        var result = ThirdPartyReportExtraction.Extract(JohnRBellOcr(), Context());

        var reading = OriginalReportPrefillPolicy.Read(result.Candidate, new string('a', 64));

        Assert.Equal("John R Bell", reading.Assessor);
        Assert.Equal("2026-09-30", reading.ReportDate);
        Assert.Equal("roadworthy", reading.Roadworthiness);
        Assert.Null(reading.Outcome);
        Assert.False(reading.OutcomeUnreadable);
        Assert.Contains(reading.Assessor!, ThirdPartyReportProfiles.KnownIssuers);
    }

    [Fact]
    public void AnInstructionLetterThatNamesJohnRBellIsNotAReport()
    {
        var letter = Read("""
            Dear Sirs,
            Please find attached the engineer's report prepared by John R Bell & Associates.
            We would be grateful for your audit of its figures.
            """);

        Assert.Equal(ThirdPartySelectionOutcome.NotApplicable, letter.Selection.Outcome);
        Assert.Equal(ThirdPartySelectionReason.NoDocumentSignature, letter.Selection.Reason);
        Assert.Null(letter.Candidate);
    }

    private static IntakeSourceReadResult JohnRBellOcr() =>
        Readable(
        [
            (1, JohnRBellPageOne),
            (2, JohnRBellPageTwo),
            (3, JohnRBellPageThree)
        ]);

    private static string? PartyValue(ThirdPartyReportExtractionResult result, string field, string partyRole) =>
        result.Candidates
            .SingleOrDefault(row => row.Field == field && row.PartyRole == partyRole && row.Disposition == SourceCandidateDisposition.Usable)
            ?.NormalizedValue;
}
