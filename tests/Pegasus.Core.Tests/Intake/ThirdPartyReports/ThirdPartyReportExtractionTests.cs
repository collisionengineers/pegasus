using System.Globalization;
using System.Text.RegularExpressions;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;

namespace Pegasus.Core.Tests.Intake.ThirdPartyReports;

/// <summary>
/// Reading a third-party engineer report as source evidence (INTK-031).
///
/// Two kinds of test live here. The bounded ones use inline excerpts of the
/// printed layout and run everywhere, so the rules are provable on any machine.
/// The corpus ones read the reference pack's own extracted text; the pack is
/// local and git-ignored, so they skip with a stated reason when absent rather
/// than passing silently.
///
/// The recorded classification of the 29 originals is NOT restated here. It
/// has one owner — ThirdPartyReportCorpusTests, which reads the real PDFs
/// through the production reader — and a second copy of a recorded fact is a
/// second thing to keep in step. What this project proves instead is what its
/// own text shape can prove and the other cannot: that the verdict and the
/// values do not depend on how the text engine spaced the columns.
///
/// Every excerpt below is written in the shape the PDF text engine actually
/// produces, and the padding-independence test proves the same rules read the
/// same values when a text engine collapses the column padding instead.
/// </summary>
public sealed partial class ThirdPartyReportExtractionTests
{
    private const string ConnexusHeader = """
        Mr D Roberton                                        Date:  09/03/2026
        29 Waterton Avenue
        Gravesend                                            Our Ref:  00077570/PK
        DA12 2PY                                             Your Ref: EHR97818

                       Engineer Repairable Report - Amended Report

        Dear Sirs,

        Client/Insured: Mr D Roberton

            Vehicle: RENAULT CLIO ICONIC TCE      Colour: WHITE       Speedo: 46954     Miles

           Reg No: LD71JHJ      Registered: Sep 2021    Type: 5 Door Hatchback

            Vin No: VF1RJA00667876340       MOT Exp:

          Damage: Light        Accidental Damage Front       Incident: 05/03/2026

             Vehicle Value: £9,267.00      Repair Cost: £6,143.90 inc VAT     Roadworthy: No

        Phil Kendrick AQP CAE AMIMI
        Connexus Vehicle Assessors
        """;

    /// <summary>
    /// The Connexus cost narrative, wrapped mid-sentence exactly as the reader
    /// wraps it. The initial and the agreed labour are both printed and must
    /// both survive as separate amount roles.
    /// </summary>
    private const string ConnexusCosts = """
        COST OF REPAIRS
        The repairers, Miles Better Vehicle Solutions Ltd, Unit 1, Woking, GU21 5SB (Tel.
        01483 757788), have compiled an estimate in the sum of £2,394.25 plus parts at list prices plus
        £834.65 for paint and materials plus £259.82 for specialist/sundry charges. Having calculated
        the time involved in this repair we consider this to be low and we have agreed an amended
        labour figure of £3,351.95. The labour charge
        is based on 35 hours at a rate of £95.77 per hour. We have also agreed that the cost of paint
        and materials will be
        limited to £859.05. The cost of the necessary replacement parts will be approximately
        £715.97, the specialist/sundry
        charges will be £192.95, the VAT liability on this repair will amount to some £1,023.98 giving
        a total repair cost of
        £6,143.90 including VAT subject to any undisclosed damage.
        """;

    private const string MontgomeryCosts = """
        MontgomeryAssessors
              Consulting Motor Engineers, locus reports, Claims investigation.
                                 David Montgomery

        REPAIR
        Our Reference No:    DA/425
        Your Reference No:   47592/1

        Vehicle Details
        Make       RANGE ROVER                  Registration      LP02LOU
        Model          115,477                     Type            5 DOOR SUV
        Odometer      115,977                     Tax Expiry       N/A

        Repair Cost Calculations

        Hours                   26.20
        Hourly rate               90.00
        Total Labour           1,582.20
        Parts                 12,987.21
        Paint/Materials         1,228.11
        Specialist               267.00
        Sub Total             16,064.52
        VAT                   3,212.90
        Total Reserve         19,277.42

        Valuation AUGUST 2026      Trade                              Retail
        Glasses                       14,223                        18,880
        Urban edition adjustment                                       11,120
        Valuation                                                    18,880

        VEHICLE VALUE £30,000.00
        """;

