using System.Net;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Pegasus.Core.Intake;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MimeKit;
using System.Globalization;
using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms;
using UglyToad.PdfPig.AcroForms.Fields;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Pegasus.Infrastructure.Intake;

public sealed partial class MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider timeProvider) : IIntakeSourceReader
{
    private const string ReaderKey = "mimekit_pdfpig_openxml";
    private const string ReaderVersion = "mimekit-4.17.0;pdfpig-0.1.15;openxml-3.5.1;collisiondocnet-doc-msg-0.1";
    private const int MinimumReadablePdfCharacters = 80;
    private const double ScannedPageImageCoverage = 0.8;
    private const int MaximumPdfTextCharacters = 5 * 1024 * 1024;
    private const int MaximumPdfImageObjects = 512;
    private const long MaximumPdfImageSamplePixels = 100_000_000;
    private const long MaximumPdfExtractedImageBytes = 25L * 1024 * 1024;
    private static readonly TimeSpan MaximumPdfProcessingDuration = TimeSpan.FromSeconds(30);
    private const int MaximumNestedEmailDepth = 8;
    private const int MaximumMimeEntities = 128;
    private const long MaximumDecodedMimeBytes = 25L * 1024 * 1024;
    private const int MaximumDocxPackageEntries = 512;
    private const int MaximumDocxTableCells = 4096;
    private const int MaximumPdfFormFields = 1024;
    private const long MaximumDocxUncompressedBytes = 50L * 1024 * 1024;
    private const long MaximumDocxXmlPartBytes = 10L * 1024 * 1024;
    private const long MaximumDocxImageBytes = 25L * 1024 * 1024;
    private const string StaffTransportDomain = "collisionengineers.co.uk";

    public async Task<IntakeSourceReadResult> ReadAsync(
        IntakeSource source,
        CancellationToken cancellationToken)
    {
        var result = new ReadAccumulator(
            [
                new(IntakeEvidenceSource.FileName, source.FileName),
                new(IntakeEvidenceSource.MimeType, source.MediaType)
            ],
            timeProvider);

        var outcome = await DispatchAsync(
            source.Content,
            source.FileName,
            source.MediaType,
            SourceLabel(source.FileName),
            result,
            cancellationToken,
            isRoot: true);

        return outcome switch
        {
            ReadOutcome.Unsupported => result.ToResult(
                IntakeSourceReadStatus.Unsupported,
                result.FailureCode ?? "unsupported_file_type",
                result.FailureReason ?? "This file type is not supported by this intake path."),
            ReadOutcome.TechnicalFailure => result.ToResult(
                IntakeSourceReadStatus.TechnicalFailure,
                result.FailureCode ?? "source_read_failure",
                result.FailureReason ?? "The source could not be read because of a technical failure."),
            _ => result.ToResult(IntakeSourceReadStatus.Readable)
        };
    }

    private static async Task<ReadOutcome> DispatchAsync(
        ReadOnlyMemory<byte> bytes,
        string fileName,
        string mediaType,
        string sourceLabel,
        ReadAccumulator result,
        CancellationToken cancellationToken,
        bool isRoot = false,
        IntakeSenderIdentityKind? emailSenderIdentityKind = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var format = DetectFormat(fileName, mediaType);
        switch (format)
        {
            case SourceFormat.Pdf:
                return ReadPdf(bytes, sourceLabel, result, isRoot, cancellationToken);
            case SourceFormat.Email:
                return await ReadEmailAsync(
                    bytes,
                    sourceLabel,
                    result,
                    emailSenderIdentityKind
                        ?? (isRoot ? IntakeSenderIdentityKind.Transport : null),
                    isRoot,
                    cancellationToken);
            case SourceFormat.Docx:
                return ReadDocx(bytes, sourceLabel, result, isRoot);
            case SourceFormat.Image:
                if (isRoot)
                {
                    result.Issues.Add(new(
                        "image-review-required",
                        "The image is retained for operator review.",
                        IntakeEvidenceSource.ImageContent));
                }

                return ReadOutcome.Readable;
            case SourceFormat.Doc:
                return ReadDoc(bytes, sourceLabel, result, cancellationToken);
            case SourceFormat.Msg:
                return await ReadMsgAsync(
                    bytes,
                    sourceLabel,
                    result,
                    emailSenderIdentityKind
                        ?? (isRoot ? IntakeSenderIdentityKind.Transport : null),
                    cancellationToken);
            default:
                if (isRoot)
                {
                    result.FailureCode = "unsupported_file_type";
                    result.FailureReason = "Supported sources are .eml, .msg, .pdf, .doc, .docx, .jpg, .jpeg, .png.";
                    return ReadOutcome.Unsupported;
                }

                return ReadOutcome.Readable;
        }
    }

