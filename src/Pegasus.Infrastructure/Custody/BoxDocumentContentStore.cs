using System.Collections.Concurrent;
using System.Security.Cryptography;
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
        var versionFolder = await ResolveVersionFolderAsync(address, create: true, cancellationToken)
            ?? throw new InvalidOperationException("The managed document folder could not be resolved.");
        var fileName = CustodyNames.SafeName(address.FileName);
        var existing = await client.FindChildAsync(versionFolder, fileName, "file", cancellationToken);
        if (existing is not null)
        {
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
        var versionFolder = await ResolveVersionFolderAsync(address, create: false, cancellationToken);
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
        var content = await client.DownloadAsync(file.Id, cancellationToken);
        Verify(content, NormalizeSha256(expectedSha256), expectedLength);
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
        var bindingFile = await client.FindChildAsync(
            root.Id,
            "pegasus-case-binding.json",
            "file",
            cancellationToken)
            ?? throw new InvalidDataException("The Case custody root is missing its immutable binding.");
        var binding = await client.DownloadAsync(bindingFile.Id, cancellationToken);
        if (!binding.AsSpan().SequenceEqual(
                BoxCaseCustody.CaseBinding(address.CaseId, address.CaseReference)))
        {
            throw new InvalidDataException("The Case custody root belongs to another Case identity.");
        }

        var current = root.Id;
        var segments = new[]
        {
            "Evidence",
            RoleName(address.SemanticRole),
            $"{address.OccurrenceOrdinal:000} {CustodyNames.SafeName(address.FileName)}",
            $"Revision {address.Version:000}"
        };
        foreach (var segment in segments)
        {
            if (create)
            {
                current = (await client.GetOrCreateFolderAsync(current, segment, cancellationToken)).Id;
                continue;
            }
            var child = await client.FindChildAsync(current, segment, "folder", cancellationToken);
            if (child is null)
            {
                return null;
            }
            current = child.Id;
        }
        return current;
    }

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
