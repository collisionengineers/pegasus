using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Pegasus.Core.Documents;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// Production managed-document content storage beneath the one bound Case/PO
/// root. Names contain persisted business role, occurrence ordinal, business
/// revision and original filename; remote/internal identifiers stay in SQL and
/// provenance rather than Box names.
/// </summary>
internal sealed class BoxDocumentContentStore(BoxContentClient client) : IDocumentContentStore
{
    private const string OccurrenceBindingFileName = "pegasus-document-binding.json";
    private const string VersionBindingFileName = "pegasus-version-binding.json";
    private const string BindingMediaType = "application/json";
    private readonly ConcurrentDictionary<Guid, CreatedFile> createdFiles = [];

    public async Task<DocumentContentWriteResult> StoreVersionAsync(
        ManagedDocumentContentAddress address,
        ReadOnlyMemory<byte> content,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        Validate(address);
        var normalizedHash = NormalizeSha256(expectedSha256);
        Verify(content.Span, normalizedHash, content.Length);
        var versionFolder = await ResolveVersionFolderAsync(
                address, normalizedHash, content.Length, create: true, cancellationToken)
            ?? throw new InvalidOperationException("The managed document folder could not be resolved.");
        var fileName = CustodyNames.SafeName(address.FileName);
        var existing = await client.FindChildAsync(versionFolder, fileName, "file", cancellationToken);
        if (existing is not null)
        {
            await VerifyFileMetadataAsync(
                existing, versionFolder, address.MediaType, content.Length, cancellationToken);
            var retained = await client.DownloadAsync(existing.Id, cancellationToken);
            Verify(retained, normalizedHash, content.Length);
            return new(DocumentContentWriteDisposition.Replay, existing.Id);
        }

        var created = await client.UploadAsync(
            versionFolder,
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
        var versionFolder = await ResolveVersionFolderAsync(
            address, normalizedHash, expectedLength, create: false, cancellationToken);
        if (versionFolder is null)
        {
            throw new FileNotFoundException("The document content is unavailable.");
        }
        var file = await client.FindChildAsync(
            versionFolder,
            CustodyNames.SafeName(address.FileName),
            "file",
            cancellationToken)
            ?? throw new FileNotFoundException("The document content is unavailable.");
        await VerifyFileMetadataAsync(
            file, versionFolder, address.MediaType, expectedLength, cancellationToken);
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

    private async Task<string?> ResolveVersionFolderAsync(
        ManagedDocumentContentAddress address,
        string expectedSha256,
        long expectedLength,
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
        var evidence = await ResolvePlainFolderAsync(root.Id, "Evidence", create, cancellationToken);
        if (evidence is null)
        {
            return null;
        }
        var role = await ResolvePlainFolderAsync(
            evidence, RoleName(address.SemanticRole), create, cancellationToken);
        if (role is null)
        {
            return null;
        }
        var occurrence = await ResolveBoundFolderAsync(
            role,
            $"{address.OccurrenceOrdinal:000} {CustodyNames.SafeName(address.FileName)}",
            OccurrenceBindingFileName,
            OccurrenceBinding(address),
            create,
            cancellationToken);
        if (occurrence is null)
        {
            return null;
        }
        return await ResolveBoundFolderAsync(
            occurrence,
            $"Revision {address.Version:000}",
            VersionBindingFileName,
            VersionBinding(address, expectedSha256, expectedLength),
            create,
            cancellationToken);
    }

    private async Task<string?> ResolvePlainFolderAsync(
        string parentId,
        string name,
        bool create,
        CancellationToken cancellationToken)
    {
        var child = await client.FindChildAsync(parentId, name, "folder", cancellationToken);
        if (child is not null)
        {
            return child.Id;
        }
        return create
            ? (await client.CreateFolderAsync(parentId, name, cancellationToken)).Id
            : null;
    }

    private async Task<string?> ResolveBoundFolderAsync(
        string parentId,
        string name,
        string bindingName,
        byte[] binding,
        bool create,
        CancellationToken cancellationToken)
    {
        var folder = await client.FindChildAsync(parentId, name, "folder", cancellationToken);
        if (folder is null)
        {
            if (!create)
            {
                return null;
            }
            folder = await client.CreateFolderAsync(parentId, name, cancellationToken);
            await client.UploadAsync(
                folder.Id, bindingName, binding, BindingMediaType, cancellationToken);
            return folder.Id;
        }

        var bindingFile = await client.FindChildAsync(
            folder.Id, bindingName, "file", cancellationToken)
            ?? throw new InvalidDataException(
                "A managed Box custody folder is missing its immutable identity binding.");
        await VerifyBindingAsync(folder.Id, bindingFile, binding, cancellationToken);
        return folder.Id;
    }

    private async Task VerifyBindingAsync(
        string parentId,
        BoxContentClient.BoxItem file,
        ReadOnlyMemory<byte> expected,
        CancellationToken cancellationToken)
    {
        await VerifyFileMetadataAsync(
            file, parentId, BindingMediaType, expected.Length, cancellationToken);
        var actual = await client.DownloadAsync(file.Id, cancellationToken);
        if (!actual.AsSpan().SequenceEqual(expected.Span))
        {
            throw new InvalidDataException("A managed Box custody binding has different immutable content.");
        }
    }

    private async Task VerifyFileMetadataAsync(
        BoxContentClient.BoxItem file,
        string expectedParentId,
        string expectedMediaType,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        var metadata = await client.GetFileAsync(file.Id, cancellationToken);
        if (!string.Equals(metadata.ParentId, expectedParentId, StringComparison.Ordinal)
            || metadata.Size != expectedLength
            || !string.Equals(metadata.MediaType, expectedMediaType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Managed Box custody type, ancestry, or length metadata is inconsistent.");
        }
    }

    internal static byte[] OccurrenceBinding(ManagedDocumentContentAddress address) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            address.CaseId,
            address.CaseReference,
            address.OccurrenceId,
            address.OccurrenceOrdinal,
            address.DocumentId,
            semanticRole = address.SemanticRole.ToString(),
            address.FileName
        });

    internal static byte[] VersionBinding(
        ManagedDocumentContentAddress address,
        string sha256,
        long contentLength) => JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            address.CaseId,
            address.OccurrenceId,
            address.DocumentId,
            address.VersionId,
            address.Version,
            address.FileName,
            address.MediaType,
            contentLength,
            sha256
        });

    private static string RoleName(DocumentSemanticRole role) => role switch
    {
        DocumentSemanticRole.Image => "Images",
        DocumentSemanticRole.Correspondence => "Correspondence",
        DocumentSemanticRole.EngineerReport or DocumentSemanticRole.AuditReport => "Reports",
        _ => "Other evidence"
    };

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
