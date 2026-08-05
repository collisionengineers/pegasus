using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Core;
using MimeKit;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Email;

public sealed record GraphApprovedMailboxOptions(
    Uri BaseUri,
    string MailboxId,
    string MailboxAddress,
    string InboxFolderId,
    string SentFolderId) : IApprovedInboxSourceSettings, IApprovedSentSourceSettings
{
    string IApprovedSentSourceSettings.SentFolderIdentity => SentFolderId;
    string IApprovedInboxSourceSettings.InboxFolderIdentity => InboxFolderId;
    public static GraphApprovedMailboxOptions Create(
        string? baseUri,
        string? mailboxId,
        string? mailboxAddress,
        string? inboxFolderId,
        string? sentFolderId)
    {
        if (!Uri.TryCreate(baseUri, UriKind.Absolute, out var parsedBaseUri)
            || parsedBaseUri.Scheme != Uri.UriSchemeHttps
            || !parsedBaseUri.Host.Equals("graph.microsoft.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Graph:BaseUri must be the Microsoft Graph HTTPS endpoint.");
        }

        return new(
            EnsureTrailingSlash(parsedBaseUri),
            Require(mailboxId, "Graph:MailboxId", 200),
            ApprovedMailboxAddress.Normalize(Require(mailboxAddress, "Graph:MailboxAddress", 320)),
            Require(inboxFolderId, "Graph:InboxFolderId", 500),
            Require(sentFolderId, "Graph:SentFolderId", 500));
    }

    private static Uri EnsureTrailingSlash(Uri value) =>
        value.AbsoluteUri.EndsWith('/')
            ? value
            : new Uri($"{value.AbsoluteUri}/", UriKind.Absolute);

    private static string Require(string? value, string key, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Trim().Length > maximumLength
            || value.Any(char.IsControl))
        {
            throw new InvalidOperationException($"{key} is required and must be a valid exact identity.");
        }
        return value.Trim();
    }
}

