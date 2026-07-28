using System.Net;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Pegasus.Core.Intake;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MimeKit;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Pegasus.Infrastructure.Intake;

internal sealed partial class MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider timeProvider) : IIntakeSourceReader
{
    private const string ReaderKey = "mimekit_pdfpig_openxml";
    private const string ReaderVersion = "mimekit-4.17.0;pdfpig-0.1.15;openxml-3.5.1";
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
    private const long MaximumDocxUncompressedBytes = 50L * 1024 * 1024;
    private const long MaximumDocxXmlPartBytes = 10L * 1024 * 1024;
    private const long MaximumDocxImageBytes = 25L * 1024 * 1024;

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
        bool isRoot = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var format = DetectFormat(fileName, mediaType);
        switch (format)
        {
            case SourceFormat.Pdf:
                return ReadPdf(bytes, sourceLabel, result, isRoot, cancellationToken);
            case SourceFormat.Email:
                return await ReadEmailAsync(bytes, sourceLabel, result, isRoot, cancellationToken);
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
            case SourceFormat.Deferred:
                result.Issues.Add(new(
                    "deferred_file_type",
                    $"{Path.GetExtension(fileName).ToLowerInvariant()} extraction is deferred; the file is retained for operator review.",
                    IntakeEvidenceSource.DocumentContent));
                return ReadOutcome.Readable;
            default:
                if (isRoot)
                {
                    result.FailureCode = "unsupported_file_type";
                    result.FailureReason = "Supported sources are .eml, .pdf, .docx, .jpg, .jpeg, .png, with .doc and .msg retained for manual sorting.";
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
                    page.Text));
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

        return ReadOutcome.Readable;
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

        return new(pages);
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

            var textRoots = new List<DocumentFormat.OpenXml.OpenXmlElement> { mainPart.Document };
            textRoots.AddRange(mainPart.HeaderParts.Where(part => part.Header is not null).Select(part => part.Header!));
            textRoots.AddRange(mainPart.FooterParts.Where(part => part.Footer is not null).Select(part => part.Footer!));
            if (mainPart.FootnotesPart?.Footnotes is not null)
            {
                textRoots.Add(mainPart.FootnotesPart.Footnotes);
            }

            if (mainPart.EndnotesPart?.Endnotes is not null)
            {
                textRoots.Add(mainPart.EndnotesPart.Endnotes);
            }

            var text = string.Join(
                Environment.NewLine,
                textRoots
                    .SelectMany(root => root
                        .Descendants<Paragraph>()
                        .Select(paragraph => string.Concat(paragraph.Descendants<Text>().Select(item => item.Text))))
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.Content.Add(new(IntakeEvidenceSource.DocumentContent, sourceLabel, text));
            }

            var imageNumber = 0;
            long totalImageBytes = 0;
            foreach (var imagePart in EnumerateImageParts(mainPart))
            {
                imageNumber++;
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

                var fileName = Path.GetFileName(imagePart.Uri.OriginalString);
                result.Assets.Add(new(
                    $"{sourceLabel}, embedded image {imageNumber}",
                    string.IsNullOrWhiteSpace(fileName) ? $"embedded-image-{imageNumber}" : fileName,
                    imagePart.ContentType,
                    imageBytes.ToArray(),
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

    private static List<ImagePart> EnumerateImageParts(OpenXmlPartContainer container)
    {
        var images = new List<ImagePart>();
        var pending = new Stack<OpenXmlPart>(container.Parts.Select(relationship => relationship.OpenXmlPart));
        var visited = new HashSet<Uri>();

        while (pending.TryPop(out var part))
        {
            if (!visited.Add(part.Uri))
            {
                continue;
            }

            if (part is ImagePart imagePart)
            {
                images.Add(imagePart);
            }

            foreach (var relationship in part.Parts)
            {
                pending.Push(relationship.OpenXmlPart);
            }
        }

        return images;
    }

    private static async Task<ReadOutcome> ReadEmailAsync(
        ReadOnlyMemory<byte> bytes,
        string sourceLabel,
        ReadAccumulator result,
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
        await ReadMessageAsync(message, sourceLabel, result, limits, 0, addTransport: isRoot, cancellationToken);
        return ReadOutcome.Readable;
    }

    private static async Task ReadMessageAsync(
        MimeMessage message,
        string sourceLabel,
        ReadAccumulator result,
        MimeLimitState limits,
        int nestedDepth,
        bool addTransport,
        CancellationToken cancellationToken)
    {
        if (addTransport)
        {
            var sender = message.From.Mailboxes.FirstOrDefault()?.Address;
            if (!string.IsNullOrWhiteSpace(sender))
            {
                result.Transport.Add(new(IntakeEvidenceSource.Sender, sender));
            }

            if (!string.IsNullOrWhiteSpace(message.Subject))
            {
                result.Transport.Add(new(IntakeEvidenceSource.Subject, message.Subject));
            }
        }

        var body = message.TextBody;
        if (string.IsNullOrWhiteSpace(body) && !string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            body = WebUtility.HtmlDecode(HtmlTagRegex().Replace(message.HtmlBody, " "));
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            result.Content.Add(new(IntakeEvidenceSource.EmailBody, $"{sourceLabel}, email body", body));
        }

        if (message.Body is not null)
        {
            await ReadMimeEntityAsync(
                message.Body,
                sourceLabel,
                result,
                limits,
                nestedDepth,
                cancellationToken);
        }
    }

    private static async Task ReadMimeEntityAsync(
        MimeEntity entity,
        string sourceLabel,
        ReadAccumulator result,
        MimeLimitState limits,
        int nestedDepth,
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
                await ReadMimeEntityAsync(child, sourceLabel, result, limits, nestedDepth, cancellationToken);
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
                    addTransport: false,
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

        if (entity is not MimePart part || entity is TextPart)
        {
            return;
        }

        var fileName = part.FileName ?? InferFileName(part, limits);
        var format = DetectFormat(fileName, part.ContentType.MimeType);
        var isInlineImage = format == SourceFormat.Image
            && (part.ContentDisposition?.Disposition.Equals("inline", StringComparison.OrdinalIgnoreCase) == true
                || !string.IsNullOrWhiteSpace(part.ContentId));
        var shouldRetain = format is SourceFormat.Pdf
            or SourceFormat.Email
            or SourceFormat.Docx
            or SourceFormat.Image
            or SourceFormat.Deferred;
        if (!shouldRetain || part.Content is null)
        {
            return;
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

        var attachmentNumber = ++limits.AttachmentCount;
        var attachmentLabel = $"{sourceLabel}, attachment {attachmentNumber}: {fileName}";
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
                cancellationToken);
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
            || extension.Equals(".msg", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/msword", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/vnd.ms-outlook", StringComparison.OrdinalIgnoreCase))
        {
            return SourceFormat.Deferred;
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

    private enum SourceFormat
    {
        Unsupported,
        Pdf,
        Email,
        Docx,
        Image,
        Deferred
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
                ReaderVersion);
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

    private sealed record PdfResult(IReadOnlyList<PdfPageResult> Pages);

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

    private sealed class DocxLimitExceededException : Exception
    {
    }

    private sealed class PdfLimitExceededException(string message) : Exception(message)
    {
    }
}
