using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using Pegasus.Core.Custody;
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
    /// How many Box downloads this store has in flight at once, from
    /// separate callers.
    ///
    /// Reads are the fan-out this gate bounds, so the number is deliberately
    /// small: a case may hold far more photographs than the handful a typical
    /// one does, and Box rate limits per application.
    ///
    /// A write here is one upload for each call, and this gate does not bound
    /// it. The Worker's custody processor files up to three files of one work
    /// item at the same time (<c>EfQueuedCustodyProcessor</c>), but those
    /// uploads do not come through this store: <c>BoxCaseCustody</c> sends
    /// them to <see cref="BoxContentClient"/> directly.
    ///
    /// <see cref="ReadGate"/> is the bound itself, shared by every managed
    /// read, and it is process-wide because Box's limit is.
    /// </summary>
    private const int MaximumConcurrentReads = 4;

    /// <summary>
    /// How many times one Box call is attempted before its failure is the answer.
    /// </summary>
    private const int MaximumAttempts = 3;

    /// <summary>
    /// The longest a call waits before its next attempt, whatever Box asks.
    /// </summary>
    internal static readonly TimeSpan MaximumRetryWait = TimeSpan.FromSeconds(10);

    private static readonly SemaphoreSlim ReadGate =
        new(MaximumConcurrentReads, MaximumConcurrentReads);

    private readonly ConcurrentDictionary<Guid, CreatedFile> createdFiles = [];

    /// <summary>
    /// The folder proofs of the document-write batch the current call flow is
    /// inside, or null outside one. Per batch, never a time window. A proof is
    /// held as the one read that makes it, so writes running at the same time
    /// share that read rather than each making their own.
    /// </summary>
    private readonly AsyncLocal<ConcurrentDictionary<string, Lazy<Task<BoxContentClient.ProvedFolder>>>?> batchProofs = new();

    /// <summary>
    /// Opens a batch of document writes: the first write into a folder proves
    /// it and the rest of the batch reuses that proof. A fence failure on an
    /// upload drops the folder's proof, and disposing the scope drops them all.
    /// A batch already open is joined, not replaced.
    /// </summary>
    internal IDisposable BeginFolderProofBatch()
    {
        if (batchProofs.Value is not null)
        {
            return NoCustodyBatch.Instance;
        }
        batchProofs.Value = new();
        return new BatchScope(this);
    }

    private sealed class BatchScope(BoxDocumentContentStore store) : IDisposable
    {
        public void Dispose() => store.batchProofs.Value = null;
    }

    /// <summary>
    /// Proves a folder fit to be written into: inside a batch, once for the
    /// batch, and outside one, every time. A proof that fails is not kept.
    /// </summary>
    internal async Task<BoxContentClient.ProvedFolder> ProveFolderAsync(
        string folderId,
        CancellationToken cancellationToken)
    {
        var proofs = batchProofs.Value;
        if (proofs is null)
        {
            return await client.ProveFolderAsync(folderId, cancellationToken);
        }
        var proof = proofs.GetOrAdd(
            folderId,
            id => new Lazy<Task<BoxContentClient.ProvedFolder>>(
                () => client.ProveFolderAsync(id, cancellationToken)));
        try
        {
            return await proof.Value;
        }
        catch
        {
            proofs.TryRemove(KeyValuePair.Create(folderId, proof));
            throw;
        }
    }

    /// <summary>
    /// Drops the batch's proof of a folder after a fence failure on a write
    /// into it, so the next write proves the folder again.
    /// </summary>
    internal void DropProof(string folderId) =>
        batchProofs.Value?.TryRemove(folderId, out _);

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
        return await StoreVerifiedVersionAsync(
            address,
            content.Length,
            normalizedHash,
            folder => client.UploadAsync(
                folder,
                FlatFileName(address),
                content,
                address.MediaType,
                cancellationToken),
            cancellationToken);
    }

    public async Task<DocumentContentWriteResult> StoreVersionAsync(
        ManagedDocumentContentAddress address,
        Stream content,
        long contentLength,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        Validate(address);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegative(contentLength);
        if (!content.CanRead)
        {
            throw new ArgumentException("The managed document stream must be readable.", nameof(content));
        }

        var normalizedHash = NormalizeSha256(expectedSha256);
        await using var staged = await StageVerifiedStreamAsync(
            content,
            contentLength,
            normalizedHash,
            cancellationToken);
        return await StoreVerifiedStreamAsync(
            address, staged, contentLength, normalizedHash, cancellationToken);
    }

    /// <summary>
    /// Stores a stream whose <paramref name="contentLength"/> bytes the caller
    /// has already verified against <paramref name="normalizedSha256"/>. The
    /// stream must be readable and seekable; it is neither copied nor hashed
    /// again.
    /// </summary>
    internal async Task<DocumentContentWriteResult> StoreVerifiedStreamAsync(
        ManagedDocumentContentAddress address,
        Stream verified,
        long contentLength,
        string normalizedSha256,
        CancellationToken cancellationToken)
    {
        Validate(address);
        return await StoreVerifiedVersionAsync(
            address,
            contentLength,
            normalizedSha256,
            folder => client.UploadVerifiedAsync(
                folder,
                FlatFileName(address),
                verified,
                contentLength,
                address.MediaType,
                normalizedSha256,
                cancellationToken),
            cancellationToken);
    }

    private async Task<DocumentContentWriteResult> StoreVerifiedVersionAsync(
        ManagedDocumentContentAddress address,
        long contentLength,
        string normalizedHash,
        Func<BoxContentClient.ProvedFolder, Task<BoxContentClient.BoxUpload>> createAsync,
        CancellationToken cancellationToken)
    {
        var caseFolder = address.CaseRootRemoteId!;
        if (address.BoxFileId is { Length: > 0 }
            || address.BoxVersionId is { Length: > 0 })
        {
            RequirePersistedBoxIdentity(address);
            // A replay proves the folder's ancestry with a read of its own,
            // never from the read path's memory.
            await using var persisted = await OpenOwnedExactVersionAsync(
                address.BoxFileId!,
                address.BoxVersionId!,
                caseFolder,
                contentLength,
                proveAncestry: true,
                cancellationToken);
            await VerifyStreamAsync(persisted, normalizedHash, contentLength, cancellationToken);
            return new(
                DocumentContentWriteDisposition.Replay,
                address.BoxFileId!,
                address.BoxVersionId);
        }
        // The folder is proved before anything is filed into it, and never
        // from the read path's memory. The store cannot tell a Case folder
        // from the Audit's a. folder, so the proof is the folder's place under
        // the approved root and its trash state: one read for a Case folder,
        // one more for the a. folder inside it. The name is not looked
        // for first: a file that already holds it is Box's 409, and the client
        // says so, having compared its content with this content.
        // Inside a batch the first write proves the folder and the rest share
        // that proof (FRD-05); outside one every write proves it.
        var folder = await ProveFolderAsync(caseFolder, cancellationToken);
        BoxContentClient.BoxUpload upload;
        try
        {
            upload = await createAsync(folder);
        }
        catch (Exception exception) when (BoxContentClient.IsFenceFailure(exception))
        {
            DropProof(caseFolder);
            throw;
        }
        var file = upload.File;
        var versionId = file.VersionId
            ?? throw new InvalidDataException(upload.Created
                ? "Box omitted the created file version identity."
                : "Box omitted the existing file version identity.");
        if (!upload.Created)
        {
            return new(DocumentContentWriteDisposition.Replay, file.Id, versionId);
        }
        createdFiles[address.VersionId] = new(
            file.Id,
            address.CaseId,
            address.CaseReference,
            normalizedHash,
            contentLength,
            versionId,
            caseFolder);
        return new(DocumentContentWriteDisposition.Created, file.Id, versionId);
    }

    /// <summary>
    /// One managed version's content, gated and retried.
    /// </summary>
    /// <remarks>
    /// This path is what a gallery tile, a preview and a report
    /// input all reach, so a Box 429 arrived here as a failed page element
    /// rather than as a wait. The read now enters <see cref="ReadGate"/> and a
    /// throttled or unavailable response is retried within
    /// <see cref="MaximumAttempts"/> attempts before it becomes the
    /// caller's failure.
    /// </remarks>
    public async Task<Stream> OpenReadVersionAsync(
        ManagedDocumentContentAddress address,
        string expectedSha256,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        Validate(address);
        RequirePersistedBoxIdentity(address);
        var normalizedHash = NormalizeSha256(expectedSha256);
        var content = await ReadGatedWithRetryAsync(
            async token =>
            {
                await using var exact = await OpenOwnedExactVersionAsync(
                    address.BoxFileId!,
                    address.BoxVersionId!,
                    address.CaseRootRemoteId!,
                    expectedLength,
                    proveAncestry: false,
                    token);
                return await ReadExactlyAsync(exact, expectedLength, token);
            },
            cancellationToken);
        Verify(content, normalizedHash, expectedLength);
        return new MemoryStream(content, writable: false);
    }

    /// <summary>
    /// Runs one Box read inside the process-wide read gate, retrying a
    /// throttled (429) or unavailable (5xx) response.
    /// </summary>
    /// <remarks>
    /// The whole read is retried, metadata call included, because either half
    /// of it can be the throttled one and the content stream is not reusable.
    /// A read that fails verification is not retried: those bytes are wrong,
    /// not late.
    ///
    /// A read gives its gate slot back while it waits, so one throttled read
    /// never holds a slot other reads could use. The wait is Box's
    /// <c>Retry-After</c> when that is longer than the backoff, and never more
    /// than <see cref="MaximumRetryWait"/>.
    /// </remarks>
    internal static Task<T> ReadGatedWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> read,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        return RetryTransientAsync(
            async token =>
            {
                using (DocumentReadTelemetry.Start("document.provider.gate"))
                {
                    await ReadGate.WaitAsync(token);
                }
                try
                {
                    using var providerRead = DocumentReadTelemetry.Start("document.provider.read");
                    return await read(token);
                }
                finally
                {
                    ReadGate.Release();
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Runs one Box call, retrying a throttled (429) or unavailable (5xx)
    /// response within <see cref="MaximumAttempts"/> attempts, after
    /// <see cref="WaitBeforeRetry"/>. Any other failure is the answer at once.
    /// Holding custody's upload and read-back use it ungated; reads use it
    /// through <see cref="ReadGatedWithRetryAsync"/>.
    /// </summary>
    internal static async Task<T> RetryTransientAsync<T>(
        Func<CancellationToken, Task<T>> call,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        for (var attempt = 1; ; attempt++)
        {
            TimeSpan wait;
            try
            {
                return await call(cancellationToken);
            }
            catch (HttpRequestException exception)
                when (attempt < MaximumAttempts && IsTransientFailure(exception))
            {
                wait = WaitBeforeRetry(exception, attempt + 1);
            }
            await Task.Delay(wait, cancellationToken);
        }
    }

    /// <summary>
    /// How long to wait before <paramref name="attempt"/>: the larger of Box's
    /// <c>Retry-After</c> and the backoff, capped at
    /// <see cref="MaximumRetryWait"/>.
    /// </summary>
    internal static TimeSpan WaitBeforeRetry(HttpRequestException exception, int attempt)
    {
        var wait = BackoffBeforeAttempt(attempt);
        if (exception is BoxThrottledException { RetryAfter: { } retryAfter } && retryAfter > wait)
        {
            wait = retryAfter;
        }
        return wait > MaximumRetryWait ? MaximumRetryWait : wait;
    }

    /// <summary>
    /// Whether Box said "later" rather than "no": the rate limit, or one of
    /// its own failures.
    /// </summary>
    private static bool IsTransientFailure(HttpRequestException exception) =>
        exception.StatusCode == HttpStatusCode.TooManyRequests
        || (int?)exception.StatusCode >= 500;

    private static TimeSpan BackoffBeforeAttempt(int attempt) =>
        TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 2));

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

    private static async Task<FileStream> StageVerifiedStreamAsync(
        Stream content,
        long expectedLength,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pegasus-box-upload-{Guid.NewGuid():N}.tmp");
        var staged = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try
        {
            await CopyAndVerifyAsync(content, staged, expectedLength, expectedSha256, cancellationToken);
            await staged.FlushAsync(cancellationToken);
            staged.Position = 0;
            return staged;
        }
        catch
        {
            await staged.DisposeAsync();
            throw;
        }
    }

    private static Task VerifyStreamAsync(
        Stream content,
        string expectedSha256,
        long expectedLength,
        CancellationToken cancellationToken) =>
        CopyAndVerifyAsync(content, destination: null, expectedLength, expectedSha256, cancellationToken);

    private static async Task CopyAndVerifyAsync(
        Stream content,
        Stream? destination,
        long expectedLength,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long copied = 0;
        while (true)
        {
            var read = await content.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }
            copied = checked(copied + read);
            if (copied > expectedLength)
            {
                throw new InvalidDataException("Document custody length verification failed.");
            }
            hash.AppendData(buffer, 0, read);
            if (destination is not null)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        var actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (copied != expectedLength
            || !string.Equals(expectedSha256, actualHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Document custody hash verification failed.");
        }
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

    private async Task<Stream> OpenOwnedExactVersionAsync(
        string fileId,
        string versionId,
        string caseRootRemoteId,
        long expectedLength,
        bool proveAncestry,
        CancellationToken cancellationToken)
    {
        try
        {
            return proveAncestry
                ? await client.OpenOwnedVersionProvedAsync(
                    fileId, versionId, caseRootRemoteId, expectedLength, cancellationToken)
                : await client.OpenOwnedVersionReadAsync(
                    fileId, versionId, caseRootRemoteId, expectedLength, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException("The exact managed Box version is unavailable.", exception);
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
