using System.Net;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using MimeKit;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Email;

public sealed record GraphApprovedMailboxOptions(Uri BaseUri)
{
    public static GraphApprovedMailboxOptions Create(string? baseUri) =>
        new(ParseBaseUri(baseUri));

    /// <summary>
    /// Shared with <see cref="GraphApprovedMailboxResolver"/>'s composition: one place
    /// validates that the configured Graph base URI is the real Microsoft Graph HTTPS
    /// endpoint. Polling identities come only from approved mailbox leases.
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
}

/// <summary>
/// Resolves an address to its exact Graph mailbox and well-known folder identities for
/// the mailbox-administration "add an address" flow. Independent of
/// <see cref="GraphApprovedMailboxOptions"/> — that type validates the Graph endpoint;
/// this resolves any address the tenant directory recognizes. A 404 (address not
/// in the tenant) or any other transport/authorization failure both resolve to null: the
/// caller fails closed either way, and never learns which one happened.
/// </summary>
internal sealed partial class GraphApprovedMailboxResolver(
    TokenCredential credential,
    Uri baseUri,
    HttpClient httpClient,
    ILogger<GraphApprovedMailboxResolver> logger) : IResolveApprovedMailboxIdentity, ICheckApprovedMailboxAccess
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

            return new ApprovedMailboxIdentityResolution(mailboxId, inboxId, sentId);
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
        ValidateGraphOrigin(uri);
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

    public async Task<bool> CanReadInboxAsync(
        ApprovedMailboxIdentityResolution mailbox,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mailbox);
        try
        {
            var uri = new Uri(
                baseUri,
                $"users/{Uri.EscapeDataString(mailbox.MailboxIdentity)}/mailFolders/" +
                $"{Uri.EscapeDataString(mailbox.InboxFolderIdentity)}/messages?$select=id&$top=1");
            ValidateGraphOrigin(uri);
            var token = await credential.GetTokenAsync(TokenContext, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Headers.TryAddWithoutValidation("Prefer", "IdType=\"ImmutableId\"");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("value", out var value)
                && value.ValueKind == JsonValueKind.Array;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogResolutionFailed(logger, exception);
            return false;
        }
    }

    private void ValidateGraphOrigin(Uri uri)
    {
        if (!HasConfiguredOrigin(uri))
        {
            throw new UnauthorizedAccessException(
                "The Microsoft Graph request escaped the configured origin.");
        }
    }

    private bool HasConfiguredOrigin(Uri uri) =>
        uri.IsAbsoluteUri
        && uri.Scheme.Equals(baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
        && uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase)
        && uri.Port == baseUri.Port;
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
        return new(values, next ?? delta!, next is not null);
    }

    /// <summary>
    /// Reads one message's MIME through <paramref name="read"/>, which is handed the
    /// response content before any of the body has been read. The caller decides from
    /// the declared length whether to hold the message in memory.
    /// </summary>
    public async Task<T> ReadMimeAsync<T>(
        string mailboxId,
        string immutableMessageId,
        Func<HttpContent, CancellationToken, Task<T>> read,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(
            baseUri,
            $"users/{Uri.EscapeDataString(mailboxId)}/messages/{Uri.EscapeDataString(immutableMessageId)}/$value");
        return await ReadMimeAsync(uri, read, cancellationToken);
    }

    private async Task<T> ReadMimeAsync<T>(
        Uri uri,
        Func<HttpContent, CancellationToken, Task<T>> read,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Prefer", "IdType=\"ImmutableId\"");
        using var response = await SendAsync(request, cancellationToken);
        await ThrowForFailureAsync(response, cancellationToken);
        return await read(response.Content, cancellationToken);
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
        return await ReadMimeAsync(
            uri,
            static (content, token) => content.ReadAsByteArrayAsync(token),
            cancellationToken);
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
        if (!HasConfiguredOrigin(uri)
            || !actual.Equals(expected, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "The Microsoft Graph message page escaped the exact approved mailbox folder.");
        }
    }

    internal Uri CreateStaffUri(string mailboxId, string path) => new(
        baseUri,
        $"users/{Uri.EscapeDataString(mailboxId)}/{path}");

    internal async Task<HttpResponseMessage> SendStaffAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync(request, cancellationToken);
        if ((int)response.StatusCode is >= 400 and < 500
            && response.StatusCode is not HttpStatusCode.RequestTimeout
            and not HttpStatusCode.TooManyRequests)
        {
            response.Dispose();
            throw new StaffMailTransportRejectedException(
                $"graph_rejected_{(int)response.StatusCode}");
        }
        await ThrowForFailureAsync(response, cancellationToken);
        return response;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is null || !HasConfiguredOrigin(request.RequestUri))
        {
            throw new UnauthorizedAccessException(
                "The Microsoft Graph request escaped the configured origin.");
        }
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
            var retryAfter = response.Headers.RetryAfter?.Delta
                ?? (response.Headers.RetryAfter?.Date is { } retryAt
                    ? retryAt - DateTimeOffset.UtcNow
                    : TimeSpan.FromSeconds(30));
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
        if (!Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri))
        {
            throw new InvalidDataException("Microsoft Graph returned a malformed continuation link.");
        }
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
        if (!HasConfiguredOrigin(uri)
            || !approvedPaths.Contains(actualPath, StringComparer.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "The Microsoft Graph cursor escaped the exact approved mailbox folder.");
        }
    }

    public async Task<GraphDeltaItem?> ReadMessageAsync(
        string mailboxId,
        string approvedFolderId,
        string immutableMessageId,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(
            baseUri,
            $"users/{Uri.EscapeDataString(mailboxId)}/messages/{Uri.EscapeDataString(immutableMessageId)}" +
            "?$select=id,parentFolderId,receivedDateTime,conversationId,internetMessageId,isRead");
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
        var item = ParseItem(document.RootElement);
        if (!string.Equals(item.Id, immutableMessageId, StringComparison.Ordinal)
            || !string.Equals(item.ParentFolderId, approvedFolderId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "The notified message is outside the exact approved Inbox folder.");
        }
        return item;
    }

    private bool HasConfiguredOrigin(Uri uri) =>
        uri.IsAbsoluteUri
        && uri.Scheme.Equals(baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
        && uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase)
        && uri.Port == baseUri.Port;

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
/// <param name="maximumContentLength">
/// The mailbox envelope bound Core enforces. A message Graph declares longer than
/// this is streamed into quarantine rather than read into memory, and Core accepts
/// that as a valid oversize rejection only because the two bounds are the same. It
/// is a parameter only so that a test can exercise both sides of the boundary.
/// </param>
internal sealed class GraphApprovedInboxSource(
    GraphMailClient client,
    IIntakeQuarantineArtifactStore quarantineArtifactStore,
    long maximumContentLength = IntakeEnvelopeLimits.MaximumMailboxContentLength) : IApprovedInboxSource
{
    private const int MaximumMailboxIdentityLength = 100;
    private const int MaximumFolderIdentityLength = 200;

    public Task<ApprovedInboxPage> ReadAsync(
        ApprovedInboxPollLease lease,
        int maximumMessages,
        CancellationToken cancellationToken) =>
        ReadPageAsync(lease, maximumMessages, alreadyRetained: null, cancellationToken);

    public Task<ApprovedInboxPage> ReadAsync(
        ApprovedInboxPollLease lease,
        int maximumMessages,
        RetainedMessageCheck alreadyRetained,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alreadyRetained);
        return ReadPageAsync(lease, maximumMessages, alreadyRetained, cancellationToken);
    }

    private async Task<ApprovedInboxPage> ReadPageAsync(
        ApprovedInboxPollLease lease,
        int maximumMessages,
        RetainedMessageCheck? alreadyRetained,
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
            if (item.ReceivedAtUtc < lease.StartBoundaryUtc)
            {
                continue;
            }
            if (alreadyRetained is not null
                && item.InternetMessageId is { } internetMessageId
                && await alreadyRetained(item.Id, internetMessageId, cancellationToken))
            {
                // Already retained: the webhook wake downloaded this same item. The
                // wake and the delta both ask for immutable ids, so the retained row
                // carries this item's id. Not downloaded again and not passed on,
                // like any other item left out above. The cursors below count its
                // place, so they move past it.
                continue;
            }
            var mime = await ReadMimeAsync(mailboxId, item.Id, cancellationToken);
            var next = GraphCursor.Serialize(
                processed >= page.Items.Count ? page.NextUri : cursor.PageUri,
                processed >= page.Items.Count ? 0 : processed);
            // A message refused before it was read has no subject to name it by.
            var metadata = mime.Rejection is not null
                ? null
                : await ReadRetainedMetadataAsync(
                    mime.Content,
                    item,
                    inboxFolderId,
                    cancellationToken);
            messages.Add(new(
                item.Id,
                EmailSourceFormat.RetainedMessageFileName(metadata?.Subject),
                mime.Content,
                item.ReceivedAtUtc.Value,
                next)
            {
                SourceRejection = mime.Rejection,
                RetainedMetadata = metadata
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

    public async Task<ApprovedInboxMessage?> ReadNotifiedAsync(
        ApprovedInboxPollLease lease,
        string immutableMessageId,
        CancellationToken cancellationToken)
    {
        ValidateLease(lease);
        ArgumentException.ThrowIfNullOrWhiteSpace(immutableMessageId);
        var item = await client.ReadMessageAsync(
            lease.GraphMailboxId,
            lease.InboxFolderIdentity,
            immutableMessageId,
            cancellationToken);
        if (item?.ReceivedAtUtc is null || item.ReceivedAtUtc < lease.StartBoundaryUtc)
        {
            return null;
        }
        var mime = await ReadMimeAsync(lease.GraphMailboxId, immutableMessageId, cancellationToken);
        var metadata = mime.Rejection is not null
            ? null
            : await ReadRetainedMetadataAsync(
                mime.Content,
                item,
                lease.InboxFolderIdentity,
                cancellationToken);
        return new(
            immutableMessageId,
            EmailSourceFormat.RetainedMessageFileName(metadata?.Subject),
            mime.Content,
            item.ReceivedAtUtc.Value,
            GraphCursor.Serialize(client.InitialDeltaUri(
                lease.GraphMailboxId,
                lease.InboxFolderIdentity,
                1), 0))
        {
            SourceRejection = mime.Rejection,
            RetainedMetadata = metadata
        };
    }

    /// <summary>
    /// The message's MIME, unless Graph declares it longer than the mailbox bound.
    /// </summary>
    /// <remarks>
    /// Core quarantines a message over the bound as <c>message_too_large</c> once it
    /// has been read. A body Graph declares over the bound is instead streamed
    /// straight into the quarantine store and handed to Core as the source rejection
    /// it already accepts, so the quarantine record is the same and the message is
    /// never held in memory. Core verifies the retained artifact before it records
    /// the quarantine, so it is not verified here as well. A body with no declared
    /// length, or one within the bound, is read whole as before, and Core still
    /// applies the bound to what arrives.
    /// </remarks>
    private Task<GraphInboxMime> ReadMimeAsync(
        string mailboxId,
        string immutableMessageId,
        CancellationToken cancellationToken) =>
        client.ReadMimeAsync(
            mailboxId,
            immutableMessageId,
            async (content, token) =>
            {
                if (content.Headers.ContentLength is not { } declaredLength
                    || declaredLength <= maximumContentLength)
                {
                    return new GraphInboxMime(await content.ReadAsByteArrayAsync(token), null);
                }

                await using var body = new GraphBodyStream(await content.ReadAsStreamAsync(token));
                IntakeQuarantineArtifact retained;
                try
                {
                    retained = await quarantineArtifactStore.StoreStreamAsync(
                        body,
                        declaredLength,
                        token);
                }
                catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
                {
                    // A fault reading Graph's body is a Graph read failure, as it is for
                    // a message read whole. Only a fault in the store is a retention failure.
                    if (body.ReadFault is { } readFault)
                    {
                        ExceptionDispatchInfo.Throw(readFault);
                    }
                    throw new IntakeArtifactRetentionException(exception);
                }

                return new GraphInboxMime(
                    [],
                    new(
                        "message_too_large",
                        retained.ContentLength,
                        retained.ContentHash,
                        retained.StorageKey));
            },
            cancellationToken);

    private sealed record GraphInboxMime(
        byte[] Content,
        ApprovedInboxSourceRejection? Rejection);

    /// <summary>
    /// Graph's response body as the quarantine store reads it. It keeps any fault the
    /// body raises, so a Graph read fault is told apart from a fault in the store.
    /// </summary>
    private sealed class GraphBodyStream(Stream source) : Stream
    {
        public Exception? ReadFault { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            try
            {
                return source.Read(buffer);
            }
            catch (Exception exception)
            {
                ReadFault = exception;
                throw;
            }
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await source.ReadAsync(buffer, cancellationToken);
            }
            catch (Exception exception)
            {
                ReadFault = exception;
                throw;
            }
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                source.Dispose();
            }
            base.Dispose(disposing);
        }
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
    internal static async Task<RetainedMailboxMessageMetadata?> ReadRetainedMetadataAsync(
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

internal sealed class GraphApprovedSentSource(GraphMailClient client) : IApprovedSentSource
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
            if (!item.Removed && item.SentAtUtc is { } sentAtUtc
                && sentAtUtc < lease.StartBoundaryUtc)
            {
                continue;
            }
            if (item.Removed)
            {
                items.Add(DeletedItem(lease.MailboxId, item, next));
            }
            else if (await DiscoveredItemAsync(lease, item, next, cancellationToken) is { } discovered)
            {
                items.Add(discovered);
            }
        }
        var consumed = cursor.SkipCount + available.Length;
        var pageCursor = GraphCursor.Serialize(
            consumed >= page.Items.Count ? page.NextUri : cursor.PageUri,
            consumed >= page.Items.Count ? 0 : consumed);
        if (items.Count > 0)
        {
            items[^1] = items[^1] with { NextCursor = pageCursor };
        }
        return new(
            items,
            pageCursor,
            items.Count > 0 && (page.Items.Count > consumed || page.HasNextPage));
    }

    private async Task<ApprovedSentItem?> DiscoveredItemAsync(
        ApprovedSentPollLease lease,
        GraphDeltaItem item,
        string nextCursor,
        CancellationToken cancellationToken)
    {
        var mime = await client.ReadFolderMimeAsync(
            lease.MailboxId, lease.SentFolderIdentity, item.Id, cancellationToken);
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
                return Malformed(lease.MailboxId, lease.SentFolderIdentity, item, sourceHash, "graph_sent_provenance_incomplete", nextCursor);
            }
            if (sentAtUtc < lease.StartBoundaryUtc)
            {
                return null;
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
            var operationMarkers = HeaderValues(message, StaffMailCorrelationHeaders.OperationId);
            if (operationMarkers.Length > 1
                || operationMarkers.Length == 1 && !Guid.TryParse(operationMarkers[0], out _))
            {
                return Malformed(lease.MailboxId, lease.SentFolderIdentity, item, sourceHash, "graph_sent_operation_marker_invalid", nextCursor);
            }
            // The provider keeps the Message-ID Pegasus assigned where it drops
            // the custom X- headers, so the Message-ID names the operation when
            // the header is absent. It carries no frozen mailbox, generation or
            // payload marker; the poll checks those against its lease instead.
            var operationId = operationMarkers.Length == 0
                ? StaffMailCorrelationHeaders.TryReadOperationId(messageId)
                : Guid.Parse(operationMarkers[0]);
            Guid? markerMailboxId = null;
            long? markerGeneration = null;
            string? markerPayloadHash = null;
            if (operationMarkers.Length == 1)
            {
                var mailboxMarkers = HeaderValues(message, StaffMailCorrelationHeaders.MailboxId);
                var generationMarkers = HeaderValues(message, StaffMailCorrelationHeaders.MailboxGeneration);
                var payloadMarkers = HeaderValues(message, StaffMailCorrelationHeaders.PayloadSha256);
                if (mailboxMarkers.Length != 1 || !Guid.TryParse(mailboxMarkers[0], out var parsedMailboxId)
                    || generationMarkers.Length != 1 || !long.TryParse(generationMarkers[0],
                        System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsedGeneration)
                    || parsedGeneration <= 0
                    || payloadMarkers.Length != 1 || payloadMarkers[0].Length != 64
                    || !payloadMarkers[0].All(Uri.IsHexDigit))
                {
                    return Malformed(lease.MailboxId, lease.SentFolderIdentity, item, sourceHash,
                        "graph_sent_frozen_marker_invalid", nextCursor);
                }
                markerMailboxId = parsedMailboxId;
                markerGeneration = parsedGeneration;
                markerPayloadHash = payloadMarkers[0].ToUpperInvariant();
            }
            var attachmentHashes = new List<string>();
            foreach (var attachment in message.Attachments)
            {
                if (attachment is not MimePart part || part.Content is null)
                {
                    return Malformed(lease.MailboxId, lease.SentFolderIdentity, item, sourceHash, "graph_sent_attachment_invalid", nextCursor);
                }
                using var bytes = new MemoryStream();
                part.Content.DecodeTo(bytes, cancellationToken);
                attachmentHashes.Add(Convert.ToHexString(
                    SHA256.HashData(bytes.GetBuffer().AsSpan(0, (int)bytes.Length))));
            }
            return new(
                Occurrence(lease.MailboxId, item.Id),
                sourceHash,
                lease.SentFolderIdentity,
                ApprovedSentItemObservationKind.Discovered,
                new(
                    lease.MailboxId,
                    lease.MailboxAddress,
                    lease.SentFolderIdentity,
                    item.Id,
                    messageId,
                    conversationId,
                    replyChain,
                    inReplyTo,
                    caseIds,
                    sentAtUtc,
                    sourceHash,
                    operationId,
                    attachmentHashes,
                    markerMailboxId,
                    markerGeneration,
                    markerPayloadHash),
                null,
                nextCursor)
            {
                // The Sent Items scope's row, read the way the Inbox poll reads
                // its own: Graph's identities, the MIME's display content.
                RetainedMetadata = await GraphApprovedInboxSource.ReadRetainedMetadataAsync(
                    mime, item, lease.SentFolderIdentity, cancellationToken),
                SourceLength = mime.LongLength
            };
        }
        catch (FormatException)
        {
            return Malformed(lease.MailboxId, lease.SentFolderIdentity, item, sourceHash, "graph_sent_mime_invalid", nextCursor);
        }
    }

    private static string[] HeaderValues(MimeMessage message, string name) =>
        message.Headers
            .Where(header => header.Field.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static ApprovedSentItem DeletedItem(string mailboxId, GraphDeltaItem item, string nextCursor)
    {
        var sourceHash = Hash($"deleted\n{mailboxId}\n{item.Id}");
        return new(
            Hash($"deleted-occurrence\n{mailboxId}\n{item.Id}"),
            sourceHash,
            null,
            ApprovedSentItemObservationKind.Deleted,
            null,
            "graph_sent_deleted_without_retained_mime",
            nextCursor);
    }

    private static ApprovedSentItem Malformed(
        string mailboxId,
        string sentFolderIdentity,
        GraphDeltaItem item,
        string sourceHash,
        string reason,
        string nextCursor) => new(
            Hash($"malformed-occurrence\n{mailboxId}\n{item.Id}"),
            sourceHash,
            sentFolderIdentity,
            ApprovedSentItemObservationKind.Discovered,
            null,
            reason,
            nextCursor);

    private static void ValidateLease(ApprovedSentPollLease lease)
    {
        if (lease.ApprovedMailboxId == Guid.Empty || lease.Generation <= 0
            || lease.StartBoundaryUtc == default || lease.StartBoundaryUtc.Offset != TimeSpan.Zero
            || string.IsNullOrWhiteSpace(lease.MailboxId)
            || string.IsNullOrWhiteSpace(lease.MailboxAddress)
            || string.IsNullOrWhiteSpace(lease.SentFolderIdentity))
        {
            throw new UnauthorizedAccessException("The Sent lease is outside the approved Graph mailbox and folder.");
        }
    }

    private static string Occurrence(string mailboxId, string id) => Hash($"{mailboxId}\n{id}");
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

internal sealed record GraphDeltaPage(
    IReadOnlyList<GraphDeltaItem> Items, Uri NextUri, bool HasNextPage);
internal sealed record GraphFolderPage(IReadOnlyList<GraphDeltaItem> Items, Uri? NextUri);

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
