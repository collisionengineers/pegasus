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

        var sender = message.From.Mailboxes.FirstOrDefault();
        return new LocalEmailDisplay(
            message.From.ToString(),
            message.To.ToString(),
            message.Cc.ToString(),
            message.Date == DateTimeOffset.MinValue ? string.Empty : message.Date.ToString("u"),
            message.Subject ?? string.Empty,
            body ?? string.Empty,
            message.Attachments
                .Select(part => part.ContentDisposition?.FileName ?? part.ContentType.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToArray(),
            sender?.Address,
            string.IsNullOrWhiteSpace(sender?.Name) ? null : sender!.Name,
            Addresses(message.To),
            Addresses(message.Cc),
            Attachments(message),
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
                continue;
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
