using System.Collections.Concurrent;
using System.Security.Cryptography;
using Pegasus.Core.Documents;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// Production managed-document content storage: the files sit directly in the
/// one bound Case/PO folder, named by occurrence ordinal, business revision
/// and original filename. Remote and internal identifiers stay in SQL and
/// provenance rather than in Box names or sidecar files.
/// </summary>
internal sealed class BoxDocumentContentStore(BoxContentClient client) : IDocumentContentStore
{
    private readonly ConcurrentDictionary<Guid, CreatedFile> createdFiles = [];

    /// <summary>
    /// The document's name in the flat case folder: its occurrence ordinal,
    /// then the original file name. A later revision of the same occurrence
    /// says so in its own name, because a flat folder has nowhere else to
    /// put it and two revisions must never collide. The name is derived
    /// wholly from the persisted address, so a read finds exactly what the
    /// write produced without needing a binding file to tell it.
    /// </summary>
    internal static string FlatFileName(ManagedDocumentContentAddress address)
    {
        var safe = CustodyNames.SafeName(address.FileName);
        if (address.Version <= 1)
        {
            return $"{address.OccurrenceOrdinal:000} {safe}";
        }

        var extension = Path.GetExtension(safe);
        var stem = Path.GetFileNameWithoutExtension(safe);
        return $"{address.OccurrenceOrdinal:000} {stem} (revision {address.Version:000}){extension}";
    }

    public async Task<DocumentContentWriteResult> StoreVersionAsync(
        ManagedDocumentContentAddress address,
        ReadOnlyMemory<byte> content,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        Validate(address);
        var normalizedHash = NormalizeSha256(expectedSha256);
        Verify(content.Span, normalizedHash, content.Length);
        var caseFolder = await ResolveCaseFolderAsync(address, create: true, cancellationToken)
            ?? throw new InvalidOperationException("The managed document folder could not be resolved.");
        var fileName = FlatFileName(address);
        var existing = await client.FindChildAsync(caseFolder, fileName, "file", cancellationToken);
        if (existing is not null)
        {
            await VerifyFileMetadataAsync(
                existing, caseFolder, address.MediaType, content.Length, cancellationToken);
            var retained = await client.DownloadAsync(existing.Id, cancellationToken);
            Verify(retained, normalizedHash, content.Length);
            return new(DocumentContentWriteDisposition.Replay, existing.Id);
        }

        var created = await client.UploadAsync(
            caseFolder,
            fileName,
            content,
            address.MediaType,
            cancellationToken);
        createdFiles[address.VersionId] = new(
            created.Id,
            address.CaseId,
            address.CaseReference,
            normalizedHash,
            content.Length);
        return new(DocumentContentWriteDisposition.Created, created.Id);
    }

    public async Task<Stream> OpenReadVersionAsync(
        ManagedDocumentContentAddress address,
        string expectedSha256,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        Validate(address);
        var normalizedHash = NormalizeSha256(expectedSha256);
        var caseFolder = await ResolveCaseFolderAsync(address, create: false, cancellationToken);
        if (caseFolder is null)
        {
            throw new FileNotFoundException("The document content is unavailable.");
        }
        var file = await client.FindChildAsync(
            caseFolder,
            FlatFileName(address),
            "file",
            cancellationToken)
            ?? throw new FileNotFoundException("The document content is unavailable.");
        await VerifyFileMetadataAsync(
            file, caseFolder, address.MediaType, expectedLength, cancellationToken);
        var content = await client.DownloadAsync(file.Id, cancellationToken);
        Verify(content, normalizedHash, expectedLength);
        return new MemoryStream(content, writable: false);
    }

    public Task StoreAsync(
        Guid caseId,
        string caseReference,
        Guid versionId,
        ReadOnlyMemory<byte> content,
        string expectedSha256,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "Managed Box writes require the persisted business occurrence and revision address.");

    public Task<Stream> OpenReadAsync(
        Guid caseId,
        string caseReference,
        Guid versionId,
        string expectedSha256,
        long expectedLength,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            "Managed Box reads require the persisted business occurrence and revision address.");