/// <summary>
/// A mailbox-agnostic Graph reader. The mailbox and folder are passed on every call
/// rather than closed over, because the approved estate — not deployment
/// configuration — now decides which mailboxes a tick reads.
/// </summary>
internal sealed class GraphMailClient(
    TokenCredential credential,
    Uri baseUri,
    HttpClient httpClient)
{
    private static readonly TokenRequestContext TokenContext =
        new(["https://graph.microsoft.com/.default"]);

    public Uri InitialDeltaUri(string mailboxId, string folderId, int maximumItems) => new(
        baseUri,
        $"users/{Uri.EscapeDataString(mailboxId)}/mailFolders/{Uri.EscapeDataString(folderId)}" +
        // isRead is selected, never written: the workspace shows the retained read
        // state and this application never changes it. Only the query string grows,
        // and ValidateDeltaUri compares the path alone, so every existing cursor
        // still validates.
        $"/messages/delta?$select=id,parentFolderId,receivedDateTime,sentDateTime,conversationId,internetMessageId,isRead&$top={maximumItems}");

    public async Task<GraphDeltaPage> ReadDeltaAsync(
        Uri uri,
        string mailboxId,
        string approvedFolderId,
        CancellationToken cancellationToken)
    {
        ValidateDeltaUri(uri, mailboxId, approvedFolderId);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Prefer", "IdType=\"ImmutableId\"");
        using var response = await SendAsync(request, cancellationToken);
        await ThrowForFailureAsync(response, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var values = root.GetProperty("value")
            .EnumerateArray()
            .Select(ParseItem)
            .ToArray();
        foreach (var item in values.Where(item => !item.Removed))
        {
            if (!string.Equals(item.ParentFolderId, approvedFolderId, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException(
                    "Microsoft Graph returned a message outside the exact approved mailbox folder.");
            }
        }
        var next = ReadLink(root, "@odata.nextLink", mailboxId, approvedFolderId);
        var delta = ReadLink(root, "@odata.deltaLink", mailboxId, approvedFolderId);
        if (next is null && delta is null)
        {
            throw new InvalidDataException("Microsoft Graph returned no next or delta cursor.");
        }
        return new(values, next ?? delta!);
    }

    public async Task<byte[]> ReadMimeAsync(
        string mailboxId,
        string immutableMessageId,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(
            baseUri,
            $"users/{Uri.EscapeDataString(mailboxId)}/messages/{Uri.EscapeDataString(immutableMessageId)}/$value");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Prefer", "IdType=\"ImmutableId\"");
        using var response = await SendAsync(request, cancellationToken);
        await ThrowForFailureAsync(response, cancellationToken);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(TokenContext, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static async Task ThrowForFailureAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(30);
            throw new ApprovedSentSourceThrottledException(retryAfter > TimeSpan.Zero
                ? retryAfter
                : TimeSpan.FromSeconds(30));
        }
        if (response.StatusCode == HttpStatusCode.Gone)
        {
            throw new GraphDeltaResetRequiredException();
        }
        // A tenant that has not admitted this application to this mailbox answers 401 or
        // 403 for that mailbox alone. Naming it separately is what lets the
        // administration surface say "the tenant has not granted access" rather than
        // reporting an indistinguishable transport failure.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ApprovedMailboxAccessDeniedException(
                $"Microsoft Graph refused access to the mailbox with {(int)response.StatusCode}.");
        }
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Microsoft Graph returned {(int)response.StatusCode}; response length {body.Length}.",
            inner: null,
            response.StatusCode);
    }

    private Uri? ReadLink(
        JsonElement root,
        string property,
        string mailboxIdentity,
        string approvedFolderId)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        var uri = new Uri(value.GetString()!, UriKind.Absolute);
        ValidateDeltaUri(uri, mailboxIdentity, approvedFolderId);
        return uri;
    }

    public void ValidateDeltaUri(Uri uri, string mailboxIdentity, string approvedFolderId)
    {
        var mailboxId = Uri.EscapeDataString(mailboxIdentity);
        var folderId = Uri.EscapeDataString(approvedFolderId);
        var approvedPaths = new[]
        {
            InitialDeltaUri(mailboxIdentity, approvedFolderId, 1),
            new Uri(
                baseUri,
                $"users/{mailboxId}/mailfolders('{folderId}')/messages/delta"),
            new Uri(
                baseUri,
                $"users/{mailboxId}/mailFolders('{folderId}')/messages/delta"),
            new Uri(
                baseUri,
                $"users('{mailboxId}')/mailfolders('{folderId}')/messages/delta"),
            new Uri(
                baseUri,
                $"users('{mailboxId}')/mailFolders('{folderId}')/messages/delta")
        }.Select(value => value.GetComponents(UriComponents.Path, UriFormat.Unescaped));
        var actualPath = uri.GetComponents(UriComponents.Path, UriFormat.Unescaped);
        if (uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase)
            || !approvedPaths.Contains(actualPath, StringComparer.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "The Microsoft Graph cursor escaped the exact approved mailbox folder.");
        }
    }

    private static GraphDeltaItem ParseItem(JsonElement value)
    {
        var removed = value.TryGetProperty("@removed", out _);
        return new(
            RequiredString(value, "id"),
            OptionalString(value, "parentFolderId"),
            OptionalInstant(value, "receivedDateTime"),
            OptionalInstant(value, "sentDateTime"),
            OptionalString(value, "conversationId"),
            OptionalString(value, "internetMessageId"),
            OptionalBoolean(value, "isRead"),
            removed);
    }

    private static bool OptionalBoolean(JsonElement value, string property) =>
        value.TryGetProperty(property, out var result)
        && result.ValueKind == JsonValueKind.True;

    private static string RequiredString(JsonElement value, string property) =>
        OptionalString(value, property)
        ?? throw new InvalidDataException($"Microsoft Graph omitted {property}.");

    private static string? OptionalString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var result) && result.ValueKind == JsonValueKind.String
            ? result.GetString()
            : null;

    private static DateTimeOffset? OptionalInstant(JsonElement value, string property) =>
        OptionalString(value, property) is { } text
        && DateTimeOffset.TryParse(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var instant)
            ? instant.ToUniversalTime()
            : null;
}