    private static ReadOutcome ReadPdf(
        ReadOnlyMemory<byte> bytes,
        string sourceLabel,
        ReadAccumulator result,
        bool isRoot,
        CancellationToken cancellationToken)
    {
        PdfResult pdf;
        try
        {
            pdf = ExtractPdf(bytes, sourceLabel, result, result.PdfLimits, cancellationToken);
        }
        catch (PdfLimitExceededException exception)
        {
            result.IsIncomplete = true;
            result.Issues.Add(new(
                "intake_limit_exceeded",
                $"{sourceLabel} {exception.Message}",
                IntakeEvidenceSource.PdfContent));
            return ReadOutcome.Readable;
        }
        catch (Exception exception) when (
            exception is PdfDocumentFormatException
                or PdfDocumentStackDepthException
                or PdfDocumentEncryptedException)
        {
            if (isRoot)
            {
                result.FailureCode = "unreadable_pdf";
                result.FailureReason = "The PDF is corrupt, encrypted, or otherwise unreadable.";
                return ReadOutcome.Unsupported;
            }

            result.Issues.Add(new(
                "unreadable-pdf-attachment",
                $"{sourceLabel} is corrupt, encrypted, or otherwise unreadable.",
                IntakeEvidenceSource.PdfContent));
            return ReadOutcome.Readable;
        }

        result.Issues.Add(new(
            "pdf-engine",
            $"{sourceLabel} embedded text and discrete images were read.",
            IntakeEvidenceSource.PdfContent));

        foreach (var page in pdf.Pages)
        {
            if (!string.IsNullOrWhiteSpace(page.Text))
            {
                result.Content.Add(new(
                    IntakeEvidenceSource.PdfContent,
                    $"{sourceLabel}, page {page.Number}",
                    page.Text,
                    IntakeSourceLocator.ForPage(page.Number)));
            }

            if (page.RequiresOcr)
            {
                result.OcrCandidates.Add(new(sourceLabel, page.Number));
                result.Issues.Add(new(
                    "scanned-pdf-page",
                    $"{sourceLabel}, page {page.Number} has little embedded text and a dominant raster image, so that page requires text review.",
                    IntakeEvidenceSource.PdfContent));
            }
            else if (page.HasInsufficientText)
            {
                result.Issues.Add(new(
                    "insufficient-embedded-text",
                    $"{sourceLabel}, page {page.Number} has little embedded text but is not an image-led scanned page; it requires operator review.",
                    IntakeEvidenceSource.PdfContent));
            }
        }

        // A filled form field is the one place a PDF states a field's identity
        // itself. Each becomes its own fragment so extraction binds by the
        // field's name rather than by whatever text happens to be printed beside
        // it once the page is flattened.
        foreach (var field in pdf.FormFields)
        {
            result.Content.Add(new(
                IntakeEvidenceSource.PdfContent,
                $"{sourceLabel}, form field {field.Name}",
                field.Value,
                IntakeSourceLocator.ForFormField(field.Name, field.Page, field.Region)));
        }

        return ReadOutcome.Readable;
    }