    private const string LairdSupplement = """
            Our Reference     Your Reference                       Date

             26-                   AMA/46319/1                     14th Aug 2026
             1868851/2512558

         Supplementary Report

            Dear Sirs

             Re: Mr Lee Rowland
            Road Traffic Accident on 14/06/2026

             Further to our report regarding your client's , registration LX68HLA.

            The repairing garage, have contacted us as Parts prices have altered with repair time
             increasing after further damage was found.

              Labour:                          £4,064.06 (48.80 hours at £83.28 per hour)
               Parts:                             £2,707.84
               Paint/materials:                    £2,368.03
               Specialist:                         £751.46
               Subtotal:                           £9,891.39
              VAT:                               £1,978.28
               Total:                               £11,869.67

        Email: enquiries@laird-assessors.com   Web:  www.laird-assessors.com
        """;

    private const string SPrintTotals = """
                                                     EMAIL:   sprintassessors@btinternet.com
                                                                       Consulting Engineers
        1 CHERRYTREE CRESCENT, SALFORD PRIORS, WR11 8XF.            Automotive Claims Assessors

        Our Ref       :  17288                             Date of Report     :  18 August 2026
        Your Ref     : ND 47652 1                        Date Instructed    :  12 August 2026

        Insured            : MRS. J FURNELL

        Reg No           : YH70TKZ                 OSF             :
        Make                : SUZUKI                  NSF             :
        Model               : VITARA                 OSR            :
        Body                : HATCHBACK              NSR             :
        Body                : HEAVY

        Labour Rate        £  85.00
        Repair Time In Days  :   10

        Labour         £           0.00                    V.A.T @ 20 %          £           0.00
        Paint / Materials £           0.00                     Contract Repair          £       8250.00
        Parts          £           0.00                 Excess                 £
        Specialist       £           0.00                      Vehicle Market Value     £      11790.00

        Total Exc VAT   £           0.00
        Total Inc VAT £         0.00

        Comments / Repair Notes   :
        THE ABOVE VEHICLE HAS SUSTAINED HEAVY REAR IMPACT DAMAGE, NO ESTIMATES WERE RAISED.
        NOTING ORIGINAL LABOUR £3303, PARTS, £2652, MATERIALS £1554, SPEC £287.
        """;

    /// <summary>
    /// The same sPrint header with the ordinary totals table absent, and a
    /// contract repair printed beside it. A role that prints no total is not a
    /// role that prints a zero.
    /// </summary>
    private const string SPrintContractRepairWithoutTotals = """
                                                             EMAIL:   sprintassessors@btinternet.com
                                                                               Consulting Engineers
        1 CHERRYTREE CRESCENT, SALFORD PRIORS, WR11 8XF.            Automotive Claims Assessors

        Our Ref       :  17289                             Date of Report     :  19 August 2026

        Insured            : MRS. J FURNELL

        Reg No           : YH70TKZ

        Labour Rate        £  85.00
        Repair Time In Days  :   10

        Contract Repair          £       8250.00
        """;

    [Fact]
    public void AConnexusReportIsSelectedFromItsOwnPrintedSignature()
    {
        var result = Read(ConnexusHeader);

        Assert.Equal(ThirdPartySelectionOutcome.Selected, result.Selection.Outcome);
        Assert.Equal(ThirdPartyReportFamily.Connexus, result.Selection.Family);
        Assert.Equal(ThirdPartyDocumentRole.EngineerReport, result.Selection.DocumentRole);
        Assert.Equal("Connexus Vehicle Assessors", result.Selection.Issuer.NormalizedValue);
        Assert.Equal(SourceCandidateDisposition.Usable, result.Selection.Issuer.Disposition);
    }

    [Fact]
    public void TheIssuerIsNeverTakenFromTheFileNameOrTheRetainedPrincipal()
    {
        // The same printed body under a file name and a source label that both
        // claim a different issuer. Nothing but the document decides.
        var result = ThirdPartyReportExtraction.Extract(
            Readable(ConnexusHeader, label: "uploaded MontgomeryRepairable1.pdf, page 1"),
            Context());

        Assert.Equal(ThirdPartyReportFamily.Connexus, result.Selection.Family);

        // And a document whose only Montgomery evidence is its file name is not
        // a report at all.
        var nameOnly = ThirdPartyReportExtraction.Extract(
            Readable(
                "Please find the enclosed paperwork for your attention.",
                label: "uploaded MontgomeryRepairable1.pdf, page 1"),
            Context());

        Assert.Equal(ThirdPartySelectionOutcome.NotApplicable, nameOnly.Selection.Outcome);
        Assert.Equal(ThirdPartySelectionReason.NoDocumentSignature, nameOnly.Selection.Reason);
        Assert.Null(nameOnly.Candidate);
    }