/// <summary>
/// Stateless with respect to the mailbox: every identity it uses comes from the lease
/// the Core poll handed it, which the approved estate produced. The exact-folder
/// guarantee is unchanged — it is still enforced on every cursor and every item — but
/// the folder it is enforced against is now per lease.
/// </summary>
internal sealed class GraphApprovedInboxSource(GraphMailClient client) : IApprovedInboxSource
{
    private const int MaximumMailboxIdentityLength = 100;
    private const int MaximumFolderIdentityLength = 200;

    public async Task<ApprovedInboxPage> ReadAsync(
        ApprovedInboxPollLease lease,
        int maximumMessages,
        CancellationToken cancellationToken)
    {
        ValidateLease(lease);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMessages);
        var mailboxId = lease.MailboxId;
        var inboxFolderId = lease.InboxFolderIdentity;
        var cursor = GraphCursor.Parse(
            lease.Cursor,
            client.InitialDeltaUri(mailboxId, inboxFolderId, maximumMessages));
        GraphDeltaPage page;
        try
        {
            page = await client.ReadDeltaAsync(cursor.PageUri, mailboxId, inboxFolderId, cancellationToken);
        }
        catch (GraphDeltaResetRequiredException)
        {
            cursor = GraphCursor.Parse(
                null,
                client.InitialDeltaUri(mailboxId, inboxFolderId, maximumMessages));
            page = await client.ReadDeltaAsync(cursor.PageUri, mailboxId, inboxFolderId, cancellationToken);
        }
        var available = page.Items.Skip(cursor.SkipCount).Take(maximumMessages).ToArray();
        var messages = new List<ApprovedInboxMessage>(available.Length);
        var processed = cursor.SkipCount;
        foreach (var item in available)
        {
            processed++;
            if (item.Removed)
            {
                continue;
            }
            var mime = await client.ReadMimeAsync(mailboxId, item.Id, cancellationToken);
            var next = GraphCursor.Serialize(
                processed >= page.Items.Count ? page.NextUri : cursor.PageUri,
                processed >= page.Items.Count ? 0 : processed);
            messages.Add(new(
                item.Id,
                $"{SanitizeFileName(item.Id)}.eml",
                mime,
                item.ReceivedAtUtc ?? throw new InvalidDataException("Graph Inbox message omitted receivedDateTime."),
                next)
            {
                RetainedMetadata = await ReadRetainedMetadataAsync(
                    mime,
                    item,
                    inboxFolderId,
                    cancellationToken)
            });
        }
        var consumed = cursor.SkipCount + available.Length;
        var pageCursor = GraphCursor.Serialize(
            consumed >= page.Items.Count ? page.NextUri : cursor.PageUri,
            consumed >= page.Items.Count ? 0 : consumed);
        if (messages.Count > 0)
        {
            messages[^1] = messages[^1] with { NextCursor = pageCursor };
        }
        return new(messages, pageCursor);
    }

    /// <summary>
    /// The display facts of one polled message: the MIME supplies the content,
    /// Graph supplies the identities it is authoritative for.
    /// </summary>
    /// <remarks>
    /// Where the two disagree Graph wins on identity — conversation, internet
    /// message id, folder and read state are the provider's own facts, and a header
    /// a sender wrote is not evidence about them. The body, recipients and
    /// attachments come from the MIME because Graph was never asked for them.
    /// </remarks>
    private static async Task<RetainedMailboxMessageMetadata?> ReadRetainedMetadataAsync(
        byte[] mime,
        GraphDeltaItem item,
        string inboxFolderId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new MemoryStream(mime, writable: false);
            var display = await Pegasus.Infrastructure.Intake.LocalEmailDisplayReader.ReadAsync(
                stream,
                cancellationToken);
            return new(
                inboxFolderId,
                item.ConversationId ?? display.ThreadIdentity,
                item.InternetMessageId ?? display.MessageIdentity,
                display.SenderAddress,
                display.SenderDisplayName,
                display.ToAddresses ?? [],
                display.CcAddresses ?? [],
                string.IsNullOrWhiteSpace(display.Subject) ? null : display.Subject,
                string.IsNullOrWhiteSpace(display.Body) ? null : display.Body,
                display.Attachments ?? [],
                item.IsRead);
        }
        catch (FormatException)
        {
            // The message is still received, retained and processed; only its
            // display view is unavailable, and the workspace shows the gap rather
            // than the poll refusing the message.
            return null;
        }
    }

    /// <summary>
    /// Shape only. Which mailbox is legitimate is settled upstream by the approved
    /// estate and re-asserted inside the claiming transaction; what remains here is
    /// refusing an identity Graph should never be asked for.
    /// </summary>
    private static void ValidateLease(ApprovedInboxPollLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (!IsExactIdentity(lease.MailboxId, MaximumMailboxIdentityLength)
            || !IsExactIdentity(lease.InboxFolderIdentity, MaximumFolderIdentityLength)
            || string.IsNullOrWhiteSpace(lease.MailboxAddress))
        {
            throw new UnauthorizedAccessException(
                "The Inbox lease does not carry an exact Graph mailbox and folder identity.");
        }
    }

    private static bool IsExactIdentity(string value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= maximumLength
        && !value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character));

    private static string SanitizeFileName(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

internal sealed class GraphApprovedSentSource(
    GraphApprovedMailboxOptions options,
    GraphMailClient client) : IApprovedSentSource
{
    public async Task<ApprovedSentPage> ReadAsync(
        ApprovedSentPollLease lease,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        // ValidateLease has already proved the lease equals the configured mailbox and
        // Sent folder, so passing the lease's identities to the client is the same call
        // it made when the client closed over the configuration.
        ValidateLease(lease);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var mailboxId = lease.MailboxId;
        var sentFolderId = lease.SentFolderIdentity;
        var cursor = GraphCursor.Parse(
            lease.Cursor,
            client.InitialDeltaUri(mailboxId, sentFolderId, maximumItems));
        GraphDeltaPage page;
        try
        {
            page = await client.ReadDeltaAsync(cursor.PageUri, mailboxId, sentFolderId, cancellationToken);
        }
        catch (GraphDeltaResetRequiredException)
        {
            cursor = GraphCursor.Parse(
                null,
                client.InitialDeltaUri(mailboxId, sentFolderId, maximumItems));
            page = await client.ReadDeltaAsync(cursor.PageUri, mailboxId, sentFolderId, cancellationToken);
        }
        var available = page.Items.Skip(cursor.SkipCount).Take(maximumItems).ToArray();
        var items = new List<ApprovedSentItem>(available.Length);
        var processed = cursor.SkipCount;
        foreach (var item in available)
        {
            processed++;
            var next = GraphCursor.Serialize(
                processed >= page.Items.Count ? page.NextUri : cursor.PageUri,
                processed >= page.Items.Count ? 0 : processed);
            items.Add(item.Removed
                ? DeletedItem(item, next)
                : await DiscoveredItemAsync(item, next, cancellationToken));
        }
        var consumed = cursor.SkipCount + available.Length;
        var pageCursor = GraphCursor.Serialize(
            consumed >= page.Items.Count ? page.NextUri : cursor.PageUri,
            consumed >= page.Items.Count ? 0 : consumed);
        if (items.Count > 0)
        {
            items[^1] = items[^1] with { NextCursor = pageCursor };
        }
        return new(items, pageCursor, page.Items.Count > consumed);
    }

    private async Task<ApprovedSentItem> DiscoveredItemAsync(
        GraphDeltaItem item,
        string nextCursor,
        CancellationToken cancellationToken)
    {
        var mime = await client.ReadMimeAsync(options.MailboxId, item.Id, cancellationToken);
        var sourceHash = Convert.ToHexString(SHA256.HashData(mime));
        try
        {
            await using var stream = new MemoryStream(mime, writable: false);
            var message = await MimeMessage.LoadAsync(stream, cancellationToken);
            var messageId = item.InternetMessageId ?? message.MessageId;
            var conversationId = item.ConversationId;
            var sentAtUtc = item.SentAtUtc ?? message.Date.ToUniversalTime();
            if (string.IsNullOrWhiteSpace(messageId)
                || string.IsNullOrWhiteSpace(conversationId)
                || sentAtUtc.Offset != TimeSpan.Zero)
            {
                return Malformed(item, sourceHash, "graph_sent_provenance_incomplete", nextCursor);
            }
            var inReplyTo = message.References
                .Append(message.InReplyTo)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var replyChain = inReplyTo.FirstOrDefault() ?? messageId;
            var caseIds = message.Headers
                .Where(header => header.Field.Equals("X-Pegasus-Case-Id", StringComparison.OrdinalIgnoreCase))
                .SelectMany(header => header.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Select(value => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty)
                .Where(value => value != Guid.Empty)
                .Distinct()
                .ToArray();
            return new(
                Occurrence(item.Id),
                sourceHash,
                options.SentFolderId,
                ApprovedSentItemObservationKind.Discovered,
                new(
                    options.MailboxId,
                    options.MailboxAddress,
                    options.SentFolderId,
                    item.Id,
                    messageId,
                    conversationId,
                    replyChain,
                    inReplyTo,
                    caseIds,
                    sentAtUtc,
                    sourceHash),
                null,
                nextCursor);
        }
        catch (FormatException)
        {
            return Malformed(item, sourceHash, "graph_sent_mime_invalid", nextCursor);
        }
    }

    private ApprovedSentItem DeletedItem(GraphDeltaItem item, string nextCursor)
    {
        var sourceHash = Hash($"deleted\n{options.MailboxId}\n{item.Id}");
        return new(
            Occurrence(item.Id),
            sourceHash,
            null,
            ApprovedSentItemObservationKind.Deleted,
            null,
            "graph_sent_deleted_without_retained_mime",
            nextCursor);
    }

    private ApprovedSentItem Malformed(
        GraphDeltaItem item,
        string sourceHash,
        string reason,
        string nextCursor) => new(
            Occurrence(item.Id),
            sourceHash,
            options.SentFolderId,
            ApprovedSentItemObservationKind.Discovered,
            null,
            reason,
            nextCursor);

    private void ValidateLease(ApprovedSentPollLease lease)
    {
        if (!lease.MailboxId.Equals(options.MailboxId, StringComparison.Ordinal)
            || !lease.MailboxAddress.Equals(options.MailboxAddress, StringComparison.OrdinalIgnoreCase)
            || !lease.SentFolderIdentity.Equals(options.SentFolderId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The Sent lease is outside the approved Graph mailbox and folder.");
        }
    }

    private string Occurrence(string id) => Hash($"{options.MailboxId}\n{id}");
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

internal sealed record GraphDeltaPage(IReadOnlyList<GraphDeltaItem> Items, Uri NextUri);
internal sealed class GraphDeltaResetRequiredException : Exception;
internal sealed record GraphDeltaItem(
    string Id,
    string? ParentFolderId,
    DateTimeOffset? ReceivedAtUtc,
    DateTimeOffset? SentAtUtc,
    string? ConversationId,
    string? InternetMessageId,
    bool IsRead,
    bool Removed);

internal sealed record GraphCursor(int Version, Uri PageUri, int SkipCount)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static GraphCursor Parse(string? value, Uri initialUri)
    {
        if (value is null)
        {
            return new(1, initialUri, 0);
        }
        try
        {
            var cursor = JsonSerializer.Deserialize<GraphCursor>(value, JsonOptions);
            if (cursor is null || cursor.Version != 1 || cursor.SkipCount < 0 || !cursor.PageUri.IsAbsoluteUri)
            {
                throw new InvalidDataException("The Microsoft Graph cursor is invalid.");
            }
            return cursor;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The Microsoft Graph cursor is malformed.", exception);
        }
    }

    public static string Serialize(Uri pageUri, int skipCount) =>
        JsonSerializer.Serialize(new GraphCursor(1, pageUri, skipCount), JsonOptions);
}
