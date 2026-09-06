using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Box.Sdk.Gen;
using Pegasus.Core.Custody;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Custody;

public sealed record BoxCustodyOptions(
    Uri BaseUri,
    Uri UploadUri,
    string RootFolderId,
    string ClientId,
    string ClientSecret,
    string JwtKeyId,
    string PrivateKey,
    string PrivateKeyPassphrase,
    string EnterpriseId,
    string HoldingFolderId)
{
    public static BoxCustodyOptions Create(
        string? baseUri,
        string? uploadUri,
        string? rootFolderId,
        string? configJson,
        string? clientSecret,
        string? holdingFolderId = null)
    {
        var api = RequireBoxUri(baseUri, "api.box.com", "Box:BaseUri");
        var upload = RequireBoxUri(uploadUri, "upload.box.com", "Box:UploadUri");
        if (!string.Equals(rootFolderId, "405543781910", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Box:RootFolderId must be the approved pegasus root 405543781910.");
        }
        if (string.IsNullOrWhiteSpace(holdingFolderId)
            || string.Equals(holdingFolderId, rootFolderId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Box:HoldingFolderId must identify the configured holding folder below the approved root.");
        }
        if (string.IsNullOrWhiteSpace(configJson))
        {
            throw new InvalidOperationException("Box:ConfigJson is required through a Key Vault reference.");
        }
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("Box:ClientSecret is required through a Key Vault reference.");
        }
        // During provisioning App Service can hand the app the literal
        // @Microsoft.KeyVault(...) placeholder instead of the secret. Name that
        // state directly so it is never mistaken for a malformed secret.
        if (IsUnresolvedKeyVaultReference(configJson))
        {
            throw new InvalidOperationException(
                "Box:ConfigJson is an unresolved Key Vault reference; the platform has not resolved the secret.");
        }
        if (IsUnresolvedKeyVaultReference(clientSecret))
        {
            throw new InvalidOperationException(
                "Box:ClientSecret is an unresolved Key Vault reference; the platform has not resolved the secret.");
        }

        try
        {
            using var document = JsonDocument.Parse(configJson);
            var root = document.RootElement;
            var settings = root.GetProperty("boxAppSettings");
            var appAuth = settings.GetProperty("appAuth");
            return new(
                api,
                upload,
                rootFolderId!,
                RequireJsonString(settings, "clientID"),
                clientSecret,
                RequireJsonString(appAuth, "publicKeyID"),
                RequireJsonString(appAuth, "privateKey"),
                RequireJsonString(appAuth, "passphrase"),
                RequireJsonString(root, "enterpriseID"),
                holdingFolderId);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new InvalidOperationException("Box:ConfigJson is not a valid Box JWT configuration.", exception);
        }
    }

    private static bool IsUnresolvedKeyVaultReference(string? value) =>
        value is not null
        && value.TrimStart().StartsWith("@Microsoft.KeyVault(", StringComparison.OrdinalIgnoreCase);

    private static string RequireJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidOperationException($"Box JWT configuration omitted {propertyName}.");
        }
        return property.GetString()!;
    }

    private static Uri RequireBoxUri(string? value, string host, string key)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{key} must be the approved Box HTTPS endpoint.");
        }
        return uri.AbsoluteUri.EndsWith('/')
            ? uri
            : new Uri($"{uri.AbsoluteUri}/", UriKind.Absolute);
    }
}

internal interface IBoxAuthorizationHeaderProvider
{
    Task<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken);
}

/// <summary>The access token Box granted, and how long it said it lasts.</summary>
internal readonly record struct BoxAccessToken(string? Value, long? LifetimeSeconds);

/// <summary>
/// Holds one Box access token and renews it before Box's stated expiry.
///
/// PLAT-039: this used to call the SDK's
/// <c>RetrieveAuthorizationHeaderAsync</c>, which answers from a token cache
/// the SDK never expires — it re-mints only when the cache is empty, and
/// leaves 401 recovery to its own HTTP client. Pegasus calls Box with its own
/// <see cref="HttpClient"/>, so that recovery never ran: a long-lived Web
/// container minted one token and reused it for the life of the replica, and
/// every Box call failed with 401 an hour after start.
///
/// The lifetime is read from Box's own response rather than assumed, and the
/// mint is single-flight so a burst of concurrent Box work takes one token,
/// not one each.
/// </summary>
internal sealed class BoxJwtAuthorizationHeaderProvider : IBoxAuthorizationHeaderProvider, IDisposable
{
    /// <summary>
    /// How long a Box request is allowed to run. Declared here, beside the
    /// renewal margin that has to exceed it, and read by the registration that
    /// builds the client — the margin's correctness depends on this number, so
    /// the two are joined by the compiler rather than by a comment.
    /// </summary>
    internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Renew this far ahead of expiry, so a request that starts just under the
    /// wire still holds a live token for its whole life. Longer than
    /// <see cref="RequestTimeout"/>, or a long photograph transfer could begin
    /// inside the margin and still be running after the token died — the
    /// intermittent-looking 401 this class exists to remove.
    /// </summary>
    private static readonly TimeSpan RenewalMargin = RequestTimeout + TimeSpan.FromSeconds(20);

    private readonly Func<CancellationToken, Task<BoxAccessToken>> mint;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>
    /// Header and expiry together in one immutable object, so the lock-free
    /// read below takes a single reference and can never see one token's
    /// header against another's expiry.
    /// </summary>
    private sealed record Lease(string Header, DateTimeOffset ExpiresAtUtc);

    private Lease? lease;

    public BoxJwtAuthorizationHeaderProvider(BoxCustodyOptions options, TimeProvider timeProvider)
        : this(SdkMint(options), timeProvider)
    {
    }

