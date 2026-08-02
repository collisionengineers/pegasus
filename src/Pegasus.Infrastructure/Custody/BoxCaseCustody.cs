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
    string EnterpriseId)
{
    public static BoxCustodyOptions Create(
        string? baseUri,
        string? uploadUri,
        string? rootFolderId,
        string? configJson,
        string? clientSecret)
    {
        var api = RequireBoxUri(baseUri, "api.box.com", "Box:BaseUri");
        var upload = RequireBoxUri(uploadUri, "upload.box.com", "Box:UploadUri");
        if (!string.Equals(rootFolderId, "405543781910", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Box:RootFolderId must be the approved pegasus root 405543781910.");
        }
        if (string.IsNullOrWhiteSpace(configJson))
        {
            throw new InvalidOperationException("Box:ConfigJson is required through a Key Vault reference.");
        }
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("Box:ClientSecret is required through a Key Vault reference.");
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
                RequireJsonString(root, "enterpriseID"));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new InvalidOperationException("Box:ConfigJson is not a valid Box JWT configuration.", exception);
        }
    }

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

internal sealed class BoxJwtAuthorizationHeaderProvider(BoxCustodyOptions options)
    : IBoxAuthorizationHeaderProvider
{
    private readonly Lazy<(BoxJwtAuth Auth, NetworkSession Session)> authentication = new(() =>
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

    public async Task<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken)
    {
        var (auth, session) = authentication.Value;
        var header = await auth.RetrieveAuthorizationHeaderAsync(session).WaitAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(header))
        {
            throw new InvalidOperationException("Box JWT authentication returned no authorization header.");
        }
        return header;
    }
}

