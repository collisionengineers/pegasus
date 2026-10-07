using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// OCR text enters a reading in the reader's own shape: labelled by document
/// and page, with the OCR provenance in the locator. That is what lets the
/// instruction reader and the third-party report reader read an OCR page
/// exactly as they read an embedded-text page.
/// </summary>
public sealed class IntakeOcrTextTests
{
    private const string SourceHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string ResponseHash = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

    [Fact]
    public void OcrPagesBecomeReaderShapedFragmentsWithOcrProvenance()
    {
        var read = IntakeOcrText.ReadResult(
            "uploaded mail.eml, attachment 2: report.pdf",
            SourceHash,
            Result(new IntakeOcrPage(3, "Page three", [], []), new IntakeOcrPage(1, "Page one", [], [])));

        Assert.Equal(IntakeSourceReadStatus.Readable, read.Status);
        Assert.Equal("azure-document-intelligence/prebuilt-layout", read.ReaderKey);
        Assert.Equal("2024-11-30", read.ReaderVersion);
        Assert.False(read.RequiresOcr);
        Assert.Empty(read.ScannedPdfPages);
        Assert.Collection(
            read.Content,
            first =>
            {
                Assert.Equal("uploaded mail.eml, attachment 2: report.pdf, page 1", first.SourceLabel);
                Assert.Equal("Page one", first.Text);
                Assert.Equal(1, first.Locator!.Page);
                Assert.Equal(IntakeOcrText.DocumentRole, first.Locator.DocumentRole);
                Assert.Equal(SourceHash, first.Locator.Sha256);
                Assert.Contains(ResponseHash, first.Locator.Region, StringComparison.Ordinal);
                Assert.True(IntakeOcrText.IsOcrLocator(first.Locator));
            },
            second =>
            {
                Assert.Equal("uploaded mail.eml, attachment 2: report.pdf, page 3", second.SourceLabel);
                Assert.Equal(3, second.Locator!.Page);
            });
    }

    [Fact]
    public void ABlankOcrPageProducesNoFragment()
    {
        var read = IntakeOcrText.ReadResult(
            "uploaded scan.pdf",
            SourceHash,
            Result(new IntakeOcrPage(1, "   ", [], []), new IntakeOcrPage(2, "Second page", [], [])));

        var fragment = Assert.Single(read.Content);
        Assert.Equal("uploaded scan.pdf, page 2", fragment.SourceLabel);
    }

    [Fact]
    public void MergeReplacesOnlyTheQualifiedPagesOfTheOcrDocument()
    {
        var ordinary = new IntakeSourceReadResult(
            IntakeSourceReadStatus.Readable,
            [
                new(IntakeEvidenceSource.EmailBody, "uploaded mail.eml, message body", "Please see the attached report."),
                new(IntakeEvidenceSource.PdfContent, "uploaded mail.eml, attachment 1: letter.pdf, page 1", "The letter", IntakeSourceLocator.ForPage(1)),
                new(IntakeEvidenceSource.PdfContent, "uploaded mail.eml, attachment 2: report.pdf, page 1", "Readable cover", IntakeSourceLocator.ForPage(1)),
                new(IntakeEvidenceSource.PdfContent, "uploaded mail.eml, attachment 2: report.pdf, page 2", "x", IntakeSourceLocator.ForPage(2))
            ],
            [],
            [],
            true,
            OcrCandidates:
            [
                new("uploaded mail.eml, attachment 2: report.pdf", 2),
                new("uploaded mail.eml, attachment 2: report.pdf", 3),
                new("uploaded mail.eml, attachment 3: invoice.pdf", 1)
            ],
            ReaderKey: "mimekit_pdfpig_openxml",
            ReaderVersion: "r");

        var merged = IntakeOcrText.Merge(
            ordinary,
            "uploaded mail.eml, attachment 2: report.pdf",
            SourceHash,
            [2, 3],
            Result(new IntakeOcrPage(2, "OCR page two", [], []), new IntakeOcrPage(3, "OCR page three", [], [])));

        // Everything that is not the OCR'd document's qualified pages survives.
        Assert.Contains(merged.Content, fragment => fragment.Text == "Please see the attached report.");
        Assert.Contains(merged.Content, fragment => fragment.Text == "The letter");
        Assert.Contains(merged.Content, fragment => fragment.Text == "Readable cover");
        // The stale embedded text of a qualified page goes; its OCR text comes.
        Assert.DoesNotContain(merged.Content, fragment => fragment.Text == "x");
        Assert.Contains(merged.Content, fragment =>
            fragment.Text == "OCR page two"
            && fragment.SourceLabel == "uploaded mail.eml, attachment 2: report.pdf, page 2"
            && IntakeOcrText.IsOcrLocator(fragment.Locator));
        Assert.Contains(merged.Content, fragment => fragment.Text == "OCR page three");
        // The other document's scanned page still awaits its own OCR.
        var remaining = Assert.Single(merged.ScannedPdfPages);
        Assert.Equal("uploaded mail.eml, attachment 3: invoice.pdf", remaining.SourceLabel);
        Assert.True(merged.RequiresOcr);
        // The whole-source provenance stays the reader's.
        Assert.Equal("mimekit_pdfpig_openxml", merged.ReaderKey);
    }

    [Fact]
    public void MergeOfTheOnlyScannedDocumentLeavesNothingAwaitingOcr()
    {
        var ordinary = new IntakeSourceReadResult(
            IntakeSourceReadStatus.Readable,
            [],
            [],
            [],
            true,
            OcrCandidates: [new("uploaded scan.pdf", 1)]);

        var merged = IntakeOcrText.Merge(ordinary, "uploaded scan.pdf", SourceHash, [1], Result(new IntakeOcrPage(1, "Text", [], [])));

        Assert.False(merged.RequiresOcr);
        Assert.Empty(merged.ScannedPdfPages);
        Assert.Single(merged.Content);
    }

    [Fact]
    public void AResultWithoutItsResponseHashIsRefused()
    {
        var result = new IntakeOcrResult(
            IntakeOcrState.Completed,
            "azure-document-intelligence",
            "prebuilt-layout",
            "2024-11-30",
            Pages: [new(1, "Text", [], [])]);

        Assert.Throws<ArgumentException>(() => IntakeOcrText.ReadResult("uploaded scan.pdf", SourceHash, result));
    }

    private static IntakeOcrResult Result(params IntakeOcrPage[] pages) =>
        new(
            IntakeOcrState.Completed,
            "azure-document-intelligence",
            "prebuilt-layout",
            "2024-11-30",
            ProviderOperationId: "op-1",
            ResponseSha256: ResponseHash,
            Pages: pages);
}
