using System.Text.RegularExpressions;
using System.Security.Cryptography;
using CollisionSpike.Core.Intake;

namespace CollisionSpike.Infrastructure.Intake;

internal sealed partial class FileSystemIntakeArtifactStore(string rootPath) : IIntakeArtifactStore
{
    private readonly string rootPath = Path.GetFullPath(rootPath);

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
        var destination = Resolve(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        if (File.Exists(destination))
        {
            await VerifyFileAsync(destination, normalisedHash, content.Length, cancellationToken);
            return storageKey;
        }

        var temporary = Path.Combine(
            Path.GetDirectoryName(destination)!,
            $".{normalisedHash}.{Guid.NewGuid():N}.tmp");
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
                // Another request retained the same immutable content first.
                await VerifyFileAsync(destination, normalisedHash, content.Length, cancellationToken);
            }

            return storageKey;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public async Task<ReadOnlyMemory<byte>?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        var path = Resolve(storageKey);
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