    [Fact]
    public void ASourceWithNoReadableTextIsNotGivenAFamilyOrARoleWithoutOcr()
    {
        var result = ThirdPartyReportExtraction.Extract(
            new(
                IntakeSourceReadStatus.Readable,
                [],
                [],
                [],
                RequiresOcr: true,
                OcrCandidates: [new("uploaded JohnRBell1.pdf", 2)]),
            Context());

        Assert.Equal(ThirdPartySelectionOutcome.NotApplicable, result.Selection.Outcome);
        Assert.Equal(
            ThirdPartySelectionReason.TextUnavailableRequiresOcr,
            result.Selection.Reason);
        Assert.Null(result.Selection.Family);
        Assert.Null(result.Selection.DocumentRole);
        Assert.Null(result.Candidate);

        // The scan-only page is named, with its locator, so a person can check
        // that exact page. It is never reported as a read value.
        var page = Assert.Single(
            result.Candidates,
            row => row.Field == ThirdPartyReportFields.PageRequiresHumanVerification);
        Assert.Equal(2, page.Page);
        Assert.Equal(SourceCandidateDisposition.Missing, page.Disposition);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.PageRequiresHumanVerification);

        // Every finding it raises names the source it is about. This document
        // matched no signature, so its issuer row carries no page and no label
        // at all; filing a finding against that row would persist a statement
        // about a document while naming no part of it (C05-R-10).
        var findings = result.Candidates
            .Where(row => ThirdPartyReportFields.IsFinding(row.Field))
            .ToList();
        Assert.NotEmpty(findings);
        Assert.All(
            findings,
            row => Assert.False(
                string.IsNullOrWhiteSpace(row.SourceLabel),
                $"the {row.NormalizedValue} row names no source"));