    /// <summary>
    /// The filled text-bearing form fields of a PDF, in the order the form
    /// declares them. A field with no name has no identity to bind to and is
    /// skipped rather than numbered into one.
    /// </summary>
    private static List<PdfFormFieldResult> ExtractPdfFormFields(
        PdfDocument document,
        string sourceLabel,
        ReadAccumulator result)
    {
        var fields = new List<PdfFormFieldResult>();
        AcroForm? form;
        try
        {
            if (!document.TryGetForm(out form) || form is null)
            {
                return fields;
            }
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            result.Issues.Add(new(
                "pdf-form-read-failure",
                $"{sourceLabel} declares an interactive form that could not be read.",
                IntakeEvidenceSource.PdfContent));
            return fields;
        }

        foreach (var field in form.GetFields())
        {
            if (fields.Count >= MaximumPdfFormFields)
            {
                result.IsIncomplete = true;
                result.Issues.Add(new(
                    "intake_limit_exceeded",
                    $"{sourceLabel} declares more than {MaximumPdfFormFields} form fields; the rest were not read.",
                    IntakeEvidenceSource.PdfContent));
                break;
            }

            var name = field.Information.PartialName ?? field.Information.AlternateName;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            // Text fields only. A checkbox or a radio group states a choice, not
            // a printed value, and deciding what a tick MEANS is a provider
            // policy question rather than a reader one.
            if (field is not AcroTextField { Value: { } value } || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            fields.Add(new(
                name.Trim(),
                value,
                field.PageNumber,
                field.Bounds is { } bounds
                    ? string.Create(
                        CultureInfo.InvariantCulture,
                        $"{bounds.Left:F2},{bounds.Bottom:F2},{bounds.Right:F2},{bounds.Top:F2}")
                    : null));
        }

        return fields;
    }

    private static PdfResult ExtractPdf(
        ReadOnlyMemory<byte> bytes,
        string sourceLabel,
        ReadAccumulator result,
        PdfLimitState limits,
        CancellationToken cancellationToken)
    {
        limits.ThrowIfProcessingDeadlineExceeded();
        cancellationToken.ThrowIfCancellationRequested();
        using var document = PdfDocument.Open(bytes.ToArray());
        var pages = new List<PdfPageResult>();
        for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
        {
            limits.ThrowIfProcessingDeadlineExceeded();
            cancellationToken.ThrowIfCancellationRequested();
            var page = document.GetPage(pageNumber);
            limits.ThrowIfProcessingDeadlineExceeded();
            var text = ContentOrderTextExtractor.GetText(page);
            limits.ThrowIfProcessingDeadlineExceeded();
            limits.AddTextCharacters(text.Length);
            var readableCharacters = text.Count(character => !char.IsWhiteSpace(character));
            IPdfImage[] images;
            try
            {
                images = page.GetImages().Take(limits.RemainingImageObjects + 1).ToArray();
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                images = [];
                result.Issues.Add(new(
                    "pdf-image-read-failure",
                    $"{sourceLabel}, page {page.Number} contains an image stream that could not be read.",
                    IntakeEvidenceSource.ImageContent));
            }

            limits.AddImageObjects(images.Length);

            var hasDominantRaster = images.Any(image => Coverage(image, page) >= ScannedPageImageCoverage);
            var hasInsufficientText = readableCharacters < MinimumReadablePdfCharacters;

            var imageNumber = 0;
            foreach (var image in images)
            {
                limits.ThrowIfProcessingDeadlineExceeded();
                cancellationToken.ThrowIfCancellationRequested();
                imageNumber++;
                limits.AddImageSamplePixels(image.WidthInSamples, image.HeightInSamples);
                var extracted = false;
                ReadOnlyMemory<byte> imageBytes = default;
                var mediaType = string.Empty;
                var extension = string.Empty;
                try
                {
                    extracted = TryReadPdfImage(image, out imageBytes, out mediaType, out extension);
                }
                catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
                {
                    // Keep processing the page. The issue below makes the missing image explicit.
                }

                if (!extracted)
                {
                    result.Issues.Add(new(
                        "pdf-image-decode-failure",
                        $"{sourceLabel}, page {page.Number}, image {imageNumber} could not be converted to a reviewable image stream.",
                        IntakeEvidenceSource.ImageContent));
                    continue;
                }

                limits.AddExtractedImageBytes(imageBytes.Length);

                result.Assets.Add(new(
                    $"{sourceLabel}, page {page.Number}, image {imageNumber}",
                    $"page-{page.Number}-image-{imageNumber}{extension}",
                    mediaType,
                    imageBytes,
                    IntakeAssetKind.EmbeddedImage,
                    IntakeAssetDisposition.Embedded,
                    page.Number,
                    new(
                        image.BoundingBox.Left,
                        image.BoundingBox.Bottom,
                        image.BoundingBox.Right,
                        image.BoundingBox.Top),
                    image.WidthInSamples,
                    image.HeightInSamples));
            }

            pages.Add(new(
                page.Number,
                readableCharacters == 0 ? null : text,
                hasInsufficientText,
                hasInsufficientText && hasDominantRaster));
            limits.ThrowIfProcessingDeadlineExceeded();
        }

        return new(pages, ExtractPdfFormFields(document, sourceLabel, result));
    }

    private static double Coverage(IPdfImage image, Page page)
    {
        var visiblePage = page.CropBox.Bounds;
        var pageArea = Math.Abs(visiblePage.Width * visiblePage.Height);
        if (pageArea <= 0)
        {
            return 0;
        }

        var left = Math.Max(image.BoundingBox.Left, visiblePage.Left);
        var right = Math.Min(image.BoundingBox.Right, visiblePage.Right);
        var bottom = Math.Max(image.BoundingBox.Bottom, visiblePage.Bottom);
        var top = Math.Min(image.BoundingBox.Top, visiblePage.Top);
        var imageArea = Math.Max(0, right - left) * Math.Max(0, top - bottom);
        return imageArea / pageArea;
    }

    private static bool TryReadPdfImage(
        IPdfImage image,
        out ReadOnlyMemory<byte> bytes,
        out string mediaType,
        out string extension)
    {
        var raw = image.RawBytes.ToArray();
        if (raw.Length >= 2 && raw[0] == 0xff && raw[1] == 0xd8)
        {
            bytes = raw;
            mediaType = "image/jpeg";
            extension = ".jpg";
            return true;
        }

        if (image.TryGetPng(out var png))
        {
            bytes = png;
            mediaType = "image/png";
            extension = ".png";
            return true;
        }

        bytes = ReadOnlyMemory<byte>.Empty;
        mediaType = string.Empty;
        extension = string.Empty;
        return false;
    }

    private static ReadOutcome ReadDocx(
        ReadOnlyMemory<byte> bytes,
        string sourceLabel,
        ReadAccumulator result,
        bool isRoot)
    {
        try
        {
            using var stream = new MemoryStream(bytes.ToArray(), writable: false);
            ValidateDocxPackage(stream);
            using var document = WordprocessingDocument.Open(
                stream,
                false,
                new OpenSettings
                {
                    AutoSave = false,
                    MaxCharactersInPart = MaximumDocxXmlPartBytes
                });
            var mainPart = document.MainDocumentPart;
            if (mainPart?.Document is null)
            {
                throw new FileFormatException("The DOCX does not contain a main document part.");
            }

            var contentRoots = new List<DocxContentRoot> { new(mainPart, mainPart.Document) };
            contentRoots.AddRange(
                mainPart.HeaderParts
                    .Where(part => part.Header is not null)
                    .Select(part => new DocxContentRoot(part, part.Header!)));
            contentRoots.AddRange(
                mainPart.FooterParts
                    .Where(part => part.Footer is not null)
                    .Select(part => new DocxContentRoot(part, part.Footer!)));
            if (mainPart.FootnotesPart?.Footnotes is not null)
            {
                contentRoots.Add(new(mainPart.FootnotesPart, mainPart.FootnotesPart.Footnotes));
            }

            if (mainPart.EndnotesPart?.Endnotes is not null)
            {
                contentRoots.Add(new(mainPart.EndnotesPart, mainPart.EndnotesPart.Endnotes));
            }

            var text = string.Join(
                Environment.NewLine,
                contentRoots
                    .SelectMany(contentRoot => contentRoot.Root
                        .Descendants<Paragraph>()
                        .Select(paragraph => string.Concat(paragraph.Descendants<Text>().Select(item => item.Text))))
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.Content.Add(new(IntakeEvidenceSource.DocumentContent, sourceLabel, text));
            }

            AddDocxTableCells(contentRoots, sourceLabel, result);

            long totalImageBytes = 0;
            var imageContentByPart = new Dictionary<Uri, byte[]>();
            foreach (var placement in EnumerateImagePlacements(contentRoots))
            {
                var imagePart = placement.Part;
                if (!imageContentByPart.TryGetValue(imagePart.Uri, out var content))
                {
                    using var imageStream = imagePart.GetStream(FileMode.Open, FileAccess.Read);
                    if (imageStream.CanSeek
                        && imageStream.Length > MaximumDocxImageBytes - totalImageBytes)
                    {
                        throw new DocxLimitExceededException();
                    }

                    using var imageBytes = new MemoryStream();
                    imageStream.CopyTo(imageBytes);
                    totalImageBytes += imageBytes.Length;
                    if (totalImageBytes > MaximumDocxImageBytes)
                    {
                        throw new DocxLimitExceededException();
                    }

                    content = imageBytes.ToArray();
                    imageContentByPart.Add(imagePart.Uri, content);
                }

                var fileName = Path.GetFileName(imagePart.Uri.OriginalString);
                result.Assets.Add(new(
                    $"{sourceLabel}, embedded image {placement.Number}",
                    string.IsNullOrWhiteSpace(fileName) ? $"embedded-image-{placement.Number}" : fileName,
                    imagePart.ContentType,
                    content,
                    IntakeAssetKind.EmbeddedImage,
                    IntakeAssetDisposition.Embedded));
            }

            result.Issues.Add(new(
                "openxml-engine",
                $"{sourceLabel} text and internal images were read; external links were not opened.",
                IntakeEvidenceSource.DocumentContent));
            return ReadOutcome.Readable;
        }
        catch (DocxLimitExceededException)
        {
            if (isRoot)
            {
                result.FailureCode = "docx_limit_exceeded";
                result.FailureReason = "The DOCX exceeds the safe package, part, or extracted-image processing limits.";
                return ReadOutcome.Unsupported;
            }

            result.IsIncomplete = true;
            result.Issues.Add(new(
                "intake_limit_exceeded",
                $"{sourceLabel} exceeds the safe DOCX package, part, or extracted-image processing limits.",
                IntakeEvidenceSource.DocumentContent));
            return ReadOutcome.Readable;
        }
        catch (Exception exception) when (
            exception is FileFormatException
                or DocumentFormat.OpenXml.Packaging.OpenXmlPackageException
                or InvalidDataException)
        {
            if (isRoot)
            {
                result.FailureCode = "unreadable_docx";
                result.FailureReason = "The DOCX is corrupt or otherwise unreadable.";
                return ReadOutcome.Unsupported;
            }

            result.Issues.Add(new(
                "unreadable-docx-attachment",
                $"{sourceLabel} is corrupt or otherwise unreadable.",
                IntakeEvidenceSource.DocumentContent));
            return ReadOutcome.Readable;
        }
    }

    /// <summary>
    /// Every table cell of the document, each as its own fragment carrying its
    /// table, row and column. The flattened paragraph text above stays exactly
    /// as it was — this adds the layout the flattening loses, so a label in one
    /// cell can be bound to the value in the cell beside or beneath it instead
    /// of to whatever text follows it on a flattened line.
    /// </summary>
    private static void AddDocxTableCells(
        IReadOnlyList<DocxContentRoot> contentRoots,
        string sourceLabel,
        ReadAccumulator result)
    {
        var tableNumber = 0;
        var cellCount = 0;
        foreach (var contentRoot in contentRoots)
        {
            // Top-level tables only: a nested table is read as part of the cell
            // that contains it, which is where its own layout is meaningful.
            foreach (var table in contentRoot.Root.Descendants<Table>()
                .Where(table => table.Ancestors<Table>().Any() == false))
            {
                tableNumber++;
                var rowNumber = 0;
                foreach (var row in table.Elements<TableRow>())
                {
                    rowNumber++;
                    var columnNumber = 0;
                    foreach (var cell in row.Elements<TableCell>())
                    {
                        columnNumber++;
                        if (cellCount >= MaximumDocxTableCells)
                        {
                            result.IsIncomplete = true;
                            result.Issues.Add(new(
                                "intake_limit_exceeded",
                                $"{sourceLabel} contains more than {MaximumDocxTableCells} table cells; the rest were not read as cells.",
                                IntakeEvidenceSource.DocumentContent));
                            return;
                        }

                        cellCount++;
                        var cellText = string.Join(
                            Environment.NewLine,
                            cell.Descendants<Paragraph>()
                                .Select(paragraph => string.Concat(
                                    paragraph.Descendants<Text>().Select(item => item.Text)))
                                .Where(value => !string.IsNullOrWhiteSpace(value)));
                        if (string.IsNullOrWhiteSpace(cellText))
                        {
                            continue;
                        }

                        result.Content.Add(new(
                            IntakeEvidenceSource.DocumentContent,
                            $"{sourceLabel}, table {tableNumber} row {rowNumber} column {columnNumber}",
                            cellText,
                            IntakeSourceLocator.ForCell(tableNumber, rowNumber, columnNumber)));
                    }
                }
            }
        }
    }

    private static void ValidateDocxPackage(MemoryStream stream)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > MaximumDocxPackageEntries)
            {
                throw new DocxLimitExceededException();
            }

