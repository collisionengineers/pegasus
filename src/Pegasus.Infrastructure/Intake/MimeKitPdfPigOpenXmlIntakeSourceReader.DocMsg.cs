using System.Globalization;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake.DocumentExtraction;
using Pegasus.Infrastructure.Intake.DocumentExtraction.Msg;
using Pegasus.Infrastructure.Intake.DocumentExtraction.Word;

namespace Pegasus.Infrastructure.Intake;

/// <summary>
/// The legacy Word (<c>.doc</c>) and Outlook item (<c>.msg</c>) branches of the
/// intake reader, backed by the CollisionDocNet-derived compound-file readers
/// integrated under ADR-0025. Extraction is passive: no macro, OLE, script, or
/// external content is ever opened, and an unreadable container falls back to
/// the retained-for-review outcome instead of failing intake.
/// </summary>
public sealed partial class MimeKitPdfPigOpenXmlIntakeSourceReader
{
    private static ReadOutcome ReadDoc(
        ReadOnlyMemory<byte> bytes,
        string sourceLabel,
        ReadAccumulator result,
        CancellationToken cancellationToken)
    {
        if (bytes.Span.StartsWith("{\\rtf"u8))
        {
            return ReadRtfDoc(bytes, sourceLabel, result, cancellationToken);
        }

        WordBinaryExtractionResult parsed;
        try
        {
            parsed = WordBinaryExtractor.Extract(bytes, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return AddUnreadableContainerFallback(
                "unreadable-doc-file",
                $"{sourceLabel} could not be read as a legacy Word document and is retained for review.",
                result);
        }

        switch (parsed.Outcome)
        {
            case WordBinaryOutcome.Cancelled:
                throw new OperationCanceledException(cancellationToken);
            case WordBinaryOutcome.Complete:
            case WordBinaryOutcome.Partial:
                break;
            case WordBinaryOutcome.Encrypted:
                return AddUnreadableContainerFallback(
                    "encrypted-doc-file",
                    $"{sourceLabel} is an encrypted Word document; it was not decrypted and is retained for review.",
                    result);
            case WordBinaryOutcome.ResourceLimitExceeded:
                return AddUnreadableContainerFallback(
                    "intake_limit_exceeded",
                    $"{sourceLabel} exceeds the safe legacy Word processing limits.",
                    result,
                    markIncomplete: true);
            default:
                return AddUnreadableContainerFallback(
                    "unreadable-doc-file",
                    $"{sourceLabel} could not be read as a legacy Word document and is retained for review.",
                    result);
        }

        var text = string.Join(
            Environment.NewLine,
            parsed.Stories
                .Select(story => story.Text)
                .Where(storyText => !string.IsNullOrWhiteSpace(storyText)));
        if (!string.IsNullOrWhiteSpace(text))
        {
            result.Content.Add(new(IntakeEvidenceSource.DocumentContent, sourceLabel, text));
        }

        result.Issues.Add(new(
            "doc-engine",
            $"{sourceLabel} legacy Word text was read; embedded objects and macros were not opened.",
            IntakeEvidenceSource.DocumentContent));
        if (parsed.Outcome == WordBinaryOutcome.Partial)
        {
            result.IsIncomplete = true;
            result.Issues.Add(new(
                "doc-partial-extraction",
                $"{sourceLabel} contains legacy Word structures outside the supported text extraction, so some content may be missing.",
                IntakeEvidenceSource.DocumentContent));
        }

        return ReadOutcome.Readable;
    }

    private static ReadOutcome ReadRtfDoc(
        ReadOnlyMemory<byte> bytes,
        string sourceLabel,
        ReadAccumulator result,
        CancellationToken cancellationToken)
    {
        var issues = new List<MsgIssue>();
        string text;
        try
        {
            text = PassiveRtfText.Extract(
                bytes.Span, issues, preserveTableControls: true, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return AddUnreadableContainerFallback(
                "unreadable-doc-file",
                $"{sourceLabel} could not be read as an RTF Word document and is retained for review.",
                result);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return AddUnreadableContainerFallback(
                "unreadable-doc-file",
                $"{sourceLabel} contains no readable RTF document text and is retained for review.",
                result);
        }

        var readableText = text
            .Replace(PassiveRtfText.TableRowStart.ToString(), string.Empty, StringComparison.Ordinal)
            .Replace(PassiveRtfText.TableCellBoundary, '\t')
            .Replace(PassiveRtfText.TableRowEnd.ToString(), Environment.NewLine, StringComparison.Ordinal);
        result.Content.Add(new(IntakeEvidenceSource.DocumentContent, sourceLabel, readableText));
        AddRtfTableCells(text, sourceLabel, result);
        result.Issues.Add(new(
            "doc-rtf-engine",
            $"{sourceLabel} RTF text was read passively; embedded objects and scripts were not opened.",
            IntakeEvidenceSource.DocumentContent));
        if (issues.Any(issue => string.Equals(
                issue.Code, "MSG_RTF_RESERVED_STRUCTURE_TEXT", StringComparison.Ordinal)))
        {
            result.IsIncomplete = true;
            result.Issues.Add(new(
                "doc-rtf-reserved-structure-text",
                $"{sourceLabel} contains textual values reserved for RTF structure parsing and requires review.",
                IntakeEvidenceSource.DocumentContent));
        }
        if (issues.Any(issue => string.Equals(issue.Code, "MSG_RTF_GROUP_INVALID", StringComparison.Ordinal)))
        {
            result.IsIncomplete = true;
            result.Issues.Add(new(
                "doc-rtf-partial-extraction",
                $"{sourceLabel} contains malformed or skipped RTF structures, so some content may be missing.",
                IntakeEvidenceSource.DocumentContent));
        }

        return ReadOutcome.Readable;
    }

    private static void AddRtfTableCells(string text, string sourceLabel, ReadAccumulator result)
    {
        var row = 0;
        var cursor = 0;
        while (cursor < text.Length)
        {
            var start = text.IndexOf(PassiveRtfText.TableRowStart, cursor);
            if (start < 0)
                break;
            var end = text.IndexOf(PassiveRtfText.TableRowEnd, start + 1);
            if (end < 0)
                break;
            cursor = end + 1;
            var encodedRow = text[(start + 1)..end];
            if (!encodedRow.Contains(PassiveRtfText.TableCellBoundary))
                continue;
            row++;
            var cells = encodedRow.Split(PassiveRtfText.TableCellBoundary);
            for (var column = 0; column < cells.Length; column++)
            {
                var value = cells[column].Trim();
                if (value.Length == 0)
                    continue;
                result.Content.Add(new(
                    IntakeEvidenceSource.DocumentContent,
                    $"{sourceLabel}, table 1 row {row} column {column + 1}",
                    value,
                    IntakeSourceLocator.ForCell(1, row, column + 1)));
            }
        }
    }

    private static async Task<ReadOutcome> ReadMsgAsync(
        ReadOnlyMemory<byte> bytes,
        string sourceLabel,
        ReadAccumulator result,
        IntakeSenderIdentityKind? senderIdentityKind,
        CancellationToken cancellationToken)
    {
        MsgDocument parsed;
        try
        {
            parsed = MsgReader.Read(bytes, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return AddUnreadableContainerFallback(
                "unreadable-msg-file",
                $"{sourceLabel} could not be read as an Outlook message and is retained for review.",
                result);
        }

        switch (parsed.Outcome)
        {
            case MsgReadOutcome.Cancelled:
                throw new OperationCanceledException(cancellationToken);
            case MsgReadOutcome.Complete:
            case MsgReadOutcome.Partial:
                break;
            case MsgReadOutcome.Encrypted:
                return AddUnreadableContainerFallback(
                    "protected-msg-file",
                    $"{sourceLabel} is a protected message; it was not decrypted and is retained for review.",
                    result);
            case MsgReadOutcome.ResourceLimitExceeded:
                return AddUnreadableContainerFallback(
                    "intake_limit_exceeded",
                    $"{sourceLabel} exceeds the safe Outlook message processing limits.",
                    result,
                    markIncomplete: true);
            default:
                return AddUnreadableContainerFallback(
                    "unreadable-msg-file",
                    $"{sourceLabel} could not be read as an Outlook message and is retained for review.",
                    result);
        }

        await MapMsgDocumentAsync(parsed, sourceLabel, result, senderIdentityKind, 0, cancellationToken);
        result.Issues.Add(new(
            "msg-engine",
            $"{sourceLabel} message text and attachments were read; embedded objects were not opened.",
            IntakeEvidenceSource.EmailBody));
        return ReadOutcome.Readable;
    }

    private static async Task MapMsgDocumentAsync(
        MsgDocument message,
        string sourceLabel,
        ReadAccumulator result,
        IntakeSenderIdentityKind? senderIdentityKind,
        int nestedDepth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (senderIdentityKind is { } identityKind)
        {
            if (message.Projection.Fields.TryGetValue("senderAddress", out var senderAddress)
                && !string.IsNullOrWhiteSpace(senderAddress))
            {
                var sanitizedSenderAddress = SanitizeText(senderAddress);
                if (TryGetMailboxDomain(sanitizedSenderAddress, out _))
                {
                    AddSenderTransportEvidence(sanitizedSenderAddress, identityKind, sourceLabel, result);
                }
            }

            if (identityKind == IntakeSenderIdentityKind.Transport
                && message.Projection.Fields.TryGetValue("subject", out var subject)
                && !string.IsNullOrWhiteSpace(subject))
            {
                result.Transport.Add(new(
                    IntakeEvidenceSource.Subject,
                    SanitizeText(subject),
                    SourceLabel: sourceLabel));
            }
        }

        if (!string.IsNullOrWhiteSpace(message.Bodies.CanonicalText))
        {
            result.Content.Add(new(
                IntakeEvidenceSource.EmailBody,
                $"{sourceLabel}, message body",
                SanitizeText(message.Bodies.CanonicalText)));
        }

        var limits = result.MimeLimits ??= new MimeLimitState();
        var allowAttachedOriginal = senderIdentityKind == IntakeSenderIdentityKind.Transport;
        foreach (var attachment in message.Attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (attachment.EmbeddedMessage is not null)
            {
                if (nestedDepth >= MaximumNestedEmailDepth)
                {
                    limits.AddExceededIssueOnce(result, "The message nesting depth exceeds 8; deeper attached messages were not opened.");
                    continue;
                }

                var nestedLabel = $"{sourceLabel}, attached message {++limits.NestedMessageCount}";
                await MapMsgDocumentAsync(
                    attachment.EmbeddedMessage,
                    nestedLabel,
                    result,
                    allowAttachedOriginal && nestedDepth == 0
                        ? IntakeSenderIdentityKind.AttachedOriginal
                        : null,
                    nestedDepth + 1,
                    cancellationToken);
                continue;
            }

            if (attachment.Content.IsDefaultOrEmpty)
            {
                result.Issues.Add(new(
                    "msg-attachment-not-materialised",
                    $"{sourceLabel} contains an attachment that is only a reference or embedded object, so it stays with the retained original for review.",
                    IntakeEvidenceSource.FileName));
                continue;
            }

            var fileName = SanitizeText(
                attachment.FileName
                ?? attachment.DisplayName
                ?? $"attachment-{attachment.SourceOrder.ToString(CultureInfo.InvariantCulture)}");
            var mediaType = attachment.MediaType ?? "application/octet-stream";
            var format = DetectFormat(fileName, mediaType);
            var shouldRetain = format is not SourceFormat.Unsupported;
            if (!shouldRetain)
            {
                continue;
            }

            var payload = attachment.Content.ToArray();
            if (!limits.TryAddBytes(payload.Length, result))
            {
                continue;
            }

            var isInlineImage = format == SourceFormat.Image
                && (attachment.IsInline || !string.IsNullOrWhiteSpace(attachment.ContentId));
            var attachmentNumber = ++limits.AttachmentCount;
            var attachmentLabel = $"{sourceLabel}, attachment {attachmentNumber}: {fileName}";
            result.Assets.Add(new(
                attachmentLabel,
                fileName,
                format == SourceFormat.Image
                    ? NormalizeImageMediaType(fileName, mediaType)
                    : mediaType,
                payload,
                isInlineImage ? IntakeAssetKind.InlineImage : IntakeAssetKind.Attachment,
                isInlineImage ? IntakeAssetDisposition.Inline : IntakeAssetDisposition.Attachment));

            try
            {
                await DispatchAsync(
                    payload,
                    fileName,
                    mediaType,
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
    }

    private static ReadOutcome AddUnreadableContainerFallback(
        string code,
        string reason,
        ReadAccumulator result,
        bool markIncomplete = false)
    {
        if (markIncomplete)
        {
            result.IsIncomplete = true;
        }

        result.Issues.Add(new(code, reason, IntakeEvidenceSource.DocumentContent));
        return ReadOutcome.Readable;
    }

    private static string SanitizeText(string value) =>
        TextSanitation.ReplaceLoneSurrogates(value, out _);
}