    /// <summary>
    /// The mint seam. <c>BoxJwtAuth</c> is a concrete SDK class with no
    /// interface, so without this the renewal rule cannot be tested at all —
    /// which is how a token that never renewed reached production.
    /// </summary>
    internal BoxJwtAuthorizationHeaderProvider(
        Func<CancellationToken, Task<BoxAccessToken>> mint,
        TimeProvider timeProvider)
    {
        this.mint = mint;
        this.timeProvider = timeProvider;
    }

    public async Task<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken)
    {
        if (Live(timeProvider.GetUtcNow()) is { } current)
        {
            return current;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            if (Live(now) is { } renewed)
            {
                return renewed;
            }

            var token = await mint(cancellationToken);
            // A token that expires inside the renewal margin would never be
            // live, so every Box call would mint another — a silent storm
            // against Box's token endpoint instead of a fault anyone can see.
            // Box JWT tokens last an hour; anything shorter is a broken
            // premise, and this says so rather than absorbing it.
            if (string.IsNullOrWhiteSpace(token.Value)
                || token.LifetimeSeconds is not > 0
                || TimeSpan.FromSeconds(token.LifetimeSeconds.Value) <= RenewalMargin)
            {
                throw new InvalidOperationException(
                    "Box JWT authentication returned no usable access token.");
            }

            var renewal = new Lease(
                $"Bearer {token.Value}",
                now + TimeSpan.FromSeconds(token.LifetimeSeconds.Value));
            Volatile.Write(ref lease, renewal);
            return renewal.Header;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();

    private string? Live(DateTimeOffset now) =>
        Volatile.Read(ref lease) is { } held && now + RenewalMargin < held.ExpiresAtUtc
            ? held.Header
            : null;

    private static Func<CancellationToken, Task<BoxAccessToken>> SdkMint(BoxCustodyOptions options)
    {
        var authentication = new Lazy<(BoxJwtAuth Auth, NetworkSession Session)>(() =>
        {
            var configuration = new JwtConfig(
                options.ClientId,
                options.ClientSecret,
                options.JwtKeyId,
                options.PrivateKey,
                options.PrivateKeyPassphrase)
            {
                EnterpriseId = options.EnterpriseId
            };
            return (new BoxJwtAuth(configuration), new NetworkSession());
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        return async cancellationToken =>
        {
            var (auth, session) = authentication.Value;
            var token = await auth.RefreshTokenAsync(session).WaitAsync(cancellationToken);
            return new(token?.AccessTokenField, token?.ExpiresIn);
        };
    }
}

/// <summary>
/// The shared, root-fenced Box object primitives. Every Box caller goes through
/// this type so the approved-root descendant check and the duplicate-child and
/// trashed-object failures are proved in exactly one place.
/// </summary>
internal sealed class BoxContentClient(
    BoxCustodyOptions options,
    HttpClient httpClient,
    IBoxAuthorizationHeaderProvider authorizationHeaderProvider)
{
    internal string HoldingFolderId => options.HoldingFolderId;
    internal sealed record BoxItem(
        string Id,
        string Name,
        string Type,
        string? ETag,
        string? VersionId,
        long? Size,
        string? MediaType,
        string? ParentId);

    public string RootFolderId => options.RootFolderId;

    public async Task<BoxItem> GetOrCreateFolderAsync(
        string parentId,
        string name,
        CancellationToken cancellationToken)
    {
        await EnsureDescendantAsync(parentId, cancellationToken);
        return await FindChildAsync(parentId, name, "folder", cancellationToken)
            ?? await CreateFolderAsync(parentId, name, cancellationToken);
    }

    public Task<IReadOnlyList<BoxItem>> ListChildrenAsync(
        string parentId,
        CancellationToken cancellationToken) =>
        CollectChildrenAsync(parentId, nameFilter: null, cancellationToken);

    private async Task<IReadOnlyList<BoxItem>> CollectChildrenAsync(
        string parentId,
        string? nameFilter,
        CancellationToken cancellationToken)
    {
        await EnsureDescendantAsync(parentId, cancellationToken);
        const int pageLimit = 1000;
        var offset = 0;
        var children = new List<BoxItem>();
        while (true)
        {
            var uri = new Uri(options.BaseUri,
                $"folders/{Uri.EscapeDataString(parentId)}/items?fields=id,name,type,etag,file_version,size,content_type,parent&limit={pageLimit}&offset={offset}");
            using var response = await SendAsync(HttpMethod.Get, uri, null, cancellationToken);
            using var document = await ReadSuccessJsonAsync(response, cancellationToken);
            var entries = document.RootElement.GetProperty("entries").EnumerateArray().ToArray();
            children.AddRange(entries
                .Where(item => nameFilter is null || ReadString(item, "name") == nameFilter)
                .Select(ParseItem));
            if (entries.Length < pageLimit)
            {
                break;
            }
            offset += entries.Length;
        }
        return children;
    }

    public async Task<BoxItem?> FindChildAsync(
        string parentId,
        string name,
        string type,
        CancellationToken cancellationToken) =>
        SelectChild(await CollectChildrenAsync(parentId, name, cancellationToken), name, type);

    /// <summary>
    /// The one child with this exact name and type, or null — the duplicate and
    /// wrong-type refusals written once, so a caller that already holds a
    /// listing decides them the same way <see cref="FindChildAsync"/> does
    /// rather than asking Box again for what it has (PLAT-041).
    /// </summary>
    public static BoxItem? SelectChild(IEnumerable<BoxItem> children, string name, string type)
    {
        BoxItem? match = null;
        foreach (var child in children)
        {
            if (!string.Equals(child.Name, name, StringComparison.Ordinal))
            {
                continue;
            }
            if (match is not null)
            {
                throw new InvalidDataException("Box contains duplicate custody children for one exact identity.");
            }
            match = child;
        }
        if (match is not null && !string.Equals(match.Type, type, StringComparison.Ordinal))
        {
            throw new InvalidDataException("A Box custody child has the expected name but the wrong type.");
        }
        return match;
    }

    public async Task<BoxItem> CreateFolderAsync(
        string parentId,
        string name,
        CancellationToken cancellationToken)
    {
        await EnsureDescendantAsync(parentId, cancellationToken);
        using var content = JsonContent.Create(new { name, parent = new { id = parentId } });
        using var response = await SendAsync(HttpMethod.Post, new Uri(options.BaseUri, "folders"), content, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return await FindChildAsync(parentId, name, "folder", cancellationToken)
                ?? throw new InvalidDataException("Box reported a folder conflict without the exact existing folder.");
        }
        using var document = await ReadSuccessJsonAsync(response, cancellationToken);
        var folder = ParseItem(document.RootElement);
        await EnsureDescendantAsync(folder.Id, cancellationToken);
        return folder;
    }

    public async Task<BoxItem> GetFolderAsync(
        string folderId,
        CancellationToken cancellationToken)
    {
        await EnsureDescendantAsync(folderId, cancellationToken);
        using var response = await SendAsync(
            HttpMethod.Get,
            new Uri(options.BaseUri,
                $"folders/{Uri.EscapeDataString(folderId)}?fields=id,name,type,etag,parent,trashed_at"),
            null,
            cancellationToken);
        using var document = await ReadSuccessJsonAsync(response, cancellationToken);
        var folder = ParseItem(document.RootElement);
        if (!string.Equals(folder.Type, "folder", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Box returned the wrong type for a custody folder.");
        }
        return folder;
    }

    public async Task<BoxItem> GetFileAsync(
        string fileId,
        CancellationToken cancellationToken)
    {
        await EnsureDescendantAsync(fileId, cancellationToken, isFile: true);
        using var response = await SendAsync(
            HttpMethod.Get,
            new Uri(options.BaseUri,
                $"files/{Uri.EscapeDataString(fileId)}?fields=id,name,type,etag,file_version,size,content_type,parent,trashed_at"),
            null,
            cancellationToken);
        using var document = await ReadSuccessJsonAsync(response, cancellationToken);
        var file = ParseItem(document.RootElement);
        if (!string.Equals(file.Type, "file", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Box returned the wrong type for a custody file.");
        }
        return file;
    }

    public async Task<BoxItem> RenameFolderAsync(
        string folderId,
        string name,
        string etag,
        CancellationToken cancellationToken)
    {
        await EnsureDescendantAsync(folderId, cancellationToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(etag);
        using var content = JsonContent.Create(new { name });
        using var response = await SendAsync(
            HttpMethod.Put,
            new Uri(options.BaseUri, $"folders/{Uri.EscapeDataString(folderId)}"),
            content,
            cancellationToken,
            request => request.Headers.TryAddWithoutValidation("If-Match", etag));
        using var document = await ReadSuccessJsonAsync(response, cancellationToken);
        var folder = ParseItem(document.RootElement);
        await EnsureDescendantAsync(folder.Id, cancellationToken);
        return folder;
    }

    public async Task<BoxItem> UploadAsync(
        string parentId,
        string name,
        ReadOnlyMemory<byte> content,
        string mediaType,
        CancellationToken cancellationToken)
    {
        await EnsureDescendantAsync(parentId, cancellationToken);
        using var multipart = new MultipartFormDataContent();
        multipart.Add(JsonContent.Create(new { name, parent = new { id = parentId } }), "attributes");
        var fileContent = new ByteArrayContent(content.ToArray());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
        multipart.Add(fileContent, "file", name);
        using var response = await SendAsync(HttpMethod.Post, new Uri(options.UploadUri, "files/content"), multipart, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var errorCode = await ReadBoxErrorCodeAsync(response, cancellationToken);
            if (string.Equals(errorCode, "item_name_in_use", StringComparison.Ordinal))
            {
                var existing = await FindChildAsync(parentId, name, "file", cancellationToken)
                    ?? throw new HttpRequestException(
                        "Box reported an occupied file name but the exact existing file could not be resolved.",
                        null,
                        HttpStatusCode.Conflict);
                if (string.IsNullOrWhiteSpace(existing.VersionId))
                {
                    throw new InvalidDataException("Box omitted the existing file version identity.");
                }
                if (existing.Size is { } size && size != content.Length)
                {
                    throw new InvalidDataException(
                        "The occupied Box file name contains different content.");
                }
                await using var retained = await OpenVersionReadAsync(
                    existing.Id,
                    existing.VersionId,
                    content.Length,
                    cancellationToken);
                var verified = new byte[content.Length];
                await retained.ReadExactlyAsync(verified, cancellationToken);
                if (await retained.ReadAsync(new byte[1], cancellationToken) != 0
                    || !verified.AsSpan().SequenceEqual(content.Span))
                {
                    throw new InvalidDataException(
                        "The occupied Box file name contains different content.");
                }
                return existing;
            }
            throw new HttpRequestException(
                string.Equals(errorCode, "name_temporarily_reserved", StringComparison.Ordinal)
                    ? "Box temporarily reserved the deterministic custody file name; retry reconciliation later."
                    : "Box rejected the deterministic custody file name.",
                null,
                HttpStatusCode.Conflict);
        }
        using var document = await ReadSuccessJsonAsync(response, cancellationToken);
        var entries = document.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        if (entries.Length != 1)
        {
            throw new InvalidDataException("Box upload returned an unexpected file count.");
        }
        var result = ParseItem(entries[0]);
        await EnsureDescendantAsync(result.Id, cancellationToken, isFile: true);
        return result;
    }

    public async Task<byte[]> DownloadAsync(string fileId, CancellationToken cancellationToken)
    {
        await EnsureDescendantAsync(fileId, cancellationToken, isFile: true);
        return await DownloadContentAsync(fileId, cancellationToken);
    }

    public async Task<Stream> OpenVersionReadAsync(
        string fileId,
        string versionId,
        long maximumLength,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionId);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);
        await EnsureDescendantAsync(fileId, cancellationToken, isFile: true);
        return await DownloadVersionAsync(fileId, versionId, maximumLength, cancellationToken);
    }

    public async Task<Stream> OpenOwnedVersionReadAsync(
        string fileId,
        string versionId,
        string expectedParentId,
        long maximumLength,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedParentId);
        using var metadataResponse = await SendAsync(
            HttpMethod.Get,
            new Uri(options.BaseUri,
                $"files/{Uri.EscapeDataString(fileId)}?fields=id,name,type,etag,file_version,size,content_type,parent,trashed_at"),
            null,
            cancellationToken);
        using var metadataDocument = await ReadSuccessJsonAsync(metadataResponse, cancellationToken);
        var file = ParseItem(metadataDocument.RootElement);
        if (!string.Equals(file.Type, "file", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Box returned the wrong type for a custody file.");
        }
        if (!string.Equals(file.ParentId, expectedParentId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The Box file is outside its expected Case root.");
        }
        await EnsureDescendantAsync(expectedParentId, cancellationToken);
        return await DownloadVersionWithoutAncestryAsync(
            fileId, versionId, maximumLength, cancellationToken);
    }

    private async Task<Stream> DownloadVersionAsync(
        string fileId,
        string versionId,
        long maximumLength,
        CancellationToken cancellationToken) =>
        await DownloadVersionWithoutAncestryAsync(
            fileId, versionId, maximumLength, cancellationToken);

    private async Task<Stream> DownloadVersionWithoutAncestryAsync(
        string fileId,
        string versionId,
        long maximumLength,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionId);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);
        using var response = await SendAsync(
            HttpMethod.Get,
            new Uri(options.BaseUri,
                $"files/{Uri.EscapeDataString(fileId)}/content?version={Uri.EscapeDataString(versionId)}"),
            null,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Box version download returned {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }
        if (response.Content.Headers.ContentLength is { } length && length > maximumLength)
        {
            throw new InvalidDataException("Box version content exceeds its recorded length.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        var temporaryPath = Path.Combine(
            Path.GetTempPath(),
            $"pegasus-box-version-{Guid.NewGuid():N}.tmp");
        var retained = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try
        {
            var buffer = new byte[81920];
            long copied = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }
                copied = checked(copied + read);
                if (copied > maximumLength)
                {
                    throw new InvalidDataException("Box version content exceeds its recorded length.");
                }
                await retained.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            retained.Position = 0;
            return retained;
        }
        catch
        {
            await retained.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Downloads a file whose descent from the approved root is already proved:
    /// <paramref name="listedChild"/> was returned by listing
    /// <paramref name="fencedParentId"/>, and that folder's own descent was
    /// proved when it was listed.
    ///
    /// PLAT-041: <see cref="EnsureDescendantAsync"/> re-walks the same ancestry
    /// on every call, one GET per level, and it dominated the case export —
    /// roughly twenty of its forty-five Box round trips proved, over and over,
    /// what the listing had just established. The fence is re-checked here
    /// rather than assumed: the caller must hand back the parent it listed
    /// under, and the child must still claim it. Nothing is remembered between
    /// calls, so a Box-side move cannot be read through a stale identity —
    /// the next operation resolves the folder again and fails loudly.
    ///
    /// The listing is the proof, not the parent Box restates on each entry: a
    /// stated parent that disagrees is refused, but a parent Box declines to
    /// send cannot refuse a child that was returned by listing the fenced
    /// folder itself. DOCS-010 is what that sentence is for — a field Box
    /// silently omitted made every managed read fail in production, and no
    /// check here may be made to depend on Box volunteering one.
    ///
    /// Callers holding only an identifier must use <see cref="DownloadAsync"/>.
    /// </summary>
    public async Task<byte[]> DownloadFencedAsync(
        BoxItem listedChild,
        string fencedParentId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(listedChild);
        if (listedChild.ParentId is { Length: > 0 } parentId
            && !string.Equals(parentId, fencedParentId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The Box object is outside the approved custody root.");
        }
        return await DownloadContentAsync(listedChild.Id, cancellationToken);
    }

    private async Task<byte[]> DownloadContentAsync(string fileId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            new Uri(options.BaseUri, $"files/{Uri.EscapeDataString(fileId)}/content"),
            null,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Box download returned {(int)response.StatusCode}.");
        }
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<BoxItem> MoveFileAsync(
        string fileId,
        string newParentId,
        string name,
        CancellationToken cancellationToken)
    {
        await EnsureDescendantAsync(fileId, cancellationToken, isFile: true);
        await EnsureDescendantAsync(newParentId, cancellationToken);
        using var content = JsonContent.Create(new { name, parent = new { id = newParentId } });
        using var response = await SendAsync(
            HttpMethod.Put,
            new Uri(options.BaseUri, $"files/{Uri.EscapeDataString(fileId)}"),
            content,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new InvalidDataException(
                "The Box destination already holds a different item with the moved file's name.");
        }
        using var document = await ReadSuccessJsonAsync(response, cancellationToken);
        var file = ParseItem(document.RootElement);
        await EnsureDescendantAsync(file.Id, cancellationToken, isFile: true);
        return file;
    }

    public async Task DeleteFolderAsync(string folderId, CancellationToken cancellationToken)
    {
        if (folderId.Equals(options.RootFolderId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The approved custody root can never be removed.");
        }
        await EnsureDescendantAsync(folderId, cancellationToken);
        // Deliberately non-recursive: Box refuses to delete a non-empty
        // folder, so anything unexpectedly still inside fails the removal
        // closed instead of being destroyed with it.
        using var response = await SendAsync(
            HttpMethod.Delete,
            new Uri(options.BaseUri, $"folders/{Uri.EscapeDataString(folderId)}"),
            null,
            cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent
            || response.IsSuccessStatusCode)
        {
            return;
        }
        throw new HttpRequestException($"Box folder delete returned {(int)response.StatusCode}.");
    }

    public async Task DeleteFileAsync(string fileId, CancellationToken cancellationToken)
    {
        await EnsureDescendantAsync(fileId, cancellationToken, isFile: true);
        using var response = await SendAsync(
            HttpMethod.Delete,
            new Uri(options.BaseUri, $"files/{Uri.EscapeDataString(fileId)}"),
            null,
            cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent
            || response.IsSuccessStatusCode)
        {
            return;
        }
        throw new HttpRequestException($"Box delete returned {(int)response.StatusCode}.");
    }

    public async Task EnsureDescendantAsync(
        string itemId,
        CancellationToken cancellationToken,
        bool isFile = false)
    {
        if (itemId.Equals(options.RootFolderId, StringComparison.Ordinal))
        {
            return;
        }
        var type = isFile ? "files" : "folders";
        var current = itemId;
        for (var depth = 0; depth < 100; depth++)
        {
            var uri = new Uri(options.BaseUri,
                $"{type}/{Uri.EscapeDataString(current)}?fields=id,parent,trashed_at");
            using var response = await SendAsync(HttpMethod.Get, uri, null, cancellationToken);
            using var document = await ReadSuccessJsonAsync(response, cancellationToken);
            if (document.RootElement.TryGetProperty("trashed_at", out var trashed)
                && trashed.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                throw new UnauthorizedAccessException("A Box custody object is in trash.");
            }
            if (!document.RootElement.TryGetProperty("parent", out var parent)
                || parent.ValueKind == JsonValueKind.Null)
            {
                break;
            }
            current = ReadString(parent, "id")
                ?? throw new InvalidDataException("Box omitted a parent identity.");
            if (current.Equals(options.RootFolderId, StringComparison.Ordinal))
            {
                return;
            }
            type = "folders";
        }
        throw new UnauthorizedAccessException("The Box object is outside the approved custody root.");
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        Uri uri,
        HttpContent? content,
        CancellationToken cancellationToken,
        Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = content };
        configure?.Invoke(request);
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(
            await authorizationHeaderProvider.GetAuthorizationHeaderAsync(cancellationToken));
        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static async Task<JsonDocument> ReadSuccessJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Box returned {(int)response.StatusCode}; response length {body.Length}.",
                null,
                response.StatusCode);
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static async Task<string?> ReadBoxErrorCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(body);
            return ReadString(document.RootElement, "code");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static BoxItem ParseItem(JsonElement value) => new(
        ReadString(value, "id") ?? throw new InvalidDataException("Box omitted an item identity."),
        ReadString(value, "name") ?? string.Empty,
        ReadString(value, "type") ?? string.Empty,
        ReadString(value, "etag"),
        value.TryGetProperty("file_version", out var fileVersion)
            && fileVersion.ValueKind == JsonValueKind.Object
                ? ReadString(fileVersion, "id")
                : null,
        value.TryGetProperty("size", out var size)
            && size.ValueKind == JsonValueKind.Number
            && size.TryGetInt64(out var length)
                ? length
                : null,
        ReadString(value, "content_type"),
        value.TryGetProperty("parent", out var parent) && parent.ValueKind == JsonValueKind.Object
            ? ReadString(parent, "id")
            : null);

    private static string? ReadString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var result) && result.ValueKind == JsonValueKind.String
            ? result.GetString()
            : null;
}

internal sealed class BoxCaseCustody(
    IIntakeArtifactStore artifactStore,
    BoxContentClient client) : ICaseCustody
{
    private const string CaseBindingFileName = "pegasus-case-binding.json";
    private const string CreationAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public async Task<CaseCustodyRoot> CreateCaseRootAsync(
        Guid caseId,
        string caseReference,
        string creationOwnerToken,
        string operationKey,
        CancellationToken cancellationToken)
        => await CreateCaseRootCoreAsync(
            caseId, caseReference, creationOwnerToken, operationKey, null, cancellationToken);

    public async Task<CaseCustodyRoot> CreateCaseRootAsync(
        Guid caseId,
        string caseReference,
        string creationOwnerToken,
        string operationKey,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
        => await CreateCaseRootCoreAsync(
            caseId, caseReference, creationOwnerToken, operationKey, leaseGuard, cancellationToken);

    private async Task<CaseCustodyRoot> CreateCaseRootCoreAsync(
        Guid caseId,
        string caseReference,
        string creationOwnerToken,
        string operationKey,
        CustodyEffectLeaseGuard? leaseGuard,
        CancellationToken cancellationToken)
    {
        ValidateCase(caseId, caseReference);
        ValidateOperation(operationKey);
        var folder = await GetOrCreateOwnedFolderAsync(
            client.RootFolderId,
            CaseFolderName(caseReference),
            creationOwnerToken,
            leaseGuard,
            cancellationToken);
        return new(caseId, folder.Id, caseReference);
    }

    public async Task<CaseCustodyRoot> GetExistingCaseRootAsync(
        Guid caseId,
        string caseReference,
        CancellationToken cancellationToken)
    {
        ValidateCase(caseId, caseReference);
        var folder = await client.FindChildAsync(
            client.RootFolderId,
            CaseFolderName(caseReference),
            "folder",
            cancellationToken)
            ?? throw new InvalidOperationException("The case custody root has not been created.");
        await VerifyFolderIdentityAsync(folder, client.RootFolderId, folder.Name, cancellationToken);
        return new(caseId, folder.Id, caseReference);
    }

    public async Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        string operationKey,
        CancellationToken cancellationToken)
        => await RetainAcceptedIntakeSourceCoreAsync(
            root, source, operationKey, null, cancellationToken);

    public async Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        string operationKey,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
        => await RetainAcceptedIntakeSourceCoreAsync(
            root, source, operationKey, leaseGuard, cancellationToken);

    private async Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceCoreAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        string operationKey,
        CustodyEffectLeaseGuard? leaseGuard,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(source);
        ValidateOperation(operationKey);
        await ValidateRootAsync(root, cancellationToken);
        var (content, actualHash) = await ReadVerifiedSourceAsync(source, cancellationToken);

        // Operator direction (2026-08-21): the case folder holds the files.
        // The Evidence / Original instruction nesting was never asked for.
        var fileName = $"001 {SafeName(source.SourceFileName)}";
        var file = await UploadOrVerifyFileAsync(
            root.RemoteId, fileName, content, source.MediaType, leaseGuard, cancellationToken);
        return new(
            root.CaseId,
            file.Id,
            actualHash,
            file.ETag ?? actualHash,
            file.VersionId
                ?? throw new InvalidDataException("Box omitted the retained file version identity."));
    }

    public async Task<CustodyDocumentVersion> RetainAcceptedIntakeAttachmentAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference attachment,
        int ordinal,
        string operationKey,
        CancellationToken cancellationToken)
        => await RetainAcceptedIntakeAttachmentCoreAsync(
            root, attachment, ordinal, operationKey, null, cancellationToken);

    public async Task<CustodyDocumentVersion> RetainAcceptedIntakeAttachmentAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference attachment,
        int ordinal,
        string operationKey,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
        => await RetainAcceptedIntakeAttachmentCoreAsync(
            root, attachment, ordinal, operationKey, leaseGuard, cancellationToken);

    private async Task<CustodyDocumentVersion> RetainAcceptedIntakeAttachmentCoreAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference attachment,
        int ordinal,
        string operationKey,
        CustodyEffectLeaseGuard? leaseGuard,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(attachment);
        ArgumentOutOfRangeException.ThrowIfLessThan(ordinal, 2);
        ValidateOperation(operationKey);
        await ValidateRootAsync(root, cancellationToken);
        var (content, actualHash) = await ReadVerifiedSourceAsync(attachment, cancellationToken);

        var fileName = $"{ordinal:D3} {SafeName(attachment.SourceFileName)}";
        var file = await UploadOrVerifyFileAsync(
            root.RemoteId, fileName, content, attachment.MediaType, leaseGuard, cancellationToken);
        return new(
            root.CaseId,
            file.Id,
            actualHash,
            file.ETag ?? actualHash,
            file.VersionId
                ?? throw new InvalidDataException("Box omitted the retained file version identity."));
    }

    public async Task<CustodyDocumentVersion> RetainImageCaseAssetAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        int ordinal,
        string operationKey,
        CancellationToken cancellationToken)
        => await RetainImageCaseAssetCoreAsync(
            root, source, ordinal, operationKey, null, cancellationToken);

    public async Task<CustodyDocumentVersion> RetainImageCaseAssetAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        int ordinal,
        string operationKey,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
        => await RetainImageCaseAssetCoreAsync(
            root, source, ordinal, operationKey, leaseGuard, cancellationToken);

    private async Task<CustodyDocumentVersion> RetainImageCaseAssetCoreAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        int ordinal,
        string operationKey,
        CustodyEffectLeaseGuard? leaseGuard,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(ordinal, 1);
        ValidateOperation(operationKey);
        await ValidateRootAsync(root, cancellationToken);
        var (content, actualHash) = await ReadVerifiedSourceAsync(source, cancellationToken);

        var fileName = $"{ordinal:000} {SafeName(source.SourceFileName)}";
        var file = await UploadOrVerifyFileAsync(
            root.RemoteId, fileName, content, source.MediaType, leaseGuard, cancellationToken);
        return new(
            root.CaseId,
            file.Id,
            actualHash,
            file.ETag ?? actualHash,
            file.VersionId
                ?? throw new InvalidDataException("Box omitted the retained file version identity."));
    }

    public async Task MergeImageCaseContentsAsync(
        CaseCustodyRoot imageRoot,
        CaseCustodyRoot caseRoot,
        string operationKey,
        CancellationToken cancellationToken)
        => await MergeImageCaseContentsCoreAsync(
            imageRoot, caseRoot, operationKey, null, cancellationToken);

    public async Task MergeImageCaseContentsAsync(
        CaseCustodyRoot imageRoot,
        CaseCustodyRoot caseRoot,
        string operationKey,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
        => await MergeImageCaseContentsCoreAsync(
            imageRoot, caseRoot, operationKey, leaseGuard, cancellationToken);

    private async Task MergeImageCaseContentsCoreAsync(
        CaseCustodyRoot imageRoot,
        CaseCustodyRoot caseRoot,
        string operationKey,
        CustodyEffectLeaseGuard? leaseGuard,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageRoot);
        ArgumentNullException.ThrowIfNull(caseRoot);
        ValidateOperation(operationKey);
        // A previous attempt that emptied and removed the image-case folder
        // but could not persist its completion replays as an already-complete
        // fold; the non-recursive removal below proves the folder was empty.
        var imageFolder = await client.FindChildAsync(
            client.RootFolderId,
            CaseFolderName(imageRoot.Reference),
            "folder",
            cancellationToken);
        if (imageFolder is null)
        {
            return;
        }
        ValidateCase(imageRoot.CaseId, imageRoot.Reference);
        if (!imageFolder.Id.Equals(imageRoot.RemoteId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The custody root does not match the retained case identity.");
        }
        await ValidateRootAsync(caseRoot, cancellationToken);

        // Flat, like everything else in the case folder (operator direction,
        // 2026-08-21). The folded images join the instruction's files rather
        // than going into an Evidence/Images pocket of their own; the
        // existing name-collision rule below already keeps them distinct.
        var destination = caseRoot.RemoteId;

        var children = await client.ListChildrenAsync(imageRoot.RemoteId, cancellationToken);
        var destinationNames = new HashSet<string>(
            (await client.ListChildrenAsync(destination, cancellationToken))
                .Select(item => item.Name),
            StringComparer.Ordinal);
        foreach (var child in children)
        {
            if (!string.Equals(child.Type, "file", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The image-case custody folder holds an unexpected non-file child; the fold fails closed.");
            }
            if (string.Equals(child.Name, CaseBindingFileName, StringComparison.Ordinal))
            {
                continue;
            }

            // A same-named file already folded from another Image intake keeps
            // its name unique by prefixing the source reference.
            string targetName;
            if (destinationNames.Add(child.Name))
            {
                targetName = child.Name;
            }
            else
            {
                targetName = $"{CaseFolderName(imageRoot.Reference)} {child.Name}";
                destinationNames.Add(targetName);
            }
            await RequireLeaseAsync(leaseGuard, cancellationToken);
            await client.MoveFileAsync(child.Id, destination, targetName, cancellationToken);
        }

        var binding = await client.FindChildAsync(
            imageRoot.RemoteId, CaseBindingFileName, "file", cancellationToken);
        if (binding is not null)
        {
            await RequireLeaseAsync(leaseGuard, cancellationToken);
            await client.DeleteFileAsync(binding.Id, cancellationToken);
        }
        await RequireLeaseAsync(leaseGuard, cancellationToken);
        await client.DeleteFolderAsync(imageRoot.RemoteId, cancellationToken);
    }

    public async Task<string> CreateAuditReferenceFolderAsync(
        CaseCustodyRoot root,
        string auditReference,
        string creationOwnerToken,
        string operationKey,
        CancellationToken cancellationToken)
        => await CreateAuditReferenceFolderCoreAsync(
            root, auditReference, creationOwnerToken, operationKey, null, cancellationToken);

    public async Task<string> CreateAuditReferenceFolderAsync(
        CaseCustodyRoot root,
        string auditReference,
        string creationOwnerToken,
        string operationKey,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
        => await CreateAuditReferenceFolderCoreAsync(
            root, auditReference, creationOwnerToken, operationKey, leaseGuard, cancellationToken);

    private async Task<string> CreateAuditReferenceFolderCoreAsync(
        CaseCustodyRoot root,
        string auditReference,
        string creationOwnerToken,
        string operationKey,
        CustodyEffectLeaseGuard? leaseGuard,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(auditReference);
        ValidateOperation(operationKey);
        await ValidateRootAsync(root, cancellationToken);
        var folder = await GetOrCreateOwnedFolderAsync(
            root.RemoteId,
            CustodyNames.SafeName(auditReference),
            creationOwnerToken,
            leaseGuard,
            cancellationToken);
        return folder.Id;
    }

    private async Task ValidateRootAsync(CaseCustodyRoot root, CancellationToken cancellationToken)
    {
        ValidateCase(root.CaseId, root.Reference);
        var expected = await client.FindChildAsync(
            client.RootFolderId,
            CaseFolderName(root.Reference),
            "folder",
            cancellationToken)
            ?? throw new InvalidOperationException("The case custody root has not been created.");
        if (!expected.Id.Equals(root.RemoteId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The custody root does not match the retained case identity.");
        }
    }

    private async Task<(ReadOnlyMemory<byte> Content, string Hash)> ReadVerifiedSourceAsync(
        IntakeSourceCustodyReference source,
        CancellationToken cancellationToken)
    {
        var content = await artifactStore.ReadAsync(source.SourceObjectKey, cancellationToken)
            ?? throw new FileNotFoundException("The retained intake source is unavailable.");
        var actualHash = Convert.ToHexString(SHA256.HashData(content.Span));
        if (!actualHash.Equals(source.SourceHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The retained intake source failed its custody integrity check.");
        }
        if (source.SourceLength >= 0 && source.SourceLength != content.Length)
        {
            throw new InvalidDataException("The retained intake source failed its custody length check.");
        }
        return (content, actualHash);
    }

    private async Task<BoxContentClient.BoxItem> UploadOrVerifyFileAsync(
        string parentId,
        string fileName,
        ReadOnlyMemory<byte> content,
        string mediaType,
        CustodyEffectLeaseGuard? leaseGuard,
        CancellationToken cancellationToken)
    {
        var existing = await client.FindChildAsync(parentId, fileName, "file", cancellationToken);
        if (existing is not null)
        {
            await VerifyFileAsync(existing, parentId, mediaType, content, cancellationToken);
            return existing;
        }
        await RequireLeaseAsync(leaseGuard, cancellationToken);
        return await client.UploadAsync(parentId, fileName, content, mediaType, cancellationToken);
    }

    /// <summary>
    /// The staged two-phase folder create: a crash between create and rename
    /// leaves an owner-token staging folder the same replay resumes, and a
    /// same-name folder created by anything else is accepted as the case's —
    /// the durable folder identity lives in the database (DOCS-005), not in a
    /// marker file inside the folder.
    /// </summary>
    private async Task<BoxContentClient.BoxItem> GetOrCreateOwnedFolderAsync(
        string parentId,
        string finalName,
        string creationOwnerToken,
        CustodyEffectLeaseGuard? leaseGuard,
        CancellationToken cancellationToken)
    {
        ValidateCreationOwnerToken(creationOwnerToken);
        var existing = await client.FindChildAsync(parentId, finalName, "folder", cancellationToken);
        if (existing is not null)
        {
            await VerifyFolderIdentityAsync(existing, parentId, finalName, cancellationToken);
            return existing;
        }

        var stagingName = $".pegasus-create-{creationOwnerToken}";
        var staging = await client.FindChildAsync(parentId, stagingName, "folder", cancellationToken);
        if (staging is null)
        {
            await RequireLeaseAsync(leaseGuard, cancellationToken);
            staging = await client.CreateFolderAsync(parentId, stagingName, cancellationToken);
        }
        await VerifyFolderIdentityAsync(staging, parentId, stagingName, cancellationToken);

        var finalConflict = await client.FindChildAsync(parentId, finalName, "folder", cancellationToken);
        if (finalConflict is not null)
        {
            if (string.Equals(finalConflict.Id, staging.Id, StringComparison.Ordinal))
            {
                return finalConflict;
            }
            throw new InvalidDataException(
                "The final Box custody name is already occupied by another folder.");
        }

        var latest = await client.GetFolderAsync(staging.Id, cancellationToken);
        await RequireLeaseAsync(leaseGuard, cancellationToken);
        var promoted = await client.RenameFolderAsync(
            staging.Id,
            finalName,
            latest.ETag ?? throw new InvalidDataException("Box omitted the staging folder ETag."),
            cancellationToken);
        if (!string.Equals(promoted.Id, staging.Id, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Box changed the custody folder identity during promotion.");
        }
        return promoted;
    }

    private async Task<BoxContentClient.BoxItem> GetOrCreateFolderAsync(
        string parentId,
        string name,
        CustodyEffectLeaseGuard? leaseGuard,
        CancellationToken cancellationToken)
    {
        var existing = await client.FindChildAsync(parentId, name, "folder", cancellationToken);
        if (existing is not null)
        {
            return existing;
        }
        await RequireLeaseAsync(leaseGuard, cancellationToken);
        return await client.CreateFolderAsync(parentId, name, cancellationToken);
    }

    private static Task RequireLeaseAsync(
        CustodyEffectLeaseGuard? leaseGuard,
        CancellationToken cancellationToken) =>
        leaseGuard?.RequireCurrentAsync(cancellationToken) ?? Task.CompletedTask;


    private async Task VerifyFolderIdentityAsync(
        BoxContentClient.BoxItem folder,
        string parentId,
        string expectedName,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(folder.Type, "folder", StringComparison.Ordinal)
            || !string.Equals(folder.Name, expectedName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The Box custody folder identity is inconsistent.");
        }
        await client.EnsureDescendantAsync(folder.Id, cancellationToken);
        var parent = await client.FindChildAsync(parentId, expectedName, "folder", cancellationToken);
        if (parent is null || !string.Equals(parent.Id, folder.Id, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The Box custody folder parent is inconsistent.");
        }
    }


    private async Task VerifyFileAsync(
        BoxContentClient.BoxItem file,
        string expectedParentId,
        string expectedMediaType,
        ReadOnlyMemory<byte> expected,
        CancellationToken cancellationToken)
    {
        var metadata = await client.GetFileAsync(file.Id, cancellationToken);
        if (!string.Equals(metadata.ParentId, expectedParentId, StringComparison.Ordinal)
            || metadata.Size != expected.Length
            || !string.Equals(metadata.MediaType, expectedMediaType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "A Box custody file has inconsistent immutable type, parent, or length metadata.");
        }
        var actual = await client.DownloadAsync(file.Id, cancellationToken);
        if (!actual.AsSpan().SequenceEqual(expected.Span))
        {
            throw new InvalidDataException("A Box custody file has different immutable content.");
        }
    }

    private static void ValidateCase(Guid caseId, string reference)
    {
        if (caseId == Guid.Empty || string.IsNullOrWhiteSpace(reference) || reference.Any(char.IsControl))
        {
            throw new ArgumentException("A valid immutable case identity is required.");
        }
    }

    private static void ValidateOperation(string operationKey)
    {
        if (string.IsNullOrWhiteSpace(operationKey) || operationKey.Length > 200 || operationKey.Any(char.IsControl))
        {
            throw new ArgumentException("A valid custody operation identity is required.", nameof(operationKey));
        }
    }

    internal static void ValidateCreationOwnerToken(string value)
    {
        if (value.Length != 26 || value.Any(character => !CreationAlphabet.Contains(character)))
        {
            throw new ArgumentException(
                "A valid predeclared custody creation owner is required.",
                nameof(value));
        }
    }

    private static string CaseFolderName(string reference) => CustodyNames.SafeName(reference);

    private static string SafeName(string value) => CustodyNames.SafeName(value);
}