            long totalUncompressedBytes = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.Length > MaximumDocxUncompressedBytes - totalUncompressedBytes)
                {
                    throw new DocxLimitExceededException();
                }

                totalUncompressedBytes += entry.Length;
                if ((entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                     || entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                    && entry.Length > MaximumDocxXmlPartBytes)
                {
                    throw new DocxLimitExceededException();
                }
            }
        }
        finally
        {
            stream.Position = 0;
        }
    }

    private static IEnumerable<DocxImagePlacement> EnumerateImagePlacements(
        IEnumerable<DocxContentRoot> contentRoots)
    {
        var placementNumber = 0;
        foreach (var contentRoot in contentRoots)
        {
            foreach (var element in contentRoot.Root.Descendants())
            {
                var relationshipId = element switch
                {
                    DocumentFormat.OpenXml.Drawing.Blip blip => blip.Embed?.Value,
                    DocumentFormat.OpenXml.Vml.ImageData imageData => imageData.RelationshipId?.Value,
                    _ => null
                };
                if (string.IsNullOrWhiteSpace(relationshipId))
                {
                    continue;
                }

                if (!contentRoot.Part.TryGetPartById(relationshipId, out var relatedPart)
                    || relatedPart is not ImagePart imagePart)
                {
                    throw new FileFormatException(
                        "The DOCX contains an invalid embedded-image relationship.");
                }

                yield return new(imagePart, ++placementNumber);
            }
        }
    }

    private static async Task<ReadOutcome> ReadEmailAsync(
        ReadOnlyMemory<byte> bytes,
        string sourceLabel,
        ReadAccumulator result,
        IntakeSenderIdentityKind? senderIdentityKind,
        bool isRoot,
        CancellationToken cancellationToken)
    {
        MimeMessage message;
        try
        {
            await using var stream = new MemoryStream(bytes.ToArray(), writable: false);
            message = await MimeMessage.LoadAsync(stream, cancellationToken);
        }
        catch (Exception exception) when (exception is FormatException or ParseException)
        {
            if (isRoot)
            {
                result.FailureCode = "unreadable_email";
                result.FailureReason = "The email file is corrupt or is not a valid MIME message.";
                return ReadOutcome.Unsupported;
            }

            result.Issues.Add(new(
                "unreadable-email-attachment",
                $"{sourceLabel} is corrupt or is not a valid MIME message.",
                IntakeEvidenceSource.EmailBody));
            return ReadOutcome.Readable;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            if (isRoot)
            {
                result.FailureCode = "email_read_failure";
                result.FailureReason = "The email could not be read because of a technical failure.";
                return ReadOutcome.TechnicalFailure;
            }

            result.Issues.Add(new(
                "email-attachment-read-failure",
                $"{sourceLabel} could not be read because of a technical failure.",
                IntakeEvidenceSource.EmailBody));
            return ReadOutcome.Readable;
        }

        var limits = result.MimeLimits ??= new MimeLimitState();
        await ReadMessageAsync(
            message,
            sourceLabel,
            result,
            limits,
            0,
            senderIdentityKind,
            cancellationToken);
        return ReadOutcome.Readable;
    }

    private static async Task ReadMessageAsync(
        MimeMessage message,
        string sourceLabel,
        ReadAccumulator result,
        MimeLimitState limits,
        int nestedDepth,
        IntakeSenderIdentityKind? senderIdentityKind,
        CancellationToken cancellationToken)
    {
        if (senderIdentityKind is { } identityKind)
        {
            foreach (var mailbox in message.From.Mailboxes)
            {
                AddSenderTransportEvidence(
                    mailbox.Address,
                    identityKind,
                    sourceLabel,
                    result);
            }

            AddSenderTransportEvidence(
                message.Sender?.Address,
                identityKind,
                sourceLabel,
                result);

            if (identityKind == IntakeSenderIdentityKind.Transport
                && !string.IsNullOrWhiteSpace(message.Subject))
            {
                result.Transport.Add(new(
                    IntakeEvidenceSource.Subject,
                    message.Subject,
                    SourceLabel: sourceLabel));
            }
        }

        var body = message.TextBody;
        if (string.IsNullOrWhiteSpace(body) && !string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            body = WebUtility.HtmlDecode(HtmlTagRegex().Replace(
                HtmlLineBreakRegex().Replace(message.HtmlBody, "\n"),
                " "));
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            // The retained body is kept whole and first, exactly as it was: it
            // is the evidence, and bounding it here would change what every
            // existing reading of it sees. What is added is the boundary — the
            // current message is the region above the first quoted header, and
            // the history beneath it becomes its own fragment. An operator, and
            // a profile, can then tell what this sender wrote from what an
            // earlier sender wrote.
            var quotedAt = StaffForwardBodyCleaner.ForwardedHeaderPattern.Match(body);
            var hasQuotedHistory = quotedAt.Success && quotedAt.Index > 0;
            var quotedStart = quotedAt.Index;
            while (hasQuotedHistory
                && quotedStart < body.Length
                && (body[quotedStart] == '\r' || body[quotedStart] == '\n'))
            {
                quotedStart++;
            }
            result.Content.Add(new(
                IntakeEvidenceSource.EmailBody,
                $"{sourceLabel}, email body",
                body,
                IntakeSourceLocator.ForMessagePart(
                    IntakeMessagePart.CurrentBody,
                    hasQuotedHistory
                        ? string.Create(CultureInfo.InvariantCulture, $"chars 0-{quotedAt.Index}")
                        : null)));
            if (hasQuotedHistory)
            {
                result.Content.Add(new(
                    IntakeEvidenceSource.EmailBody,
                    $"{sourceLabel}, quoted history",
                    body[quotedStart..],
                    IntakeSourceLocator.ForMessagePart(
                        IntakeMessagePart.QuotedHistory,
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"chars {quotedStart}-{body.Length}"))));
            }

            if (senderIdentityKind == IntakeSenderIdentityKind.Transport
                && HasSingleStaffTransportSender(message)
                && TryReadInlineForwardedOriginalSender(body, out var forwardedSender))
            {
                AddSenderTransportEvidence(
                    forwardedSender,
                    IntakeSenderIdentityKind.InlineForwardedOriginal,
                    $"{sourceLabel}, inline forwarded-message header",
                    result);
            }
        }

        if (message.Body is not null)
        {
            await ReadMimeEntityAsync(
                message.Body,
                sourceLabel,
                result,
                limits,
                nestedDepth,
                senderIdentityKind == IntakeSenderIdentityKind.Transport,
                cancellationToken);
        }
    }

    private static void AddSenderTransportEvidence(
        string? address,
        IntakeSenderIdentityKind senderIdentityKind,
        string sourceLabel,
        ReadAccumulator result)
    {
        if (!string.IsNullOrWhiteSpace(address))
        {
            result.Transport.Add(new(
                IntakeEvidenceSource.Sender,
                address,
                senderIdentityKind,
                sourceLabel));
        }
    }

    private static bool HasSingleStaffTransportSender(MimeMessage message)
    {
        var addresses = message.From.Mailboxes
            .Select(mailbox => mailbox.Address)
            .Append(message.Sender?.Address)
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return addresses.Length == 1
            && TryGetMailboxDomain(addresses[0], out var domain)
            && string.Equals(domain, StaffTransportDomain, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadInlineForwardedOriginalSender(string body, out string sender)
    {
        sender = string.Empty;
        // MAIL-011: Core owns the forwarded-header shape. Route evidence keeps
        // its own bar on top of it — exactly one forwarded block, carrying
        // exactly one address — because an ambiguous body must not prove a
        // route. The shape and the bar are different rules.
        var matches = StaffForwardBodyCleaner.ForwardedHeaderPattern.Matches(body);
        if (matches.Count != 1)
        {
            return false;
        }

        var fromValue = matches[0].Groups["from"].Value.Trim();
        var mailboxes = ForwardedMailboxRegex().Matches(fromValue)
            .Select(match => match.Groups["address"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (mailboxes.Length != 1 || !TryGetMailboxDomain(mailboxes[0], out _))
        {
            return false;
        }

        sender = mailboxes[0];
        return true;
    }

    private static bool TryGetMailboxDomain(string address, out string domain)
    {
        domain = string.Empty;
        var separator = address.IndexOf('@');
        if (separator <= 0
            || separator != address.LastIndexOf('@')
            || separator == address.Length - 1
            || address.Any(char.IsWhiteSpace)
            || address.Contains('<')
            || address.Contains('>'))
        {
            return false;
        }

        domain = address[(separator + 1)..];
        return true;
    }

    private static async Task ReadMimeEntityAsync(
        MimeEntity entity,
        string sourceLabel,
        ReadAccumulator result,
        MimeLimitState limits,
        int nestedDepth,
        bool allowAttachedOriginal,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        limits.EntityCount++;
        if (limits.EntityCount > MaximumMimeEntities)
        {
            limits.AddExceededIssueOnce(result, "The email contains more than 128 MIME entities; remaining parts were retained only where already decoded.");
            return;
        }

        if (entity is Multipart multipart)
        {
            foreach (var child in multipart)
            {
                await ReadMimeEntityAsync(
                    child,
                    sourceLabel,
                    result,
                    limits,
                    nestedDepth,
                    allowAttachedOriginal,
                    cancellationToken);
                if (limits.EntityCount > MaximumMimeEntities || limits.DecodedBytes > MaximumDecodedMimeBytes)
                {
                    break;
                }
            }

            return;
        }

        if (entity is MessagePart messagePart)
        {
            if (nestedDepth >= MaximumNestedEmailDepth)
            {
                limits.AddExceededIssueOnce(result, "The email nesting depth exceeds 8; deeper attached messages were not opened.");
                return;
            }

            if (messagePart.Message is null)
            {
                result.Issues.Add(new(
                    "unreadable-email-attachment",
                    $"{sourceLabel} contains an attached email with no readable message body.",
                    IntakeEvidenceSource.EmailBody));
                return;
            }

            await using var nestedBytes = new MemoryStream();
            await messagePart.Message.WriteToAsync(nestedBytes, cancellationToken);
            var nestedPayload = nestedBytes.ToArray();
            if (!limits.TryAddBytes(nestedPayload.Length, result))
            {
                return;
            }

            var nestedNumber = ++limits.NestedMessageCount;
            var nestedLabel = $"{sourceLabel}, attached email {nestedNumber}";
            var nestedFileName = messagePart.ContentDisposition?.FileName
                ?? messagePart.ContentType.Name
                ?? $"attached-email-{nestedNumber}.eml";
            result.Attachments.Add(new(
                nestedFileName,
                "message/rfc822",
                nestedPayload.Length,
                result.Attachments.Count,
                nestedLabel));
            result.Assets.Add(new(
                nestedLabel,
                nestedFileName,
                "message/rfc822",
                nestedPayload,
                IntakeAssetKind.Attachment,
                IntakeAssetDisposition.Attachment));
            try
            {
                await ReadMessageAsync(
                    messagePart.Message,
                    nestedLabel,
                    result,
                    limits,
                    nestedDepth + 1,
                    allowAttachedOriginal && nestedDepth == 0
                        ? IntakeSenderIdentityKind.AttachedOriginal
                        : null,
                    cancellationToken);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                result.IsIncomplete = true;
                result.Issues.Add(new(
                    "attachment-processing-failure",
                    $"{nestedLabel} could not be completely processed and requires manual sorting.",
                    IntakeEvidenceSource.EmailBody));
            }
            return;
        }

        if (entity is not MimePart part
            || (entity is TextPart && !entity.IsAttachment))
        {
            return;
        }

        var fileName = part.FileName ?? InferFileName(part, limits);
        var format = DetectFormat(fileName, part.ContentType.MimeType);
        var isExplicitAttachment = part.ContentDisposition?.IsAttachment == true;
        var isInlineImage = format == SourceFormat.Image
            && !isExplicitAttachment
            && (part.ContentDisposition?.Disposition.Equals("inline", StringComparison.OrdinalIgnoreCase) == true
                || !string.IsNullOrWhiteSpace(part.ContentId));
        var descriptorOrdinal = result.Attachments.Count;
        var shouldRetain = format is SourceFormat.Pdf
            or SourceFormat.Email
            or SourceFormat.Docx
            or SourceFormat.Image
            or SourceFormat.Doc
            or SourceFormat.Msg;
        if (!shouldRetain || part.Content is null)
        {
            if (!isInlineImage)
            {
                result.Attachments.Add(new(
                    fileName,
                    part.ContentType.MimeType,
                    part.Content?.Stream?.CanSeek == true ? part.Content.Stream.Length : null,
                    descriptorOrdinal));
            }
            return;
        }

        var attachmentNumber = ++limits.AttachmentCount;
        var attachmentLabel = $"{sourceLabel}, attachment {attachmentNumber}: {fileName}";
        if (!isInlineImage)
        {
            result.Attachments.Add(new(
                fileName,
                part.ContentType.MimeType,
                part.Content.Stream?.CanSeek == true ? part.Content.Stream.Length : null,
                descriptorOrdinal,
                attachmentLabel));
        }

        await using var decoded = new MemoryStream();
        try
        {
            await part.Content.DecodeToAsync(decoded, cancellationToken);
        }
        catch (Exception exception) when (exception is FormatException or ParseException)
        {
            result.Issues.Add(new(
                "attachment-decode-failure",
                $"{sourceLabel}, attachment {fileName} could not be decoded.",
                IntakeEvidenceSource.FileName));
            return;
        }

        var payload = decoded.ToArray();
        if (!limits.TryAddBytes(payload.Length, result))
        {
            return;
        }

        result.Assets.Add(new(
            attachmentLabel,
            fileName,
            format == SourceFormat.Image
                ? NormalizeImageMediaType(fileName, part.ContentType.MimeType)
                : part.ContentType.MimeType,
            payload,
            isInlineImage ? IntakeAssetKind.InlineImage : IntakeAssetKind.Attachment,
            isInlineImage ? IntakeAssetDisposition.Inline : IntakeAssetDisposition.Attachment));

        try
        {
            await DispatchAsync(
                payload,
                fileName,
                part.ContentType.MimeType,
                attachmentLabel,
                result,
                cancellationToken,
                emailSenderIdentityKind: allowAttachedOriginal && nestedDepth == 0
                    ? IntakeSenderIdentityKind.AttachedOriginal
                    : null);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            result.IsIncomplete = true;
            result.Issues.Add(new(
                "attachment-processing-failure",
                $"{attachmentLabel} could not be completely processed and requires manual sorting.",
                IntakeEvidenceSource.FileName));
        }
    }

    private static string InferFileName(MimePart part, MimeLimitState limits)
    {
        var extension = part.ContentType.MimeType.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "message/rfc822" => ".eml",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "application/msword" => ".doc",
            "application/vnd.ms-outlook" => ".msg",
            _ => string.Empty
        };
        return $"unnamed-part-{limits.EntityCount}{extension}";
    }

    private static SourceFormat DetectFormat(string fileName, string mediaType)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return SourceFormat.Pdf;
        }

        if (extension.Equals(".eml", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("message/rfc822", StringComparison.OrdinalIgnoreCase))
        {
            return SourceFormat.Email;
        }

        if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.OrdinalIgnoreCase))
        {
            return SourceFormat.Docx;
        }

        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
        {
            return SourceFormat.Image;
        }

        if (extension.Equals(".doc", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/msword", StringComparison.OrdinalIgnoreCase))
        {
            return SourceFormat.Doc;
        }

        if (extension.Equals(".msg", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/vnd.ms-outlook", StringComparison.OrdinalIgnoreCase))
        {
            return SourceFormat.Msg;
        }

        return SourceFormat.Unsupported;
    }

    private static string NormalizeImageMediaType(string fileName, string mediaType)
    {
        if (mediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(fileName).Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(fileName).Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return "image/jpeg";
        }

        return "image/png";
    }

    private static string SourceLabel(string fileName) => $"uploaded {Path.GetFileName(fileName)}";

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("</?(?:br|p|div|tr|li|h[1-6])\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlLineBreakRegex();

    [GeneratedRegex(
        "(?i)(?<![a-z0-9._%+-])(?<address>[a-z0-9._%+-]+@[a-z0-9.-]+\\.[a-z]{2,})(?![a-z0-9._%+-])",
        RegexOptions.CultureInvariant)]
    private static partial Regex ForwardedMailboxRegex();

    private enum SourceFormat
    {
        Unsupported,
        Pdf,
        Email,
        Docx,
        Image,
        Doc,
        Msg
    }

    private enum ReadOutcome
    {
        Readable,
        Unsupported,
        TechnicalFailure
    }

    private sealed class ReadAccumulator(
        IReadOnlyList<IntakeTransportEvidence> transport,
        TimeProvider timeProvider)
    {
        public List<IntakeContentFragment> Content { get; } = [];

        public List<IntakeTransportEvidence> Transport { get; } = [.. transport];

        public List<IntakeSourceIssue> Issues { get; } = [];

        public List<IntakeAssetCandidate> Assets { get; } = [];

        public List<IntakeAttachmentDescriptor> Attachments { get; } = [];

        public List<ScannedPdfOcrCandidate> OcrCandidates { get; } = [];

        public MimeLimitState? MimeLimits { get; set; }

        public PdfLimitState PdfLimits { get; } = new(timeProvider);

        public string? FailureCode { get; set; }

        public string? FailureReason { get; set; }

        public bool IsIncomplete { get; set; }

        public IntakeSourceReadResult ToResult(
            IntakeSourceReadStatus status,
            string? failureCode = null,
            string? failureReason = null) => new(
                status,
                Content,
                Transport,
                Issues,
                OcrCandidates.Count > 0,
                failureCode,
                failureReason,
                Assets,
                OcrCandidates,
                IsIncomplete,
                ReaderKey,
                ReaderVersion,
                Attachments);
    }

    private sealed class MimeLimitState
    {
        private bool limitIssueAdded;

        public int EntityCount { get; set; }

        public long DecodedBytes { get; private set; }

        public int AttachmentCount { get; set; }

        public int NestedMessageCount { get; set; }

        public bool TryAddBytes(long count, ReadAccumulator result)
        {
            if (DecodedBytes + count > MaximumDecodedMimeBytes)
            {
                DecodedBytes = MaximumDecodedMimeBytes + 1;
                AddExceededIssueOnce(result, "Decoded email attachments exceed 25 MB; remaining parts were not opened.");
                return false;
            }

            DecodedBytes += count;
            return true;
        }

        public void AddExceededIssueOnce(ReadAccumulator result, string reason)
        {
            result.IsIncomplete = true;
            if (limitIssueAdded)
            {
                return;
            }

            limitIssueAdded = true;
            result.Issues.Add(new("intake_limit_exceeded", reason, IntakeEvidenceSource.FileName));
        }
    }

    private sealed record PdfResult(
        IReadOnlyList<PdfPageResult> Pages,
        IReadOnlyList<PdfFormFieldResult> FormFields);

    private sealed record PdfFormFieldResult(
        string Name,
        string Value,
        int? Page,
        string? Region);

    private sealed record PdfPageResult(
        int Number,
        string? Text,
        bool HasInsufficientText,
        bool RequiresOcr);

    private sealed class PdfLimitState(TimeProvider timeProvider)
    {
        private long extractedTextCharacters;
        private int imageObjects;
        private long imageSamplePixels;
        private long extractedImageBytes;
        private long? processingStartedTimestamp;

        public int RemainingImageObjects => MaximumPdfImageObjects - imageObjects;

        public void ThrowIfProcessingDeadlineExceeded()
        {
            var now = timeProvider.GetTimestamp();
            if (processingStartedTimestamp is null)
            {
                processingStartedTimestamp = now;
                return;
            }

            if (timeProvider.GetElapsedTime(processingStartedTimestamp.Value, now) > MaximumPdfProcessingDuration)
            {
                throw new PdfLimitExceededException(
                    "exceeds the safe PDF processing time limit.");
            }
        }

        public void AddTextCharacters(int count)
        {
            if (count > MaximumPdfTextCharacters - extractedTextCharacters)
            {
                throw new PdfLimitExceededException(
                    "expands beyond the safe extracted-text processing limit.");
            }

            extractedTextCharacters += count;
        }

        public void AddImageObjects(int count)
        {
            if (count > MaximumPdfImageObjects - imageObjects)
            {
                throw new PdfLimitExceededException(
                    "contains more than 512 discrete image objects.");
            }

            imageObjects += count;
        }

        public void AddImageSamplePixels(int width, int height)
        {
            var count = (long)width * height;
            if (count > MaximumPdfImageSamplePixels - imageSamplePixels)
            {
                throw new PdfLimitExceededException(
                    "expands beyond the safe decoded-image pixel limit.");
            }

            imageSamplePixels += count;
        }

        public void AddExtractedImageBytes(int count)
        {
            if (count > MaximumPdfExtractedImageBytes - extractedImageBytes)
            {
                throw new PdfLimitExceededException(
                    "expands beyond the safe extracted-image processing limit.");
            }

            extractedImageBytes += count;
        }
    }

    private readonly record struct DocxContentRoot(OpenXmlPart Part, OpenXmlElement Root);
    private readonly record struct DocxImagePlacement(ImagePart Part, int Number);

    private sealed class DocxLimitExceededException : Exception
    {
    }

    private sealed class PdfLimitExceededException(string message) : Exception(message)
    {
    }
}
