using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Pegasus.Core.Documents;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// The names both staging adapters use: <c>uploads/{id}/manifest</c> and
/// <c>uploads/{id}/{part:D4}</c>. The intake sweep lists only
/// <c>staging/</c>, so nothing here is ever classified as an intake artifact.
/// </summary>
internal static class DocumentUploadNames
{
    public const string Root = "uploads";
    public const string RootPrefix = Root + "/";
    private const string ManifestName = "manifest";

    public static string Prefix(Guid uploadId) => $"{RootPrefix}{uploadId:D}/";

    public static string Manifest(Guid uploadId) => Prefix(uploadId) + ManifestName;

    public static string Part(Guid uploadId, int partNumber) =>
        Prefix(uploadId) + partNumber.ToString("D4", CultureInfo.InvariantCulture);

    public static bool TryParsePart(string leaf, out int partNumber)
    {
        partNumber = 0;
        return leaf.Length == 4
            && leaf.All(char.IsAsciiDigit)
            && int.TryParse(leaf, NumberStyles.None, CultureInfo.InvariantCulture, out partNumber);
    }

    /// <summary>Whether the name is one this staging writes, and so one it may delete.</summary>
    public static bool IsObject(string name)
    {
        var segments = name.Split('/');
        return segments.Length == 3
            && segments[0] == Root
            && Guid.TryParseExact(segments[1], "D", out var uploadId)
            && segments[1] == uploadId.ToString("D")
            && (segments[2] == ManifestName || TryParsePart(segments[2], out _));
    }

    public static byte[] Encode(DocumentUploadManifest manifest) =>
        JsonSerializer.SerializeToUtf8Bytes(new ManifestDocument(
            manifest.Owner,
            manifest.TotalBytes,
            manifest.Sha256,
            manifest.CreatedAtUtc));

    public static DocumentUploadManifest Decode(Guid uploadId, ReadOnlySpan<byte> json)
    {
        var document = JsonSerializer.Deserialize<ManifestDocument>(json)
            ?? throw new JsonException("The staged upload manifest is empty.");
        return new(uploadId, document.Owner, document.TotalBytes, document.Sha256, document.CreatedAtUtc);
    }

    private sealed record ManifestDocument(
        string Owner,
        long TotalBytes,
        string Sha256,
        DateTimeOffset CreatedAtUtc);
}

