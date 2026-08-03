using System.Security.Cryptography;
using Pegasus.Core.Documents;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// Production managed-document content storage in the approved Box custody
/// root, at <c>{caseReference}/managed/{versionId:N}/content</c> under the
/// same Case/PO folder as retained intake sources.
/// <see cref="LocalDocumentContentStore"/> resolves the same identity under
/// its local <c>cases/</c> prefix, so both profiles resolve one version to
/// one object; every Box call is fenced to the approved root by
/// <see cref="BoxContentClient"/>.
/// </summary>
internal sealed class BoxDocumentContentStore(BoxContentClient client) : IDocumentContentStore
{
    private const string ContentFileName = "content";
    private const string ContentMediaType = "application/octet-stream";

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

        var versionFolder = await ResolveVersionFolderAsync(caseReference, versionId, create: true, cancellationToken);
        var existing = await client.FindChildAsync(versionFolder!, ContentFileName, "file", cancellationToken);
        if (existing is not null)
        {
            // A repeated store of identical content is a successful replay, not a conflict.
            var retained = await client.DownloadAsync(existing.Id, cancellationToken);
            Verify(retained, normalizedHash, content.Length);
            return;
        }

        await client.UploadAsync(versionFolder!, ContentFileName, content, ContentMediaType, cancellationToken);
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
        var versionFolder = await ResolveVersionFolderAsync(caseReference, versionId, create: false, cancellationToken);
        if (versionFolder is null)
        {
            throw new FileNotFoundException("The document content is unavailable.");
        }

        var file = await client.FindChildAsync(versionFolder, ContentFileName, "file", cancellationToken)
            ?? throw new FileNotFoundException("The document content is unavailable.");
        var content = await client.DownloadAsync(file.Id, cancellationToken);
        Verify(content, NormalizeSha256(expectedSha256), expectedLength);
        return new MemoryStream(content, writable: false);
    }

    public async Task DeleteAsync(
        Guid caseId,
        string caseReference,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        ValidateIdentifiers(caseId, caseReference, versionId);
        var versionFolder = await ResolveVersionFolderAsync(caseReference, versionId, create: false, cancellationToken);
        if (versionFolder is null)
        {
            return;
        }

        var file = await client.FindChildAsync(versionFolder, ContentFileName, "file", cancellationToken);
        if (file is null)
        {
            return;
        }

        await client.DeleteFileAsync(file.Id, cancellationToken);
    }

    private async Task<string?> ResolveVersionFolderAsync(
        string caseReference,
        Guid versionId,
        bool create,
        CancellationToken cancellationToken)
    {
        var current = client.RootFolderId;
        if (create)
        {
            current = (await client.GetOrCreateFolderAsync(
                current,
                SafeCaseFolderName(caseReference),
                cancellationToken)).Id;
        }
        else
        {
            var existingRoot = await client.FindChildAsync(
                current,
                SafeCaseFolderName(caseReference),
                "folder",
                cancellationToken);
            if (existingRoot is null)
            {
                return null;
            }

            current = existingRoot.Id;
        }

        foreach (var segment in new[] { "managed", versionId.ToString("N") })
        {
            if (create)
            {
                current = (await client.GetOrCreateFolderAsync(current, segment, cancellationToken)).Id;
                continue;
            }

            var existing = await client.FindChildAsync(current, segment, "folder", cancellationToken);
            if (existing is null)
            {
                return null;
            }

            current = existing.Id;
        }

        return current;
    }

    private static void Verify(
        ReadOnlySpan<byte> content,
        string expectedSha256,
        long expectedLength)
    {
        if (content.Length != expectedLength)
        {
            throw new InvalidDataException("Document custody length verification failed.");
        }

        var actualHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (!string.Equals(expectedSha256, actualHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Document custody hash verification failed.");
        }
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