        // And the production gate keeps it: a scan-only source is the one
        // unsignatured document that has something to say about itself, so
        // discarding it here would compute these page rows and throw them
        // away (C05-R-11).
        Assert.True(ThirdPartyReportAnalysis.IsRecordable(result));
    }

    /// <summary>
    /// The other half of the recording gate: a document that was read, matched
    /// no signature and states nothing about itself is left entirely alone.
    /// Writing an empty analysis for every unrelated attachment would bury the
    /// ones that matter.
    /// </summary>
    [Fact]
    public void AReadableDocumentThatIsNoReportAndSaysNothingAboutItselfIsNotRecorded()
    {
        var result = Read("Please find the enclosed paperwork for your attention.");

        Assert.Empty(result.Selection.Matches);
        Assert.Empty(result.Findings);
        Assert.False(ThirdPartyReportAnalysis.IsRecordable(result));
    }

    /// <summary>
    /// Two findings can legitimately state the same sentence about the same
    /// page of the same document — and a source row's identifier is derived,
    /// not generated, so without the finding's position in the raised order
    /// both would derive one identifier, collide inside the single write and
    /// lose every candidate for that source rather than the duplicate
    /// (C05-R-12). The derivation stays a pure function of its inputs: the same
    /// position reproduces the same identifier.
    /// </summary>
    [Fact]
    public void TwoFindingsThatStateTheSameSentenceDoNotShareAnIdentifier()
    {
        SourceFieldCandidate Finding(int ordinal) => ThirdPartySourceCandidates.Create(
            Context(),
            ThirdPartyReportFields.Finding(ThirdPartyFindingCodes.FieldConflict),
            ThirdPartyReportProfiles.ReportDocumentRole,
            rawValue: "'estimate.net' has 2 competing printed values; both are retained.",
            normalizedValue: ThirdPartyFindingCodes.FieldConflict,
            page: 2,
            sourceLabel: "uploaded report.pdf, page 2",
            policyVersion: ThirdPartyReportValidation.PolicyVersion,
            disposition: SourceCandidateDisposition.Conflicting,
            region: "finding",
            ordinal: ordinal);

        Assert.NotEqual(Finding(1).Id, Finding(2).Id);
        Assert.Equal(Finding(1).Id, Finding(1).Id);
    }

    [Fact]
    public void TwoMatchingSignaturesAreAmbiguousRatherThanFirstMatchWins()
    {
        var result = Read(ConnexusHeader + "\n" + LairdSupplement);

        Assert.Equal(ThirdPartySelectionOutcome.Ambiguous, result.Selection.Outcome);
        Assert.Equal(
            ThirdPartySelectionReason.MultipleDocumentSignatures,
            result.Selection.Reason);
        Assert.Null(result.Selection.Family);
        Assert.Equal(SourceCandidateDisposition.Ambiguous, result.Selection.Issuer.Disposition);
        Assert.True(result.Selection.Matches.Count > 1);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.DocumentSignatureAmbiguous);
    }

    [Fact]
    public void TheInitialAndTheAgreedLabourAreBothKeptUnderTheirOwnRole()
    {
        var result = Read(ConnexusHeader + "\n" + ConnexusCosts);

        Assert.Equal("2394.25", Value(result, ThirdPartyReportFields.LabourAmount, "initial"));
        Assert.Equal("3351.95", Value(result, ThirdPartyReportFields.LabourAmount, "agreed"));
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.InitialAndAgreedDiffer);
    }

    [Fact]
    public void AConnexusNetIsDerivedAsAFindingAndNeverWrittenBackAsASourceValue()
    {
        var result = Read(ConnexusHeader + "\n" + ConnexusCosts);

        // The document prints no agreed net. The components total £5,119.92 and
        // that total plus the printed VAT is the printed gross — recorded as
        // two findings, with no invented net row.
        Assert.Null(Value(result, ThirdPartyReportFields.Net, "agreed"));
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.NetNotPrinted
                && finding.Message.Contains("5119.92", StringComparison.Ordinal));
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.NetVatGrossReconciles);
    }

    [Fact]
    public void TheHeaderDateIsTheReportDateAndALaterCommentDateDoesNotReplaceIt()
    {
        var result = Read(
            ConnexusHeader
            + "\n\nENGINEER'S COMMENTS\n\n24/03/2026 - More damage found on stripping.\n"
            + "08/04/2026 - Final costs\n");

        Assert.Equal("2026-03-09", Value(result, ThirdPartyReportFields.ReportDate, ""));
    }

    [Fact]
    public void TheCombinedVehicleDescriptionIsAmbiguousRatherThanSplitIntoAMakeAndAModel()
    {
        var result = Read(ConnexusHeader);

        var model = Row(result, ThirdPartyReportFields.Model, "");
        Assert.Equal("RENAULT CLIO ICONIC TCE", model!.NormalizedValue);
        Assert.Equal(SourceCandidateDisposition.Ambiguous, model.Disposition);

        // The narrative layout declares no separate make rule at all, so
        // there is not even an empty make row to mistake for one.
        Assert.Null(Row(result, ThirdPartyReportFields.Make, ""));
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.MakeAndModelNotSeparated);
    }

    [Fact]
    public void TheMontgomeryHoursTimesRateContradictionIsPreservedBesideTheFiguresThatReconcile()
    {
        var result = Read(MontgomeryCosts);

        Assert.Equal(ThirdPartyReportFamily.Montgomery, result.Selection.Family);
        Assert.Equal("26.20", Value(result, ThirdPartyReportFields.LabourHours, "assessed"));
        Assert.Equal("90.00", Value(result, ThirdPartyReportFields.LabourRate, "assessed"));
        Assert.Equal("1582.20", Value(result, ThirdPartyReportFields.LabourAmount, "assessed"));

        // 26.2 x £90 is £2,358, not the printed £1,582.20. The printed labour
        // is what the component total and the gross both reconcile with, so all
        // three findings stand together and no value is repaired.
        var mismatch = Assert.Single(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.LabourHoursRateMismatch);
        Assert.Contains("2358", mismatch.Message, StringComparison.Ordinal);
        Assert.Contains("1582.2", mismatch.Message, StringComparison.Ordinal);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.ComponentSumReconciles);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.NetVatGrossReconciles);
    }

    /// <summary>
    /// A persisted row says which printed cell it was read from. Where the
    /// printed label names something other than the field — a trade value read
    /// from a "Glasses" guide row, a pre-accident value printed as "Valuation",
    /// a gross printed as "Total Reserve" — the label stays in the raw text, so
    /// the smallest useful layout locator survives persistence rather than only
    /// the number surviving it.
    /// </summary>
    [Fact]
    public void APrintedLabelThatNamesSomethingOtherThanTheFieldIsKeptInTheRawText()
    {
        var montgomery = Read(MontgomeryCosts);

        var trade = Row(montgomery, ThirdPartyReportFields.Trade, "")!;
        Assert.Equal("14223", trade.NormalizedValue);
        Assert.Contains("Glasses", trade.RawValue!, StringComparison.Ordinal);

        var retail = Row(montgomery, ThirdPartyReportFields.Retail, "")!;
        Assert.Equal("18880", retail.NormalizedValue);
        Assert.Contains("Glasses", retail.RawValue!, StringComparison.Ordinal);

        var value = Row(montgomery, ThirdPartyReportFields.PreAccidentValue, "")!;
        Assert.Equal("18880", value.NormalizedValue);
        Assert.Contains("Valuation", value.RawValue!, StringComparison.Ordinal);

        var gross = Row(montgomery, ThirdPartyReportFields.Gross, "assessed")!;
        Assert.Equal("19277.42", gross.NormalizedValue);
        Assert.Contains("Total Reserve", gross.RawValue!, StringComparison.Ordinal);

        // The supplement's subtotal and its total are two printed cells that
        // differ by one printed word, and each row now carries its own.
        var laird = Read(LairdSupplement);
        Assert.StartsWith(
            "Subtotal:",
            Row(laird, ThirdPartyReportFields.Net, "supplement")!.RawValue,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "Total:",
            Row(laird, ThirdPartyReportFields.Gross, "supplement")!.RawValue,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheMontgomeryModelAndOdometerConflictStaysVisible()
    {
        var result = Read(MontgomeryCosts);

        Assert.Equal("115,477", Value(result, ThirdPartyReportFields.Model, ""));
        Assert.Equal("115977", Value(result, ThirdPartyReportFields.Mileage, ""));
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.ModelOdometerConflict);
    }

    [Fact]
    public void APrintedAdjustmentKeepsItsOwnLabelAndStillReconcilesTheValuation()
    {
        var result = Read(MontgomeryCosts);

        var adjustment = Row(result, ThirdPartyReportFields.ValuationAdjustment, "");
        Assert.Equal("11120", adjustment!.NormalizedValue);

        // The printed label is neither mileage nor condition, so it is kept
        // verbatim rather than filed under a typed slot it does not belong to.
        Assert.Contains("Urban edition adjustment", adjustment.RawValue!, StringComparison.Ordinal);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.ValuationAdjustmentReconciles);
    }

    [Fact]
    public void TheLairdSupplementaryHeadingControlsAndNoBaseFieldIsFilledIn()
    {
        var result = Read(LairdSupplement);

        Assert.Equal(ThirdPartyReportFamily.Laird, result.Selection.Family);
        Assert.Equal("Supplementary Report", Value(result, ThirdPartyReportFields.Revision, ""));
        Assert.Equal("4064.06", Value(result, ThirdPartyReportFields.LabourAmount, "supplement"));
        Assert.Equal("11869.67", Value(result, ThirdPartyReportFields.Gross, "supplement"));
        Assert.Equal("26-1868851/2512558", Value(result, ThirdPartyReportFields.ReportReference, "our-ref"));

        // The base report's own assessed figures are absent from a supplement
        // and stay absent: nothing is borrowed from another document.
        Assert.Null(Value(result, ThirdPartyReportFields.LabourAmount, "assessed"));
        Assert.Null(Value(result, ThirdPartyReportFields.Net, "assessed"));
        Assert.Null(result.Candidate!.Identity.BaseReportDocumentId);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.SupplementWithoutProvedBase);
    }

    [Fact]
    public void TheLairdSubtotalIsNotAlsoReadAsTheTotal()
    {
        var result = Read(LairdSupplement);

        // "Subtotal:" ends in "total:". An unanchored total rule read it twice
        // and reported a conflict the document does not have.
        Assert.Equal("9891.39", Value(result, ThirdPartyReportFields.Net, "supplement"));
        Assert.Equal(
            SourceCandidateDisposition.Usable,
            Row(result, ThirdPartyReportFields.Gross, "supplement")!.Disposition);
    }

    [Fact]
    public void SPrintZeroTotalsAndTheContractRepairAreDifferentAmountRoles()
    {
        var result = Read(SPrintTotals);

        Assert.Equal(ThirdPartyReportFamily.SPrint, result.Selection.Family);
        Assert.Equal("0.00", Value(result, ThirdPartyReportFields.Net, "assessed"));
        Assert.Equal("8250.00", Value(result, ThirdPartyReportFields.Net, "contract-repair"));
        Assert.Equal("3303", Value(result, ThirdPartyReportFields.LabourAmount, "initial"));
        Assert.Equal("2652", Value(result, ThirdPartyReportFields.Parts, "initial"));

        // Neither figure is chosen: the zero total is reported as not being the
        // agreed repair cost, and both stay on the record.
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.ZeroTotalsWithContractRepair);
    }

    [Fact]
    public void AnUnprintedTotalIsNotReadAsAPrintedZeroBesideAContractRepair()
    {
        var result = Read(SPrintContractRepairWithoutTotals);

        Assert.Equal(ThirdPartyReportFamily.SPrint, result.Selection.Family);
        Assert.Equal("8250.00", Value(result, ThirdPartyReportFields.Net, "contract-repair"));

        // This document prints no ordinary totals at all, so they are
        // unavailable rather than zero. "The ordinary repair totals are zero"
        // would be a statement about figures the document does not make.
        Assert.Null(Value(result, ThirdPartyReportFields.Net, "assessed"));
        Assert.Null(Value(result, ThirdPartyReportFields.Gross, "assessed"));
        Assert.Null(Value(result, ThirdPartyReportFields.LabourAmount, "assessed"));
        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.ZeroTotalsWithContractRepair);

        // What the document does print is still reported: a contract repair
        // figure with no stated VAT basis.
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.ContractRepairBasisNotPrinted);
    }

    [Fact]
    public void ALairdSupplementThatNamesAnAppendedImageIsStillOnlyALairdReport()
    {
        var result = Read(LairdSupplement + "\n  ClientVehicleDamageImage1jpg-V1.jpg\n");

        // The image-evidence signature denies the Laird domain and the
        // Supplementary heading, so an appended image filename cannot make a
        // report match two signatures, turn Ambiguous, and lose every value
        // the document actually prints.
        Assert.Equal(ThirdPartySelectionOutcome.Selected, result.Selection.Outcome);
        Assert.Equal(ThirdPartyReportFamily.Laird, result.Selection.Family);
        Assert.Single(result.Selection.Matches);
        Assert.Equal("9891.39", Value(result, ThirdPartyReportFields.Net, "supplement"));
        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.DocumentSignatureAmbiguous);
    }

    [Fact]
    public void ANegativeDocumentIsRoutedToItsOwnRoleAndGetsNoReportVerdict()
    {
        var estimate = Read("""
            Full Estimate Report
            Audatex UK Limited
            Total Estimate      £4,120.55
            """);

        Assert.Equal(ThirdPartySelectionOutcome.NotApplicable, estimate.Selection.Outcome);
        Assert.Equal(ThirdPartySelectionReason.NonReportDocumentRole, estimate.Selection.Reason);
        Assert.Equal(ThirdPartyDocumentRole.Estimate, estimate.Selection.DocumentRole);
        Assert.Null(estimate.Selection.Family);
        Assert.Null(estimate.Candidate);

        // No outcome, repairability or amount is asserted for it at all.
        Assert.DoesNotContain(
            estimate.Candidates,
            row => row.Field == ThirdPartyReportFields.Outcome
                || row.Field == ThirdPartyReportFields.Repairability
                || row.Field == ThirdPartyReportFields.Gross);
    }

    [Fact]
    public void AnImageOnlyReportPageYieldsImageEvidenceAndNoVehicleOrCostFacts()
    {
        var images = Read("""
             Our Ref:  43317                    Your Ref:  47510/1              Page 1

            ClientVehicleDamageImage1jpg-V1.jpg           2_ClientVehicleDamageImage2jpg-V1.jpg
            """);

        Assert.Equal(ThirdPartyDocumentRole.ImageEvidence, images.Selection.DocumentRole);
        Assert.Equal(ThirdPartySelectionReason.NonReportDocumentRole, images.Selection.Reason);
        Assert.Null(images.Candidate);
    }

    [Fact]
    public void ReadingTheSameBytesTwiceProducesTheIdenticalRecord()
    {
        var first = Read(ConnexusHeader + "\n" + ConnexusCosts);
        var second = Read(ConnexusHeader + "\n" + ConnexusCosts);

        Assert.Equal(
            first.Candidates.Select(Describe),
            second.Candidates.Select(Describe));
        Assert.Equal(
            first.Findings.Select(finding => finding.Code + "|" + finding.Message),
            second.Findings.Select(finding => finding.Code + "|" + finding.Message));

        // The identity is derived from the source hash, field, role and
        // locator, so replay is stable rather than merely equal in content.
        Assert.Equal(
            first.Candidates.Select(row => row.Id),
            second.Candidates.Select(row => row.Id));
    }

    [Theory]
    [InlineData(nameof(ConnexusHeader))]
    [InlineData(nameof(ConnexusCosts))]
    [InlineData(nameof(MontgomeryCosts))]
    [InlineData(nameof(LairdSupplement))]
    [InlineData(nameof(SPrintTotals))]
    public void TheSameValuesAreReadWhetherOrNotTheTextEngineKeepsTheColumnPadding(string excerpt)
    {
        var text = Excerpt(excerpt);
        var padded = Read(text);
        var collapsed = Read(Collapse(text));

        Assert.Equal(padded.Selection.Family, collapsed.Selection.Family);
        Assert.Equal(
            padded.Candidates.Select(Describe),
            collapsed.Candidates.Select(Describe));
    }

    /// <summary>
    /// The verdict for every one of the 29 originals is the same whether the
    /// text engine keeps a printed column's padding or collapses it to single
    /// spaces. That is the property this project can prove and the real-PDF
    /// corpus test cannot: it reads through one engine, and this reads the same
    /// text in both shapes.
    ///
    /// It also proves that no original matches two document signatures at once.
    /// An ambiguous verdict costs a report every candidate it prints, so it is
    /// a failure here rather than a shrug.
    /// </summary>
    [ReferencePackFact]
    public void EveryCorpusOriginalClassifiesTheSameWhicheverWayTheTextIsSpaced()
    {
        var wrong = new List<string>();
        var count = 0;

        foreach (var (name, pages) in CorpusText())
        {
            count++;
            var padded = ThirdPartyReportExtraction.Extract(Readable(pages), Context());
            var collapsed = ThirdPartyReportExtraction.Extract(
                Readable([.. pages.Select(page => (page.Page, Collapse(page.Text)))]),
                Context());
            if (!string.Equals(
                    Classification(padded),
                    Classification(collapsed),
                    StringComparison.Ordinal))
            {
                wrong.Add(
                    $"{name}: padded {Classification(padded)}, "
                    + $"collapsed {Classification(collapsed)}");
            }

            if (padded.Selection.Outcome == ThirdPartySelectionOutcome.Ambiguous)
            {
                wrong.Add(
                    $"{name}: matched {padded.Selection.Matches.Count} document signatures at once");
            }
        }

        Assert.Equal(29, count);
        Assert.True(wrong.Count == 0, string.Join("; ", wrong));
    }

    /// <summary>The verdict for one source, in the words the record uses.</summary>
    private static string Classification(ThirdPartyReportExtractionResult result) =>
        result.Selection switch
        {
            { Family: { } family } => family.ToString(),
            { Reason: ThirdPartySelectionReason.TextUnavailableRequiresOcr } =>
                nameof(ThirdPartySelectionReason.TextUnavailableRequiresOcr),
            { DocumentRole: { } role } => role.ToString(),
            var selection => selection.Reason.ToString()
        };

    /// <summary>
    /// The per-family reading, against the pack's own extracted text: for every
    /// original that resolves to a family with rules, the reader must produce a
    /// candidate and at least one usable field. A family that classified but
    /// read nothing is a regression the classification test cannot see.
    /// </summary>
    [ReferencePackFact]
    public void EveryClassifiedCorpusReportReadsAtLeastOneUsableField()
    {
        var empty = new List<string>();
        foreach (var (name, pages) in CorpusText())
        {
            var result = ThirdPartyReportExtraction.Extract(Readable(pages), Context());
            if (result.Selection.Family is null)
            {
                continue;
            }

            var usable = result.Candidates.Count(row =>
                row.Disposition == SourceCandidateDisposition.Usable
                && row.Field != ThirdPartyReportFields.Issuer);
            if (usable == 0)
            {
                empty.Add(name);
            }
        }

        Assert.Empty(empty);
    }

    private static ThirdPartyReportExtractionResult Read(string text) =>
        ThirdPartyReportExtraction.Extract(Readable(text), Context());

    private static IntakeSourceReadResult Readable(
        string text,
        string label = "uploaded report.pdf, page 1") =>
        new(
            IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.PdfContent, label, text)],
            [],
            [],
            RequiresOcr: false);

    private static IntakeSourceReadResult Readable(List<(int Page, string Text)> pages) =>
        new(
            IntakeSourceReadStatus.Readable,
            [.. pages.Select(page => new IntakeContentFragment(
                IntakeEvidenceSource.PdfContent,
                $"uploaded report.pdf, page {page.Page}",
                page.Text))],
            [],
            [],
            RequiresOcr: pages.Count == 0);

    private static ThirdPartyReportSourceContext Context() =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new string('a', 64),
            Occurrence: 0,
            IntakeAssetId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ReaderVersion: "1");

    private static SourceFieldCandidate? Row(
        ThirdPartyReportExtractionResult result,
        string field,
        string referenceRole) =>
        result.Candidates.FirstOrDefault(row =>
            row.Field == field
            && row.ReferenceRole == referenceRole);

    private static string? Value(
        ThirdPartyReportExtractionResult result,
        string field,
        string referenceRole)
    {
        var row = Row(result, field, referenceRole);
        return row?.Disposition == SourceCandidateDisposition.Missing ? null : row?.NormalizedValue;
    }

    private static string Describe(SourceFieldCandidate row) =>
        string.Join(
            '|',
            row.Field,
            row.PartyRole,
            row.ReferenceRole,
            row.RawValue,
            row.NormalizedValue,
            row.Unit,
            row.Currency,
            row.Page?.ToString(CultureInfo.InvariantCulture),
            row.Disposition.ToString());

    /// <summary>
    /// The harsher text shape: a PDF text engine that joins the words of a line
    /// with one space instead of preserving the column padding. Line breaks are
    /// kept, because every engine keeps those.
    /// </summary>
    private static string Collapse(string text) =>
        string.Join(
            '\n',
            text.Split('\n').Select(line => Runs().Replace(line, " ").TrimEnd()));

    private static string Excerpt(string name) => name switch
    {
        nameof(ConnexusHeader) => ConnexusHeader,
        nameof(ConnexusCosts) => ConnexusHeader + "\n" + ConnexusCosts,
        nameof(MontgomeryCosts) => MontgomeryCosts,
        nameof(LairdSupplement) => LairdSupplement,
        nameof(SPrintTotals) => SPrintTotals,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown excerpt.")
    };

    /// <summary>
    /// The pack's own extracted text per original, page by page. It reads the
    /// pack's recorded text paths — never the PDFs and never a copy of them.
    /// </summary>
    private static IEnumerable<(string Name, List<(int Page, string Text)> Pages)> CorpusText()
    {
        var root = ConfiguredPackRoot()
            ?? throw new InvalidOperationException("This test should have been skipped.");
        var astra = Path.Combine(root, "astra_output");
        var inventory = Path.Combine(astra, "reports", "third-party-source-inventory.json");
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(inventory));
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var source = entry.GetProperty("source").GetString()!;
            var textPath = entry.GetProperty("text_path").GetString()!.Replace('\\', '/');
            var full = Path.Combine(astra, textPath);
            if (!File.Exists(full))
            {
                throw new FileNotFoundException($"The pack records no extracted text at {textPath}.");
            }

            yield return (Path.GetFileName(source), SplitPages(File.ReadAllText(full)));
        }
    }

    private static List<(int Page, string Text)> SplitPages(string text)
    {
        var pages = new List<(int, string)>();
        var matches = PageMarker().Matches(text);
        for (var index = 0; index < matches.Count; index++)
        {
            var start = matches[index].Index + matches[index].Length;
            var end = index + 1 < matches.Count ? matches[index + 1].Index : text.Length;
            var body = text[start..end];
            if (!string.IsNullOrWhiteSpace(body))
            {
                pages.Add((
                    int.Parse(matches[index].Groups["n"].ValueSpan, provider: CultureInfo.InvariantCulture),
                    body));
            }
        }

        return pages;
    }

    internal static string? ConfiguredPackRoot()
    {
        // The same variable the integration corpus tests resolve the pack from.
        // The locator itself lives in each project because the pack is read by
        // two assemblies and neither is a dependency of the other.
        var root = Environment.GetEnvironmentVariable("PEGASUS_REFERENCE_PACK_ROOT");
        return string.IsNullOrWhiteSpace(root) ? null : root;
    }

    [GeneratedRegex(@"[ \t]+", RegexOptions.CultureInvariant, 100)]
    private static partial Regex Runs();

    [GeneratedRegex(@"=== PAGE (?<n>\d+) ===\r?\n", RegexOptions.CultureInvariant, 100)]
    private static partial Regex PageMarker();
}

/// <summary>
/// A fact that needs the local, git-ignored reference pack. It skips with a
/// stated reason when the pack is not on this machine — never silently, and
/// never reported as a pass.
/// </summary>
internal sealed class ReferencePackFactAttribute : FactAttribute
{
    public ReferencePackFactAttribute()
    {
        var root = ThirdPartyReportExtractionTests.ConfiguredPackRoot();
        if (root is null)
        {
            Skip = "PEGASUS_REFERENCE_PACK_ROOT is not set; the reference pack is a local, "
                + "git-ignored collection that differs per machine. INCONCLUSIVE, not passed.";
        }
        else if (!Directory.Exists(root))
        {
            Skip = "PEGASUS_REFERENCE_PACK_ROOT names a directory that does not exist on this "
                + "machine. INCONCLUSIVE, not passed.";
        }
    }
}