/// <summary>
/// Staged upload parts in the <c>transient-intake</c> container under
/// <c>uploads/</c>, where the Web and the Worker identities are Blob Data
/// Owner. Every object is uploaded once (no overwrite); a part keeps its
/// SHA-256 in metadata so a repeated part is compared without its body.
/// </summary>
public sealed class AzureBlobDocumentUploadStaging(
    BlobContainerClient container,
    bool allowCreateIfNotExists = false) : IDocumentUploadStaging
{
    private const string HashMetadataName = "sha256";

    public async Task<bool> CreateManifestAsync(
        DocumentUploadManifest manifest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        await EnsureContainerExistsAsync(cancellationToken);
        return await TryUploadAsync(
            container.GetBlobClient(DocumentUploadNames.Manifest(manifest.UploadId)),
            DocumentUploadNames.Encode(manifest),
            metadata: null,
            cancellationToken);
    }

    public async Task<DocumentUploadManifest?> ReadManifestAsync(
        Guid uploadId,
        CancellationToken cancellationToken) =>
        await TryDownloadAsync(DocumentUploadNames.Manifest(uploadId), cancellationToken) is { } content
            ? DocumentUploadNames.Decode(uploadId, content.Span)
            : null;

    public async Task<DocumentUploadPartWrite> CreatePartAsync(
        Guid uploadId,
        int partNumber,
        ReadOnlyMemory<byte> content,
        string sha256,
        CancellationToken cancellationToken)
    {
        var blob = container.GetBlobClient(DocumentUploadNames.Part(uploadId, partNumber));
        if (await TryUploadAsync(
                blob,
                content,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [HashMetadataName] = sha256 },
                cancellationToken))
        {
            return DocumentUploadPartWrite.Created;
        }

        var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
        return properties.Value.Metadata.TryGetValue(HashMetadataName, out var stored)
            && string.Equals(stored, sha256, StringComparison.Ordinal)
                ? DocumentUploadPartWrite.AlreadyStored
                : DocumentUploadPartWrite.Conflict;
    }

    public async Task<IReadOnlyList<int>> ListPartNumbersAsync(
        Guid uploadId,
        CancellationToken cancellationToken)
    {
        var prefix = DocumentUploadNames.Prefix(uploadId);
        var parts = new List<int>();
        await foreach (var blob in container.GetBlobsAsync(
                           BlobTraits.None,
                           BlobStates.None,
                           prefix,
                           cancellationToken))
        {
            if (DocumentUploadNames.TryParsePart(blob.Name[prefix.Length..], out var partNumber))
            {
                parts.Add(partNumber);
            }
        }

        return parts;
    }

    public Task<ReadOnlyMemory<byte>?> ReadPartAsync(
        Guid uploadId,
        int partNumber,
        CancellationToken cancellationToken) =>
        TryDownloadAsync(DocumentUploadNames.Part(uploadId, partNumber), cancellationToken);

    public async Task<DocumentUploadStagedPage> ListAsync(
        int maximumItems,
        string? continuation,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        await EnsureContainerExistsAsync(cancellationToken);
        await foreach (var page in container.GetBlobsAsync(
                               BlobTraits.None,
                               BlobStates.None,
                               DocumentUploadNames.RootPrefix,
                               cancellationToken)
                           .AsPages(continuation, maximumItems))
        {
            // The service ends the listing with an empty marker.
            return new(
                [.. page.Values.Select(blob => new DocumentUploadStagedObject(
                    blob.Name,
                    blob.Properties.CreatedOn ?? DateTimeOffset.UnixEpoch))],
                string.IsNullOrEmpty(page.ContinuationToken) ? null : page.ContinuationToken);
        }

        return new([], null);
    }

    public async Task<bool> DeleteAsync(
        string name,
        DateTimeOffset createdAtOrBeforeUtc,
        CancellationToken cancellationToken)
    {
        if (!DocumentUploadNames.IsObject(name))
        {
            throw new ArgumentException("The staged upload object name is invalid.", nameof(name));
        }

        // Objects are never overwritten, so last-modified is creation: the
        // condition refuses an object written again after the listing.
        try
        {
            var deleted = await container.GetBlobClient(name).DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                new BlobRequestConditions { IfUnmodifiedSince = createdAtOrBeforeUtc },
                cancellationToken);
            return deleted.Value;
        }
        catch (RequestFailedException exception) when (exception.Status == 412)
        {
            return false;
        }
    }

    private static async Task<bool> TryUploadAsync(
        BlobClient blob,
        ReadOnlyMemory<byte> content,
        IDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        using var stream = DocumentContentCachePublisherExtensions.StreamOf(content);
        try
        {
            await blob.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                    Metadata = metadata
                },
                cancellationToken);
            return true;
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            return false;
        }
    }

    private async Task<ReadOnlyMemory<byte>?> TryDownloadAsync(
        string name,
        CancellationToken cancellationToken)
    {
        try
        {
            var download = await container.GetBlobClient(name)
                .DownloadContentAsync(cancellationToken);
            return download.Value.Content.ToMemory();
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    private async ValueTask EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        if (allowCreateIfNotExists)
        {
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }
    }
}

/// <summary>
/// Staged upload parts on the local artifact root, for DevelopmentOffline and
/// the integration tests: the same names as files under <c>uploads/</c>, each
/// written once (<see cref="FileMode.CreateNew"/>) and aged by its write time.
/// </summary>
public sealed class FileSystemDocumentUploadStaging(string rootPath) : IDocumentUploadStaging
{
    public Task<bool> CreateManifestAsync(
        DocumentUploadManifest manifest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return TryCreateAsync(
            DocumentUploadNames.Manifest(manifest.UploadId),
            DocumentUploadNames.Encode(manifest),
            cancellationToken);
    }

