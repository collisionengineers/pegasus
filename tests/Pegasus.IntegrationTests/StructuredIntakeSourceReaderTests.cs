using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;
using Pegasus.IntegrationTests.DocumentExtraction;
using SkiaSharp;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The real MimeKit/PdfPig/Open XML reader against synthesized fixtures, on the
/// structure it is now required to report.
///
/// The fixtures are structural and non-domain: an e-mail forwarded once, and a
/// Word document laid out as a labelled table. No genuine correspondence is
/// embedded here — the genuine local corpus is exercised by
/// <c>MultiFormatGenuineCorpusWebTests</c>, which reads the originals in place.
/// </summary>
public sealed class StructuredIntakeSourceReaderTests
{
    private static readonly DateTimeOffset Received = new(2026, 5, 4, 8, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void FullPageRasterCoverageUsesRotatedVisibleCoordinates(int rotation)
    {
        // Geometry only: no fabricated instruction or raster document. PdfPig
        // offsets the crop origin and rotates content into this display space.
        var visible = new CropBox(new PdfRectangle(20, 30, 220, 130))
            .GetVisibleBounds(new PageRotationDegrees(rotation));
        var image = rotation switch
        {
            90 => new PdfRectangle(visible.TopRight, visible.BottomRight, visible.TopLeft, visible.BottomLeft),
            180 => new PdfRectangle(visible.BottomRight, visible.BottomLeft, visible.TopRight, visible.TopLeft),
            270 => new PdfRectangle(visible.BottomLeft, visible.TopLeft, visible.BottomRight, visible.TopRight),
            _ => visible
        };
        Assert.Equal(1, MimeKitPdfPigOpenXmlIntakeSourceReader.Coverage(image, visible), 10);
    }

    [Theory]
    [InlineData(80, 0.2)]
    [InlineData(100, 0)]
    [InlineData(120, 0)]
    public void RasterCoverageClipsImagesOutsideTheVisibleCrop(double left, double expected)
    {
        var visible = new CropBox(new PdfRectangle(20, 30, 220, 130))
            .GetVisibleBounds(new PageRotationDegrees(270));
        var image = new PdfRectangle(left, 0, left + 100, 200);
        Assert.Equal(expected, MimeKitPdfPigOpenXmlIntakeSourceReader.Coverage(image, visible), 10);
    }

    /// <summary>
    /// Operator, 12 October 2026: an image budget stops images only. Page one
    /// places an image whose declared samples exceed the pixel budget on their
    /// own; every page's text is still read, no later image is extracted, and
    /// the read is incomplete with one limit issue.
    /// </summary>
    [Fact]
    public async Task APdfPixelBudgetTripKeepsTheTextOfEveryPage()
    {
        var result = await ReadAsync(new TestEmail(
            "report.pdf",
            "application/pdf",
            BuildTwoPagePdfOverThePixelBudget("First page of the report", "Second page of the report")));

        Assert.Equal(IntakeSourceReadStatus.Readable, result.Status);
        Assert.True(result.IsIncomplete);
        var limit = Assert.Single(result.Issues, issue => issue.Code == "intake_limit_exceeded");
        Assert.Equal("uploaded report.pdf expands beyond the safe decoded-image pixel limit.", limit.Reason);
        Assert.Contains(result.Content, fragment =>
            fragment.SourceLabel == "uploaded report.pdf, page 1"
            && fragment.Text.Contains("First page of the report", StringComparison.Ordinal));
        Assert.Contains(result.Content, fragment =>
            fragment.SourceLabel == "uploaded report.pdf, page 2"
            && fragment.Text.Contains("Second page of the report", StringComparison.Ordinal));
        Assert.Empty(result.AssetCandidates);
    }

    /// <summary>
    /// Issue #989: a photograph filling a page whose image PdfPig hands over as
    /// neither a JPEG nor a PNG is read by the third decode path, kept as an
    /// image and not sent to OCR.
    /// </summary>
    [Theory]
    [InlineData(FallbackJpeg, "image/jpeg")]
    [InlineData(FallbackPng, "image/png")]
    public async Task AFullPagePhotographOnlyTheFallbackCanDecodeIsKeptAndNotSentToOcr(
        string fallback,
        string mediaType)
    {
        var result = await ReadAsync(new TestEmail(
            "photographs.pdf",
            "application/pdf",
            BuildPdf(new PdfFixturePage(null, FallbackOnlyImage(fallback, PhotographPixels(12), FullPage)))));

        var photograph = Assert.Single(result.AssetCandidates);
        Assert.Equal(IntakeAssetKind.EmbeddedImage, photograph.Kind);
        Assert.Equal(mediaType, photograph.MediaType);
        Assert.Equal(PhotoWidth, photograph.WidthPixels);
        Assert.Equal(PhotoHeight, photograph.HeightPixels);
        using var decoded = SKBitmap.Decode(photograph.Content.Span);
        Assert.NotNull(decoded);
        Assert.Equal(PhotoWidth, decoded.Width);
        Assert.Equal(PhotoHeight, decoded.Height);
        Assert.True(photograph.Content.Length >= InstructionEvidenceImages.EmbeddedPhotographMinimumBytes);
        Assert.Empty(result.ScannedPdfPages);
        Assert.False(result.RequiresOcr);
        Assert.Contains(result.Issues, issue => issue.Code == "scan-page-photograph");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "pdf-image-decode-failure");
    }