internal sealed class BoxCaseCustody(
    BoxCustodyOptions options,
    IIntakeArtifactStore artifactStore,
    HttpClient httpClient,
    IBoxAuthorizationHeaderProvider authorizationHeaderProvider) : ICaseCustody
{
    public async Task<CaseCustodyRoot> CreateCaseRootAsync(
        Guid caseId,
        string caseReference,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ValidateCase(caseId, caseReference);
        ValidateOperation(operationKey);
        var folderName = CaseFolderName(caseId, caseReference);
        var folder = await FindChildAsync(options.RootFolderId, folderName, "folder", cancellationToken)
            ?? await CreateFolderAsync(options.RootFolderId, folderName, cancellationToken);
        await EnsureDescendantAsync(folder.Id, cancellationToken);
        return new(caseId, folder.Id, caseReference);
    }

    public async Task<CaseCustodyRoot> GetExistingCaseRootAsync(
        Guid caseId,
        string caseReference,
        CancellationToken cancellationToken)
    {
        ValidateCase(caseId, caseReference);
        var folder = await FindChildAsync(
            options.RootFolderId,
            CaseFolderName(caseId, caseReference),
            "folder",
            cancellationToken)
            ?? throw new InvalidOperationException("The case custody root has not been created.");
        await EnsureDescendantAsync(folder.Id, cancellationToken);
        return new(caseId, folder.Id, caseReference);
    }

    public async Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(source);
        ValidateOperation(operationKey);
        await ValidateRootAsync(root, cancellationToken);
        var content = await artifactStore.ReadAsync(source.SourceObjectKey, cancellationToken)
            ?? throw new FileNotFoundException("The retained intake source is unavailable.");
        var actualHash = Convert.ToHexString(SHA256.HashData(content.Span));
        if (!actualHash.Equals(source.SourceHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The retained intake source failed its custody integrity check.");
        }

        var documents = await GetOrCreateFolderAsync(root.RemoteId, "documents", cancellationToken);
        var receipt = await GetOrCreateFolderAsync(documents.Id, source.IntakeReceiptId.ToString("N"), cancellationToken);
        var fileName = $"{actualHash.ToLowerInvariant()}-{SafeName(source.SourceFileName)}";
        var existing = await FindChildAsync(receipt.Id, fileName, "file", cancellationToken);
        BoxItem file;
        if (existing is null)
        {
            file = await UploadAsync(receipt.Id, fileName, content, source.MediaType, cancellationToken);
        }
        else
        {
            await EnsureDescendantAsync(existing.Id, cancellationToken, isFile: true);
            var retained = await DownloadAsync(existing.Id, cancellationToken);
            if (!SHA256.HashData(retained).AsSpan().SequenceEqual(SHA256.HashData(content.Span)))
            {
                throw new InvalidDataException("An existing Box custody file has different content.");
            }
            file = existing;
        }
        return new(root.CaseId, file.Id, actualHash, file.ETag ?? actualHash);
    }

    public async Task<string> CreateAuditReferenceFolderAsync(
        CaseCustodyRoot root,
        string auditReference,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(auditReference);
        ValidateOperation(operationKey);
        await ValidateRootAsync(root, cancellationToken);
        var auditRoot = await GetOrCreateFolderAsync(root.RemoteId, "audit", cancellationToken);
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(auditReference)))[..24];
        var folder = await GetOrCreateFolderAsync(auditRoot.Id, $"audit-{identity.ToLowerInvariant()}", cancellationToken);
        return folder.Id;
    }

    private async Task ValidateRootAsync(CaseCustodyRoot root, CancellationToken cancellationToken)
    {
        ValidateCase(root.CaseId, root.Reference);
        var expected = await FindChildAsync(
            options.RootFolderId,
            CaseFolderName(root.CaseId, root.Reference),
            "folder",
            cancellationToken)
            ?? throw new InvalidOperationException("The case custody root has not been created.");
        if (!expected.Id.Equals(root.RemoteId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The custody root does not match the retained case identity.");
        }
        await EnsureDescendantAsync(root.RemoteId, cancellationToken);
    }

    private async Task<BoxItem> GetOrCreateFolderAsync(
        string parentId,
        string name,
        CancellationToken cancellationToken)
    {
        await EnsureDescendantAsync(parentId, cancellationToken);
        return await FindChildAsync(parentId, name, "folder", cancellationToken)
            ?? await CreateFolderAsync(parentId, name, cancellationToken);
    }

    private async Task<BoxItem?> FindChildAsync(
        string parentId,
        string name,
        string type,
        CancellationToken cancellationToken)
    {
        await EnsureDescendantAsync(parentId, cancellationToken);
        var uri = new Uri(options.BaseUri,
            $"folders/{Uri.EscapeDataString(parentId)}/items?fields=id,name,type,etag&limit=1000");
        using var response = await SendAsync(HttpMethod.Get, uri, null, cancellationToken);
        using var document = await ReadSuccessJsonAsync(response, cancellationToken);
        var matches = document.RootElement.GetProperty("entries")
            .EnumerateArray()
            .Where(item => ReadString(item, "name") == name && ReadString(item, "type") == type)
            .Select(ParseItem)
            .ToArray();
        return matches.Length switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidDataException("Box contains duplicate custody children for one exact identity.")
        };
    }

    private async Task<BoxItem> CreateFolderAsync(
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

    private async Task<BoxItem> UploadAsync(
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

    private async Task<byte[]> DownloadAsync(string fileId, CancellationToken cancellationToken)
    {
        await EnsureDescendantAsync(fileId, cancellationToken, isFile: true);
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

    private async Task EnsureDescendantAsync(
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
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = content };
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

    private static BoxItem ParseItem(JsonElement value) => new(
        ReadString(value, "id") ?? throw new InvalidDataException("Box omitted an item identity."),
        ReadString(value, "name") ?? string.Empty,
        ReadString(value, "type") ?? string.Empty,
        ReadString(value, "etag"));

    private static string? ReadString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var result) && result.ValueKind == JsonValueKind.String
            ? result.GetString()
            : null;

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

    private static string CaseFolderName(Guid caseId, string reference) =>
        $"{SafeName(reference)}-{caseId:N}";

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var result = new string(value.Trim().Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        if (string.IsNullOrWhiteSpace(result) || result.Length > 180)
        {
            throw new ArgumentException("The Box custody name is invalid.", nameof(value));
        }
        return result;
    }

    private sealed record BoxItem(string Id, string Name, string Type, string? ETag);
}
