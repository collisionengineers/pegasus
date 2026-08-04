using System.Security.Cryptography;
using Pegasus.Core.Documents;

namespace Pegasus.Infrastructure.Custody;

public sealed class LocalDocumentContentStore(string rootPath) : IDocumentContentStore
{
    private readonly string rootPath = Path.GetFullPath(rootPath);

    public async Task StoreAsync(
        Guid caseId,
        string caseReference,
        Guid versionId,
        ReadOnlyMemory<byte> content,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        ValidateIdentifiers(caseId, caseReference, versionId);
        var normalizedHash = NormalizeSha256(expectedSha256);
        var actualHash = Convert.ToHexString(SHA256.HashData(content.Span)).ToLowerInvariant();
        if (!string.Equals(normalizedHash, actualHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Document content does not match its custody hash.");
        }

        var path = Resolve(caseReference, versionId);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        if (File.Exists(path))
        {
            await VerifyAsync(path, normalizedHash, content.Length, cancellationToken);
            return;
        }

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                RandomAccess.FlushToDisk(stream.SafeFileHandle);
            }

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Move(temporaryPath, path, overwrite: false);
            }
            catch (IOException) when (File.Exists(path))
            {
                await VerifyAsync(path, normalizedHash, content.Length, cancellationToken);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public Task DeleteAsync(
        Guid caseId,
        string caseReference,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        ValidateIdentifiers(caseId, caseReference, versionId);
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(caseReference, versionId);
        File.Delete(path);
        return Task.CompletedTask;
    }

    public async Task<Stream> OpenReadAsync(
        Guid caseId,
        string caseReference,
        Guid versionId,
        string expectedSha256,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        ValidateIdentifiers(caseId, caseReference, versionId);
        var path = Resolve(caseReference, versionId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The document content is unavailable.");
        }

        await VerifyAsync(path, NormalizeSha256(expectedSha256), expectedLength, cancellationToken);
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    private static async Task VerifyAsync(
        string path,
        string expectedSha256,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length != expectedLength)
        {
            throw new InvalidDataException("Document custody length verification failed.");
        }

        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken))
            .ToLowerInvariant();
        if (!string.Equals(expectedSha256, actualHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Document custody hash verification failed.");
        }
    }

    private string Resolve(string caseReference, Guid versionId)
    {
        var path = Path.GetFullPath(Path.Combine(
            rootPath,
            "cases",
            SafeCaseFolderName(caseReference),
            "managed",
            versionId.ToString("N"),
            "content"));
        var rootPrefix = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("The document content is outside the configured custody root.");
        }

        return path;
    }

    private static void ValidateIdentifiers(Guid caseId, string caseReference, Guid versionId)
    {
        if (caseId == Guid.Empty
            || versionId == Guid.Empty
            || string.IsNullOrWhiteSpace(caseReference)
            || caseReference.Any(char.IsControl))
        {
            throw new ArgumentException("Case, Case/PO, and document version identifiers are required.");
        }
    }

    private static string SafeCaseFolderName(string value)
    {
        var result = CustodyNames.SafeName(value);

        return result;
    }

    private static string NormalizeSha256(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != SHA256.HashSizeInBytes * 2 || !value.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("A SHA-256 hash is required.", nameof(value));
        }

        return value.ToLowerInvariant();
    }
}
