using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;

namespace Pegasus.Core.Tests.Intake.ThirdPartyReports;

/// <summary>
/// A report is read as one document. An e-mail that carries two documents is
/// two readings, never one flattened text; an OCR page is read by its page
/// number like any other page.
/// </summary>
public sealed partial class ThirdPartyReportExtractionTests
{
    private const string MailLabel = "uploaded mail.eml";
    private const string ReportLabel = MailLabel + ", attachment 1: connexus-report.pdf";
    private const string SecondReportLabel = MailLabel + ", attachment 2: montgomery-report.pdf";

    [Fact]
    public void AnEmailWithTwoReportsReadsEachFileAlone()
    {
        var whole = WholeMail();

        // Flattened, two issuers speak at once and nothing can be read.
        var flattened = ThirdPartyReportExtraction.Extract(whole, Context());
        Assert.Equal(ThirdPartySelectionOutcome.Ambiguous, flattened.Selection.Outcome);

        // Read as documents, each file is its own report.
        var connexus = ThirdPartyReportExtraction.Extract(
            ThirdPartyReportDocuments.ForDocument(whole, ReportLabel), Context());
        var montgomery = ThirdPartyReportExtraction.Extract(
            ThirdPartyReportDocuments.ForDocument(whole, SecondReportLabel), Context());

        Assert.Equal(ThirdPartySelectionOutcome.Selected, connexus.Selection.Outcome);
        Assert.Equal(ThirdPartyReportFamily.Connexus, connexus.Selection.Family);
        Assert.Equal("LD71JHJ", Value(connexus, ThirdPartyReportFields.Registration));
        Assert.Equal(ThirdPartySelectionOutcome.Selected, montgomery.Selection.Outcome);
        Assert.Equal(ThirdPartyReportFamily.Montgomery, montgomery.Selection.Family);
        Assert.Equal("LP02LOU", Value(montgomery, ThirdPartyReportFields.Registration));
    }

    [Fact]
    public void ForDocumentKeepsOnlyThatDocumentsPagesScansAndImages()
    {
        var whole = WholeMail();

        var document = ThirdPartyReportDocuments.ForDocument(whole, ReportLabel);

        var fragment = Assert.Single(document.Content);
        Assert.Equal(ReportLabel + ", page 1", fragment.SourceLabel);
        var scanned = Assert.Single(document.ScannedPdfPages);
        Assert.Equal(ReportLabel, scanned.SourceLabel);
        Assert.Equal(2, scanned.PageNumber);
        Assert.True(document.RequiresOcr);
        var image = Assert.Single(document.AssetCandidates);
        Assert.Equal(ReportLabel + ", page 1, image 1", image.SourceLabel);
        Assert.Empty(document.Issues);
        Assert.Empty(document.TransportEvidence);
        // The reader's provenance is the whole source's and travels with each document.
        Assert.Equal("mimekit_pdfpig_openxml", document.ReaderKey);
    }

    [Fact]
    public void TheEmailBodyIsNeverPartOfADocument()
    {
        var whole = WholeMail();

        var second = ThirdPartyReportDocuments.ForDocument(whole, SecondReportLabel);

        Assert.DoesNotContain(second.Content, fragment => fragment.Text.Contains("Please find", StringComparison.Ordinal));
        Assert.Empty(second.ScannedPdfPages);
        Assert.False(second.RequiresOcr);
        Assert.Empty(second.AssetCandidates);
    }

    [Fact]
    public void AnOcrPageIsReadByItsPageNumberLikeAnyOtherPage()
    {
        var ocr = IntakeOcrText.ReadResult(
            "uploaded report.pdf",
            new string('a', 64),
            new(
                IntakeOcrState.Completed,
                "azure-document-intelligence",
                "prebuilt-layout",
                "2024-11-30",
                ResponseSha256: new string('b', 64),
                Pages: [new(2, ConnexusHeader, [], [])]));

        var result = ThirdPartyReportExtraction.Extract(ocr, Context());

        Assert.Equal(ThirdPartySelectionOutcome.Selected, result.Selection.Outcome);
        Assert.Equal(ThirdPartyReportFamily.Connexus, result.Selection.Family);
        var registration = Assert.Single(
            result.Candidates,
            row => row.Field == ThirdPartyReportFields.Registration);
        Assert.Equal(2, registration.Page);
        Assert.Equal("uploaded report.pdf, page 2", registration.SourceLabel);
        Assert.DoesNotContain(result.Findings, finding => finding.Code == ThirdPartyFindingCodes.SourceRequiresOcr);
    }

    /// <summary>
    /// One mail as the reader hands it over: a body, a Connexus report whose
    /// second page is a scan with a photograph on its first, and a Montgomery
    /// report.
    /// </summary>
    private static IntakeSourceReadResult WholeMail() =>
        new(
            IntakeSourceReadStatus.Readable,
            [
                new(IntakeEvidenceSource.EmailBody, MailLabel + ", message body", "Please find the two reports attached."),
                new(IntakeEvidenceSource.PdfContent, ReportLabel + ", page 1", ConnexusHeader, IntakeSourceLocator.ForPage(1)),
                new(IntakeEvidenceSource.PdfContent, SecondReportLabel + ", page 1", MontgomeryCosts, IntakeSourceLocator.ForPage(1))
            ],
            [new(IntakeEvidenceSource.Sender, "reports@example.test")],
            [new("scanned-pdf-page", ReportLabel + ", page 2 has little embedded text", IntakeEvidenceSource.PdfContent)],
            true,
            Assets:
            [
                new(ReportLabel + ", page 1, image 1", "page-1-image-1.jpg", "image/jpeg", new byte[] { 0xff, 0xd8 },
                    IntakeAssetKind.EmbeddedImage, IntakeAssetDisposition.Embedded, PageNumber: 1)
            ],
            OcrCandidates: [new(ReportLabel, 2)],
            ReaderKey: "mimekit_pdfpig_openxml",
            ReaderVersion: "r");

    private static string? Value(ThirdPartyReportExtractionResult result, string field) =>
        result.Candidates.SingleOrDefault(row => row.Field == field)?.NormalizedValue;
}
