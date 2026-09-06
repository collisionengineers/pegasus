using System.Net;
using System.Text.RegularExpressions;
using MimeKit;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Intake;

/// <summary>
/// The decoded display view of one message.
/// </summary>
/// <remarks>
/// The first six members are the formatted header lines the desktop evaluation
/// tool shows. The structured members below them are the same facts in the shape
/// the retained-mail read model stores: one sender split into address and display
/// name, recipients as addresses rather than a rendered string, and attachments
/// with their decoded length.
/// </remarks>
public sealed record LocalEmailDisplay(
    string From,
    string To,
    string Cc,
    string SentAt,
    string Subject,
    string Body,
    IReadOnlyList<string> AttachmentNames,
    IReadOnlyList<string> ReplyToAddresses,
    string? SenderAddress = null,
    string? SenderDisplayName = null,
    IReadOnlyList<string>? ToAddresses = null,
    IReadOnlyList<string>? CcAddresses = null,
    IReadOnlyList<RetainedMailboxAttachment>? Attachments = null,
    string? MessageIdentity = null,
    string? ThreadIdentity = null);

/// <summary>
/// Reads only decoded message display data. It never renders HTML or resolves resources.
/// </summary>
public static partial class LocalEmailDisplayReader
{
    public static async Task<LocalEmailDisplay> ReadAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var message = await MimeMessage.LoadAsync(source, cancellationToken);
        var body = message.TextBody;
        if (string.IsNullOrWhiteSpace(body) && !string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            body = ToInertText(message.HtmlBody);
        }

        // A staff forward that carries the provider's original as an attached
        // message/rfc822 part: surface that original's body as the focus rather
        // than the forwarder's wrapper, which is often only a blank line and the
        // Collision Engineers signature.
        var attachedOriginal = FindAttachedOriginal(message);
        // Only treat an attached message/rfc822 as a forward to surface when the
        // subject marks it as a forward, so an email that merely attaches a prior
        // .eml as evidence keeps its own top-level body.
        var isStaffForward = attachedOriginal is not null && IsForwardSubject(message.Subject);
        if (isStaffForward && attachedOriginal is not null)
        {
            var originalBody = attachedOriginal.TextBody;
            if (string.IsNullOrWhiteSpace(originalBody)
                && !string.IsNullOrWhiteSpace(attachedOriginal.HtmlBody))
            {
                originalBody = ToInertText(attachedOriginal.HtmlBody);
            }

            if (!string.IsNullOrWhiteSpace(originalBody))
            {
                body = originalBody;
            }
        }

        body = StaffForwardBodyCleaner.Clean(body ?? string.Empty, isStaffForward);

        var sender = message.From.Mailboxes.FirstOrDefault();
        var replyToAddresses = Addresses(
            message.ReplyTo.Count > 0 ? message.ReplyTo : message.From);
        var attachments = Attachments(message);
        return new LocalEmailDisplay(
            message.From.ToString(),
            message.To.ToString(),
            message.Cc.ToString(),
            message.Date == DateTimeOffset.MinValue ? string.Empty : message.Date.ToString("u"),
            message.Subject ?? string.Empty,
            body ?? string.Empty,
            attachments.Select(item => item.FileName).ToArray(),
            replyToAddresses,
            sender?.Address,
            string.IsNullOrWhiteSpace(sender?.Name) ? null : sender!.Name,
            Addresses(message.To),
            Addresses(message.Cc),
            attachments,
            NullIfBlank(message.MessageId),
            // The conversation this message belongs to, as the MIME can express it:
            // the root of its References chain, or itself where it starts one.
            NullIfBlank(message.References.FirstOrDefault()) ?? NullIfBlank(message.MessageId));
    }

    private static string[] Addresses(InternetAddressList list) =>
        list.Mailboxes
            .Select(mailbox => mailbox.Address)
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .ToArray();

    /// <summary>
    /// The first attached original message (a <c>message/rfc822</c> part) in the
    /// body tree, if any. This is the provider's original when a staff member
    /// forwards it as an attachment rather than inline.
    /// </summary>
    private static MimeMessage? FindAttachedOriginal(MimeMessage message)
    {
        foreach (var entity in EnumerateEntities(message.Body))
        {
            if (entity is MessagePart { Message: { } nested })
            {
                return nested;
            }
        }

        return null;
    }

    private static IEnumerable<MimeEntity> EnumerateEntities(MimeEntity? entity)
    {
        if (entity is null)
        {
            yield break;
        }

        yield return entity;
        if (entity is Multipart multipart)
        {
            foreach (var child in multipart)
            {
                foreach (var descendant in EnumerateEntities(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    /// <summary>
    /// Attachment names, media types and decoded lengths. The length is measured by
    /// decoding the part, because the encoded size a transfer encoding produces is
    /// not the size of the file an operator would receive.
    /// </summary>
    private static List<RetainedMailboxAttachment> Attachments(MimeMessage message)
    {
        var attachments = new List<RetainedMailboxAttachment>();
        foreach (var part in message.Attachments)
        {
            var fileName = part.ContentDisposition?.FileName ?? part.ContentType.Name;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"Unnamed attachment {attachments.Count + 1}";
            }

            attachments.Add(new(
                fileName,
                part.ContentType.MimeType,
                MeasureDecodedLength(part)));
        }

        return attachments;
    }

    private static long MeasureDecodedLength(MimeEntity entity)
    {
        using var counter = new CountingStream();
        switch (entity)
        {
            case MimePart part:
                part.Content?.DecodeTo(counter);
                break;
            case MessagePart rfc822:
                rfc822.Message?.WriteTo(counter);
                break;
            default:
                entity.WriteTo(counter);
                break;
        }

        return counter.Length;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool IsForwardSubject(string? subject)
    {
        if (subject is null)
        {
            return false;
        }

        var trimmed = subject.TrimStart();
        return trimmed.StartsWith("fw:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("fwd:", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToInertText(string html) =>
        WebUtility.HtmlDecode(HtmlTagRegex().Replace(html, " "))
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    /// <summary>
    /// Counts bytes without keeping them: an attachment's size is wanted, its
    /// content is not.
    /// </summary>
    private sealed class CountingStream : Stream
    {
        private long length;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => length;

        public override long Position
        {
            get => length;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => length += count;

        public override void Write(ReadOnlySpan<byte> buffer) => length += buffer.Length;

        public override void WriteByte(byte value) => length++;
    }
}
