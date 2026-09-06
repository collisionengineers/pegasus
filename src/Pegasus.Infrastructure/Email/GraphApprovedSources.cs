using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using MimeKit;
using Microsoft.Extensions.Logging;
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
        string? sentFolderId) => new(
        ParseBaseUri(baseUri),
        Require(mailboxId, "Graph:MailboxId", 200),
        ApprovedMailboxAddress.Normalize(Require(mailboxAddress, "Graph:MailboxAddress", 320)),
        Require(inboxFolderId, "Graph:InboxFolderId", 500),
        Require(sentFolderId, "Graph:SentFolderId", 500));

    /// <summary>
    /// Shared with <see cref="GraphApprovedMailboxResolver"/>'s composition: one place
    /// validates that a configured Graph base URI is the real Microsoft Graph HTTPS
    /// endpoint, whether the caller also needs a fixed polling mailbox or not.
    /// </summary>
    internal static Uri ParseBaseUri(string? baseUri)
    {
        if (!Uri.TryCreate(baseUri, UriKind.Absolute, out var parsedBaseUri)
            || parsedBaseUri.Scheme != Uri.UriSchemeHttps
            || !parsedBaseUri.Host.Equals("graph.microsoft.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Graph:BaseUri must be the Microsoft Graph HTTPS endpoint.");
        }

        return EnsureTrailingSlash(parsedBaseUri);
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
/// Resolves an address to its exact Graph mailbox and well-known folder identities for
/// the mailbox-administration "add an address" flow. Independent of
/// <see cref="GraphApprovedMailboxOptions"/> — that type names one fixed polling
/// mailbox; this resolves any address the tenant directory recognizes. A 404 (address not
/// in the tenant) or any other transport/authorization failure both resolve to null: the
/// caller fails closed either way, and never learns which one happened.
/// </summary>
internal sealed partial class GraphApprovedMailboxResolver(
    TokenCredential credential,
    Uri baseUri,
    HttpClient httpClient,
    ILogger<GraphApprovedMailboxResolver> logger) : IResolveApprovedMailboxIdentity
{
    private static readonly TokenRequestContext TokenContext =
        new(["https://graph.microsoft.com/.default"]);

    public async Task<ApprovedMailboxIdentityResolution?> ResolveAsync(
        string address,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        try
        {
            var mailboxId = await GetIdAsync(
                new Uri(baseUri, $"users/{Uri.EscapeDataString(address)}?$select=id"),
                cancellationToken);
            if (mailboxId is null)
            {
                return null;
            }

            var inboxId = await GetIdAsync(
                new Uri(baseUri, $"users/{Uri.EscapeDataString(mailboxId)}/mailFolders/inbox?$select=id"),
                cancellationToken);
            var sentId = await GetIdAsync(
                new Uri(baseUri, $"users/{Uri.EscapeDataString(mailboxId)}/mailFolders/sentitems?$select=id"),
                cancellationToken);
            if (inboxId is null || sentId is null)
            {
                return null;
            }

            var folderBindings = await GetFolderBindingsAsync(mailboxId, cancellationToken);
            return new ApprovedMailboxIdentityResolution(mailboxId, inboxId, sentId, folderBindings);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogResolutionFailed(logger, exception);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Approved-mailbox address resolution failed.")]
    private static partial void LogResolutionFailed(ILogger logger, Exception exception);

    private async Task<string?> GetIdAsync(Uri uri, CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(TokenContext, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.TryGetProperty("id", out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private async Task<IReadOnlyList<ApprovedMailboxFolderBinding>> GetFolderBindingsAsync(
        string mailboxId,
        CancellationToken cancellationToken)
    {
        var candidates = new Dictionary<MailLogicalFolderType, List<string>>();
        var pending = new Queue<Uri>();
        var visitedFolders = new HashSet<string>(StringComparer.Ordinal);
        pending.Enqueue(FolderListUri(mailboxId, parentFolderId: null));

        while (pending.TryDequeue(out var pageUri))
        {
            do
            {
                ValidateFolderListUri(pageUri, mailboxId);
                var token = await credential.GetTokenAsync(TokenContext, cancellationToken);
                using var request = new HttpRequestMessage(HttpMethod.Get, pageUri);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
                using var response = await httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                foreach (var folder in document.RootElement.GetProperty("value").EnumerateArray())
                {
                    var id = folder.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
                    var displayName = folder.TryGetProperty("displayName", out var displayValue)
                        ? displayValue.GetString()
                        : null;
                    if (!IsExactFolderIdentity(id))
                    {
                        continue;
                    }

                    var definition = MailLogicalFolders.All.SingleOrDefault(item =>
                        string.Equals(item.Label, displayName, StringComparison.OrdinalIgnoreCase));
                    if (definition is not null)
                    {
                        candidates.TryAdd(definition.Type, []);
                        candidates[definition.Type].Add(id!);
                    }

                    if (folder.TryGetProperty("childFolderCount", out var count)
                        && count.TryGetInt32(out var childCount)
                        && childCount > 0
                        && visitedFolders.Add(id!))
                    {
                        pending.Enqueue(FolderListUri(mailboxId, id));
                    }
                }

                pageUri = document.RootElement.TryGetProperty("@odata.nextLink", out var next)
                    && Uri.TryCreate(next.GetString(), UriKind.Absolute, out var parsed)
                        ? parsed
                        : null;
            }
            while (pageUri is not null);
        }

        return candidates
            .Where(item => item.Value.Distinct(StringComparer.Ordinal).Count() == 1)
            .OrderBy(item => item.Key)
            .Select(item => new ApprovedMailboxFolderBinding(item.Key, item.Value[0]))
            .ToArray();
    }

    private Uri FolderListUri(string mailboxId, string? parentFolderId)
    {
        var root = $"users/{Uri.EscapeDataString(mailboxId)}/mailFolders";
        var path = parentFolderId is null
            ? root
            : $"{root}/{Uri.EscapeDataString(parentFolderId)}/childFolders";
        return new Uri(
            baseUri,
            $"{path}?$select=id,displayName,childFolderCount&$top=200&includeHiddenFolders=true");
    }

    private void ValidateFolderListUri(Uri uri, string mailboxId)
    {
        var approvedRoot = new Uri(
            baseUri,
            $"users/{Uri.EscapeDataString(mailboxId)}/mailFolders").AbsolutePath;
        if (uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase)
            || !uri.AbsolutePath.StartsWith(approvedRoot, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Microsoft Graph returned a folder page outside the exact approved mailbox.");
        }
    }

    private static bool IsExactFolderIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 200
        && !value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character));
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
        return await ReadMimeAsync(uri, cancellationToken);
    }

    private async Task<byte[]> ReadMimeAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Prefer", "IdType=\"ImmutableId\"");
        using var response = await SendAsync(request, cancellationToken);
        await ThrowForFailureAsync(response, cancellationToken);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<byte[]> ReadFolderMimeAsync(
        string mailboxId,
        string folderId,
        string immutableMessageId,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(
            baseUri,
            $"users/{Uri.EscapeDataString(mailboxId)}/mailFolders/{Uri.EscapeDataString(folderId)}" +
            $"/messages/{Uri.EscapeDataString(immutableMessageId)}/$value");
        return await ReadMimeAsync(uri, cancellationToken);
    }

    public async Task MoveMessageAsync(
        RetainedMailFolderMoveCoordinates coordinates,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(
            baseUri,
            $"users/{Uri.EscapeDataString(coordinates.MailboxId)}/mailFolders/{Uri.EscapeDataString(coordinates.SourceFolderId)}" +
            $"/messages/{Uri.EscapeDataString(coordinates.ImmutableMessageId)}/move");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.TryAddWithoutValidation("Prefer", "IdType=\"ImmutableId\"");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { destinationId = coordinates.DestinationFolderId }),
            Encoding.UTF8,
            "application/json");
        using var response = await SendAsync(request, cancellationToken);
        await ThrowForFailureAsync(response, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw new HttpRequestException("Microsoft Graph did not create the moved message.");
        }
    }

    public async Task<string?> ReadMessageParentFolderAsync(
        string mailboxId,
        string immutableMessageId,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(
            baseUri,
            $"users/{Uri.EscapeDataString(mailboxId)}/messages/{Uri.EscapeDataString(immutableMessageId)}?$select=parentFolderId");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Prefer", "IdType=\"ImmutableId\"");
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        await ThrowForFailureAsync(response, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return RequiredString(document.RootElement, "parentFolderId");
    }

    public async Task<string> ResolveDeletedItemsFolderAsync(
        string mailboxId,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(
            baseUri,
            $"users/{Uri.EscapeDataString(mailboxId)}/mailFolders/deleteditems?$select=id");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await SendAsync(request, cancellationToken);
        await ThrowForFailureAsync(response, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "Microsoft Graph returned an invalid Deleted Items folder response.");
        }
        return RequiredString(document.RootElement, "id");
    }

    public Uri InitialFolderMessagesUri(string mailboxId, string folderId, int maximumItems) =>
        new(
            baseUri,
            $"users/{Uri.EscapeDataString(mailboxId)}/mailFolders/{Uri.EscapeDataString(folderId)}" +
            $"/messages?$select=id,parentFolderId,receivedDateTime,conversationId,internetMessageId,isRead&$orderby=receivedDateTime%20desc&$top={maximumItems}");

    public async Task<GraphFolderPage> ReadFolderMessagesAsync(
        Uri uri,
        string mailboxId,
        string folderId,
        CancellationToken cancellationToken)
    {
        ValidateFolderMessagesUri(uri, mailboxId, folderId);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Prefer", "IdType=\"ImmutableId\"");
        using var response = await SendAsync(request, cancellationToken);
        await ThrowForFailureAsync(response, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("value", out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Microsoft Graph returned an invalid Deleted Items message page.");
        }
        var items = value.EnumerateArray().Select(ParseItem).ToArray();
        if (items.Any(item => item.Removed
            || !string.Equals(item.ParentFolderId, folderId, StringComparison.Ordinal)))
        {
            throw new UnauthorizedAccessException(
                "Microsoft Graph returned a message outside the exact approved Deleted Items folder.");
        }
        if (items.Any(item => item.ReceivedAtUtc is null))
        {
            throw new InvalidDataException(
                "Microsoft Graph returned a Deleted Items message without its received time.");
        }
        Uri? next = null;
        if (root.TryGetProperty("@odata.nextLink", out var nextValue))
        {
            if (nextValue.ValueKind != JsonValueKind.String
                || !Uri.TryCreate(nextValue.GetString(), UriKind.Absolute, out next))
            {
                throw new InvalidDataException(
                    "Microsoft Graph returned an invalid Deleted Items next link.");
            }
            ValidateFolderMessagesUri(next, mailboxId, folderId);
        }
        return new(items, next);
    }

    private void ValidateFolderMessagesUri(Uri uri, string mailboxId, string folderId)
    {
        var expected = InitialFolderMessagesUri(mailboxId, folderId, 1)
            .GetComponents(UriComponents.Path, UriFormat.Unescaped);
        var actual = uri.GetComponents(UriComponents.Path, UriFormat.Unescaped);
        if (uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase)
            || !actual.Equals(expected, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "The Microsoft Graph message page escaped the exact approved mailbox folder.");
        }
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
            removed,
            value.TryGetProperty("receivedDateTime", out _));
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
        var mailboxId = lease.GraphMailboxId;
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
            if (item.ReceivedAtUtc is null)
            {
                // Graph guarantees only "at least the updated properties" on a sparse
                // delta entry, so an already-known item can recur here (e.g. a read/flag
                // change) genuinely without receivedDateTime even though it was selected
                // on the initial call. A present-but-unparseable value is a different,
                // reportable fault and must not be silently treated the same way.
                if (item.ReceivedDateTimePresent)
                {
                    throw new InvalidDataException(
                        "Microsoft Graph returned an unparseable receivedDateTime.");
                }
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
                item.ReceivedAtUtc.Value,
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
                display.ReplyToAddresses,
                string.IsNullOrWhiteSpace(display.Subject) ? null : display.Subject,
                string.IsNullOrWhiteSpace(display.Body) ? null : display.Body,
                display.Attachments ?? [],
                item.IsRead);
        }
        catch (FormatException)
        {
            // Only the MIME display view is unavailable. Returning null here
            // used to skip the retained row entirely, so the message was
            // received and processed while the workspace showed nothing at all —
            // silently, with the received-item record and the Inbox disagreeing
            // about whether the mail exists. Graph's own facts are still good,
            // so the row is written from those with the display fields empty:
            // the workspace shows the gap, which is what the poll intended.
            return new(
                inboxFolderId,
                item.ConversationId,
                item.InternetMessageId,
                SenderAddress: null,
                SenderDisplayName: null,
                ToAddresses: [],
                CcAddresses: [],
                ReplyToAddresses: [],
                Subject: null,
                BodyPlainText: null,
                Attachments: [],
                item.IsRead);
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
        if (!IsExactIdentity(lease.GraphMailboxId, MaximumMailboxIdentityLength)
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

internal sealed class GraphDeletedMailSearchSource(
    GraphMailClient client,
    IApprovedIntakeMailboxes approvedMailboxes,
    IIntakeSourceReader sourceReader) : IDeletedMailSearchSource
{
    public async Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(
        CancellationToken cancellationToken) =>
        (await approvedMailboxes.ListPollableAsync(cancellationToken))
            .Select(item => new RetainedMailMailbox(item.ApprovedMailboxId, item.Address, true))
            .OrderBy(item => item.MailboxAddress, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public async Task<DeletedMailSourceResult> SearchAsync(
        Guid? mailboxId,
        string searchTerm,
        int maximumMessages,
        CancellationToken cancellationToken)
    {
        var approved = await approvedMailboxes.ListPollableAsync(cancellationToken);
        var selected = mailboxId is null
            ? approved
            : approved.Where(item => item.ApprovedMailboxId == mailboxId).ToArray();
        if (mailboxId is not null && selected.Count == 0)
        {
            return new([], false, DeletedMailSearchState.Unavailable);
        }

        var truncated = false;
        var matches = new List<DeletedMailSearchItem>();
        try
        {
            var candidates = new List<(
                ApprovedIntakeMailbox Mailbox,
                string FolderId,
                GraphDeltaItem Message)>();
            foreach (var mailbox in selected)
            {
                var folderId = await client.ResolveDeletedItemsFolderAsync(
                    mailbox.GraphMailboxId,
                    cancellationToken);
                Uri? pageUri = client.InitialFolderMessagesUri(
                    mailbox.GraphMailboxId,
                    folderId,
                    Math.Min(maximumMessages + 1, 1000));
                var mailboxCount = 0;
                while (pageUri is not null && mailboxCount <= maximumMessages)
                {
                    var page = await client.ReadFolderMessagesAsync(
                        pageUri,
                        mailbox.GraphMailboxId,
                        folderId,
                        cancellationToken);
                    foreach (var item in page.Items)
                    {
                        if (mailboxCount++ >= maximumMessages)
                        {
                            truncated = true;
                            break;
                        }
                        candidates.Add((mailbox, folderId, item));
                    }
                    pageUri = page.NextUri;
                }
                truncated |= pageUri is not null;
            }

            var selectedCandidates = candidates
                .OrderByDescending(candidate => candidate.Message.ReceivedAtUtc)
                .ThenBy(candidate => candidate.Message.Id, StringComparer.Ordinal)
                .Take(maximumMessages)
                .ToArray();
            truncated |= candidates.Count > maximumMessages;
            foreach (var candidate in selectedCandidates)
            {
                var mailbox = candidate.Mailbox;
                var item = candidate.Message;
                var mime = await client.ReadFolderMimeAsync(
                    mailbox.GraphMailboxId,
                    candidate.FolderId,
                    item.Id,
                    cancellationToken);
                var read = await sourceReader.ReadAsync(
                    new(
                        $"{item.Id}.eml",
                        "message/rfc822",
                        mime,
                        item.ReceivedAtUtc ?? DateTimeOffset.MinValue,
                        "system-worker:deleted-mail-search",
                        new(IntakeSourceChannel.Mailbox, $"deleted:{mailbox.ApprovedMailboxId:D}:{item.Id}")),
                    cancellationToken);
                if (Match(read, searchTerm) is not { Length: > 0 } found)
                {
                    continue;
                }
                var documents = IntakeSearchProjection.Create(read, routeDecision: null);
                var searchableOrdinals = documents
                    .Where(document => document.AttachmentOrdinal is not null && document.IsSearchable)
                    .Select(document => document.AttachmentOrdinal!.Value)
                    .ToHashSet();
                matches.Add(new(
                    mailbox.ApprovedMailboxId,
                    mailbox.Address,
                    item.Id,
                    Sender(read),
                    null,
                    Subject(read),
                    documents.FirstOrDefault(document => document.AttachmentOrdinal is null)?.Text,
                    item.ReceivedAtUtc ?? DateTimeOffset.MinValue,
                    item.IsRead,
                    read.AttachmentRecords.Select(attachment => new RetainedMailAttachment(
                        attachment.FileName,
                        attachment.MediaType,
                        attachment.ContentLength ?? 0,
                        searchableOrdinals.Contains(attachment.Ordinal))).ToArray(),
                    found));
            }
        }
        catch (Exception exception) when (
            exception is ApprovedMailboxAccessDeniedException
                or ApprovedSentSourceThrottledException
                or AuthenticationFailedException
                or HttpRequestException
                or InvalidDataException
                or JsonException
                or UnauthorizedAccessException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return new([], false, DeletedMailSearchState.Unavailable);
        }
        return new(matches, truncated);
    }

    private static RetainedMailSearchMatch[] Match(
        IntakeSourceReadResult read,
        string searchTerm)
    {
        var documents = IntakeSearchProjection.Create(read, routeDecision: null);
        var found = new List<RetainedMailSearchMatch>();
        if (documents.Any(document => document.AttachmentFileName is null
            && document.Text?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true))
        {
            found.Add(new(MailSearchMatchKind.MessageBody));
        }
        found.AddRange(read.AttachmentRecords
            .Where(attachment => attachment.FileName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .Select(attachment => new RetainedMailSearchMatch(
                MailSearchMatchKind.AttachmentFileName,
                attachment.FileName,
                attachment.Ordinal)));
        found.AddRange(documents
            .Where(document => document.AttachmentFileName is not null
                && document.Text?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true)
            .Select(document => new RetainedMailSearchMatch(
                MailSearchMatchKind.AttachmentContent,
                document.AttachmentFileName,
                document.AttachmentOrdinal)));
        return found.Distinct().ToArray();
    }

    private static string? Sender(IntakeSourceReadResult read) =>
        read.TransportEvidence.FirstOrDefault(item =>
            item.Source == IntakeEvidenceSource.Sender
            && item.SenderIdentityKind == IntakeSenderIdentityKind.Transport)?.Value;

    private static string? Subject(IntakeSourceReadResult read) =>
        read.TransportEvidence.FirstOrDefault(item => item.Source == IntakeEvidenceSource.Subject)?.Value;
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
internal sealed record GraphFolderPage(IReadOnlyList<GraphDeltaItem> Items, Uri? NextUri);

internal sealed class GraphRetainedMailFolderMover(GraphMailClient client) : IRetainedMailFolderMover
{
    public bool IsAvailable => true;

    public Task MoveAsync(RetainedMailFolderMoveCoordinates coordinates, CancellationToken cancellationToken) =>
        client.MoveMessageAsync(coordinates, cancellationToken);

    public Task<string?> GetParentFolderIdAsync(string mailboxId, string immutableMessageId, CancellationToken cancellationToken) =>
        client.ReadMessageParentFolderAsync(mailboxId, immutableMessageId, cancellationToken);
}
internal sealed class GraphDeltaResetRequiredException : Exception;
internal sealed record GraphDeltaItem(
    string Id,
    string? ParentFolderId,
    DateTimeOffset? ReceivedAtUtc,
    DateTimeOffset? SentAtUtc,
    string? ConversationId,
    string? InternetMessageId,
    bool IsRead,
    bool Removed,
    bool ReceivedDateTimePresent);

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
