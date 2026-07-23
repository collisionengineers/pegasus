using System.Net;
using System.Text.RegularExpressions;
using CollisionSpike.Core.Intake.Qdos;
using MimeKit;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace CollisionSpike.Infrastructure.Intake.Qdos;

internal sealed partial class MimeKitPdfPigQdosSourceReader : IQdosIntakeSourceReader
{
    private const int MinimumReadablePdfCharacters = 80;

    public async Task<IntakeSourceReadResult> ReadAsync(
        QdosIntakeSource source,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(source.FileName);
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return ReadPdf(source);
        }

        if (extension.Equals(".eml", StringComparison.OrdinalIgnoreCase))
        {
            return await ReadEmailAsync(source, cancellationToken);
        }

        return new(
            IntakeSourceReadStatus.Unsupported,
            [],
            [
                new(QdosEvidenceSource.FileName, source.FileName),
                new(QdosEvidenceSource.MimeType, source.MediaType)
            ],
            [],
            false,
            "unsupported_file_type",
            "Only .eml and .pdf sources are supported by this intake path.");
    }

    private static IntakeSourceReadResult ReadPdf(QdosIntakeSource source)
    {
        var transport = new IntakeTransportEvidence[]
        {
            new(QdosEvidenceSource.FileName, source.FileName),
            new(QdosEvidenceSource.MimeType, source.MediaType)
        };

        var result = ExtractPdf(source.Content, "uploaded PDF");
        if (!result.Opened)
        {
            return new(
                IntakeSourceReadStatus.Unsupported,
                [],
                transport,
                [],
                false,
                "unreadable_pdf",
                "The PDF is corrupt, encrypted, or otherwise unreadable.");
        }

        var issues = new List<IntakeSourceIssue>
        {
            new("pdf-engine", "Embedded PDF text was read with PdfPig 0.1.15.", QdosEvidenceSource.PdfContent)
        };
        if (result.RequiresOcr)
        {
            issues.Add(new(
                "insufficient-embedded-text",
                "The PDF does not contain enough embedded text for a reliable decision.",
                QdosEvidenceSource.PdfContent));
        }

        return new(
            IntakeSourceReadStatus.Readable,
            result.Pages
                .Where(page => page.Text is not null)
                .Select(page => new IntakeContentFragment(
                    QdosEvidenceSource.PdfContent,
                    $"uploaded PDF, page {page.Number}",
                    page.Text!))
                .ToArray(),
            transport,
            issues,
            result.RequiresOcr);
    }

    private static async Task<IntakeSourceReadResult> ReadEmailAsync(
        QdosIntakeSource source,
        CancellationToken cancellationToken)
    {
        MimeMessage message;
        try
        {
            await using var stream = new MemoryStream(source.Content.ToArray(), writable: false);
            message = await MimeMessage.LoadAsync(stream, cancellationToken);
        }
        catch (Exception exception) when (exception is FormatException or ParseException)
        {
            return new(
                IntakeSourceReadStatus.Unsupported,
                [],
                [new(QdosEvidenceSource.FileName, source.FileName)],
                [],
                false,
                "unreadable_email",
                "The email file is corrupt or is not a valid MIME message.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new(
                IntakeSourceReadStatus.TechnicalFailure,
                [],
                [new(QdosEvidenceSource.FileName, source.FileName)],
                [],
                false,
                "email_read_failure",
                "The email could not be read because of a technical failure.");
        }

        var content = new List<IntakeContentFragment>();
        var issues = new List<IntakeSourceIssue>();
        var transport = new List<IntakeTransportEvidence>
        {
            new(QdosEvidenceSource.FileName, source.FileName),
            new(QdosEvidenceSource.MimeType, source.MediaType)
        };

        var sender = message.From.Mailboxes.FirstOrDefault()?.Address;
        if (!string.IsNullOrWhiteSpace(sender))
        {
            transport.Add(new(QdosEvidenceSource.Sender, sender));
        }

        if (!string.IsNullOrWhiteSpace(message.Subject))
        {
            transport.Add(new(QdosEvidenceSource.Subject, message.Subject));
        }

        var body = message.TextBody;
        if (string.IsNullOrWhiteSpace(body) && !string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            body = WebUtility.HtmlDecode(HtmlTagRegex().Replace(message.HtmlBody, " "));
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            content.Add(new(QdosEvidenceSource.EmailBody, "email body", body));
        }

        var requiresOcr = false;
        var pdfNumber = 0;
        foreach (var part in message.BodyParts.OfType<MimePart>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = part.FileName ?? string.Empty;
            var isPdf = part.ContentType.MimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
            if (!isPdf)
            {
                continue;
            }

            pdfNumber++;
            if (part.Content is null)
            {
                issues.Add(new(
                    "pdf-attachment-read-failure",
                    $"PDF attachment {pdfNumber} has no readable content.",
                    QdosEvidenceSource.PdfContent));
                continue;
            }

            await using var attachment = new MemoryStream();
            try
            {
                await part.Content.DecodeToAsync(attachment, cancellationToken);
            }
            catch (Exception exception) when (exception is FormatException or ParseException)
            {
                issues.Add(new(
                    "pdf-attachment-read-failure",
                    $"PDF attachment {pdfNumber} could not be decoded.",
                    QdosEvidenceSource.PdfContent));
                continue;
            }

            var label = $"PDF attachment {pdfNumber}";
            var pdf = ExtractPdf(attachment.ToArray(), label);
            if (!pdf.Opened)
            {
                issues.Add(new(
                    "unreadable-pdf-attachment",
                    $"{label} is corrupt, encrypted, or otherwise unreadable.",
                    QdosEvidenceSource.PdfContent));
                continue;
            }

            issues.Add(new(
                "pdf-engine",
                $"{label} embedded text was read with PdfPig 0.1.15.",
                QdosEvidenceSource.PdfContent));
            requiresOcr |= pdf.RequiresOcr;
            if (pdf.RequiresOcr)
            {
                issues.Add(new(
                    "insufficient-embedded-text",
                    $"{label} does not contain enough embedded text for a reliable decision.",
                    QdosEvidenceSource.PdfContent));
            }

            foreach (var page in pdf.Pages.Where(page => page.Text is not null))
            {
                content.Add(new(
                    QdosEvidenceSource.PdfContent,
                    $"{label}, page {page.Number}",
                    page.Text!));
            }
        }

        return new(
            IntakeSourceReadStatus.Readable,
            content,
            transport,
            issues,
            requiresOcr);
    }

    private static PdfResult ExtractPdf(ReadOnlyMemory<byte> bytes, string sourceLabel)
    {
        try
        {
            using var document = PdfDocument.Open(bytes.ToArray());
            var pages = document.GetPages()
                .Select(page =>
                {
                    var text = ContentOrderTextExtractor.GetText(page);
                    var readableCharacters = text.Count(character => !char.IsWhiteSpace(character));
                    return new PdfPageResult(
                        page.Number,
                        readableCharacters == 0 ? null : text,
                        readableCharacters < MinimumReadablePdfCharacters);
                })
                .ToArray();
            return new(true, pages, pages.Any(page => page.RequiresOcr));
        }
        catch (Exception exception) when (
            exception is PdfDocumentFormatException
                or PdfDocumentStackDepthException
                or PdfDocumentEncryptedException)
        {
            return new(false, [], false);
        }
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    private sealed record PdfResult(
        bool Opened,
        IReadOnlyList<PdfPageResult> Pages,
        bool RequiresOcr);

    private sealed record PdfPageResult(int Number, string? Text, bool RequiresOcr);
}
