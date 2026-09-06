using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MimeKit;
using MimeKit.Text;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Email;

internal sealed class GraphStaffMailSender(
    GraphMailClient client,
    HttpClient httpClient,
    IStaffMailUploadProgress uploadProgress) : IStaffMailTransport
{
    private const int SmallAttachmentLimit = 3 * 1024 * 1024;
    private const int UploadChunkSize = 10 * 320 * 1024;

    public async Task ValidateEncodedSizeAsync(
        ApprovedStaffSendMailbox mailbox, StaffMailOperation operation,
        StaffMailSendCommand command,
        IReadOnlyList<StaffMailAttachmentContent> attachments, CancellationToken cancellationToken)
    {
        var message = BuildMimeMessage(operation, command);
        if (attachments.Count > 0)
        {
            var multipart = new Multipart("mixed") { message.Body! };
            multipart.ContentType.Boundary = $"pegasus-{operation.Id:N}";
            foreach (var attachment in attachments)
            {
                if (!attachment.Content.CanSeek)
                    throw new InvalidOperationException("Staff mail attachment streams must be seekable for exact size validation.");
                attachment.Content.Position = 0;
                multipart.Add(new MimePart(ContentType.Parse(attachment.Attachment.MediaType))
                {
                    Content = new MimeContent(attachment.Content),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = attachment.Attachment.FileName
                });
            }
            message.Body = multipart;
        }
        await using var counter = new CountingWriteStream(mailbox.EncodedMessageSizeLimit);
        try
        {
            await message.WriteToAsync(counter, cancellationToken);
        }
        finally
        {
            foreach (var attachment in attachments)
                attachment.Content.Position = 0;
        }
    }

    public async Task<StaffMailDraftLookupResult> FindDraftAsync(
        ApprovedStaffSendMailbox mailbox, StaffMailOperation operation,
        CancellationToken cancellationToken)
    {
        if (operation.DraftImmutableId is { Length: > 0 } recordedDraftId)
            return await ReadRecordedDraftAsync(mailbox, operation, recordedDraftId, cancellationToken);
        if (operation.AttemptRequestedAtUtc is not { } requestedAtUtc)
            throw new InvalidDataException("The draft creation attempt time is unavailable.");
        var cursor = ParseCursor(operation.ReconciliationContinuation, requestedAtUtc);
        System.Uri? uri = cursor.NextLink is null
            ? BuildMailboxUri(mailbox,
                "mailFolders/drafts/messages?$filter="
                + Uri.EscapeDataString($"createdDateTime ge {cursor.WindowStartUtc:O} and createdDateTime le {cursor.WindowEndUtc:O}")
                + "&$select=id,internetMessageHeaders,createdDateTime&$top=50")
            : ValidateContinuation(mailbox, cursor.NextLink);
        var matchedId = cursor.MatchedImmutableId;
        for (var page = 0; uri is not null && page < 10; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("Prefer", "IdType=\"ImmutableId\"");
            using var response = await client.SendStaffAsync(request, cancellationToken);
            RequireSuccess(response);
            using var document = JsonDocument.Parse(await ReadBoundedAsync(
                response.Content, 2 * 1024 * 1024, cancellationToken));
            foreach (var value in document.RootElement.GetProperty("value").EnumerateArray())
            {
                if (value.TryGetProperty("internetMessageHeaders", out var headers)
                    && HasHeader(headers, StaffMailCorrelationHeaders.OperationId, operation.Id.ToString("D"))
                    && HasHeader(headers, StaffMailCorrelationHeaders.MailboxId, mailbox.Id.ToString("D"))
                    && HasHeader(headers, StaffMailCorrelationHeaders.MailboxGeneration, operation.MailboxGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    && HasHeader(headers, StaffMailCorrelationHeaders.PayloadSha256, operation.PayloadHash))
                {
                    var candidateId = value.GetProperty("id").GetString()
                        ?? throw new InvalidDataException("Microsoft Graph returned a draft without an identifier.");
                    if (matchedId is not null && !string.Equals(matchedId, candidateId, StringComparison.Ordinal))
                        throw new InvalidDataException("Draft reconciliation found multiple matching drafts.");
                    matchedId = candidateId;
                }
            }
            uri = document.RootElement.TryGetProperty("@odata.nextLink", out var next)
                ? ValidateContinuation(mailbox, next.GetString()
                    ?? throw new InvalidDataException("Microsoft Graph returned an invalid draft continuation."))
                : null;
        }
        if (uri is not null)
        {
            return new(null, JsonSerializer.Serialize(new DraftCursor(
                uri.AbsoluteUri, matchedId, cursor.WindowStartUtc, cursor.WindowEndUtc)), false);
        }
        return new(matchedId is null ? null : new(matchedId), null, true);
    }

    public async Task<StaffMailDraftResult> CreateDraftAsync(
        ApprovedStaffSendMailbox mailbox, StaffMailOperation operation,
        StaffMailSendCommand command,
        CancellationToken cancellationToken)
    {
        var path = command.ComposeMode switch
        {
            StaffMailComposeMode.New => "messages",
            StaffMailComposeMode.Reply => $"messages/{Escape(command.OriginalMessage!.ImmutableMessageId)}/createReply",
            StaffMailComposeMode.ReplyAll => $"messages/{Escape(command.OriginalMessage!.ImmutableMessageId)}/createReplyAll",
            StaffMailComposeMode.Forward => $"messages/{Escape(command.OriginalMessage!.ImmutableMessageId)}/createForward",
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
        var message = BuildMimeMessage(operation, command);
        await using var mime = new MemoryStream();
        await message.WriteToAsync(mime, cancellationToken);
        var payload = Convert.ToBase64String(mime.ToArray());
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildMailboxUri(mailbox, path))
        {
            Content = new StringContent(payload, System.Text.Encoding.ASCII, "text/plain")
        };
        request.Headers.TryAddWithoutValidation("Prefer", "IdType=\"ImmutableId\"");
        using var response = await client.SendStaffAsync(request, cancellationToken);
        RequireSuccess(response);
        using var document = JsonDocument.Parse(await ReadBoundedAsync(
            response.Content, 64 * 1024, cancellationToken));
        return new(document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidDataException("Microsoft Graph returned a draft without an identifier."));
    }

    private static MimeMessage BuildMimeMessage(
        StaffMailOperation operation, StaffMailSendCommand command)
    {
        var message = new MimeMessage
        {
            Subject = command.Subject,
            Date = operation.PreparedAtUtc,
            MessageId = $"{operation.Id:N}@pegasus.invalid"
        };
        foreach (var recipient in command.To)
        {
            message.To.Add(new MailboxAddress(recipient.DisplayName ?? string.Empty, recipient.Address));
        }
        foreach (var recipient in command.Cc)
        {
            message.Cc.Add(new MailboxAddress(recipient.DisplayName ?? string.Empty, recipient.Address));
        }
        if (operation.Id != Guid.Empty)
        {
            message.Headers.Add(StaffMailCorrelationHeaders.OperationId, operation.Id.ToString("D"));
            message.Headers.Add(StaffMailCorrelationHeaders.MailboxId, command.ApprovedMailboxId.ToString("D"));
            message.Headers.Add(StaffMailCorrelationHeaders.MailboxGeneration,
                command.ExpectedMailboxGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture));
            message.Headers.Add(StaffMailCorrelationHeaders.PayloadSha256, operation.PayloadHash);
        }
        if (command.OriginalMessage?.InternetMessageId is { Length: > 0 } replyTo)
        {
            message.InReplyTo = replyTo;
            message.References.Add(replyTo);
        }
        message.Body = new TextPart(TextFormat.Plain) { Text = command.Body };
        return message;
    }

    private static bool HasHeader(JsonElement headers, string name, string expected) =>
        headers.EnumerateArray().Any(header =>
            string.Equals(header.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(header.GetProperty("value").GetString(), expected, StringComparison.Ordinal));

    private static DraftCursor ParseCursor(string? serialized, DateTimeOffset requestedAtUtc)
    {
        var expectedStart = requestedAtUtc.AddMinutes(-5);
        var expectedEnd = requestedAtUtc.AddHours(1);
        if (serialized is null)
            return new(null, null, expectedStart, expectedEnd);
        if (serialized.Length > 16 * 1024)
            throw new InvalidDataException("The draft reconciliation continuation is invalid.");
        try
        {
            var cursor = JsonSerializer.Deserialize<DraftCursor>(serialized)
                ?? throw new InvalidDataException("The draft reconciliation continuation is invalid.");
            if (cursor.WindowStartUtc != expectedStart || cursor.WindowEndUtc != expectedEnd
                || string.IsNullOrWhiteSpace(cursor.NextLink)
                || cursor.MatchedImmutableId is { Length: 0 })
                throw new InvalidDataException("The draft reconciliation continuation is invalid.");
            return cursor;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The draft reconciliation continuation is invalid.", exception);
        }
    }

    private Uri ValidateContinuation(ApprovedStaffSendMailbox mailbox, string continuation)
    {
        if (!Uri.TryCreate(continuation, UriKind.Absolute, out var candidate))
            throw new InvalidDataException("The draft reconciliation continuation is invalid.");
        var expected = BuildMailboxUri(mailbox, "mailFolders/drafts/messages");
        if (!string.Equals(candidate.Scheme, expected.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(candidate.Host, expected.Host, StringComparison.OrdinalIgnoreCase)
            || candidate.Port != expected.Port
            || !string.Equals(candidate.AbsolutePath, expected.AbsolutePath, StringComparison.Ordinal))
            throw new InvalidDataException("The draft reconciliation continuation is outside the approved mailbox.");
        return candidate;
    }

    private async Task<StaffMailDraftLookupResult> ReadRecordedDraftAsync(
        ApprovedStaffSendMailbox mailbox, StaffMailOperation operation,
        string recordedDraftId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            BuildMailboxUri(mailbox, $"messages/{Escape(recordedDraftId)}?$select=id,isDraft,internetMessageHeaders"));
        request.Headers.TryAddWithoutValidation("Prefer", "IdType=\"ImmutableId\"");
        using var response = await client.SendStaffAsync(request, cancellationToken);
        RequireSuccess(response);
        using var document = JsonDocument.Parse(await ReadBoundedAsync(
            response.Content, 256 * 1024, cancellationToken));
        var root = document.RootElement;
        if (!string.Equals(root.GetProperty("id").GetString(), recordedDraftId, StringComparison.Ordinal)
            || !root.TryGetProperty("isDraft", out var isDraft) || !isDraft.GetBoolean()
            || !root.TryGetProperty("internetMessageHeaders", out var headers)
            || !HasHeader(headers, StaffMailCorrelationHeaders.OperationId, operation.Id.ToString("D"))
            || !HasHeader(headers, StaffMailCorrelationHeaders.MailboxId, mailbox.Id.ToString("D"))
            || !HasHeader(headers, StaffMailCorrelationHeaders.MailboxGeneration,
                operation.MailboxGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture))
            || !HasHeader(headers, StaffMailCorrelationHeaders.PayloadSha256, operation.PayloadHash))
            throw new InvalidDataException("The recorded Graph draft does not match the frozen staff mail operation.");
        return new(new(recordedDraftId), null, true);
    }

    private sealed record DraftCursor(
        string? NextLink, string? MatchedImmutableId,
        DateTimeOffset WindowStartUtc, DateTimeOffset WindowEndUtc);

    public async Task AttachAsync(
        ApprovedStaffSendMailbox mailbox, Guid operationId, string immutableDraftId,
        StaffMailAttachment attachment, Stream content, CancellationToken cancellationToken)
    {
        var recorded = await uploadProgress.GetAsync(operationId, attachment.VersionId, cancellationToken);
        if (recorded?.Completed == true)
        {
            return;
        }
        if (recorded is not null && recorded.UploadUrl is null)
        {
            if (await HasExactAttachmentAsync(mailbox, immutableDraftId, attachment, cancellationToken))
            {
                await uploadProgress.CompleteAsync(operationId, attachment.VersionId, cancellationToken);
                return;
            }
            throw new InvalidOperationException(
                "A pending attachment write is not yet visible for read-only reconciliation.");
        }
        if (recorded is not null
            && await HasExactAttachmentAsync(mailbox, immutableDraftId, attachment, cancellationToken))
        {
            await uploadProgress.CompleteAsync(operationId, attachment.VersionId, cancellationToken);
            return;
        }
        if (attachment.ContentLength <= SmallAttachmentLimit)
        {
            if (recorded is null)
            {
                await uploadProgress.SaveAsync(operationId, attachment.VersionId,
                    new(null, DateTimeOffset.MaxValue, 0), cancellationToken);
            }
            using var memory = new MemoryStream();
            await content.CopyToAsync(memory, cancellationToken);
            using var request = new HttpRequestMessage(
                HttpMethod.Post, BuildMailboxUri(mailbox, $"messages/{Escape(immutableDraftId)}/attachments"))
            {
                Content = JsonContent.Create(new Dictionary<string, object?>
                {
                    ["@odata.type"] = "#microsoft.graph.fileAttachment",
                    ["name"] = attachment.FileName,
                    ["contentType"] = attachment.MediaType,
                    ["contentBytes"] = Convert.ToBase64String(memory.ToArray())
                })
            };
            using var response = await client.SendStaffAsync(request, cancellationToken);
            RequireSuccess(response);
            await uploadProgress.CompleteAsync(operationId, attachment.VersionId, cancellationToken);
            return;
        }

        var progress = recorded;
        var uploadUrl = progress?.UploadUrl;
        if (uploadUrl is null || progress!.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            using var create = new HttpRequestMessage(
                HttpMethod.Post,
                BuildMailboxUri(mailbox, $"messages/{Escape(immutableDraftId)}/attachments/createUploadSession"))
            {
                Content = JsonContent.Create(new
                {
                    AttachmentItem = new
                    {
                        attachmentType = "file",
                        name = attachment.FileName,
                        size = attachment.ContentLength,
                        contentType = attachment.MediaType
                    }
                })
            };
            using var created = await client.SendStaffAsync(create, cancellationToken);
            RequireSuccess(created);
            using var document = JsonDocument.Parse(await ReadBoundedAsync(
                created.Content, 64 * 1024, cancellationToken));
            uploadUrl = new Uri(document.RootElement.GetProperty("uploadUrl").GetString()
                ?? throw new InvalidDataException("Microsoft Graph returned no attachment upload URL."));
            if (uploadUrl.Scheme != System.Uri.UriSchemeHttps
                || !(uploadUrl.Host.Equals("graph.microsoft.com", StringComparison.OrdinalIgnoreCase)
                    || uploadUrl.Host.EndsWith(".outlook.com", StringComparison.OrdinalIgnoreCase)))
            {
                throw new UnauthorizedAccessException("Microsoft Graph returned an untrusted attachment upload URL.");
            }
            var expiry = document.RootElement.GetProperty("expirationDateTime").GetDateTimeOffset();
            progress = new(uploadUrl, expiry, 0);
            await uploadProgress.SaveAsync(operationId, attachment.VersionId, progress, cancellationToken);
        }
        var activeProgress = progress
            ?? throw new InvalidDataException("The attachment upload progress was not recorded.");
        if (activeProgress.UploadUrl is not null && activeProgress.NextOffset > 0)
        {
            activeProgress = await ReconcileUploadOffsetAsync(
                operationId, attachment.VersionId, activeProgress, attachment.ContentLength,
                cancellationToken);
        }
        if (content.CanSeek)
        {
            content.Position = activeProgress.NextOffset;
        }
        else if (activeProgress.NextOffset != 0)
        {
            throw new InvalidOperationException("The attachment stream cannot resume at the recorded upload offset.");
        }
        var buffer = new byte[UploadChunkSize];
        var offset = activeProgress.NextOffset;
        while (offset < attachment.ContentLength)
        {
            var count = await content.ReadAtLeastAsync(
                buffer, (int)Math.Min(buffer.Length, attachment.ContentLength - offset), false, cancellationToken);
            using var chunk = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
            {
                Content = new ByteArrayContent(buffer, 0, count)
            };
            chunk.Content.Headers.ContentRange = new ContentRangeHeaderValue(
                offset, offset + count - 1, attachment.ContentLength);
            using var response = await httpClient.SendAsync(chunk, cancellationToken);
            RequireSuccess(response);
            offset += count;
            await uploadProgress.SaveAsync(
                operationId, attachment.VersionId, activeProgress with { NextOffset = offset }, cancellationToken);
        }
        await uploadProgress.CompleteAsync(operationId, attachment.VersionId, cancellationToken);
    }

    private async Task<StaffMailUploadSession> ReconcileUploadOffsetAsync(
        Guid operationId, Guid attachmentVersionId, StaffMailUploadSession progress,
        long contentLength, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, progress.UploadUrl), cancellationToken);
        RequireSuccess(response);
        using var document = JsonDocument.Parse(await ReadBoundedAsync(
            response.Content, 64 * 1024, cancellationToken));
        var ranges = document.RootElement.GetProperty("nextExpectedRanges");
        var first = ranges.EnumerateArray().Select(value => value.GetString())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? throw new InvalidDataException("Microsoft Graph returned no next attachment range.");
        var separator = first.IndexOf('-');
        if (separator <= 0 || !long.TryParse(first.AsSpan(0, separator), out var next)
            || next < 0 || next > contentLength)
        {
            throw new InvalidDataException("Microsoft Graph returned an invalid next attachment range.");
        }
        var reconciled = progress with { NextOffset = next };
        await uploadProgress.SaveAsync(
            operationId, attachmentVersionId, reconciled, cancellationToken);
        return reconciled;
    }

    private async Task<bool> HasExactAttachmentAsync(
        ApprovedStaffSendMailbox mailbox, string immutableDraftId,
        StaffMailAttachment expected, CancellationToken cancellationToken)
    {
        System.Uri? uri = BuildMailboxUri(mailbox,
            $"messages/{Escape(immutableDraftId)}/attachments?$select=name,size,contentType,contentBytes&$top=25");
        for (var page = 0; uri is not null && page < 10; page++)
        {
            using var response = await client.SendStaffAsync(new(HttpMethod.Get, uri), cancellationToken);
            RequireSuccess(response);
            var responseLimit = checked(expected.ContentLength * 2 + 1024 * 1024);
            using var document = JsonDocument.Parse(await ReadBoundedAsync(
                response.Content, responseLimit, cancellationToken));
            foreach (var value in document.RootElement.GetProperty("value").EnumerateArray())
            {
                if (!string.Equals(value.GetProperty("name").GetString(), expected.FileName,
                        StringComparison.Ordinal)
                    || value.GetProperty("size").GetInt64() != expected.ContentLength
                    || !string.Equals(value.GetProperty("contentType").GetString(), expected.MediaType,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!value.TryGetProperty("contentBytes", out var encoded)
                    || encoded.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException(
                        "An attachment with matching metadata cannot be verified without its content.");
                }
                var bytes = Convert.FromBase64String(encoded.GetString()!);
                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                if (string.Equals(hash, expected.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                throw new InvalidDataException(
                    "An attachment with matching metadata has different content.");
            }
            uri = document.RootElement.TryGetProperty("@odata.nextLink", out var next)
                ? new System.Uri(next.GetString()
                    ?? throw new InvalidDataException("Microsoft Graph returned an invalid attachment continuation."))
                : null;
        }
        if (uri is not null)
        {
            throw new InvalidDataException("Attachment reconciliation exceeded its bounded page limit.");
        }
        return false;
    }

    public async Task<StaffMailSubmitResult> SendDraftAsync(
        ApprovedStaffSendMailbox mailbox, string immutableDraftId, CancellationToken cancellationToken)
    {
        using var response = await client.SendStaffAsync(
            new(HttpMethod.Post, BuildMailboxUri(mailbox, $"messages/{Escape(immutableDraftId)}/send")),
            cancellationToken);
        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            throw new HttpRequestException("Microsoft Graph did not accept the draft send.");
        }
        return new(DateTimeOffset.UtcNow);
    }

    private System.Uri BuildMailboxUri(ApprovedStaffSendMailbox mailbox, string path) =>
        client.CreateStaffUri(mailbox.GraphMailboxId, path);

    private static string Escape(string value) => Uri.EscapeDataString(value);
    private static void RequireSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        if ((int)response.StatusCode is >= 400 and < 500
            && response.StatusCode is not HttpStatusCode.RequestTimeout
            and not HttpStatusCode.TooManyRequests)
        {
            throw new StaffMailTransportRejectedException(
                $"graph_rejected_{(int)response.StatusCode}");
        }
        throw new HttpRequestException(
            $"Microsoft Graph staff-mail request failed with status {(int)response.StatusCode}.",
            null,
            response.StatusCode);
    }

    private static async Task<byte[]> ReadBoundedAsync(
        HttpContent content, long maximumBytes, CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            total = checked(total + read);
            if (total > maximumBytes)
                throw new InvalidDataException("Microsoft Graph returned an oversized staff-mail response.");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return destination.ToArray();
    }
}

internal sealed class CountingWriteStream(long limit) : Stream
{
    private long count;
    public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
    public override long Length => count; public override long Position { get => count; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public override void Write(byte[] buffer, int offset, int count) => Add(count);
    public override void Write(ReadOnlySpan<byte> buffer) => Add(buffer.Length);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) { Add(buffer.Length); return ValueTask.CompletedTask; }
    private void Add(int value)
    {
        count = checked(count + value);
        if (count > limit) throw new InvalidDataException("The encoded message exceeds the approved mailbox message-size limit.");
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}

internal sealed record StaffMailUploadSession(
    Uri? UploadUrl, DateTimeOffset ExpiresAtUtc, long NextOffset, bool Completed = false);

internal interface IStaffMailUploadProgress
{
    Task<StaffMailUploadSession?> GetAsync(Guid operationId, Guid attachmentVersionId,
        CancellationToken cancellationToken);
    Task SaveAsync(Guid operationId, Guid attachmentVersionId, StaffMailUploadSession session,
        CancellationToken cancellationToken);
    Task CompleteAsync(Guid operationId, Guid attachmentVersionId, CancellationToken cancellationToken);
}
