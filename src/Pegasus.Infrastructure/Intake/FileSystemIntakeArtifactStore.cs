using System.Buffers;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text.Json;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Intake;

public sealed partial class FileSystemIntakeArtifactStore(string rootPath)
    : IIntakeArtifactStore, IIntakeQuarantineArtifactStore, IDisposable
{
    private readonly string rootPath = Path.GetFullPath(rootPath);
    private readonly SemaphoreSlim stagingGate = new(1, 1);
    private int disposeState;

    public async Task<string> StoreAsync(
        string contentHash,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var normalisedHash = NormaliseHash(contentHash);
        var actualHash = Convert.ToHexString(SHA256.HashData(content.Span));
        if (!actualHash.Equals(normalisedHash, StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }

        var storageKey = $"sha256/{normalisedHash[..2]}/{normalisedHash}";
        await StoreImmutableAsync(
            Resolve(storageKey),
            normalisedHash,
            content,
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
            throw new ArgumentException("The quarantine source stream must be readable.", nameof(content));
        }

        var temporaryDirectory = Path.Combine(rootPath, "quarantine-staging");
        Directory.CreateDirectory(temporaryDirectory);
        var temporary = Path.Combine(temporaryDirectory, $".{Guid.NewGuid():N}.tmp");
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        string contentHash;
        long retainedLength = 0;
        try
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var destination = new FileStream(
                             temporary,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                while (true)
                {
                    var read = await content.ReadAsync(buffer.AsMemory(), cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    retainedLength = checked(retainedLength + read);
                    if (retainedLength > contentLength)
                    {
                        throw new IntakeArtifactIntegrityException();
                    }

                    hasher.AppendData(buffer, 0, read);
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                if (retainedLength != contentLength)
                {
                    throw new IntakeArtifactIntegrityException();
                }

                await destination.FlushAsync(cancellationToken);
            }

            contentHash = Convert.ToHexString(hasher.GetHashAndReset());
            var storageKey = $"sha256/{contentHash[..2]}/{contentHash}";
            var finalPath = Resolve(storageKey);
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
            try
            {
                File.Move(temporary, finalPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(finalPath))
            {
                await VerifyFileAsync(
                    finalPath,
                    contentHash,
                    retainedLength,
                    cancellationToken);
            }

            var artifact = new IntakeQuarantineArtifact(
                storageKey,
                contentHash,
                retainedLength);
            return artifact;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public Task VerifyAsync(
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

        var normalisedHash = artifact.ContentHash.ToUpperInvariant();
        var expectedStorageKey = $"sha256/{normalisedHash[..2]}/{normalisedHash}";
        if (!string.Equals(artifact.StorageKey, expectedStorageKey, StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }

        var path = Resolve(artifact.StorageKey);
        if (!File.Exists(path))
        {
            throw new IntakeArtifactIntegrityException();
        }

        return VerifyFileAsync(
            path,
            normalisedHash,
            artifact.ContentLength,
            cancellationToken);
    }

    public async Task<ReadOnlyMemory<byte>?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        var path = storageKey.StartsWith("staging/", StringComparison.Ordinal)
            ? ResolveStaged(storageKey)
            : Resolve(storageKey);
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(path, cancellationToken);
        var expectedHash = Path.GetFileName(path);
        var actualHash = Convert.ToHexString(SHA256.HashData(content));
        if (!actualHash.Equals(expectedHash, StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }

        return content;
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

        var hash = NormaliseHash(contentHash);
        if (!string.Equals(
                Convert.ToHexString(SHA256.HashData(content.Span)),
                hash,
                StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }

        var storageKey = $"staging/{stagedReceiptId:D}/{hash}";
        var path = ResolveStaged(storageKey);
        await stagingGate.WaitAsync(cancellationToken);
        try
        {
            await StoreImmutableAsync(path, hash, content, cancellationToken);
            var existing = await ReadStagedMetadataAsync(path, cancellationToken);
            var metadata = IsValidMetadata(path, existing)
                ? existing!
                : new StagedArtifactMetadata(
                    hash,
                    content.Length,
                    firstSeenAtUtc,
                    StagedArtifactDisposition.Pending.ToString(),
                    Guid.NewGuid().ToString("N"));
            await WriteStagedMetadataAsync(path, metadata, cancellationToken);
            return MapStaged(storageKey, path, metadata);
        }
        finally
        {
            stagingGate.Release();
        }
    }

    public async Task<StagedArtifactInventoryItem?> GetStagedAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        var path = ResolveStaged(storageKey);
        if (!File.Exists(path) && !File.Exists(MetadataPath(path)))
        {
            return null;
        }

        var metadata = await ReadStagedMetadataAsync(path, cancellationToken);
        if (metadata is not null
            && File.Exists(path)
            && string.Equals(metadata.ContentHash, Path.GetFileName(path), StringComparison.Ordinal)
            && metadata.ContentLength == new FileInfo(path).Length
            && Enum.TryParse<StagedArtifactDisposition>(
                metadata.Disposition,
                ignoreCase: false,
                out var disposition)
            && Enum.IsDefined(disposition)
            && !string.IsNullOrWhiteSpace(metadata.ConcurrencyToken))
        {
            return new(
                storageKey,
                metadata.ContentHash,
                metadata.ContentLength,
                metadata.FirstSeenAtUtc,
                disposition,
                metadata.ConcurrencyToken);
        }

        var file = new FileInfo(path);
        return new(
            storageKey,
            HashRegex().IsMatch(file.Name) ? file.Name : string.Empty,
            file.Exists ? file.Length : metadata?.ContentLength ?? 0,
            metadata?.FirstSeenAtUtc
                ?? (file.Exists
                    ? new DateTimeOffset(file.CreationTimeUtc, TimeSpan.Zero)
                    : DateTimeOffset.UnixEpoch),
            StagedArtifactDisposition.Unmatched,
            metadata?.ConcurrencyToken ?? string.Empty);
    }

    public async Task<IReadOnlyList<StagedArtifactInventoryItem>> ListStagedAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var stagingRoot = Path.Combine(rootPath, "staging");
        if (!Directory.Exists(stagingRoot))
        {
            return [];
        }

        var items = new List<StagedArtifactInventoryItem>(maximumItems);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidatePath in Directory.EnumerateFiles(
                     stagingRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = candidatePath.EndsWith(MetadataSuffix, StringComparison.Ordinal)
                ? candidatePath[..^MetadataSuffix.Length]
                : candidatePath;
            if (!HashRegex().IsMatch(Path.GetFileName(path)) || !visited.Add(path))
            {
                continue;
            }

            var storageKey = Path.GetRelativePath(rootPath, path)
                .Replace(Path.DirectorySeparatorChar, '/');
            var item = await GetStagedAsync(storageKey, cancellationToken);
            if (item is not null)
            {
                items.Add(item);
                if (items.Count == maximumItems)
                {
                    break;
                }
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

        var path = ResolveStaged(storageKey);
        await stagingGate.WaitAsync(cancellationToken);
        try
        {
            var current = await GetStagedAsync(storageKey, cancellationToken);
            if (current is null
                || !string.Equals(
                    current.ConcurrencyToken,
                    expectedConcurrencyToken,
                    StringComparison.Ordinal))
            {
                return null;
            }

            var metadata = new StagedArtifactMetadata(
                current.ContentHash,
                current.ContentLength,
                current.FirstSeenAtUtc,
                disposition.ToString(),
                Guid.NewGuid().ToString("N"));
            await WriteStagedMetadataAsync(path, metadata, cancellationToken);
            return MapStaged(storageKey, path, metadata);
        }
        finally
        {
            stagingGate.Release();
        }
    }

    public async Task<bool> DeleteCompletedStagedAsync(
        string storageKey,
        string expectedConcurrencyToken,
        CancellationToken cancellationToken)
    {
        var path = ResolveStaged(storageKey);
        await stagingGate.WaitAsync(cancellationToken);
        try
        {
            var current = await GetStagedAsync(storageKey, cancellationToken);
            if (current is null
                || current.Disposition != StagedArtifactDisposition.Completed
                || !string.Equals(
                    current.ConcurrencyToken,
                    expectedConcurrencyToken,
                    StringComparison.Ordinal))
            {
                return false;
            }

            File.Delete(path);
            File.Delete(MetadataPath(path));
            return true;
        }
        finally
        {
            stagingGate.Release();
        }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposeState, 1) == 0)
        {
            stagingGate.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private static async Task StoreImmutableAsync(
        string destination,
        string expectedHash,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
        {
            await VerifyFileAsync(destination, expectedHash, content.Length, cancellationToken);
            return;
        }

        var temporary = Path.Combine(
            Path.GetDirectoryName(destination)!,
            $".{expectedHash}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                             temporary,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            try
            {
                File.Move(temporary, destination, overwrite: false);
            }
            catch (IOException) when (File.Exists(destination))
            {
                await VerifyFileAsync(destination, expectedHash, content.Length, cancellationToken);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static async Task VerifyFileAsync(
        string path,
        string expectedHash,
        long? expectedLength,
        CancellationToken cancellationToken)
    {
        var file = new FileInfo(path);
        if (expectedLength is not null && file.Length != expectedLength.Value)
        {
            throw new IntakeArtifactIntegrityException();
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        if (!actualHash.Equals(expectedHash, StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }
    }

    private static async Task<StagedArtifactMetadata?> ReadStagedMetadataAsync(
        string artifactPath,
        CancellationToken cancellationToken)
    {
        var path = MetadataPath(artifactPath);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var content = await File.ReadAllBytesAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<StagedArtifactMetadata>(content);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task WriteStagedMetadataAsync(
        string artifactPath,
        StagedArtifactMetadata metadata,
        CancellationToken cancellationToken)
    {
        var destination = MetadataPath(artifactPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(
                temporary,
                JsonSerializer.SerializeToUtf8Bytes(metadata),
                cancellationToken);
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static StagedArtifactInventoryItem MapStaged(
        string storageKey,
        string artifactPath,
        StagedArtifactMetadata metadata) =>
        new(
            storageKey,
            metadata.ContentHash,
            new FileInfo(artifactPath).Length,
            metadata.FirstSeenAtUtc,
            Enum.Parse<StagedArtifactDisposition>(
                metadata.Disposition,
                ignoreCase: false),
            metadata.ConcurrencyToken);

    private static bool IsValidMetadata(
        string artifactPath,
        StagedArtifactMetadata? metadata) =>
        metadata is not null
        && File.Exists(artifactPath)
        && string.Equals(
            metadata.ContentHash,
            Path.GetFileName(artifactPath),
            StringComparison.Ordinal)
        && metadata.ContentLength == new FileInfo(artifactPath).Length
        && Enum.TryParse<StagedArtifactDisposition>(
            metadata.Disposition,
            ignoreCase: false,
            out var disposition)
        && Enum.IsDefined(disposition)
        && !string.IsNullOrWhiteSpace(metadata.ConcurrencyToken);

    private string ResolveStaged(string storageKey)
    {
        var segments = storageKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3
            || !segments[0].Equals("staging", StringComparison.Ordinal)
            || !Guid.TryParseExact(segments[1], "D", out var stagedReceiptId)
            || stagedReceiptId == Guid.Empty
            || !segments[1].Equals(stagedReceiptId.ToString("D"), StringComparison.Ordinal)
            || !HashRegex().IsMatch(segments[2]))
        {
            throw new ArgumentException(
                "The staged artifact storage key is invalid.",
                nameof(storageKey));
        }

        var path = Path.GetFullPath(Path.Combine(
            rootPath,
            segments[0],
            segments[1],
            segments[2]));
        var stagingRoot = Path.GetFullPath(Path.Combine(rootPath, "staging"));
        var requiredPrefix = stagingRoot.EndsWith(Path.DirectorySeparatorChar)
            ? stagingRoot
            : stagingRoot + Path.DirectorySeparatorChar;
        if (!path.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The staged artifact storage key is outside the configured root.",
                nameof(storageKey));
        }

        return path;
    }

    private const string MetadataSuffix = ".metadata.json";

    private static string MetadataPath(string artifactPath) =>
        artifactPath + MetadataSuffix;

    private sealed record StagedArtifactMetadata(
        string ContentHash,
        long ContentLength,
        DateTimeOffset FirstSeenAtUtc,
        string Disposition,
        string ConcurrencyToken);

    private string Resolve(string storageKey)
    {
        var segments = storageKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3
            || !segments[0].Equals("sha256", StringComparison.Ordinal)
            || segments[1].Length != 2
            || !HashRegex().IsMatch(segments[2])
            || !segments[2].StartsWith(segments[1], StringComparison.Ordinal))
        {
            throw new ArgumentException("The artifact storage key is invalid.", nameof(storageKey));
        }

        var path = Path.GetFullPath(Path.Combine(rootPath, segments[0], segments[1], segments[2]));
        var requiredPrefix = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        if (!path.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The artifact storage key is outside the configured root.", nameof(storageKey));
        }

        return path;
    }

    private static string NormaliseHash(string contentHash)
    {
        var value = contentHash.ToUpperInvariant();
        return HashRegex().IsMatch(value)
            ? value
            : throw new ArgumentException("A SHA-256 content hash is required.", nameof(contentHash));
    }

    [GeneratedRegex("^[0-9A-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex HashRegex();
}
