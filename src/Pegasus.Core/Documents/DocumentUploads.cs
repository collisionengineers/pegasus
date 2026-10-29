using System.Collections.Concurrent;
using System.Security.Cryptography;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Documents;

/// <summary>
/// The figures of a document staged in parts (operator, 22 October 2026,
/// R203 and R448): one document of at most 10 MiB, sent in parts of
/// <see cref="PartBytes"/> (65,536 base64 characters) but the last, kept for
/// R100's 24 hours.
/// </summary>
public static class DocumentUploadLimits
{
    /// <summary>The largest document an Automation call adds, inline or staged.</summary>
    public const int MaximumContentBytes = 10 * 1024 * 1024;

    /// <summary>The length of every part but the last.</summary>
    public const int PartBytes = 49_152;

    /// <summary>How long a staged upload lives: R100's staging grace period.</summary>
    public static TimeSpan Lifetime => ReconcileStagedArtifacts.ExpiryGracePeriod;

    public static int PartCount(long totalBytes) =>
        (int)((totalBytes + PartBytes - 1) / PartBytes);

    public static int PartLength(long totalBytes, int partNumber) =>
        partNumber < PartCount(totalBytes)
            ? PartBytes
            : (int)(totalBytes - (long)(PartCount(totalBytes) - 1) * PartBytes);
}

/// <summary>
/// The agent-facing refusals of a staged upload. Drafted for the operator
/// (OQ-194, R448); the operation-key sentence is the one the other Automation
/// commands already use.
/// </summary>
public static class DocumentUploadRefusals
{
    public const string Unavailable = "The upload was not found or has expired.";
    public const string OperationKeyConflict = "The operation key was already used with different details.";
    public static readonly string TotalBytes =
        $"The total size must be between 1 and {DocumentUploadLimits.MaximumContentBytes} bytes.";
    public const string Sha256 = "A SHA-256 of 64 hexadecimal digits is required.";
    public const string PartSha256Mismatch = "The part does not match its SHA-256; nothing was stored.";
    public const string PartConflict = "This part number was already received with different bytes.";
    public const string LengthMismatch = "The staged parts do not add up to the declared size.";
    public const string Sha256Mismatch = "The staged document does not match its declared SHA-256.";

    public static string PartNumber(int partCount) =>
        $"The part number must be between 1 and {partCount}.";

    public static string PartLength(int partNumber, int length) =>
        $"Part {partNumber} must be {length} bytes.";

    public static string MissingParts(IEnumerable<int> missing) =>
        $"The upload is missing parts {string.Join(", ", missing)}.";
}

/// <summary>
/// One staged upload's declaration. <see cref="Owner"/> is the Automation
/// client that began it; <see cref="CreatedAtUtc"/> starts its life.
/// </summary>
public sealed record DocumentUploadManifest(
    Guid UploadId,
    string Owner,
    long TotalBytes,
    string Sha256,
    DateTimeOffset CreatedAtUtc);

public enum DocumentUploadPartWrite
{
    Created,
    AlreadyStored,
    Conflict
}

/// <summary>One staged object as the sweep lists it, aged by its own creation time.</summary>
public sealed record DocumentUploadStagedObject(string Name, DateTimeOffset CreatedAtUtc);

public sealed record DocumentUploadStagedPage(
    IReadOnlyList<DocumentUploadStagedObject> Items,
    string? NextContinuation);

/// <summary>
/// Where staged parts wait for the call that uses them. Nothing here is
/// custody: the manifest and each part are written once and never replaced,
/// and the sweep deletes them by age.
/// </summary>
public interface IDocumentUploadStaging
{
    /// <summary>Stores the manifest unless its upload already has one; false when it had.</summary>
    Task<bool> CreateManifestAsync(DocumentUploadManifest manifest, CancellationToken cancellationToken);