    /// <summary>
    /// The same page in paper white is a scanned document page: the recovered
    /// raster is measured, the page goes to OCR and keeps no image.
    /// </summary>
    [Theory]
    [InlineData(FallbackJpeg)]
    [InlineData(FallbackPng)]
    public async Task AScannedPageOnlyTheFallbackCanDecodeStillGoesToOcrWithAMeasurement(string fallback)
    {
        var result = await ReadAsync(new TestEmail(
            "scan.pdf",
            "application/pdf",
            BuildPdf(new PdfFixturePage(null, FallbackOnlyImage(fallback, PaperWhitePixels(), FullPage)))));

        var candidate = Assert.Single(result.ScannedPdfPages);
        Assert.Equal(1, candidate.PageNumber);
        Assert.Empty(result.AssetCandidates);
        var verdict = Assert.Single(result.Issues, issue => issue.Code == "scan-page-document");
        Assert.Contains("(near-white ", verdict.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// A full-page raster the third path cannot read either keeps today's
    /// path: a document page, unmeasured, that goes to OCR (ADR-0061).
    /// </summary>
    [Fact]
    public async Task AFullPageRasterTheFallbackAlsoRefusesStaysADocumentPage()
    {
        var result = await ReadAsync(new TestEmail(
            "scan.pdf",
            "application/pdf",
            BuildPdf(new PdfFixturePage(null, UnreadableImage(FullPage)))));

        var candidate = Assert.Single(result.ScannedPdfPages);
        Assert.Equal(1, candidate.PageNumber);
        Assert.Empty(result.AssetCandidates);
        var verdict = Assert.Single(result.Issues, issue => issue.Code == "scan-page-document");
        Assert.Equal(
            "uploaded scan.pdf, page 1 is a full-page raster read as a scanned document page "
            + "(raster not decodable, read as a document page); it keeps no image and goes to OCR.",
            verdict.Reason);
    }

    /// <summary>
    /// On a page with readable text each image stands alone: the one the third
    /// path reads is kept, the one it cannot is named in its own issue.
    /// </summary>
    [Fact]
    public async Task OnAReadablePageTheFallbackKeepsWhatItRecoversAndReportsWhatItCannot()
    {
        var result = await ReadAsync(new TestEmail(
            "letter.pdf",
            "application/pdf",
            BuildPdf(new PdfFixturePage(
                "This instruction letter carries a photograph of the damage and a second picture beneath its text.",
                FallbackOnlyImage(FallbackJpeg, PhotographPixels(13), "100 0 0 100 20 20"),
                UnreadableImage("100 0 0 100 200 20")))));

        Assert.Empty(result.ScannedPdfPages);
        var recovered = Assert.Single(result.AssetCandidates);
        Assert.Equal("page-1-image-1.jpg", recovered.FileName);
        var failure = Assert.Single(result.Issues, issue => issue.Code == "pdf-image-decode-failure");
        Assert.Equal(
            "uploaded letter.pdf, page 1, image 2 could not be converted to a reviewable image stream.",
            failure.Reason);
    }

    private const string ForwardedBody =
        "Please see the instruction below.\r\n"
        + "\r\n"
        + "Regards\r\n"
        + "The forwarding desk\r\n"
        + "From: Original Sender <original@a-work-provider.example>\r\n"
        + "Sent: 01 May 2026 09:00\r\n"
        + "To: desk@collisionengineers.co.uk\r\n"
        + "Subject: Assessment instruction\r\n"
        + "\r\n"
        + "Claim Number: CLM-9001\r\n"
        + "Registration: AB12 CDE\r\n";

    [Fact]
    public async Task TheOuterSenderTheCurrentBodyAndTheQuotedHistoryStayThreeSeparateThings()
    {
        var email = IntakeTestEvidence.CreateEmail(
            "forwarded-instruction.eml",
            ForwardedBody,
            senderAddress: "desk@collisionengineers.co.uk",
            subject: "FW: Assessment instruction");

        var result = await ReadAsync(email);

        Assert.Equal(IntakeSourceReadStatus.Readable, result.Status);

        // The transport envelope: who actually sent this message here.
        var transport = Assert.Single(
            result.TransportEvidence,
            evidence => evidence.Source == IntakeEvidenceSource.Sender
                && evidence.SenderIdentityKind == IntakeSenderIdentityKind.Transport);
        Assert.Equal("desk@collisionengineers.co.uk", transport.Value);

        // The original sender the forwarded header names is separate evidence,
        // and is not confused with the desk that forwarded it.
        var inlineOriginal = Assert.Single(
            result.TransportEvidence,
            evidence => evidence.SenderIdentityKind == IntakeSenderIdentityKind.InlineForwardedOriginal);
        Assert.Equal("original@a-work-provider.example", inlineOriginal.Value);

        var bodies = result.Content
            .Where(fragment => fragment.Source == IntakeEvidenceSource.EmailBody)
            .ToArray();
        Assert.Equal(2, bodies.Length);

        // The retained body is first and whole - it is the evidence - and says
        // where the message this sender wrote ends.
        var currentBody = bodies[0].Locator!;
        Assert.Equal(IntakeMessagePart.CurrentBody, currentBody.MessagePart);
        Assert.StartsWith("chars 0-", currentBody.Region!, StringComparison.Ordinal);
        Assert.Contains("Please see the instruction below.", bodies[0].Text, StringComparison.Ordinal);

        // The history quoted beneath it is its own fragment, and carries only
        // the quoted part.
        Assert.Equal(IntakeMessagePart.QuotedHistory, bodies[1].Locator!.MessagePart);
        Assert.EndsWith("quoted history", bodies[1].SourceLabel, StringComparison.Ordinal);
        Assert.StartsWith("From: Original Sender", bodies[1].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("The forwarding desk", bodies[1].Text, StringComparison.Ordinal);
        Assert.Contains("Claim Number: CLM-9001", bodies[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMessageWithNothingQuotedBeneathItHasOneBodyAndNoRegionToBound()
    {
        var email = IntakeTestEvidence.CreateEmail(
            "direct-instruction.eml",
            "Claim Number: CLM-9002\r\nRegistration: AB12 CDE\r\n");

        var result = await ReadAsync(email);

        var body = Assert.Single(
            result.Content,
            fragment => fragment.Source == IntakeEvidenceSource.EmailBody);
        Assert.Equal(IntakeMessagePart.CurrentBody, body.Locator!.MessagePart);
        Assert.Null(body.Locator.Region);
    }

    [Fact]
    public async Task AnAttachmentKeepsItsOwnIdentityAndIsNotFoldedIntoTheBody()
    {
        var attachment = BuildLabelledTableDocx();
        var email = IntakeTestEvidence.CreateEmail(
            "instruction-with-attachment.eml",
            "The instruction is attached.\r\n",
            attachments:
            [
                ("instruction.docx",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    attachment)
            ]);

        var result = await ReadAsync(email);

        var descriptor = Assert.Single(result.AttachmentRecords);
        Assert.Equal("instruction.docx", descriptor.FileName);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            descriptor.MediaType);

        // The attachment's cells are read as the attachment's, labelled with the
        // file they came from, and not merged into the covering e-mail's body.
        var cells = result.Content
            .Where(fragment => fragment.Locator?.Kind == IntakeLocatorKind.TableCell)
            .ToArray();
        Assert.NotEmpty(cells);
        Assert.All(cells, cell =>
            Assert.Contains("instruction.docx", cell.SourceLabel, StringComparison.Ordinal));
        Assert.All(
            result.Content.Where(fragment => fragment.Source == IntakeEvidenceSource.EmailBody),
            body => Assert.DoesNotContain("CLM-9003", body.Text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ALabelledTableIsReportedCellByCellBesideItsFlattenedText()
    {
        var result = await ReadAsync(new TestEmail(
            "instruction.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            BuildLabelledTableDocx()));

        Assert.Equal(IntakeSourceReadStatus.Readable, result.Status);

        // The flattened paragraph text is still there, unchanged in kind: the
        // cells are an addition, not a replacement.
        var flattened = Assert.Single(result.Content, fragment => fragment.Locator is null);
        Assert.Contains("CLM-9003", flattened.Text, StringComparison.Ordinal);

        var cells = result.Content
            .Where(fragment => fragment.Locator?.Kind == IntakeLocatorKind.TableCell)
            .ToArray();
        Assert.Equal(
            ["T1R1C1", "T1R1C2", "T1R2C1", "T1R2C2"],
            cells.Select(cell => cell.Locator!.Cell));
        Assert.Equal(
            ["Claim Number", "Registration", "CLM-9003", "AB12 CDE"],
            cells.Select(cell => cell.Text));
        Assert.All(cells, cell => Assert.Equal(
            $"instruction.docx, table {cell.Locator!.Table} row {cell.Locator.Row} column {cell.Locator.Column}",
            cell.SourceLabel.Replace("uploaded ", string.Empty, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task TwoTablesAreCountedSeparatelySoACellNamesExactlyOnePlace()
    {
        var result = await ReadAsync(new TestEmail(
            "two-tables.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            BuildTwoTableDocx()));

        var cells = result.Content
            .Where(fragment => fragment.Locator?.Kind == IntakeLocatorKind.TableCell)
            .ToArray();
        Assert.Equal(["T1R1C1", "T2R1C1"], cells.Select(cell => cell.Locator!.Cell));
        Assert.Equal(["First table", "Second table"], cells.Select(cell => cell.Text));
    }

    /// <summary>
    /// The legacy binary Word branch reports a table the way the RTF and Open
    /// XML branches already do: the flattened text unchanged, and the cells
    /// beside it. A paired label/value layout only keeps its party while the
    /// value stays attached to the column it was printed in.
    /// </summary>
    [Fact]
    public async Task ALegacyWordTableIsReportedCellByCellBesideItsFlattenedText()
    {
        const string document =
            "Assessment instruction\rClaim Number\u0007CLM-9004\u0007\u0007Registration\u0007AB12 CDE\u0007\u0007";
        var result = await ReadAsync(new TestEmail(
            "legacy-instruction.doc",
            "application/msword",
            WordBinaryFixture.CreateRawCfb([new(0, (uint)document.Length, 700, false, document)])));

        Assert.Equal(IntakeSourceReadStatus.Readable, result.Status);
        Assert.False(result.IsIncomplete);

        var flattened = Assert.Single(result.Content, fragment => fragment.Locator is null);
        Assert.Contains("CLM-9004", flattened.Text, StringComparison.Ordinal);

        var cells = result.Content
            .Where(fragment => fragment.Locator?.Kind == IntakeLocatorKind.TableCell)
            .ToArray();
        Assert.Equal(
            ["T1R1C1", "T1R1C2", "T1R2C1", "T1R2C2"],
            cells.Select(cell => cell.Locator!.Cell));
        Assert.Equal(
            ["Claim Number", "CLM-9004", "Registration", "AB12 CDE"],
            cells.Select(cell => cell.Text));
        Assert.All(cells, cell => Assert.Equal(
            $"legacy-instruction.doc, table 1 row {cell.Locator!.Row} column {cell.Locator.Column}",
            cell.SourceLabel.Replace("uploaded ", string.Empty, StringComparison.Ordinal)));
    }

    /// <summary>
    /// A running letterhead is decoded and kept, and is not the body. Merged
    /// into it, the supplier printed in the header reads as the instructing
    /// party's address.
    /// </summary>
    [Fact]
    public async Task ALegacyWordHeaderIsItsOwnLabelledFragmentAndNeverJoinsTheBody()
    {
        const string body = "Claim Number: CLM-9005\r";
        const string header = "Collision Engineers, 1 Depot Road\r";
        var document = body + header + "\r";
        var result = await ReadAsync(new TestEmail(
            "letterheaded.doc",
            "application/msword",
            WordBinaryFixture.CreateRawCfb(
                [new(0, (uint)document.Length, 700, false, document)],
                [(uint)body.Length, 0, (uint)header.Length, 0, 0, 0, 0, 0])));

        Assert.Equal(IntakeSourceReadStatus.Readable, result.Status);
        Assert.False(result.IsIncomplete);

        var main = Assert.Single(result.Content, fragment => fragment.Locator is null);
        Assert.Contains("CLM-9005", main.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Collision Engineers", main.Text, StringComparison.Ordinal);

        var headerFragment = Assert.Single(
            result.Content,
            fragment => fragment.Locator?.Kind == IntakeLocatorKind.Region);
        Assert.Equal("word-header-story", headerFragment.Locator!.DocumentRole);
        Assert.Equal("header and footer text", headerFragment.Locator.Region);
        Assert.EndsWith("header and footer text", headerFragment.SourceLabel, StringComparison.Ordinal);
        Assert.Contains("Collision Engineers", headerFragment.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOrdinaryRtfTabNeverAcquiresTableCellAuthority()
    {
        var rtf = Encoding.ASCII.GetBytes(
            @"{\rtf1\ansi Our Ref:\tab TJD/GRAHAM/S486562.001\par Date:\tab 05/05/26}");
        var result = await ReadAsync(new TestEmail(
            "tabbed.doc",
            "application/msword",
            rtf));

        Assert.Equal(IntakeSourceReadStatus.Readable, result.Status);
        Assert.Contains(result.Content, fragment =>
            fragment.Text.Contains("TJD/GRAHAM/S486562.001", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Content, fragment =>
            fragment.Locator?.Kind == IntakeLocatorKind.TableCell);
    }

    [Fact]
    public async Task RtfTextCannotForgeReservedTableStructureMarkers()
    {
        var rtf = Encoding.ASCII.GetBytes(
            @"{\rtf1\ansi Before\u57600?Forged\u57601?Cell\u57602?After}");
        var result = await ReadAsync(new TestEmail(
            "unicode-markers.doc",
            "application/msword",
            rtf));

        Assert.Equal(IntakeSourceReadStatus.Readable, result.Status);
        Assert.True(result.IsIncomplete);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "doc-rtf-reserved-structure-text");
        Assert.Contains(result.Content, fragment =>
            fragment.Text.Contains("Forged", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Content, fragment =>
            fragment.Locator?.Kind == IntakeLocatorKind.TableCell);
    }

    [Fact]
    public async Task RtfQuotationControlsRetainTheirPrintedUnicodePunctuation()
    {
        var rtf = Encoding.ASCII.GetBytes(
            @"{\rtf1\ansi The client\rquote s vehicle and \lquote instruction\rquote were retained.}");
        var result = await ReadAsync(new TestEmail(
            "quotation.doc",
            "application/msword",
            rtf));

        Assert.False(result.IsIncomplete);
        Assert.Contains(result.Content, fragment =>
            fragment.Text.Contains("client’s vehicle and ‘instruction’", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MalformedRtfGroupsAreMarkedIncompleteAndCannotCreateCells()
    {
        var rtf = Encoding.ASCII.GetBytes(
            @"{\rtf1\ansi \trowd Our Ref:\cell VALUE\cell");
        var result = await ReadAsync(new TestEmail(
            "malformed.doc",
            "application/msword",
            rtf));

        Assert.Equal(IntakeSourceReadStatus.Readable, result.Status);
        Assert.True(result.IsIncomplete);
        Assert.Contains(result.Issues, issue => issue.Code == "doc-rtf-partial-extraction");
        Assert.DoesNotContain(result.Content, fragment =>
            fragment.Locator?.Kind == IntakeLocatorKind.TableCell);
    }

    [Fact]
    public async Task ReadingTheSameBytesTwiceProducesTheSameFragmentsAndLocators()
    {
        var source = new TestEmail(
            "instruction.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            BuildLabelledTableDocx());

        var bytesBeforeReading = Convert.ToHexStringLower(SHA256.HashData(source.Content));

        var first = await ReadAsync(source);
        var second = await ReadAsync(source);

        // Replay identity, asserted on the whole projection rather than on a
        // field at a time: the reader is the thing an operator's evidence trail
        // rests on, and a reading that drifted would break every locator
        // recorded before it.
        Assert.Equal(
            first.Content.Select(Fingerprint),
            second.Content.Select(Fingerprint));
        Assert.Equal(first.ReaderKey, second.ReaderKey);
        Assert.Equal(first.ReaderVersion, second.ReaderVersion);

        // The locators are anchored to these bytes, so reading twice must not
        // have consumed or altered them. What stood here compared one hash
        // expression with itself and could not fail.
        Assert.Equal(
            bytesBeforeReading,
            Convert.ToHexStringLower(SHA256.HashData(source.Content)));
    }

    [Fact]
    public async Task ACorruptDocumentIsRefusedRatherThanPartlyRead()
    {
        var result = await ReadAsync(new TestEmail(
            "corrupt.pdf",
            "application/pdf",
            [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0x00, 0x01, 0x02, 0x03]));

        Assert.Equal(IntakeSourceReadStatus.Unsupported, result.Status);
        Assert.Equal("unreadable_pdf", result.FailureCode);
        Assert.Empty(result.Content);
        // Refused WITHOUT asking for OCR: an unreadable document is not a
        // scanned one, and sending it to a provider would be a guess.
        Assert.False(result.RequiresOcr);
        Assert.Empty(result.ScannedPdfPages);
    }

    private static string Fingerprint(IntakeContentFragment fragment) =>
        string.Join(
            '|',
            fragment.Source,
            fragment.SourceLabel,
            fragment.Text,
            fragment.Locator?.Kind.ToString() ?? "-",
            fragment.Locator?.Cell ?? "-",
            fragment.Locator?.FormField ?? "-",
            fragment.Locator?.Region ?? "-",
            fragment.Locator?.MessagePart.ToString() ?? "-");

    private static Task<IntakeSourceReadResult> ReadAsync(TestEmail source) =>
        new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System).ReadAsync(
            new(
                source.FileName,
                source.MediaType,
                source.Content,
                Received,
                "staff:fixture",
                new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N"))),
            CancellationToken.None);

    private const int PhotoWidth = 210;
    private const int PhotoHeight = 297;
    private const string FullPage = "612 0 0 792 0 0";
    private const string FallbackJpeg = "wrapped-jpeg";
    private const string FallbackPng = "long-samples";

    /// <summary>
    /// A raster PdfPig hands over as neither a JPEG nor a PNG: a JPEG behind
    /// <c>/FlateDecode</c>, which has no decoded samples, or samples one row
    /// longer than the declared size, which PdfPig refuses as a PNG.
    /// </summary>
    private static PdfFixtureImage FallbackOnlyImage(string fallback, byte[] rgb, string placement) => fallback switch
    {
        FallbackJpeg => new(
            PhotoWidth,
            PhotoHeight,
            "/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter [/FlateDecode /DCTDecode]",
            Deflate(Jpeg(rgb)),
            placement),
        FallbackPng => new(
            PhotoWidth,
            PhotoHeight,
            "/ColorSpace /DeviceRGB /BitsPerComponent 8",
            [.. rgb, .. rgb[..(PhotoWidth * 3)]],
            placement),
        _ => throw new ArgumentOutOfRangeException(nameof(fallback))
    };

    /// <summary>A JPEG 2000 filter over bytes that are not JPEG 2000: nothing reads it.</summary>
    private static PdfFixtureImage UnreadableImage(string placement) => new(
        PhotoWidth,
        PhotoHeight,
        "/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /JPXDecode",
        new byte[64],
        placement);

    private static byte[] PhotographPixels(int seed)
    {
        var pixels = new byte[PhotoWidth * PhotoHeight * 3];
        new Random(seed).NextBytes(pixels);
        return pixels;
    }

    private static byte[] PaperWhitePixels() => Enumerable.Repeat((byte)0xff, PhotoWidth * PhotoHeight * 3).ToArray();

    private static byte[] Jpeg(byte[] rgb)
    {
        using var bitmap = new SKBitmap(PhotoWidth, PhotoHeight, SKColorType.Rgba8888, SKAlphaType.Opaque);
        for (var pixel = 0; pixel < PhotoWidth * PhotoHeight; pixel++)
        {
            bitmap.SetPixel(
                pixel % PhotoWidth,
                pixel / PhotoWidth,
                new SKColor(rgb[pixel * 3], rgb[(pixel * 3) + 1], rgb[(pixel * 3) + 2]));
        }

        using var jpeg = bitmap.Encode(SKEncodedImageFormat.Jpeg, 90);
        return jpeg.ToArray();
    }

    private static byte[] Deflate(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
        {
            zlib.Write(bytes);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Two text pages, each placing one image: page one an image declared at
    /// 10,001 by 10,000 samples (never decoded), page two an ordinary 2 by 2
    /// image that would otherwise be extracted.
    /// </summary>
    private static byte[] BuildTwoPagePdfOverThePixelBudget(string firstPageText, string secondPageText) =>
        BuildPdf(
            new(firstPageText, RawRgbImage(10_001, 10_000, [0xff, 0xff, 0xff])),
            new(secondPageText, RawRgbImage(2, 2, [.. Enumerable.Range(0, 12).Select(value => (byte)(value * 20))])));

    private static PdfFixtureImage RawRgbImage(int width, int height, byte[] pixels) =>
        new(width, height, "/ColorSpace /DeviceRGB /BitsPerComponent 8", pixels, "100 0 0 100 20 20");

    /// <summary>
    /// A letter-size PDF from object bodies: each page places its images
    /// (declared size, the rest of the image dictionary, the encoded stream)
    /// and, when it has any, one line of text.
    /// </summary>
    private static byte[] BuildPdf(params PdfFixturePage[] pages)
    {
        var fontObject = pages.Length + 3;
        var objectBodies = new List<byte[]>
        {
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Encoding.ASCII.GetBytes(
                $"<< /Type /Pages /Kids [{string.Join(' ', pages.Select((_, index) => $"{index + 3} 0 R"))}] "
                + $"/Count {pages.Length} >>")
        };
        var pageObjects = new List<byte[]>();
        foreach (var page in pages)
        {
            var firstImageObject = fontObject + 1 + pageObjects.Count;
            var contentObject = firstImageObject + page.Images.Length;
            var images = string.Join(
                ' ',
                page.Images.Select((_, index) => $"/Im{index + 1} {firstImageObject + index} 0 R"));
            objectBodies.Add(Encoding.ASCII.GetBytes(
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
                + $"/Resources << /XObject << {images} >> /Font << /F1 {fontObject} 0 R >> >> "
                + $"/Contents {contentObject} 0 R >>"));
            pageObjects.AddRange(page.Images.Select(ImageObject));
            pageObjects.Add(ContentObject(page));
        }

        objectBodies.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));
        objectBodies.AddRange(pageObjects);

        using var output = new MemoryStream();
        output.Write(Encoding.ASCII.GetBytes("%PDF-1.4\n"));
        var offsets = new List<long>();
        for (var index = 0; index < objectBodies.Count; index++)
        {
            offsets.Add(output.Position);
            output.Write(Encoding.ASCII.GetBytes($"{index + 1} 0 obj\n"));
            output.Write(objectBodies[index]);
            output.Write(Encoding.ASCII.GetBytes("\nendobj\n"));
        }

        var xref = output.Position;
        output.Write(Encoding.ASCII.GetBytes($"xref\n0 {objectBodies.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets)
        {
            output.Write(Encoding.ASCII.GetBytes($"{offset:0000000000} 00000 n \n"));
        }

        output.Write(Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {objectBodies.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"));
        return output.ToArray();

        static byte[] ImageObject(PdfFixtureImage image) =>
        [
            .. Encoding.ASCII.GetBytes(
                $"<< /Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} "
                + $"{image.Dictionary} /Length {image.Stream.Length} >>\nstream\n"),
            .. image.Stream,
            .. Encoding.ASCII.GetBytes("\nendstream")
        ];

        static byte[] ContentObject(PdfFixturePage page)
        {
            var operators = Encoding.ASCII.GetBytes(
                string.Concat(page.Images.Select((image, index) => $"q\n{image.Placement} cm\n/Im{index + 1} Do\nQ\n"))
                + (page.Text is null ? string.Empty : $"BT /F1 12 Tf 72 720 Td ({page.Text}) Tj ET"));
            return
            [
                .. Encoding.ASCII.GetBytes($"<< /Length {operators.Length} >>\nstream\n"),
                .. operators,
                .. Encoding.ASCII.GetBytes("\nendstream")
            ];
        }
    }

    /// <param name="Text">One line of text, or none for a page that is only its images.</param>
    private sealed record PdfFixturePage(string? Text, params PdfFixtureImage[] Images);

    /// <param name="Dictionary">The image dictionary beyond its type, size and length.</param>
    /// <param name="Placement">The matrix that places the image on the page.</param>
    private sealed record PdfFixtureImage(int Width, int Height, string Dictionary, byte[] Stream, string Placement);

    private static byte[] BuildLabelledTableDocx() => BuildDocx(
        BuildTable(["Claim Number", "Registration"], ["CLM-9003", "AB12 CDE"]));

    private static byte[] BuildTwoTableDocx() => BuildDocx(
        BuildTable(["First table"]),
        BuildTable(["Second table"]));

    private static byte[] BuildDocx(params Table[] tables)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(
            stream,
            WordprocessingDocumentType.Document,
            autoSave: true))
        {
            var body = new Body();
            body.AppendChild(new Paragraph(new Run(new Text("Assessment instruction"))));
            foreach (var table in tables)
            {
                body.AppendChild(table);
            }

            body.AppendChild(new Paragraph(new Run(new Text("End of instruction"))));
            document.AddMainDocumentPart().Document = new Document(body);
        }

        return stream.ToArray();
    }

    private static Table BuildTable(params string[][] rows)
    {
        var table = new Table();
        foreach (var row in rows)
        {
            var tableRow = new TableRow();
            foreach (var cell in row)
            {
                tableRow.AppendChild(new TableCell(new Paragraph(new Run(new Text(cell)))));
            }

            table.AppendChild(tableRow);
        }

        return table;
    }
}
