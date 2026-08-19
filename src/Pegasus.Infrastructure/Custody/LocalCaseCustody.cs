using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Custody;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Custody;

internal sealed class LocalCaseCustody(
    string rootPath,
    IIntakeArtifactStore intakeArtifactStore) : ICaseCustody, IImageIntakeCustody
{
    private const string RootMetadataFileName = ".pegasus-case.json";
    private readonly string rootPath = Path.GetFullPath(rootPath);

    public async Task<ImageIntakeCustodyRoot> CreateOrGetRootAsync(
        Guid imageIntakeId,
        string imageIntakeReference,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(imageIntakeId, imageIntakeReference, operationKey);
        var relativeId = $"image-intakes/{imageIntakeId:N}";
        var directory = Resolve(relativeId);
        Directory.CreateDirectory(directory);
        await CreateOrValidateJsonAsync(
            Path.Combine(directory, ".pegasus-image-intake.json"),
            new ImageRootMetadata(imageIntakeId, imageIntakeReference, operationKey),
            existing => existing.ImageIntakeId == imageIntakeId
                && string.Equals(existing.Reference, imageIntakeReference, StringComparison.Ordinal)
                && string.Equals(existing.OperationKey, operationKey, StringComparison.Ordinal),
            cancellationToken);
        return new(imageIntakeId, relativeId, imageIntakeReference);
    }

    public Task<CaseCustodyRoot> CreateCaseRootAsync(
        Guid caseId,
        string caseReference,
        string operationKey,
        CancellationToken cancellationToken) => CreateCaseRootAsync(
            caseId,
            caseReference,
            CustodyCreationOwner.Create(),
            operationKey,
            cancellationToken);

    public async Task<CaseCustodyRoot> CreateCaseRootAsync(
        Guid caseId,
        string caseReference,
        string creationOwnerToken,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(caseId, caseReference, operationKey);

        var relativeId = GetCaseRelativeId(caseId);
        var directory = Resolve(relativeId);
        Directory.CreateDirectory(directory);

        var metadataPath = Path.Combine(directory, RootMetadataFileName);
        var metadata = new CaseRootMetadata(caseId, caseReference, operationKey);
        await CreateOrValidateJsonAsync(
            metadataPath,
            metadata,
            existing => existing.CaseId == caseId
                && string.Equals(existing.Reference, caseReference, StringComparison.Ordinal)
                && string.Equals(existing.OperationKey, operationKey, StringComparison.Ordinal),
            cancellationToken);

        return new(caseId, relativeId, caseReference);
    }

    public async Task<CaseCustodyRoot> GetExistingCaseRootAsync(
        Guid caseId,
        string caseReference,
        CancellationToken cancellationToken)
    {
        ValidateCaseIdentity(caseId, caseReference);
        var root = new CaseCustodyRoot(
            caseId,
            GetCaseRelativeId(caseId),
            caseReference);
        await ValidateRootAsync(root, cancellationToken);
        return root;
    }

    public async Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(source);
        ValidateOperationKey(operationKey);
        await ValidateRootAsync(root, cancellationToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.MediaType);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceObjectKey);

        var expectedHash = NormalizeSha256(source.SourceHash);
        var content = await intakeArtifactStore.ReadAsync(source.SourceObjectKey, cancellationToken)
            ?? throw new FileNotFoundException("The retained intake source is unavailable.");
        var actualHash = Convert.ToHexString(SHA256.HashData(content.Span)).ToLowerInvariant();
        if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The retained intake source failed its custody integrity check.");
        }
        var relativeId = $"{root.RemoteId}/documents/{source.IntakeReceiptId:N}/{expectedHash}";
        var directory = Resolve(relativeId);
        Directory.CreateDirectory(directory);
        var contentPath = Path.Combine(directory, "content");
        await CreateOrVerifyContentAsync(contentPath, content, expectedHash, cancellationToken);

        var metadata = new DocumentMetadata(
            source.IntakeReceiptId,
            source.SourceFileName,
            source.MediaType,
            expectedHash,
            operationKey);
        await CreateOrValidateJsonAsync(
            Path.Combine(directory, "metadata.json"),
            metadata,
            existing => existing == metadata,
            cancellationToken);

        return new(root.CaseId, relativeId, expectedHash, expectedHash);
    }

    public async Task<string> CreateAuditReferenceFolderAsync(
        CaseCustodyRoot root,
        string auditReference,
        string creationOwnerToken,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(root);
        await ValidateRootAsync(root, cancellationToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(auditReference);
        ValidateOperationKey(operationKey);

        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(auditReference)))
            .ToLowerInvariant();
        var relativeId = $"{root.RemoteId}/audit/{identity}";
        var directory = Resolve(relativeId);
        Directory.CreateDirectory(directory);
        var metadata = new AuditFolderMetadata(auditReference, operationKey);
        await CreateOrValidateJsonAsync(
            Path.Combine(directory, "metadata.json"),
            metadata,
            existing => existing == metadata,
            cancellationToken);
        return relativeId;
    }

    public Task<string> CreateAuditReferenceFolderAsync(
        CaseCustodyRoot root,
        string auditReference,
        string operationKey,
        CancellationToken cancellationToken) => CreateAuditReferenceFolderAsync(
            root,
            auditReference,
            CustodyCreationOwner.Create(),
            operationKey,
            cancellationToken);

    private async Task ValidateRootAsync(CaseCustodyRoot root, CancellationToken cancellationToken)
    {
        if (root.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(root));
        }

        var expectedRemoteId = GetCaseRelativeId(root.CaseId);
        if (!string.Equals(root.RemoteId, expectedRemoteId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The custody root is outside the configured case scope.");
        }

        var metadataPath = Path.Combine(Resolve(expectedRemoteId), RootMetadataFileName);
        if (!File.Exists(metadataPath))
        {
            throw new InvalidOperationException("The case custody root has not been created.");
        }

        await using var stream = new FileStream(
            metadataPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var metadata = await JsonSerializer.DeserializeAsync<CaseRootMetadata>(
            stream,
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("The case custody root metadata is incomplete.");
        if (string.IsNullOrWhiteSpace(metadata.OperationKey))
        {
            throw new InvalidDataException("The case custody root metadata is incomplete.");
        }
        if (metadata.CaseId != root.CaseId
            || !string.Equals(metadata.Reference, root.Reference, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The custody root does not belong to the requested case.");
        }
    }

    private string Resolve(string relativeId)
    {
        var normalized = relativeId.Replace('/', Path.DirectorySeparatorChar);
        var resolved = Path.GetFullPath(Path.Combine(rootPath, normalized));
        var rootPrefix = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("The custody identifier is outside the configured root.");
        }

        return resolved;
    }

    private static async Task CreateOrVerifyContentAsync(
        string path,
        ReadOnlyMemory<byte> content,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            var temporaryPath = CreateSiblingTemporaryPath(path);
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
                    return;
                }
                catch (IOException) when (File.Exists(path))
                {
                    // An idempotent or concurrent call published the immutable content first.
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

        await using var existing = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(existing, cancellationToken))
            .ToLowerInvariant();
        if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Existing custody content does not match the expected hash.");
        }
    }

    private static async Task CreateOrValidateJsonAsync<T>(
        string path,
        T value,
        Func<T, bool> isExpected,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            var temporaryPath = CreateSiblingTemporaryPath(path);
            try
            {
                await using (var stream = new FileStream(
                                 temporaryPath,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 4096,
                                 FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await JsonSerializer.SerializeAsync(
                        stream,
                        value,
                        cancellationToken: cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                    RandomAccess.FlushToDisk(stream.SafeFileHandle);
                }

                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Move(temporaryPath, path, overwrite: false);
                    return;
                }
                catch (IOException) when (File.Exists(path))
                {
                    // An idempotent or concurrent call published the immutable metadata first.
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

        await using var existingStream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var existing = await JsonSerializer.DeserializeAsync<T>(
                existingStream,
                cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Custody metadata is incomplete.");
        if (!isExpected(existing))
        {
            throw new InvalidOperationException("The custody operation conflicts with existing immutable metadata.");
        }
    }

    private static string CreateSiblingTemporaryPath(string path) =>
        Path.Combine(
            Path.GetDirectoryName(path)!,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

    private static string GetCaseRelativeId(Guid caseId) => $"cases/{caseId:N}";

    private static void ValidateIdentity(Guid caseId, string caseReference, string operationKey)
    {
        ValidateCaseIdentity(caseId, caseReference);
        ValidateOperationKey(operationKey);
    }

    private static void ValidateCaseIdentity(Guid caseId, string caseReference)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(caseReference);
    }

    private static void ValidateOperationKey(string operationKey) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);

    private static string NormalizeSha256(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != SHA256.HashSizeInBytes * 2 || !value.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("A SHA-256 hash is required.", nameof(value));
        }

        return value.ToLowerInvariant();
    }

    private sealed record CaseRootMetadata(Guid CaseId, string Reference, string OperationKey);

    private sealed record ImageRootMetadata(Guid ImageIntakeId, string Reference, string OperationKey);

    private sealed record DocumentMetadata(
        Guid IntakeReceiptId,
        string FileName,
        string MediaType,
        string Sha256,
        string OperationKey);

    private sealed record AuditFolderMetadata(string AuditReference, string OperationKey);
}

internal sealed class UnavailableCaseCustody : ICaseCustody, IImageIntakeCustody
{
    public Task<ImageIntakeCustodyRoot> CreateOrGetRootAsync(
        Guid imageIntakeId,
        string imageIntakeReference,
        string operationKey,
        CancellationToken cancellationToken) =>
        Unavailable<ImageIntakeCustodyRoot>();

    public Task<CaseCustodyRoot> CreateCaseRootAsync(
        Guid caseId,
        string caseReference,
        string creationOwnerToken,
        string operationKey,
        CancellationToken cancellationToken) =>
        Unavailable<CaseCustodyRoot>();

    public Task<CaseCustodyRoot> GetExistingCaseRootAsync(
        Guid caseId,
        string caseReference,
        CancellationToken cancellationToken) =>
        Unavailable<CaseCustodyRoot>();

    public Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        string operationKey,
        CancellationToken cancellationToken) =>
        Unavailable<CustodyDocumentVersion>();

    public Task<string> CreateAuditReferenceFolderAsync(
        CaseCustodyRoot root,
        string auditReference,
        string creationOwnerToken,
        string operationKey,
        CancellationToken cancellationToken) =>
        Unavailable<string>();

    private static Task<T> Unavailable<T>() =>
        Task.FromException<T>(new CaseCustodyUnavailableException());
}