    public async Task<DocumentUploadManifest?> ReadManifestAsync(
        Guid uploadId,
        CancellationToken cancellationToken) =>
        await TryReadAsync(DocumentUploadNames.Manifest(uploadId), cancellationToken) is { } content
            ? DocumentUploadNames.Decode(uploadId, content.Span)
            : null;

    public async Task<DocumentUploadPartWrite> CreatePartAsync(
        Guid uploadId,
        int partNumber,
        ReadOnlyMemory<byte> content,
        string sha256,
        CancellationToken cancellationToken)
    {
        var name = DocumentUploadNames.Part(uploadId, partNumber);
        if (await TryCreateAsync(name, content, cancellationToken))
        {
            return DocumentUploadPartWrite.Created;
        }

        var stored = await TryReadAsync(name, cancellationToken);
        return stored is { } bytes
            && string.Equals(Convert.ToHexString(SHA256.HashData(bytes.Span)), sha256, StringComparison.Ordinal)
                ? DocumentUploadPartWrite.AlreadyStored
                : DocumentUploadPartWrite.Conflict;
    }

    public Task<IReadOnlyList<int>> ListPartNumbersAsync(
        Guid uploadId,
        CancellationToken cancellationToken)
    {
        var directory = PathOf(DocumentUploadNames.Prefix(uploadId));
        var parts = new List<int>();
        if (Directory.Exists(directory))
        {
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                if (DocumentUploadNames.TryParsePart(Path.GetFileName(path), out var partNumber))
                {
                    parts.Add(partNumber);
                }
            }

            parts.Sort();
        }

        return Task.FromResult<IReadOnlyList<int>>(parts);
    }

    public Task<ReadOnlyMemory<byte>?> ReadPartAsync(
        Guid uploadId,
        int partNumber,
        CancellationToken cancellationToken) =>
        TryReadAsync(DocumentUploadNames.Part(uploadId, partNumber), cancellationToken);

    public Task<DocumentUploadStagedPage> ListAsync(
        int maximumItems,
        string? continuation,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var root = PathOf(DocumentUploadNames.RootPrefix);
        if (!Directory.Exists(root))
        {
            return Task.FromResult(new DocumentUploadStagedPage([], null));
        }

        var remaining = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => DocumentUploadNames.RootPrefix
                + Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(DocumentUploadNames.IsObject)
            .Where(name => continuation is null || string.CompareOrdinal(name, continuation) > 0)
            .Order(StringComparer.Ordinal)
            .ToArray();
        DocumentUploadStagedObject[] items =
        [
            .. remaining.Take(maximumItems).Select(name => new DocumentUploadStagedObject(
                name,
                new DateTimeOffset(File.GetLastWriteTimeUtc(PathOf(name)), TimeSpan.Zero)))
        ];
        return Task.FromResult(new DocumentUploadStagedPage(
            items,
            items.Length < remaining.Length ? items[^1].Name : null));
    }

    public Task<bool> DeleteAsync(
        string name,
        DateTimeOffset createdAtOrBeforeUtc,
        CancellationToken cancellationToken)
    {
        if (!DocumentUploadNames.IsObject(name))
        {
            throw new ArgumentException("The staged upload object name is invalid.", nameof(name));
        }

        var path = PathOf(name);
        if (!File.Exists(path)
            || new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) > createdAtOrBeforeUtc)
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        var directory = Path.GetDirectoryName(path)!;
        try
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch (IOException)
        {
            // A part written meanwhile keeps its folder; a later sweep empties it.
        }

        return Task.FromResult(true);
    }

    private string PathOf(string name) =>
        Path.Combine(rootPath, name.Replace('/', Path.DirectorySeparatorChar));

    private async Task<bool> TryCreateAsync(
        string name,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var path = PathOf(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException) when (File.Exists(path))
        {
            return false;
        }

        await using (stream)
        {
            await stream.WriteAsync(content, cancellationToken);
        }

        return true;
    }

    private async Task<ReadOnlyMemory<byte>?> TryReadAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var path = PathOf(name);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