    Task<DocumentUploadManifest?> ReadManifestAsync(Guid uploadId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the part unless its number already holds one, comparing the
    /// stored part's SHA-256 with <paramref name="sha256"/> when it does.
    /// </summary>
    Task<DocumentUploadPartWrite> CreatePartAsync(
        Guid uploadId,
        int partNumber,
        ReadOnlyMemory<byte> content,
        string sha256,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<int>> ListPartNumbersAsync(Guid uploadId, CancellationToken cancellationToken);

    Task<ReadOnlyMemory<byte>?> ReadPartAsync(Guid uploadId, int partNumber, CancellationToken cancellationToken);

    /// <summary>One page of every staged object, in name order from the continuation.</summary>
    Task<DocumentUploadStagedPage> ListAsync(
        int maximumItems,
        string? continuation,
        CancellationToken cancellationToken);

    /// <summary>Deletes the object only if it was created at or before the cutoff.</summary>
    Task<bool> DeleteAsync(string name, DateTimeOffset createdAtOrBeforeUtc, CancellationToken cancellationToken);
}

public sealed record BeginDocumentUploadRequest(Guid UploadId, string Owner, long TotalBytes, string Sha256);

public sealed record DocumentUploadTicket(
    Guid UploadId,
    int PartBytes,
    int PartCount,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<int> ReceivedParts,
    bool IsReplay);

public sealed record AppendDocumentUploadPartRequest(
    Guid UploadId,
    string Owner,
    int PartNumber,
    ReadOnlyMemory<byte> Content,
    string Sha256);

public sealed record DocumentUploadPartReceipt(Guid UploadId, int PartNumber, string Sha256, bool IsReplay);

public sealed record OpenDocumentUploadRequest(Guid UploadId, string Owner);

public sealed record DocumentUploadContent(ReadOnlyMemory<byte> Content, string Sha256);

/// <summary>
/// A document staged in parts (R203): begin declares its size and SHA-256,
/// each part is checked against its own SHA-256 before it is stored, and the
/// call that uses the upload opens it whole, checked against the declaration.
/// None of these writes a Case record or custody; opening deletes and marks
/// nothing, so a replayed using call finds the same bytes until the upload
/// expires.
/// </summary>
public sealed class DocumentUploads(IDocumentUploadStaging staging, TimeProvider timeProvider)
{
    /// <summary>How many parts an open reads at once.</summary>
    private const int ReadsAtOnce = 4;

    public async Task<DocumentUploadTicket> ExecuteAsync(
        BeginDocumentUploadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireIdentity(request.UploadId, request.Owner);
        if (request.TotalBytes is < 1 or > DocumentUploadLimits.MaximumContentBytes)
        {
            throw new ArgumentException(DocumentUploadRefusals.TotalBytes);
        }

        var sha256 = NormalizeSha256(request.Sha256)
            ?? throw new ArgumentException(DocumentUploadRefusals.Sha256);
        var declared = new DocumentUploadManifest(
            request.UploadId,
            request.Owner,
            request.TotalBytes,
            sha256,
            timeProvider.GetUtcNow());
        if (await staging.CreateManifestAsync(declared, cancellationToken))
        {
            return Ticket(declared, [], isReplay: false);
        }

        var stored = await RequireLiveAsync(request.UploadId, request.Owner, cancellationToken);
        if (stored.TotalBytes != declared.TotalBytes
            || !string.Equals(stored.Sha256, declared.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(DocumentUploadRefusals.OperationKeyConflict);
        }

        var received = await staging.ListPartNumbersAsync(request.UploadId, cancellationToken);
        return Ticket(stored, received, isReplay: true);
    }

    public async Task<DocumentUploadPartReceipt> ExecuteAsync(
        AppendDocumentUploadPartRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireIdentity(request.UploadId, request.Owner);
        var manifest = await RequireLiveAsync(request.UploadId, request.Owner, cancellationToken);
        var partCount = DocumentUploadLimits.PartCount(manifest.TotalBytes);
        if (request.PartNumber < 1 || request.PartNumber > partCount)
        {
            throw new ArgumentException(DocumentUploadRefusals.PartNumber(partCount));
        }

        var length = DocumentUploadLimits.PartLength(manifest.TotalBytes, request.PartNumber);
        if (request.Content.Length != length)
        {
            throw new ArgumentException(DocumentUploadRefusals.PartLength(request.PartNumber, length));
        }

        var declared = NormalizeSha256(request.Sha256)
            ?? throw new ArgumentException(DocumentUploadRefusals.Sha256);
        var actual = Convert.ToHexString(SHA256.HashData(request.Content.Span));
        if (!string.Equals(actual, declared, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(DocumentUploadRefusals.PartSha256Mismatch);
        }

        var written = await staging.CreatePartAsync(
            request.UploadId,
            request.PartNumber,
            request.Content,
            actual,
            cancellationToken);
        if (written == DocumentUploadPartWrite.Conflict)
        {
            throw new InvalidOperationException(DocumentUploadRefusals.PartConflict);
        }

        return new DocumentUploadPartReceipt(
            request.UploadId,
            request.PartNumber,
            actual,
            written == DocumentUploadPartWrite.AlreadyStored);
    }

    public async Task<DocumentUploadContent> ExecuteAsync(
        OpenDocumentUploadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireIdentity(request.UploadId, request.Owner);
        var manifest = await RequireLiveAsync(request.UploadId, request.Owner, cancellationToken);
        var partCount = DocumentUploadLimits.PartCount(manifest.TotalBytes);
        var content = new byte[manifest.TotalBytes];
        var missing = new ConcurrentBag<int>();
        var misfit = 0;
        await Parallel.ForEachAsync(
            Enumerable.Range(1, partCount),
            new ParallelOptions { MaxDegreeOfParallelism = ReadsAtOnce, CancellationToken = cancellationToken },
            async (partNumber, token) =>
            {
                var part = await staging.ReadPartAsync(manifest.UploadId, partNumber, token);
                if (part is not { } bytes)
                {
                    missing.Add(partNumber);
                    return;
                }

                if (bytes.Length != DocumentUploadLimits.PartLength(manifest.TotalBytes, partNumber))
                {
                    Volatile.Write(ref misfit, 1);
                    return;
                }

                bytes.CopyTo(content.AsMemory((partNumber - 1) * DocumentUploadLimits.PartBytes));
            });

        if (!missing.IsEmpty)
        {
            throw new InvalidOperationException(DocumentUploadRefusals.MissingParts(missing.Order()));
        }

        if (misfit != 0)
        {
            throw new InvalidOperationException(DocumentUploadRefusals.LengthMismatch);
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(content));
        if (!string.Equals(sha256, manifest.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(DocumentUploadRefusals.Sha256Mismatch);
        }

        return new DocumentUploadContent(content, sha256);
    }

    /// <summary>
    /// The upload, when it exists, belongs to the caller and is inside its
    /// life; otherwise one sentence that says which of the three it was not.
    /// </summary>
    private async Task<DocumentUploadManifest> RequireLiveAsync(
        Guid uploadId,
        string owner,
        CancellationToken cancellationToken)
    {
        var manifest = await staging.ReadManifestAsync(uploadId, cancellationToken);
        return manifest is not null
            && string.Equals(manifest.Owner, owner, StringComparison.Ordinal)
            && timeProvider.GetUtcNow() < ExpiresAt(manifest)
                ? manifest
                : throw new InvalidOperationException(DocumentUploadRefusals.Unavailable);
    }

    private static DocumentUploadTicket Ticket(
        DocumentUploadManifest manifest,
        IReadOnlyList<int> receivedParts,
        bool isReplay) =>
        new(
            manifest.UploadId,
            DocumentUploadLimits.PartBytes,
            DocumentUploadLimits.PartCount(manifest.TotalBytes),
            ExpiresAt(manifest),
            receivedParts,
            isReplay);

    private static DateTimeOffset ExpiresAt(DocumentUploadManifest manifest) =>
        manifest.CreatedAtUtc + DocumentUploadLimits.Lifetime;

    private static void RequireIdentity(Guid uploadId, string owner)
    {
        if (uploadId == Guid.Empty || string.IsNullOrWhiteSpace(owner))
        {
            throw new InvalidOperationException(DocumentUploadRefusals.Unavailable);
        }
    }

    private static string? NormalizeSha256(string? sha256) =>
        sha256 is { Length: 64 } && sha256.All(char.IsAsciiHexDigit) ? sha256.ToUpperInvariant() : null;
}

/// <summary>
/// Where the abandoned-upload sweep resumes its listing, so a page of young
/// objects cannot hold older ones back. A singleton, because the Worker builds
/// the timer's function afresh for every tick; a restart begins again.
/// </summary>
public sealed class DocumentUploadSweepCursor
{
    public string? Continuation { get; set; }
}

public sealed record SweepAbandonedDocumentUploadsResult(int Deleted, int Failures);

/// <summary>
/// Deletes staged objects older than <see cref="DocumentUploadLimits.Lifetime"/>,
/// each by its own creation time, one listing page a run. A failed delete is
/// counted and the rest still run.
/// </summary>
public sealed class SweepAbandonedDocumentUploads(
    IDocumentUploadStaging staging,
    DocumentUploadSweepCursor cursor,
    TimeProvider timeProvider)
{
    public async Task<SweepAbandonedDocumentUploadsResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var cutoff = timeProvider.GetUtcNow() - DocumentUploadLimits.Lifetime;
        var page = await staging.ListAsync(maximumItems, cursor.Continuation, cancellationToken);
        cursor.Continuation = page.NextContinuation;
        var deleted = 0;
        var failures = 0;
        foreach (var item in page.Items)
        {
            if (item.CreatedAtUtc > cutoff)
            {
                continue;
            }

            try
            {
                if (await staging.DeleteAsync(item.Name, cutoff, cancellationToken))
                {
                    deleted++;
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                failures++;
            }
        }

        return new(deleted, failures);
    }
}
