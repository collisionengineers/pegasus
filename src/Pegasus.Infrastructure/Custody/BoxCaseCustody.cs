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

    /// <summary>
    /// Mints a new token now if the held one is close enough to expiry that a
    /// request would soon have to. Answers whether it minted.
    /// </summary>
    Task<bool> RenewIfDueAsync(CancellationToken cancellationToken);
}

/// <summary>The access token Box granted, and how long it said it lasts.</summary>
internal readonly record struct BoxAccessToken(string? Value, long? LifetimeSeconds);

/// <summary>
/// Holds one Box access token and renews it before Box's stated expiry.
///
/// This used to call the SDK's
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
///
/// <see cref="BoxTokenRenewalService"/> calls <see cref="RenewIfDueAsync"/>
/// so the token is normally replaced in the background, before any request
/// would have to. The request path still mints on demand, so a renewal that
/// fails costs a request the mint it always paid, never a failed read.
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

    /// <summary>
    /// How much sooner than a request would need it the background renewal
    /// replaces the token, so the request path finds a live token and never
    /// waits on a mint.
    /// </summary>
    private static readonly TimeSpan EarlyRenewal = TimeSpan.FromMinutes(5);

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
        // A token that is still live for a request is returned without the
        // gate, even inside the early-renewal window while a background
        // renewal is minting. The request does not wait and mints nothing.
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

            return await MintAsync(now, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Replaces the token when there is none, or when it is within the
    /// renewal margin and the early-renewal window of expiry. It takes the
    /// same gate as a request's mint, so a request that arrives while this
    /// mints waits for this mint and does not start another.
    /// </summary>
    public async Task<bool> RenewIfDueAsync(CancellationToken cancellationToken)
    {
        if (!RenewalDue(timeProvider.GetUtcNow()))
        {
            return false;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            if (!RenewalDue(now))
            {
                return false;
            }

            await MintAsync(now, cancellationToken);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();

    /// <summary>Mints and holds a new token. The caller holds <see cref="gate"/>.</summary>
    private async Task<string> MintAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var token = await mint(cancellationToken);
        // A token that lasts no longer than the renewal margin plus the
        // early-renewal window is refused, because it would be due for renewal
        // the moment it was minted and the 60-second background loop would
        // mint again on every check. A token inside the margin alone would
        // also never be live, so every Box call would mint another. Either
        // way that is a silent storm against Box's token endpoint instead of a
        // fault anyone can see. Box JWT tokens last an hour; anything shorter
        // is a broken premise, and this says so rather than absorbing it.
        if (string.IsNullOrWhiteSpace(token.Value)
            || token.LifetimeSeconds is not > 0
            || TimeSpan.FromSeconds(token.LifetimeSeconds.Value) <= RenewalMargin + EarlyRenewal)
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

    private string? Live(DateTimeOffset now) =>
        Volatile.Read(ref lease) is { } held && now + RenewalMargin < held.ExpiresAtUtc
            ? held.Header
            : null;

    private bool RenewalDue(DateTimeOffset now) =>
        Volatile.Read(ref lease) is not { } held || now + RenewalMargin + EarlyRenewal >= held.ExpiresAtUtc;

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
/// <remarks>
/// The managed read (<see cref="OpenOwnedVersionReadAsync"/>) always reads the
/// file itself — its parent and its trash state — but it remembers, for
/// <see cref="LiveFolderMemory"/>, that the file's folder was proved to sit
/// under the approved root. It asks for the file's bytes at the same time as
/// it reads the file, and discards them unless every check passes. Only a
/// successful proof is remembered. Any 404, trashed or outside-root answer
/// that touches a folder forgets it, and so do a folder delete and a file
/// move. A folder moved out of the root, or trashed, outside Pegasus can
/// therefore still be read for up to that long.
/// A walk that succeeded at the same moment as a forget may remember the
/// folder again, still within that ten-minute bound.
///
/// A write never uses that memory. An upload goes only into a
/// <see cref="ProvedFolder"/>, which fresh reads of the folder produce:
/// one for a Case folder (<see cref="ProveCaseFolderAsync"/>), or one for any
/// other folder and one for each folder above it up to the root
/// (<see cref="ProveFolderAsync"/>).
/// The upload does not look for its name first and does not walk the path
/// afterwards. Box's own answer must name the proved folder as the file's
/// parent, and a name Box already holds is Box's 409, resolved by comparing
/// the existing file's content. Folder create, rename, move and delete still
/// walk the whole path on every call.
/// </remarks>
internal sealed class BoxContentClient(
    BoxCustodyOptions options,
    HttpClient httpClient,
    IBoxAuthorizationHeaderProvider authorizationHeaderProvider,
    TimeProvider timeProvider)
{
    /// <summary>
    /// How long a read trusts a folder's proved ancestry.
    /// </summary>
    internal static readonly TimeSpan LiveFolderMemory = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Past this many remembered folders, expired entries are dropped.
    /// </summary>
    private const int LiveFolderPruneThreshold = 10_000;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> liveFolders =
        new(StringComparer.Ordinal);

    internal sealed record BoxItem(
        string Id,
        string Name,
        string Type,
        string? ETag,
        string? VersionId,
        long? Size,
        string? MediaType,
        string? ParentId);

    /// <summary>
    /// A folder that fresh reads have proved fit to be written into. Only the
    /// client's own prove methods make one, and an upload takes nothing else,
    /// so no file reaches Box without its folder having been proved first.
    /// </summary>
    internal sealed record ProvedFolder(string Id);

    /// <summary>
    /// What an upload came to. The file is the one Box created
    /// (<paramref name="Created"/> true), or the file already holding the name
    /// when its content is the content offered (false).
    /// </summary>
    internal sealed record BoxUpload(BoxItem File, bool Created);

    public string RootFolderId => options.RootFolderId;

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
    /// rather than asking Box again for what it has.
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

    /// <summary>
    /// Proves with one read that <paramref name="folderId"/> is the Case's own
    /// folder: a folder that carries the Case's name, sits directly under the
    /// approved root and is not in the trash. Box refuses two items of one name
    /// in a folder, and a listing omits trashed items, so this read gives the
    /// guarantee that listing the root and finding the name gave. Any other
    /// answer is a fence failure and drops the folder from the read memory.
    /// </summary>
    public async Task<ProvedFolder> ProveCaseFolderAsync(
        string folderId,
        string caseFolderName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseFolderName);
        try
        {
            using var response = await SendAsync(
                HttpMethod.Get,
                new Uri(options.BaseUri,
                    $"folders/{Uri.EscapeDataString(folderId)}?fields=id,name,type,parent,trashed_at"),
                null,
                cancellationToken);
            using var document = await ReadSuccessJsonAsync(response, cancellationToken);
            var folder = ParseItem(document.RootElement);
            if (!string.Equals(folder.Id, folderId, StringComparison.Ordinal)
                || !string.Equals(folder.Type, "folder", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Box returned the wrong object for a custody folder.");
            }
            if (IsTrashed(document.RootElement))
            {
                throw new UnauthorizedAccessException("A Box custody object is in trash.");
            }
            if (!string.Equals(folder.Name, caseFolderName, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("The Box custody folder is not the Case's own folder.");
            }
            if (!string.Equals(folder.ParentId, options.RootFolderId, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("The Box custody folder parent is inconsistent.");
            }
            return new ProvedFolder(folder.Id);
        }
        catch (Exception exception) when (IsFenceFailure(exception))
        {
            Forget(folderId);
            throw;
        }
    }

    /// <summary>
    /// Proves that a folder is not in the trash and sits under the approved
    /// root, at any depth. This is the proof for a folder whose name the caller
    /// does not know: a document is filed in its Case's folder or in the Audit's
    /// <c>a.</c> folder inside it, and the holding folder sits below the root
    /// too. One read for a folder directly under the root; one more for each
    /// folder between it and the root.
    /// </summary>
    public async Task<ProvedFolder> ProveFolderAsync(string folderId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        await EnsureDescendantAsync(folderId, cancellationToken);
        return new ProvedFolder(folderId);
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

    /// <summary>
    /// Uploads a file into a folder that has been proved, without looking for
    /// its name first. The caller owns <paramref name="content"/> and keeps it
    /// unchanged until this returns. Box's answer must name the proved folder
    /// as the file's parent. A name Box already holds is Box's 409
    /// <c>item_name_in_use</c>: the file is then the same file only when it is
    /// a file of this length, in this folder, holding these bytes, and the
    /// result says it was not created. Its type is compared only if Box sends
    /// one, and Box does not (<see cref="IsExpectedRevision"/>).
    /// </summary>
    public async Task<BoxUpload> UploadAsync(
        ProvedFolder folder,
        string name,
        ReadOnlyMemory<byte> content,
        string mediaType,
        CancellationToken cancellationToken)
    {
        using var multipart = new MultipartFormDataContent();
        multipart.Add(JsonContent.Create(new { name, parent = new { id = folder.Id } }), "attributes");
        var fileContent = new ReadOnlyMemoryContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
        multipart.Add(fileContent, "file", name);
        using var response = await SendAsync(HttpMethod.Post, UploadUri, multipart, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var errorCode = await ReadBoxErrorCodeAsync(response, cancellationToken);
            if (!string.Equals(errorCode, "item_name_in_use", StringComparison.Ordinal))
            {
                throw ConflictFailure(errorCode);
            }
            var existing = await FindOccupyingFileAsync(
                folder, name, mediaType, content.Length, cancellationToken);
            await using var retained = await OpenVersionReadAsync(
                existing.Id,
                existing.VersionId!,
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
            return new(existing, Created: false);
        }
        return new(await ReadUploadedFileAsync(response, folder, cancellationToken), Created: true);
    }

    /// <summary>
    /// Streams a complete, predeclared file body into a folder that has been
    /// proved, without taking ownership of <paramref name="content"/>. The
    /// seekable input is verified before Box receives a byte, then reset and
    /// wrapped so the multipart request cannot read beyond its declared length.
    /// The name is not looked for first, Box's answer must name the proved
    /// folder as the file's parent, and a name Box already holds is resolved by
    /// comparing hashes, exactly as for the overload above.
    /// </summary>
    public async Task<BoxUpload> UploadAsync(
        ProvedFolder folder,
        string name,
        Stream content,
        long contentLength,
        string mediaType,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegative(contentLength);
        if (!content.CanRead || !content.CanSeek)
        {
            throw new ArgumentException(
                "A readable, seekable stream is required for verified Box upload.",
                nameof(content));
        }

        var normalizedHash = NormalizeSha256(expectedSha256);
        var startPosition = content.Position;
        await VerifyAndResetAsync(content, startPosition, contentLength, normalizedHash, cancellationToken);
        try
        {
            using var multipart = new MultipartFormDataContent();
            multipart.Add(JsonContent.Create(new { name, parent = new { id = folder.Id } }), "attributes");
            var fileContent = new StreamContent(new BoundedReadStream(content, contentLength));
            fileContent.Headers.ContentLength = contentLength;
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
            multipart.Add(fileContent, "file", name);
            using var response = await SendAsync(HttpMethod.Post, UploadUri, multipart, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var errorCode = await ReadBoxErrorCodeAsync(response, cancellationToken);
                if (!string.Equals(errorCode, "item_name_in_use", StringComparison.Ordinal))
                {
                    throw ConflictFailure(errorCode);
                }
                var existing = await FindOccupyingFileAsync(
                    folder, name, mediaType, contentLength, cancellationToken);
                await using var retained = await OpenVersionReadAsync(
                    existing.Id,
                    existing.VersionId!,
                    contentLength,
                    cancellationToken);
                await VerifyExactContentAsync(retained, contentLength, normalizedHash, cancellationToken);
                return new(existing, Created: false);
            }
            return new(await ReadUploadedFileAsync(response, folder, cancellationToken), Created: true);
        }
        finally
        {
            content.Position = startPosition;
        }
    }

    /// <summary>
    /// The file Box says already holds an upload's name: a file, not a folder,
    /// with a version identity, and this content's parent and length. Its type
    /// is compared only if Box sends one, and Box does not
    /// (<see cref="IsExpectedRevision"/>). A file that is not is a refusal;
    /// whether its bytes are these bytes is the caller's next check.
    /// </summary>
    private async Task<BoxItem> FindOccupyingFileAsync(
        ProvedFolder folder,
        string name,
        string mediaType,
        long contentLength,
        CancellationToken cancellationToken)
    {
        var existing = await FindChildAsync(folder.Id, name, "file", cancellationToken)
            ?? throw new HttpRequestException(
                "Box reported an occupied file name but the exact existing file could not be resolved.",
                null,
                HttpStatusCode.Conflict);
        if (string.IsNullOrWhiteSpace(existing.VersionId))
        {
            throw new InvalidDataException("Box omitted the existing file version identity.");
        }
        if (!IsExpectedRevision(existing, folder.Id, mediaType, contentLength))
        {
            throw new InvalidDataException(
                "The occupied Box file name holds a file of a different parent, length or type.");
        }
        return existing;
    }

    /// <summary>
    /// A 409 on an upload that is not a name already in use. Box says "later"
    /// for the two codes that a folder's other work can raise; anything else is
    /// a refusal of the name. Both are the same failure to the caller, and
    /// what follows is the caller's. An image-case custody item re-arms itself
    /// with backoff. A Case's custody item stays failed until staff retry it.
    /// A Case document whose upload failed stays pending, and reconciliation
    /// files it later.
    /// </summary>
    private static HttpRequestException ConflictFailure(string? errorCode) => new(
        errorCode switch
        {
            "name_temporarily_reserved" =>
                "Box temporarily reserved the deterministic custody file name; retry reconciliation later.",
            "operation_blocked_temporary" =>
                "Box blocked the upload behind another operation on the folder; retry reconciliation later.",
            _ => "Box rejected the deterministic custody file name."
        },
        null,
        HttpStatusCode.Conflict);

    /// <summary>
    /// Whether a Box file is the revision it is supposed to be.
    ///
    /// Box does not return <c>content_type</c> for a file — it is not
    /// a field of the v2 file object, and asking for it simply yields nothing —
    /// so <see cref="BoxItem.MediaType"/> is null on every
    /// read. Comparing it unconditionally made this check impossible to pass,
    /// and no managed Box read had ever succeeded in production: the Evidence
    /// gallery, the case-document download and the case export all failed the
    /// same way, each turning the exception into a 404 or a flat refusal.
    ///
    /// Ancestry and length are always checked. The type is checked only when
    /// Box actually supplied one, so a field Box does not send cannot refuse a
    /// file that is otherwise exactly right. The content hash is verified by
    /// the caller immediately afterwards and is the real integrity guarantee —
    /// this check exists to catch the wrong file, not to re-derive its type.
    /// </summary>
    internal static bool IsExpectedRevision(
        BoxItem file,
        string expectedParentId,
        string expectedMediaType,
        long expectedLength) =>
        string.Equals(file.ParentId, expectedParentId, StringComparison.Ordinal)
        && file.Size == expectedLength
        && (file.MediaType is not { Length: > 0 } mediaType
            || string.Equals(mediaType, expectedMediaType, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Where Box takes an upload. The fields are asked for by name, so the
    /// answer always carries the file's parent, which is what proves where the
    /// file landed.
    /// </summary>
    private Uri UploadUri =>
        new(options.UploadUri, "files/content?fields=id,name,type,etag,file_version,parent");

    /// <summary>
    /// The file a successful upload made. Its parent must be the folder that
    /// was proved before the upload; any other parent is a fence failure, and
    /// the read memory forgets the folder.
    /// </summary>
    private async Task<BoxItem> ReadUploadedFileAsync(
        HttpResponseMessage response,
        ProvedFolder folder,
        CancellationToken cancellationToken)
    {
        using var document = await ReadSuccessJsonAsync(response, cancellationToken);
        var entries = document.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        if (entries.Length != 1)
        {
            throw new InvalidDataException("Box upload returned an unexpected file count.");
        }
        var result = ParseItem(entries[0]);
        if (!string.Equals(result.Type, "file", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Box returned the wrong type for an uploaded custody file.");
        }
        if (!string.Equals(result.ParentId, folder.Id, StringComparison.Ordinal))
        {
            Forget(folder.Id);
            throw new UnauthorizedAccessException(
                "The uploaded Box file is outside the folder it was proved for.");
        }
        return result;
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

    /// <summary>
    /// The managed read: the exact version of a file that sits directly in
    /// <paramref name="expectedParentId"/>. The file is read fresh every time;
    /// the folder's ancestry comes from the read memory when it is still live.
    /// </summary>
    public Task<Stream> OpenOwnedVersionReadAsync(
        string fileId,
        string versionId,
        string expectedParentId,
        long maximumLength,
        CancellationToken cancellationToken) =>
        OpenOwnedVersionAsync(
            fileId, versionId, expectedParentId, maximumLength, rememberAncestry: true,
            contentHash: null, cancellationToken);

    /// <summary>
    /// The managed read, hashing the content as the download is written, so the
    /// caller that must verify it does not copy it a second time. The caller
    /// finishes the hash and compares it; nothing is compared here.
    /// </summary>
    public Task<Stream> OpenOwnedVersionReadAsync(
        string fileId,
        string versionId,
        string expectedParentId,
        long maximumLength,
        IncrementalHash contentHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contentHash);
        return OpenOwnedVersionAsync(
            fileId, versionId, expectedParentId, maximumLength, rememberAncestry: true,
            contentHash, cancellationToken);
    }

    /// <summary>
    /// The same exact-version read for a write path: the folder's ancestry is
    /// proved again, whatever the read memory holds.
    /// </summary>
    public Task<Stream> OpenOwnedVersionProvedAsync(
        string fileId,
        string versionId,
        string expectedParentId,
        long maximumLength,
        CancellationToken cancellationToken) =>
        OpenOwnedVersionAsync(
            fileId, versionId, expectedParentId, maximumLength, rememberAncestry: false,
            contentHash: null, cancellationToken);

    /// <summary>
    /// The exact version's content, once the file has passed every check.
    /// </summary>
    /// <remarks>
    /// The content is requested at the same time as the file's metadata, so a
    /// read waits for the slower of the two rather than for both. The checks
    /// then run as before, in order: the object is a file, it is not in the
    /// trash, its parent is the expected folder, and that folder sits under the
    /// approved root. The content is not handed to any caller until every check
    /// has passed. When a check fails, or the request for it does, the download
    /// is cancelled, its temporary file is deleted, and the check's exception
    /// is the answer. When the checks pass and the content request failed, the
    /// content's exception is the answer.
    /// </remarks>
    private async Task<Stream> OpenOwnedVersionAsync(
        string fileId,
        string versionId,
        string expectedParentId,
        long maximumLength,
        bool rememberAncestry,
        IncrementalHash? contentHash,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedParentId);
        using var abandon = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var content = DownloadVersionWithoutAncestryAsync(
            fileId, versionId, maximumLength, contentHash, abandon.Token);
        Stream? opened = null;
        try
        {
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
            if (IsTrashed(metadataDocument.RootElement))
            {
                throw new UnauthorizedAccessException("A Box custody object is in trash.");
            }
            if (!string.Equals(file.ParentId, expectedParentId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The Box file is outside its expected Case root.");
            }
            if (rememberAncestry)
            {
                await EnsureLiveFolderForReadAsync(expectedParentId, cancellationToken);
            }
            else
            {
                await EnsureDescendantAsync(expectedParentId, cancellationToken);
            }
            opened = await content;
            return opened;
        }
        catch (Exception exception) when (IsFenceFailure(exception))
        {
            Forget(expectedParentId);
            throw;
        }
        finally
        {
            if (opened is null)
            {
                await DiscardDownloadAsync(content, abandon);
            }
        }
    }

    /// <summary>
    /// Ends a download whose bytes will never be used: stops it, lets it finish
    /// and disposes what it produced, which deletes its temporary file. The
    /// download's own failure is not reported, because the caller is already
    /// reporting the reason the bytes are not wanted.
    /// </summary>
    private static async Task DiscardDownloadAsync(Task<Stream> content, CancellationTokenSource abandon)
    {
        await abandon.CancelAsync();
        try
        {
            var stream = await content;
            await stream.DisposeAsync();
        }
        catch (Exception)
        {
            // The download was cancelled or had already failed. Either way the
            // caller's own exception is the answer, not this one.
        }
    }

    /// <summary>
    /// The read path's ancestry check: a folder proved under the approved root
    /// within <see cref="LiveFolderMemory"/> is taken as still there, and any
    /// other folder is walked to the root and remembered only if it gets there.
    /// </summary>
    internal async Task EnsureLiveFolderForReadAsync(
        string folderId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        if (liveFolders.TryGetValue(folderId, out var expiresAtUtc)
            && expiresAtUtc > timeProvider.GetUtcNow())
        {
            return;
        }
        await EnsureDescendantAsync(folderId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (liveFolders.Count >= LiveFolderPruneThreshold)
        {
            foreach (var entry in liveFolders)
            {
                if (entry.Value <= now)
                {
                    liveFolders.TryRemove(entry.Key, out _);
                }
            }
        }
        liveFolders[folderId] = now.Add(LiveFolderMemory);
    }

    /// <summary>Drops a folder from the read memory, so its next read walks the ancestry again.</summary>
    private void Forget(string? folderId)
    {
        if (!string.IsNullOrEmpty(folderId))
        {
            liveFolders.TryRemove(folderId, out _);
        }
    }

    /// <summary>
    /// An answer that says the object is gone, trashed or not ours: a 404 or
    /// 410, a 403, a trashed or outside-root refusal, or metadata that does
    /// not describe the object asked for. A throttled or failed Box is not one.
    /// </summary>
    private static bool IsFenceFailure(Exception exception) =>
        exception is UnauthorizedAccessException or InvalidDataException
        || exception is HttpRequestException
        {
            StatusCode: HttpStatusCode.NotFound or HttpStatusCode.Gone or HttpStatusCode.Forbidden
        };

    private static bool IsTrashed(JsonElement item) =>
        item.TryGetProperty("trashed_at", out var trashed)
        && trashed.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

    private async Task<Stream> DownloadVersionAsync(
        string fileId,
        string versionId,
        long maximumLength,
        CancellationToken cancellationToken) =>
        await DownloadVersionWithoutAncestryAsync(
            fileId, versionId, maximumLength, contentHash: null, cancellationToken);

    /// <summary>
    /// Downloads one exact version to a temporary file that is deleted when the
    /// returned stream is disposed. When <paramref name="contentHash"/> is
    /// given, each chunk is added to it as it is written.
    /// </summary>
    private async Task<Stream> DownloadVersionWithoutAncestryAsync(
        string fileId,
        string versionId,
        long maximumLength,
        IncrementalHash? contentHash,
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
                contentHash?.AppendData(buffer, 0, read);
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

    public async Task<BoxItem> MoveFileAsync(
        string fileId,
        string newParentId,
        string name,
        CancellationToken cancellationToken)
    {
        Forget(newParentId);
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
        Forget(folderId);
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
        // Every folder this walk touches: a failed walk forgets each of them.
        var walked = new List<string>();
        if (!isFile)
        {
            walked.Add(itemId);
        }
        try
        {
            for (var depth = 0; depth < 100; depth++)
            {
                var uri = new Uri(options.BaseUri,
                    $"{type}/{Uri.EscapeDataString(current)}?fields=id,parent,trashed_at");
                using var response = await SendAsync(HttpMethod.Get, uri, null, cancellationToken);
                using var document = await ReadSuccessJsonAsync(response, cancellationToken);
                if (IsTrashed(document.RootElement))
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
                walked.Add(current);
                type = "folders";
            }
            throw new UnauthorizedAccessException("The Box object is outside the approved custody root.");
        }
        catch (Exception exception) when (IsFenceFailure(exception))
        {
            foreach (var folderId in walked)
            {
                Forget(folderId);
            }
            throw;
        }
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
        var response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            // Box's rate limit, with how long it asked us to wait.
            var retryAfter = RetryAfterOf(response);
            response.Dispose();
            throw new BoxThrottledException(retryAfter);
        }
        return response;
    }

    private TimeSpan? RetryAfterOf(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }
        if (header?.Date is { } date)
        {
            var remaining = date - timeProvider.GetUtcNow();
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }
        return null;
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

    private static async Task VerifyAndResetAsync(
        Stream content,
        long startPosition,
        long expectedLength,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        try
        {
            await VerifyExactContentAsync(content, expectedLength, expectedSha256, cancellationToken);
        }
        finally
        {
            content.Position = startPosition;
        }
    }

    private static async Task VerifyExactContentAsync(
        Stream content,
        long expectedLength,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long copied = 0;
        while (true)
        {
            var read = await content.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }
            copied = checked(copied + read);
            if (copied > expectedLength)
            {
                throw new InvalidDataException("Box upload content length verification failed.");
            }
            hash.AppendData(buffer, 0, read);
        }
        var actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (copied != expectedLength
            || !string.Equals(expectedSha256, actualHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Box upload content hash verification failed.");
        }
    }

    private static string NormalizeSha256(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != SHA256.HashSizeInBytes * 2
            || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A SHA-256 hash is required.", nameof(value));
        }
        return value.ToLowerInvariant();
    }

    private sealed class BoundedReadStream(Stream source, long remaining) : Stream
    {
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            Task.FromException(new NotSupportedException());

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (remaining == 0)
            {
                return 0;
            }
            var read = await source.ReadAsync(
                buffer[..(int)Math.Min(buffer.Length, remaining)], cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Box upload content ended before its declared length.");
            }
            remaining -= read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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

    /// <summary>
    /// The proof of each root this adapter has been handed, held for as long as
    /// the caller holds that <see cref="CaseCustodyRoot"/> object. A piece of
    /// work carries one root through all of its files, so it proves the Case
    /// folder once, however many files it files. A root the adapter has not
    /// proved is proved on first use, and a proof that fails is not kept. This
    /// is no time-limited memory: a new root object is a new proof.
    /// </summary>
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        CaseCustodyRoot, Lazy<Task<BoxContentClient.ProvedFolder>>> provedRoots = new();

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
        var root = new CaseCustodyRoot(caseId, folder.Id, caseReference);
        await ProveRootAsync(root, cancellationToken);
        return root;
    }

    /// <summary>
    /// The Case's root when its folder is already recorded: one read proves
    /// that folder, and the approved root is not listed.
    /// </summary>
    public async Task<CaseCustodyRoot> GetExistingCaseRootAsync(
        Guid caseId,
        string caseReference,
        string remoteId,
        CancellationToken cancellationToken)
    {
        var root = new CaseCustodyRoot(caseId, remoteId, caseReference);
        await ProveRootAsync(root, cancellationToken);
        return root;
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

        // Operator direction (2026-08-21): the case folder holds the files.
        // The Evidence / Original instruction nesting was never asked for.
        return await RetainFileAsync(
            root, source, $"001 {SafeName(source.SourceFileName)}", leaseGuard, cancellationToken);
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
        return await RetainFileAsync(
            root,
            attachment,
            $"{ordinal:D3} {SafeName(attachment.SourceFileName)}",
            leaseGuard,
            cancellationToken);
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
        if (source.IntakeAssetId is not { } assetId || assetId == Guid.Empty)
        {
            throw new ArgumentException("Image intake custody requires a retained asset identity.", nameof(source));
        }
        ValidateOperation(operationKey);
        return await RetainFileAsync(
            root,
            source,
            $"{ordinal:000} {SafeName(source.SourceFileName)}",
            leaseGuard,
            cancellationToken);
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
        await ProveRootAsync(caseRoot, cancellationToken);

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
        await ProveRootAsync(root, cancellationToken);
        var folder = await GetOrCreateOwnedFolderAsync(
            root.RemoteId,
            CustodyNames.SafeName(auditReference),
            creationOwnerToken,
            leaseGuard,
            cancellationToken);
        return folder.Id;
    }

    /// <summary>
    /// Proves, with one read, that the root's folder is the Case's own folder
    /// (<see cref="BoxContentClient.ProveCaseFolderAsync"/>). The proof is kept
    /// for the life of this <see cref="CaseCustodyRoot"/> object, so the files
    /// of one piece of work share it, and the first files to arrive together
    /// make one read between them. A proof that fails is dropped.
    /// </summary>
    private async Task<BoxContentClient.ProvedFolder> ProveRootAsync(
        CaseCustodyRoot root,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ValidateCase(root.CaseId, root.Reference);
        var proof = provedRoots.GetValue(
            root,
            held => new(() => client.ProveCaseFolderAsync(
                held.RemoteId, CaseFolderName(held.Reference), cancellationToken)));
        try
        {
            return await proof.Value;
        }
        catch
        {
            provedRoots.Remove(root);
            throw;
        }
    }

    /// <summary>
    /// Files one retained source into the Case folder: the folder is proved
    /// (once for the root), the source is read and checked against its hash
    /// and length, the lease is checked, and the upload goes to Box. The name is
    /// not looked for first. A file that already holds it is Box's 409, and the
    /// client accepts it only when its content is this content, so a replay
    /// verifies rather than uploads again.
    /// </summary>
    private async Task<CustodyDocumentVersion> RetainFileAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        string fileName,
        CustodyEffectLeaseGuard? leaseGuard,
        CancellationToken cancellationToken)
    {
        var folder = await ProveRootAsync(root, cancellationToken);
        var (content, actualHash) = await ReadVerifiedSourceAsync(source, cancellationToken);
        await RequireLeaseAsync(leaseGuard, cancellationToken);
        var file = (await client.UploadAsync(
            folder, fileName, content, source.MediaType, cancellationToken)).File;
        return new(
            root.CaseId,
            file.Id,
            actualHash,
            file.ETag ?? actualHash,
            file.VersionId
                ?? throw new InvalidDataException("Box omitted the retained file version identity."));
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

    /// <summary>
    /// The staged two-phase folder create: a crash between create and rename
    /// leaves an owner-token staging folder the same replay resumes, and a
    /// same-name folder created by anything else is accepted as the case's —
    /// the durable folder identity lives in the database, not in a
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
            await VerifyFolderIdentityAsync(existing, finalName, cancellationToken);
            return existing;
        }

        var stagingName = $".pegasus-create-{creationOwnerToken}";
        var staging = await client.FindChildAsync(parentId, stagingName, "folder", cancellationToken);
        if (staging is null)
        {
            await RequireLeaseAsync(leaseGuard, cancellationToken);
            staging = await client.CreateFolderAsync(parentId, stagingName, cancellationToken);
        }
        await VerifyFolderIdentityAsync(staging, stagingName, cancellationToken);

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

    private static Task RequireLeaseAsync(
        CustodyEffectLeaseGuard? leaseGuard,
        CancellationToken cancellationToken) =>
        leaseGuard?.RequireCurrentAsync(cancellationToken) ?? Task.CompletedTask;


    /// <summary>
    /// A folder found by name in its parent's listing is a folder of that name
    /// and sits under the approved root, not in the trash. The listing already
    /// said it is that parent's child, so the parent is not listed again.
    /// </summary>
    private async Task VerifyFolderIdentityAsync(
        BoxContentClient.BoxItem folder,
        string expectedName,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(folder.Type, "folder", StringComparison.Ordinal)
            || !string.Equals(folder.Name, expectedName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The Box custody folder identity is inconsistent.");
        }
        await client.EnsureDescendantAsync(folder.Id, cancellationToken);
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
