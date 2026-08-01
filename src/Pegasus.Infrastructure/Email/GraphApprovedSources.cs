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
    string SentFolderId) : IApprovedSentSourceSettings
{
    string IApprovedSentSourceSettings.SentFolderIdentity => SentFolderId;
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

internal sealed class GraphMailClient(
    TokenCredential credential,
    GraphApprovedMailboxOptions options,
    HttpClient httpClient)
{
    private static readonly TokenRequestContext TokenContext =
        new(["https://graph.microsoft.com/.default"]);

    public Uri InitialDeltaUri(string folderId, int maximumItems) => new(
        options.BaseUri,
        $"users/{Uri.EscapeDataString(options.MailboxId)}/mailFolders/{Uri.EscapeDataString(folderId)}" +
        $"/messages/delta?$select=id,parentFolderId,receivedDateTime,sentDateTime,conversationId,internetMessageId&$top={maximumItems}");

    public async Task<GraphDeltaPage> ReadDeltaAsync(
        Uri uri,
        string approvedFolderId,
        CancellationToken cancellationToken)
    {
        ValidateDeltaUri(uri, approvedFolderId);
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
        var next = ReadLink(root, "@odata.nextLink", approvedFolderId);
        var delta = ReadLink(root, "@odata.deltaLink", approvedFolderId);
        if (next is null && delta is null)
        {
            throw new InvalidDataException("Microsoft Graph returned no next or delta cursor.");
        }
        return new(values, next ?? delta!);
    }

    public async Task<byte[]> ReadMimeAsync(string immutableMessageId, CancellationToken cancellationToken)
    {
        var uri = new Uri(
            options.BaseUri,
            $"users/{Uri.EscapeDataString(options.MailboxId)}/messages/{Uri.EscapeDataString(immutableMessageId)}/$value");
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
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Microsoft Graph returned {(int)response.StatusCode}; response length {body.Length}.",
            inner: null,
            response.StatusCode);
    }

    private Uri? ReadLink(JsonElement root, string property, string approvedFolderId)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        var uri = new Uri(value.GetString()!, UriKind.Absolute);
        ValidateDeltaUri(uri, approvedFolderId);
        return uri;
    }

    private void ValidateDeltaUri(Uri uri, string approvedFolderId)
    {
        var mailboxId = Uri.EscapeDataString(options.MailboxId);
        var folderId = Uri.EscapeDataString(approvedFolderId);
        var approvedPaths = new[]
        {
            InitialDeltaUri(approvedFolderId, 1),
            new Uri(
                options.BaseUri,
                $"users/{mailboxId}/mailfolders('{folderId}')/messages/delta"),
            new Uri(
                options.BaseUri,
                $"users('{mailboxId}')/mailfolders('{folderId}')/messages/delta")
        }.Select(value => value.GetComponents(UriComponents.Path, UriFormat.Unescaped));
        var actualPath = uri.GetComponents(UriComponents.Path, UriFormat.Unescaped);
        if (uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.Equals(options.BaseUri.Host, StringComparison.OrdinalIgnoreCase)
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
            removed);
    }

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

internal sealed class GraphApprovedInboxSource(
    GraphApprovedMailboxOptions options,
    GraphMailClient client) : IApprovedInboxSource
{
    public async Task<ApprovedInboxPage> ReadAsync(
        ApprovedInboxPollLease lease,
        int maximumMessages,
        CancellationToken cancellationToken)
    {
        ValidateLease(lease);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMessages);
        var cursor = GraphCursor.Parse(lease.Cursor, client.InitialDeltaUri(options.InboxFolderId, maximumMessages));
        GraphDeltaPage page;
        try
        {
            page = await client.ReadDeltaAsync(cursor.PageUri, options.InboxFolderId, cancellationToken);
        }
        catch (GraphDeltaResetRequiredException)
        {
            cursor = GraphCursor.Parse(null, client.InitialDeltaUri(options.InboxFolderId, maximumMessages));
            page = await client.ReadDeltaAsync(cursor.PageUri, options.InboxFolderId, cancellationToken);
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
            var mime = await client.ReadMimeAsync(item.Id, cancellationToken);
            var next = GraphCursor.Serialize(
                processed >= page.Items.Count ? page.NextUri : cursor.PageUri,
                processed >= page.Items.Count ? 0 : processed);
            messages.Add(new(
                item.Id,
                $"{SanitizeFileName(item.Id)}.eml",
                mime,
                item.ReceivedAtUtc ?? throw new InvalidDataException("Graph Inbox message omitted receivedDateTime."),
                next));
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

    private void ValidateLease(ApprovedInboxPollLease lease)
    {
        if (!lease.MailboxId.Equals(options.MailboxId, StringComparison.Ordinal)
            || !lease.MailboxAddress.Equals(options.MailboxAddress, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("The Inbox lease is outside the approved Graph mailbox.");
        }
    }

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
        ValidateLease(lease);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var cursor = GraphCursor.Parse(lease.Cursor, client.InitialDeltaUri(options.SentFolderId, maximumItems));
        GraphDeltaPage page;
        try
        {
            page = await client.ReadDeltaAsync(cursor.PageUri, options.SentFolderId, cancellationToken);
        }
        catch (GraphDeltaResetRequiredException)
        {
            cursor = GraphCursor.Parse(null, client.InitialDeltaUri(options.SentFolderId, maximumItems));
            page = await client.ReadDeltaAsync(cursor.PageUri, options.SentFolderId, cancellationToken);
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
        var mime = await client.ReadMimeAsync(item.Id, cancellationToken);
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
