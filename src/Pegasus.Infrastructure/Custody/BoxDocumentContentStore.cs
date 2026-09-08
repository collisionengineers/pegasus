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
    /// <summary>
    /// How many of a batch's downloads are in flight at once.
    ///
    /// Every other Box primitive here serialises, and this is the one place
    /// that fans out, so the number is deliberately small: a case may hold far
    /// more photographs than the handful a typical one does, Box rate limits
    /// per application, and nothing on this path retries a 429. Four overlaps
    /// essentially all of a normal export's download time while keeping the
    /// burst close to what the sequential version already asked of Box.
    /// </summary>
    private const int MaximumConcurrentReads = 4;

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
        var caseFolder = address.CaseRootRemoteId!;
        var fileName = FlatFileName(address);
        if (address.BoxFileId is { Length: > 0 }
            || address.BoxVersionId is { Length: > 0 })
        {
            RequirePersistedBoxIdentity(address);
            await using var persisted = await OpenOwnedExactVersionAsync(
                address.BoxFileId!, address.BoxVersionId!, caseFolder, content.Length, cancellationToken);
            Verify(
                await ReadExactlyAsync(persisted, content.Length, cancellationToken),
                normalizedHash,
                content.Length);
            return new(
                DocumentContentWriteDisposition.Replay,
                address.BoxFileId!,
                address.BoxVersionId);
        }
        var existing = await client.FindChildAsync(caseFolder, fileName, "file", cancellationToken);
        if (existing is not null)
        {
            // The write path keeps the metadata GET. It decides replay against
            // conflict before any content is committed, it costs one Box call
            // per document at intake rather than one per image on the export
            // that PLAT-041 is about, and the fields it compares are the ones
            // DOCS-010 proved must come from the file object itself.
            await VerifyFileMetadataAsync(
                existing, caseFolder, address.MediaType, content.Length, cancellationToken);
            await using var retained = await client.OpenVersionReadAsync(
                existing.Id,
                existing.VersionId ?? throw new InvalidDataException(
                    "Box omitted the existing file version identity."),
                content.Length,
                cancellationToken);
            Verify(await ReadExactlyAsync(retained, content.Length, cancellationToken), normalizedHash, content.Length);
            return new(
                DocumentContentWriteDisposition.Replay,
                existing.Id,
                existing.VersionId
                    ?? throw new InvalidDataException("Box omitted the existing file version identity."));
        }

        var created = await client.UploadAsync(
            caseFolder,
            fileName,
            content,
            address.MediaType,
            cancellationToken);
        var createdVersionId = created.VersionId
            ?? throw new InvalidDataException("Box omitted the created file version identity.");
        createdFiles[address.VersionId] = new(
            created.Id,
            address.CaseId,
            address.CaseReference,
            normalizedHash,
            content.Length,
            createdVersionId,
            caseFolder);
        return new(
            DocumentContentWriteDisposition.Created,
            created.Id,
            createdVersionId);
    }

    public async Task<Stream> OpenReadVersionAsync(
        ManagedDocumentContentAddress address,
        string expectedSha256,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        Validate(address);
        RequirePersistedBoxIdentity(address);
        var normalizedHash = NormalizeSha256(expectedSha256);
        await using var exact = await OpenOwnedExactVersionAsync(
            address.BoxFileId!,
            address.BoxVersionId!,
            address.CaseRootRemoteId!,
            expectedLength,
            cancellationToken);
        var content = await ReadExactlyAsync(exact, expectedLength, cancellationToken);
        Verify(content, normalizedHash, expectedLength);
        return new MemoryStream(content, writable: false);
    }

    /// <summary>
    /// PLAT-041: every eligible photograph of one case, read with the case
    /// folder resolved once for the whole set instead of once per file. What
    /// remains per image is the download itself, and those run together rather
    /// than one after another.
    ///
    /// Every version is materialised in full before any is returned, which is
    /// what this caller wants — the EVA archive holds the bytes — and what a
    /// streaming caller must not use.
    /// </summary>
    public async Task<IReadOnlyList<ReadOnlyMemory<byte>>> ReadVersionsAsync(
        IReadOnlyList<ManagedDocumentContentRead> reads,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reads);
        if (reads.Count == 0)
        {
            return [];
        }
        // Every argument is checked before any I/O starts, so a malformed hash
        // fails the same way whatever the downloads happen to be doing.
        var first = reads[0].Address;
        var hashes = new string[reads.Count];
        for (var index = 0; index < reads.Count; index++)
        {
            var address = reads[index].Address;
            Validate(address);
            if (address.CaseId != first.CaseId
                || !string.Equals(address.CaseReference, first.CaseReference, StringComparison.Ordinal)
                || !string.Equals(
                    address.CaseRootRemoteId,
                    first.CaseRootRemoteId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A managed content batch reads one Case only.", nameof(reads));
            }
            hashes[index] = NormalizeSha256(reads[index].ExpectedSha256);
        }

        var contents = new ReadOnlyMemory<byte>[reads.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, reads.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaximumConcurrentReads,
                CancellationToken = cancellationToken
            },
            async (index, token) =>
            {
                var read = reads[index];
                RequirePersistedBoxIdentity(read.Address);
                await using var exact = await OpenOwnedExactVersionAsync(
                    read.Address.BoxFileId!,
                    read.Address.BoxVersionId!,
                    read.Address.CaseRootRemoteId!,
                    read.ExpectedLength,
                    token);
                var content = await ReadExactlyAsync(exact, read.ExpectedLength, token);
                Verify(content, hashes[index], read.ExpectedLength);
                contents[index] = content;
            });
        return contents;
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
        await using var exact = await OpenExactVersionAsync(
            created.FileId, created.BoxVersionId, created.Length, cancellationToken);
        var bytes = await ReadExactlyAsync(exact, created.Length, cancellationToken);
        Verify(bytes, created.Sha256, created.Length);
        var current = await client.GetFileAsync(created.FileId, cancellationToken);
        if (!string.Equals(current.ParentId, created.CaseRootRemoteId, StringComparison.Ordinal)
            || !string.Equals(current.VersionId, created.BoxVersionId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The managed Box file advanced after creation and cannot be rolled back safely.");
        }
        await client.DeleteFileAsync(created.FileId, cancellationToken);
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

    /// <summary>
    /// PLAT-041: only the write path still asks. A read used to spend this GET
    /// and the two ancestry calls under it on every image — three of its nine
    /// round trips — to re-derive length and parent before downloading the
    /// content and verifying its SHA-256 anyway. As the remarks on
    /// <see cref="IsExpectedRevision"/> already conceded, that hash is the real
    /// guarantee: it refuses every wrong file this check refused, and it does it
    /// against the bytes rather than against Box's description of them.
    /// </summary>
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
            || string.IsNullOrWhiteSpace(address.CaseRootRemoteId)
            || string.IsNullOrWhiteSpace(address.FileName)
            || string.IsNullOrWhiteSpace(address.MediaType))
        {
            throw new ArgumentException("A complete managed document address is required.", nameof(address));
        }
        _ = CustodyNames.SafeName(address.CaseReference);
        _ = CustodyNames.SafeName(address.FileName);
    }

    private static void RequirePersistedBoxIdentity(ManagedDocumentContentAddress address)
    {
        if (string.IsNullOrWhiteSpace(address.BoxFileId)
            || string.IsNullOrWhiteSpace(address.BoxVersionId))
        {
            throw new InvalidDataException(
                "Managed Box reads require the persisted exact file and version identities.");
        }
    }

    private static async Task<byte[]> ReadExactlyAsync(
        Stream content, long expectedLength, CancellationToken cancellationToken)
    {
        if (expectedLength > int.MaxValue) throw new InvalidDataException("Document is too large.");
        var bytes = new byte[(int)expectedLength];
        try
        {
            await content.ReadExactlyAsync(bytes, cancellationToken);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Document custody length verification failed.", exception);
        }
        if (await content.ReadAsync(new byte[1], cancellationToken) != 0)
            throw new InvalidDataException("Document custody length verification failed.");
        return bytes;
    }

    private async Task<Stream> OpenExactVersionAsync(
        string fileId,
        string versionId,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.OpenVersionReadAsync(
                fileId, versionId, expectedLength, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException("The exact managed Box version is unavailable.", exception);
        }
    }

    private async Task<BoxContentClient.BoxItem> GetFileForManagedReadAsync(
        string fileId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetFileAsync(fileId, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException("The managed Box file is unavailable.", exception);
        }
    }

    private async Task<Stream> OpenOwnedExactVersionAsync(
        string fileId,
        string versionId,
        string caseRootRemoteId,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.OpenOwnedVersionReadAsync(
                fileId, versionId, caseRootRemoteId, expectedLength, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException("The exact managed Box version is unavailable.", exception);
        }
    }

    /// <summary>
    /// PLAT-041 review: refuse a length mismatch before any bytes move. Dropping
    /// the per-read metadata GET also dropped the only pre-download size guard,
    /// so an unbounded body could be buffered — four at once under the fan-out —
    /// before <see cref="Verify"/> rejected it.
    ///
    /// Deliberately tolerant, unlike the metadata check it replaces: a size Box
    /// declines to send cannot refuse a file, the same reasoning
    /// <c>DownloadFencedAsync</c> applies to an absent parent. That strictness
    /// was the reason the old check could not simply be re-pointed at the
    /// listing. <see cref="Verify"/> stays the closing check on the content.
    /// </summary>
    private static void RefuseUnexpectedLength(
        BoxContentClient.BoxItem file,
        long expectedLength)
    {
        if (file.Size is { } size && size != expectedLength)
        {
            throw new InvalidDataException("Document custody length verification failed.");
        }
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
        if (value.Length != SHA256.HashSizeInBytes * 2 || !IsAsciiHex(value))
        {
            throw new ArgumentException("A SHA-256 hash is required.", nameof(value));
        }
        return value.ToLowerInvariant();
    }

    private static bool IsAsciiHex(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiHexDigit(character))
            {
                return false;
            }
        }
        return true;
    }

    private sealed record CreatedFile(
        string FileId,
        Guid CaseId,
        string CaseReference,
        string Sha256,
        long Length,
        string BoxVersionId,
        string CaseRootRemoteId);
}
