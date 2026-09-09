using System.Globalization;
using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Intake;

public sealed class AzureBlobIntakeArtifactStore
    : IIntakeArtifactStore, IIntakeQuarantineArtifactStore
{
    private const string StagingPrefix = "staging/";
    private const string HashMetadataName = "sha256";
    private const string ContentLengthMetadataName = "contentlength";
    private const string FirstSeenMetadataName = "firstseenatutc";
    private const string DispositionMetadataName = "disposition";
    private const string DispositionTagName = "PegasusDisposition";

    private readonly BlobContainerClient container;
    private readonly bool allowCreateIfNotExists;

    public AzureBlobIntakeArtifactStore(
        BlobContainerClient container,
        bool allowCreateIfNotExists = false)
    {
        this.container = container;
        this.allowCreateIfNotExists = allowCreateIfNotExists;
    }

    public async Task<string> StoreAsync(
        string contentHash,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var hash = ValidateContent(contentHash, content);
        var storageKey = $"sha256/{hash[..2]}/{hash}";
        await UploadImmutableAsync(
            storageKey,
            hash,
            content,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [HashMetadataName] = hash
            },
            tags: null,
            cancellationToken);
        return storageKey;
    }

    public async Task<IntakeQuarantineArtifact> StoreStreamAsync(
        Stream content,
        long contentLength,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegative(contentLength);
        if (!content.CanRead)
        {
            throw new ArgumentException(
                "The quarantine source stream must be readable.",
                nameof(content));
        }

        await using var buffered = await BufferAndHashAsync(
            content,
            contentLength,
            cancellationToken);
        var storageKey = $"sha256/{buffered.ContentHash[..2]}/{buffered.ContentHash}";
        try
        {
            await EnsureContainerExistsAsync(cancellationToken);
            await UploadStreamOrVerifyAsync(
                container.GetBlobClient(storageKey),
                buffered.ContentHash,
                buffered.ContentLength,
                buffered.Stream,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [HashMetadataName] = buffered.ContentHash
                },
                tags: null,
                cancellationToken);
        }
        catch (RequestFailedException exception)
        {
            throw DependencyUnavailable(exception);
        }

        return new IntakeQuarantineArtifact(
            storageKey,
            buffered.ContentHash,
            buffered.ContentLength);
    }

    public async Task VerifyAsync(
        IntakeQuarantineArtifact artifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (artifact.ContentLength < 0
            || artifact.ContentHash.Length != 64
            || artifact.ContentHash.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new IntakeArtifactIntegrityException();
        }

        var contentHash = artifact.ContentHash.ToUpperInvariant();
        var expectedStorageKey = $"sha256/{contentHash[..2]}/{contentHash}";
        if (!string.Equals(
                artifact.StorageKey,
                expectedStorageKey,
                StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }

        try
        {
            await VerifyBlobAsync(
                container.GetBlobClient(artifact.StorageKey),
                contentHash,
                artifact.ContentLength,
                cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            throw new IntakeArtifactIntegrityException();
        }
    }

    public async Task<ReadOnlyMemory<byte>?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        var expectedHash = GetStorageKeyHash(storageKey);
        try
        {
            var content = await container.GetBlobClient(storageKey)
                .DownloadContentAsync(cancellationToken: cancellationToken);
            var bytes = content.Value.Content.ToMemory();
            if (!string.Equals(
                    Convert.ToHexString(SHA256.HashData(bytes.Span)),
                    expectedHash,
                    StringComparison.Ordinal))
            {
                throw new IntakeArtifactIntegrityException();
            }

            return bytes;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
        catch (RequestFailedException exception)
        {
            throw DependencyUnavailable(exception);
        }
    }

    public async Task<StagedArtifactInventoryItem> StageAsync(
        Guid stagedReceiptId,
        string contentHash,
        ReadOnlyMemory<byte> content,
        DateTimeOffset firstSeenAtUtc,
        CancellationToken cancellationToken)
    {
        if (stagedReceiptId == Guid.Empty)
        {
            throw new ArgumentException(
                "A staged receipt identifier is required.",
                nameof(stagedReceiptId));
        }

        var hash = ValidateContent(contentHash, content);
        var storageKey = $"staging/{stagedReceiptId:D}/{hash}";
        await UploadImmutableAsync(
            storageKey,
            hash,
            content,
            CreateStagedMetadata(
                hash,
                content.Length,
                firstSeenAtUtc,
                StagedArtifactDisposition.Pending),
            CreateDispositionTags(StagedArtifactDisposition.Pending),
            cancellationToken);
        return await GetStagedAsync(storageKey, cancellationToken)
            ?? throw new IntakeArtifactIntegrityException();
    }

    public async Task<StagedArtifactInventoryItem> StageAsync(
        Guid stagedReceiptId,
        string contentHash,
        Stream content,
        long contentLength,
        DateTimeOffset firstSeenAtUtc,
        CancellationToken cancellationToken)
    {
        if (stagedReceiptId == Guid.Empty)
        {
            throw new ArgumentException(
                "A staged receipt identifier is required.",
                nameof(stagedReceiptId));
        }

        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegative(contentLength);
        if (!content.CanRead)
        {
            throw new ArgumentException("The staged source stream must be readable.", nameof(content));
        }

        await using var buffered = await BufferAndHashAsync(
            content,
            contentLength,
            cancellationToken);
        var hash = NormalizeHash(contentHash);
        if (!string.Equals(buffered.ContentHash, hash, StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }

        var key = $"staging/{stagedReceiptId:D}/{hash}";
        var wroteBlob = false;
        BlobClient? blob = null;
        try
        {
            await EnsureContainerExistsAsync(cancellationToken);
            blob = container.GetBlobClient(key);
            wroteBlob = await UploadStreamOrVerifyAsync(
                blob,
                hash,
                contentLength,
                buffered.Stream,
                CreateStagedMetadata(
                    hash,
                    contentLength,
                    firstSeenAtUtc,
                    StagedArtifactDisposition.Pending),
                CreateDispositionTags(StagedArtifactDisposition.Pending),
                cancellationToken);

            var staged = await GetStagedAsync(key, cancellationToken)
                ?? throw new IntakeArtifactIntegrityException();
            if (staged.ContentLength != contentLength
                || !string.Equals(staged.ContentHash, hash, StringComparison.Ordinal)
                || staged.Disposition != StagedArtifactDisposition.Pending)
            {
                throw new IntakeArtifactIntegrityException();
            }

            return staged;
        }
        catch (IntakeArtifactIntegrityException) when (wroteBlob && blob is not null)
        {
            try
            {
                await blob.DeleteIfExistsAsync(
                    DeleteSnapshotsOption.IncludeSnapshots,
                    cancellationToken: CancellationToken.None);
            }
            catch (RequestFailedException exception)
            {
                throw DependencyUnavailable(exception);
            }

            throw;
        }
        catch (RequestFailedException exception)
        {
            throw DependencyUnavailable(exception);
        }
    }

    public async Task<StagedArtifactInventoryItem?> GetStagedAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        var hash = GetStagedStorageKeyHash(storageKey);
        var blob = container.GetBlobClient(storageKey);
        try
        {
            var properties = await blob.GetPropertiesAsync(
                cancellationToken: cancellationToken);
            var tags = await blob.GetTagsAsync(
                cancellationToken: cancellationToken);
            return MapStaged(
                storageKey,
                hash,
                properties.Value.ContentLength,
                properties.Value.CreatedOn,
                properties.Value.ETag,
                properties.Value.Metadata,
                tags.Value.Tags);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<StagedArtifactInventoryItem>> ListStagedAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        await EnsureContainerExistsAsync(cancellationToken);

        var items = new List<StagedArtifactInventoryItem>(maximumItems);
        await foreach (var blob in container.GetBlobsAsync(
                           BlobTraits.Metadata | BlobTraits.Tags,
                           BlobStates.None,
                           StagingPrefix,
                           cancellationToken))
        {
            if (!TryGetStagedStorageKeyHash(blob.Name, out var hash))
            {
                continue;
            }

            items.Add(MapStaged(
                blob.Name,
                hash,
                blob.Properties.ContentLength ?? 0,
                blob.Properties.CreatedOn ?? DateTimeOffset.UnixEpoch,
                blob.Properties.ETag ?? default,
                blob.Metadata,
                blob.Tags));
            if (items.Count == maximumItems)
            {
                break;
            }
        }

        return items;
    }

    public async Task<StagedArtifactInventoryItem?> TrySetStagedDispositionAsync(
        string storageKey,
        string expectedConcurrencyToken,
        StagedArtifactDisposition disposition,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(disposition))
        {
            throw new ArgumentOutOfRangeException(nameof(disposition));
        }

        var hash = GetStagedStorageKeyHash(storageKey);
        if (string.IsNullOrWhiteSpace(expectedConcurrencyToken))
        {
            return null;
        }

        var expected = new ETag(expectedConcurrencyToken);
        var blob = container.GetBlobClient(storageKey);
        try
        {
            var properties = await blob.GetPropertiesAsync(
                new BlobRequestConditions { IfMatch = expected },
                cancellationToken);
            var tags = await blob.GetTagsAsync(
                cancellationToken: cancellationToken);
            var current = MapStaged(
                storageKey,
                hash,
                properties.Value.ContentLength,
                properties.Value.CreatedOn,
                properties.Value.ETag,
                properties.Value.Metadata,
                tags.Value.Tags);
            var metadataUpdate = await blob.SetMetadataAsync(
                CreateStagedMetadata(
                    current.ContentHash,
                    current.ContentLength,
                    current.FirstSeenAtUtc,
                    disposition,
                    properties.Value.Metadata),
                new BlobRequestConditions { IfMatch = expected },
                cancellationToken);

            var tagConditions = new BlobRequestConditions
            {
                IfMatch = metadataUpdate.Value.ETag
            };
            if (tags.Value.Tags.TryGetValue(DispositionTagName, out var previousDisposition)
                && Enum.TryParse<StagedArtifactDisposition>(
                    previousDisposition,
                    ignoreCase: false,
                    out var parsedPreviousDisposition)
                && Enum.IsDefined(parsedPreviousDisposition)
                && previousDisposition.Equals(
                    parsedPreviousDisposition.ToString(),
                    StringComparison.Ordinal))
            {
                tagConditions.TagConditions = BuildDispositionTagCondition(
                    previousDisposition);
            }

            await blob.SetTagsAsync(
                CreateDispositionTags(disposition),
                tagConditions,
                cancellationToken);
            var updated = await GetStagedAsync(storageKey, cancellationToken);
            return updated is not null
                && updated.Disposition == disposition
                && string.Equals(
                    updated.ConcurrencyToken,
                    metadataUpdate.Value.ETag.ToString(),
                    StringComparison.Ordinal)
                    ? updated
                    : null;
        }
        catch (RequestFailedException exception) when (exception.Status is 404 or 412)
        {
            return null;
        }
    }

    public async Task<bool> DeleteCompletedStagedAsync(
        string storageKey,
        string expectedConcurrencyToken,
        CancellationToken cancellationToken)
    {
        var hash = GetStagedStorageKeyHash(storageKey);
        if (string.IsNullOrWhiteSpace(expectedConcurrencyToken))
        {
            return false;
        }

        var expected = new ETag(expectedConcurrencyToken);
        var blob = container.GetBlobClient(storageKey);
        try
        {
            var properties = await blob.GetPropertiesAsync(
                new BlobRequestConditions { IfMatch = expected },
                cancellationToken);
            var tags = await blob.GetTagsAsync(
                cancellationToken: cancellationToken);
            var current = MapStaged(
                storageKey,
                hash,
                properties.Value.ContentLength,
                properties.Value.CreatedOn,
                properties.Value.ETag,
                properties.Value.Metadata,
                tags.Value.Tags);
            if (current.Disposition != StagedArtifactDisposition.Completed)
            {
                return false;
            }

            var deleted = await blob.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                new BlobRequestConditions
                {
                    IfMatch = expected,
                    TagConditions = BuildDispositionTagCondition(
                        StagedArtifactDisposition.Completed.ToString())
                },
                cancellationToken);
            return deleted.Value;
        }
        catch (RequestFailedException exception) when (exception.Status is 404 or 412)
        {
            return false;
        }
    }

    private async Task UploadImmutableAsync(
        string storageKey,
        string expectedHash,
        ReadOnlyMemory<byte> content,
        IDictionary<string, string> metadata,
        IDictionary<string, string>? tags,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureContainerExistsAsync(cancellationToken);
            await UploadOrVerifyAsync(
                container.GetBlobClient(storageKey),
                expectedHash,
                content,
                metadata,
                tags,
                cancellationToken);
        }
        catch (RequestFailedException exception)
        {
            throw DependencyUnavailable(exception);
        }
    }

    /// <summary>
    /// Writes the blob once; a blob already at this key is verified against the
    /// expected content instead, so a replay of the same bytes is a no-op and a
    /// different payload under the same key is an integrity failure.
    /// </summary>
    private static async Task UploadOrVerifyAsync(
        BlobClient blob,
        string expectedHash,
        ReadOnlyMemory<byte> content,
        IDictionary<string, string> metadata,
        IDictionary<string, string>? tags,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content.ToArray(), writable: false);
        try
        {
            await blob.UploadAsync(stream, new BlobUploadOptions
            {
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                Metadata = metadata,
                Tags = tags
            }, cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            await VerifyBlobAsync(blob, expectedHash, content.Length, cancellationToken);
        }
    }

    private static async Task<bool> UploadStreamOrVerifyAsync(
        BlobClient blob,
        string expectedHash,
        long expectedLength,
        Stream content,
        IDictionary<string, string> metadata,
        IDictionary<string, string>? tags,
        CancellationToken cancellationToken)
    {
        content.Position = 0;
        try
        {
            await blob.UploadAsync(
                content,
                new BlobUploadOptions
                {
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
                    Metadata = metadata,
                    Tags = tags
                },
                cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status is 409 or 412)
        {
            await VerifyBlobAsync(blob, expectedHash, expectedLength, cancellationToken);
            return false;
        }

        await VerifyBlobAsync(blob, expectedHash, expectedLength, cancellationToken);
        return true;
    }

    /// <summary>
    /// The named fault Core retries on. Azure's own exception type stays in the
    /// adapter; Core matches only intake faults.
    /// </summary>
    private static IntakeDependencyUnavailableException DependencyUnavailable(
        RequestFailedException exception) =>
        new("The intake artifact dependency is unavailable.", exception);

    private static async Task VerifyBlobAsync(
        BlobClient blob,
        string expectedHash,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        var download = await blob.DownloadStreamingAsync(
            cancellationToken: cancellationToken);
        using var content = download.Value.Content;
        if (download.Value.Details.ContentLength != expectedLength
            || !string.Equals(
                Convert.ToHexString(
                    await SHA256.HashDataAsync(content, cancellationToken)),
                expectedHash,
                StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }
    }

    private static Dictionary<string, string> CreateStagedMetadata(
        string hash,
        long contentLength,
        DateTimeOffset firstSeenAtUtc,
        StagedArtifactDisposition disposition,
        IDictionary<string, string>? existing = null)
    {
        var metadata = existing is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);
        metadata[HashMetadataName] = hash;
        metadata[ContentLengthMetadataName] = contentLength.ToString(
            CultureInfo.InvariantCulture);
        metadata[FirstSeenMetadataName] = firstSeenAtUtc.ToUniversalTime().ToString(
            "O",
            CultureInfo.InvariantCulture);
        metadata[DispositionMetadataName] = disposition.ToString();
        return metadata;
    }

    private static Dictionary<string, string> CreateDispositionTags(
        StagedArtifactDisposition disposition) =>
        new(StringComparer.Ordinal)
        {
            [DispositionTagName] = disposition.ToString()
        };

    private static StagedArtifactInventoryItem MapStaged(
        string storageKey,
        string hash,
        long contentLength,
        DateTimeOffset createdOn,
        ETag etag,
        IDictionary<string, string>? metadata,
        IDictionary<string, string>? tags)
    {
        var firstSeenAtUtc = default(DateTimeOffset);
        var metadataDisposition = default(StagedArtifactDisposition);
        var tagDisposition = default(StagedArtifactDisposition);
        var hasFirstSeen = metadata is not null
            && metadata.TryGetValue(FirstSeenMetadataName, out var firstSeenValue)
            && DateTimeOffset.TryParseExact(
                firstSeenValue,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out firstSeenAtUtc);
        var hasMetadataDisposition = metadata is not null
            && metadata.TryGetValue(
                DispositionMetadataName,
                out var metadataDispositionValue)
            && Enum.TryParse<StagedArtifactDisposition>(
                metadataDispositionValue,
                ignoreCase: false,
                out metadataDisposition)
            && Enum.IsDefined(metadataDisposition);
        var hasTagDisposition = tags is not null
            && tags.TryGetValue(DispositionTagName, out var tagDispositionValue)
            && Enum.TryParse<StagedArtifactDisposition>(
                tagDispositionValue,
                ignoreCase: false,
                out tagDisposition)
            && Enum.IsDefined(tagDisposition);
        var valid = metadata is not null
            && metadata.TryGetValue(HashMetadataName, out var metadataHash)
            && string.Equals(metadataHash, hash, StringComparison.Ordinal)
            && metadata.TryGetValue(ContentLengthMetadataName, out var lengthValue)
            && long.TryParse(
                lengthValue,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var metadataLength)
            && metadataLength == contentLength
            && hasFirstSeen
            && hasMetadataDisposition
            && hasTagDisposition
            && metadataDisposition == tagDisposition
            && !string.IsNullOrWhiteSpace(etag.ToString());

        return new(
            storageKey,
            hash,
            contentLength,
            hasFirstSeen ? firstSeenAtUtc : createdOn,
            valid ? metadataDisposition : StagedArtifactDisposition.Unmatched,
            etag.ToString());
    }

    private static string BuildDispositionTagCondition(string disposition) =>
        $"\"{DispositionTagName}\" = '{disposition}'";

    private static string ValidateContent(
        string contentHash,
        ReadOnlyMemory<byte> content)
    {
        var hash = NormalizeHash(contentHash);
        if (!string.Equals(
                Convert.ToHexString(SHA256.HashData(content.Span)),
                hash,
                StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }

        return hash;
    }

    private static string GetStorageKeyHash(string storageKey)
    {
        if (TryGetStagedStorageKeyHash(storageKey, out var stagedHash))
        {
            return stagedHash;
        }

        var segments = storageKey.Split('/', StringSplitOptions.None);
        if (segments.Length == 3
            && segments[0].Equals("sha256", StringComparison.Ordinal)
            && segments[1].Length == 2
            && segments[2].Length == 64
            && segments[2].All(char.IsAsciiHexDigit)
            && segments[2].Equals(
                segments[2].ToUpperInvariant(),
                StringComparison.Ordinal)
            && segments[2].StartsWith(segments[1], StringComparison.Ordinal))
        {
            return segments[2];
        }

        throw new ArgumentException(
            "The artifact storage key is invalid.",
            nameof(storageKey));
    }

    private static string GetStagedStorageKeyHash(string storageKey) =>
        TryGetStagedStorageKeyHash(storageKey, out var hash)
            ? hash
            : throw new ArgumentException(
                "The staged artifact storage key is invalid.",
                nameof(storageKey));

    private static bool TryGetStagedStorageKeyHash(
        string storageKey,
        out string hash)
    {
        var segments = storageKey.Split('/', StringSplitOptions.None);
        if (segments.Length == 3
            && segments[0].Equals("staging", StringComparison.Ordinal)
            && Guid.TryParseExact(segments[1], "D", out var stagedReceiptId)
            && stagedReceiptId != Guid.Empty
            && segments[1].Equals(
                stagedReceiptId.ToString("D"),
                StringComparison.Ordinal)
            && segments[2].Length == 64
            && segments[2].All(char.IsAsciiHexDigit)
            && segments[2].Equals(
                segments[2].ToUpperInvariant(),
                StringComparison.Ordinal))
        {
            hash = segments[2];
            return true;
        }

        hash = string.Empty;
        return false;
    }

    private static string NormalizeHash(string contentHash)
    {
        if (contentHash.Length != 64 || !contentHash.All(char.IsAsciiHexDigit))
        {
            throw new IntakeArtifactIntegrityException();
        }

        return contentHash.ToUpperInvariant();
    }

    private static async Task<BufferedStream> BufferAndHashAsync(
        Stream source,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pegasus-intake-{Guid.NewGuid():N}.tmp");
        var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose | FileOptions.SequentialScan);
        var buffer = new byte[64 * 1024];
        try
        {
            long actualLength = 0;
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                actualLength = checked(actualLength + read);
                if (actualLength > expectedLength)
                {
                    throw new IntakeArtifactIntegrityException();
                }

                hasher.AppendData(buffer, 0, read);
                await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            if (actualLength != expectedLength)
            {
                throw new IntakeArtifactIntegrityException();
            }

            await stream.FlushAsync(cancellationToken);
            stream.Position = 0;
            return new BufferedStream(
                stream,
                Convert.ToHexString(hasher.GetHashAndReset()),
                actualLength);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    private sealed class BufferedStream(
        FileStream stream,
        string contentHash,
        long contentLength) : IAsyncDisposable
    {
        public FileStream Stream { get; } = stream;

        public string ContentHash { get; } = contentHash;

        public long ContentLength { get; } = contentLength;

        public ValueTask DisposeAsync() => Stream.DisposeAsync();
    }

    private async ValueTask EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        if (allowCreateIfNotExists)
        {
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }
    }
}
