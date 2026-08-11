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
    internal sealed record BoxItem(string Id, string Name, string Type, string? ETag);

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

    public async Task<BoxItem?> FindChildAsync(
        string parentId,
        string name,
        string type,
        CancellationToken cancellationToken)
    {
        await EnsureDescendantAsync(parentId, cancellationToken);
        const int pageLimit = 1000;
        var offset = 0;
        var matches = new List<BoxItem>();
        while (true)
        {
            var uri = new Uri(options.BaseUri,
                $"folders/{Uri.EscapeDataString(parentId)}/items?fields=id,name,type,etag&limit={pageLimit}&offset={offset}");
            using var response = await SendAsync(HttpMethod.Get, uri, null, cancellationToken);
            using var document = await ReadSuccessJsonAsync(response, cancellationToken);
            var entries = document.RootElement.GetProperty("entries").EnumerateArray().ToArray();
            matches.AddRange(entries
                .Where(item => ReadString(item, "name") == name)
                .Select(ParseItem));
            if (entries.Length < pageLimit)
            {
                break;
            }
            offset += entries.Length;
        }
        var match = matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidDataException("Box contains duplicate custody children for one exact identity.")
        };
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

    private static BoxItem ParseItem(JsonElement value) => new(
        ReadString(value, "id") ?? throw new InvalidDataException("Box omitted an item identity."),
        ReadString(value, "name") ?? string.Empty,
        ReadString(value, "type") ?? string.Empty,
        ReadString(value, "etag"));

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
    private const string AuditBindingFileName = "pegasus-audit-binding.json";
    private const string BindingMediaType = "application/json";
    private const string CreationAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public async Task<CaseCustodyRoot> CreateCaseRootAsync(
        Guid caseId,
        string caseReference,
        string creationOwnerToken,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ValidateCase(caseId, caseReference);
        ValidateOperation(operationKey);
        var folderName = CaseFolderName(caseReference);
        var binding = CaseBinding(caseId, caseReference);
        var folder = await GetOrCreateBoundFolderAsync(
            client.RootFolderId,
            folderName,
            CaseBindingFileName,
            binding,
            creationOwnerToken,
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
        await VerifyBoundFolderAsync(
            folder,
            client.RootFolderId,
            CaseBindingFileName,
            CaseBinding(caseId, caseReference),
            cancellationToken);
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
        if (source.SourceLength >= 0 && source.SourceLength != content.Length)
        {
            throw new InvalidDataException("The retained intake source failed its custody length check.");
        }

        var evidence = await client.GetOrCreateFolderAsync(root.RemoteId, "Evidence", cancellationToken);
        var instruction = await client.GetOrCreateFolderAsync(
            evidence.Id,
            "Original instruction",
            cancellationToken);
        var fileName = $"001 {SafeName(source.SourceFileName)}";
        var existing = await client.FindChildAsync(instruction.Id, fileName, "file", cancellationToken);
        BoxContentClient.BoxItem file;
        if (existing is null)
        {
            file = await client.UploadAsync(instruction.Id, fileName, content, source.MediaType, cancellationToken);
        }
        else
        {
            await client.EnsureDescendantAsync(existing.Id, cancellationToken, isFile: true);
            var retained = await client.DownloadAsync(existing.Id, cancellationToken);
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
        string creationOwnerToken,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(auditReference);
        ValidateOperation(operationKey);
        await ValidateRootAsync(root, cancellationToken);
        var folder = await GetOrCreateBoundFolderAsync(
            root.RemoteId,
            CustodyNames.SafeName(auditReference),
            AuditBindingFileName,
            AuditBinding(root.CaseId, root.Reference, auditReference),
            creationOwnerToken,
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
        await VerifyBoundFolderAsync(
            expected,
            client.RootFolderId,
            CaseBindingFileName,
            CaseBinding(root.CaseId, root.Reference),
            cancellationToken);
    }

    private async Task<BoxContentClient.BoxItem> GetOrCreateBoundFolderAsync(
        string parentId,
        string finalName,
        string bindingFileName,
        byte[] binding,
        string creationOwnerToken,
        CancellationToken cancellationToken)
    {
        ValidateCreationOwnerToken(creationOwnerToken);
        var existing = await client.FindChildAsync(parentId, finalName, "folder", cancellationToken);
        if (existing is not null)
        {
            await VerifyBoundFolderAsync(
                existing,
                parentId,
                bindingFileName,
                binding,
                cancellationToken);
            return existing;
        }

        var stagingName = $".pegasus-create-{creationOwnerToken}";
        var staging = await client.FindChildAsync(parentId, stagingName, "folder", cancellationToken)
            ?? await client.CreateFolderAsync(parentId, stagingName, cancellationToken);
        await VerifyFolderIdentityAsync(staging, parentId, stagingName, cancellationToken);

        var bindingFile = await client.FindChildAsync(
            staging.Id,
            bindingFileName,
            "file",
            cancellationToken);
        if (bindingFile is null)
        {
            await client.UploadAsync(
                staging.Id,
                bindingFileName,
                binding,
                BindingMediaType,
                cancellationToken);
        }
        else
        {
            await VerifyFileBytesAsync(bindingFile, binding, cancellationToken);
        }

        var finalConflict = await client.FindChildAsync(parentId, finalName, "folder", cancellationToken);
        if (finalConflict is not null)
        {
            if (string.Equals(finalConflict.Id, staging.Id, StringComparison.Ordinal))
            {
                await VerifyBoundFolderAsync(
                    finalConflict,
                    parentId,
                    bindingFileName,
                    binding,
                    cancellationToken);
                return finalConflict;
            }
            throw new InvalidDataException(
                "The final Box custody name is already occupied by another folder.");
        }

        var latest = await client.GetFolderAsync(staging.Id, cancellationToken);
        var promoted = await client.RenameFolderAsync(
            staging.Id,
            finalName,
            latest.ETag ?? throw new InvalidDataException("Box omitted the staging folder ETag."),
            cancellationToken);
        if (!string.Equals(promoted.Id, staging.Id, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Box changed the custody folder identity during promotion.");
        }
        await VerifyBoundFolderAsync(
            promoted,
            parentId,
            bindingFileName,
            binding,
            cancellationToken);
        return promoted;
    }

    private async Task VerifyBoundFolderAsync(
        BoxContentClient.BoxItem folder,
        string parentId,
        string bindingFileName,
        byte[] binding,
        CancellationToken cancellationToken)
    {
        await VerifyFolderIdentityAsync(folder, parentId, folder.Name, cancellationToken);
        var bindingFile = await client.FindChildAsync(
            folder.Id,
            bindingFileName,
            "file",
            cancellationToken)
            ?? throw new InvalidDataException(
                "The Box custody folder is missing its immutable Pegasus binding.");
        await VerifyFileBytesAsync(bindingFile, binding, cancellationToken);
    }

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

    private async Task VerifyFileBytesAsync(
        BoxContentClient.BoxItem file,
        ReadOnlyMemory<byte> expected,
        CancellationToken cancellationToken)
    {
        await client.EnsureDescendantAsync(file.Id, cancellationToken, isFile: true);
        var actual = await client.DownloadAsync(file.Id, cancellationToken);
        if (!actual.AsSpan().SequenceEqual(expected.Span))
        {
            throw new InvalidDataException("A Box custody binding has different immutable content.");
        }
    }

    internal static byte[] CaseBinding(Guid caseId, string caseReference) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            caseId,
            caseReference
        });

    internal static byte[] AuditBinding(
        Guid caseId,
        string caseReference,
        string auditReference) => JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            caseId,
            caseReference,
            auditReference
        });

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