    public async Task DeleteAsync(
        Guid caseId,
        string caseReference,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        if (!createdFiles.TryRemove(versionId, out var created))
        {
            return;
        }
        if (created.CaseId != caseId
            || !string.Equals(created.CaseReference, caseReference, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The uncommitted managed-content rollback does not match its Case identity.");
        }
        var bytes = await client.DownloadAsync(created.FileId, cancellationToken);
        Verify(bytes, created.Sha256, created.Length);
        await client.DeleteFileAsync(created.FileId, cancellationToken);
    }

    private async Task<string?> ResolveCaseFolderAsync(
        ManagedDocumentContentAddress address,
        bool create,
        CancellationToken cancellationToken)
    {
        var rootName = CustodyNames.SafeName(address.CaseReference);
        var root = await client.FindChildAsync(
            client.RootFolderId,
            rootName,
            "folder",
            cancellationToken);
        if (root is null)
        {
            if (!create)
            {
                return null;
            }
            throw new InvalidOperationException(
                "Managed content requires the already bound Case custody root.");
        }
        // DOCS-005: the case root carries no binding file; the reference-named
        // folder resolved under the custody root is the case's, and the durable
        // folder identity lives in the database.
        // Operator direction (2026-08-21): the case folder holds the files
        // themselves. The Evidence / role / occurrence / revision folders
        // and their two binding sidecars were never asked for; they put
        // three folders and two extra files around every single document.
        // The occurrence and revision identity that the bindings carried is
        // in SQL, which is already where the case root's own identity
        // lives, and it is expressed in the file's name.
        return root.Id;
    }

    /// <summary>
    /// Whether a Box file is the revision it is supposed to be.
    ///
    /// DOCS-010: Box does not return <c>content_type</c> for a file — it is not
    /// a field of the v2 file object, and asking for it simply yields nothing —
    /// so <see cref="BoxContentClient.BoxItem.MediaType"/> is null on every
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
        BoxContentClient.BoxItem file,
        string expectedParentId,
        string expectedMediaType,
        long expectedLength) =>
        string.Equals(file.ParentId, expectedParentId, StringComparison.Ordinal)
        && file.Size == expectedLength
        && (file.MediaType is not { Length: > 0 } mediaType
            || string.Equals(mediaType, expectedMediaType, StringComparison.OrdinalIgnoreCase));

    private async Task VerifyFileMetadataAsync(
        BoxContentClient.BoxItem file,
        string expectedParentId,
        string expectedMediaType,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        var metadata = await client.GetFileAsync(file.Id, cancellationToken);
        if (!IsExpectedRevision(metadata, expectedParentId, expectedMediaType, expectedLength))
        {
            throw new InvalidDataException(
                "Managed Box custody ancestry or length metadata is inconsistent.");
        }
    }

    private static void Validate(ManagedDocumentContentAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.CaseId == Guid.Empty
            || address.OccurrenceId == Guid.Empty
            || address.DocumentId == Guid.Empty
            || address.VersionId == Guid.Empty
            || address.OccurrenceOrdinal <= 0
            || address.Version <= 0
            || string.IsNullOrWhiteSpace(address.CaseReference)
            || string.IsNullOrWhiteSpace(address.FileName)
            || string.IsNullOrWhiteSpace(address.MediaType))
        {
            throw new ArgumentException("A complete managed document address is required.", nameof(address));
        }
        _ = CustodyNames.SafeName(address.CaseReference);
        _ = CustodyNames.SafeName(address.FileName);
    }

    private static void Verify(ReadOnlySpan<byte> content, string expectedSha256, long expectedLength)
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

    private static string NormalizeSha256(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != SHA256.HashSizeInBytes * 2 || !value.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException("A SHA-256 hash is required.", nameof(value));
        }
        return value.ToLowerInvariant();
    }

    private sealed record CreatedFile(
        string FileId,
        Guid CaseId,
        string CaseReference,
        string Sha256,
        long Length);
}
