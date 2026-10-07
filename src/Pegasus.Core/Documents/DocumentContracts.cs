using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Documents;

/// <summary>
/// An immutable document version or retained intake asset in its authorized context.
/// The reader resolves custody/cache addresses internally; callers never supply storage keys.
/// </summary>
public sealed record ReadLogicalDocumentVersionRequest(
    ActionActor Actor, Guid? DocumentId, Guid? VersionId, Guid? IntakeAssetId, Guid? CaseId,
    Guid? IntakeReceiptId, string ExpectedSha256, long ExpectedContentLength);

public sealed record LogicalDocumentContent(
    Stream Content, Guid? DocumentId, Guid? VersionId, Guid? IntakeAssetId, string Sha256,
    long ContentLength, string FileName, string MediaType) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public interface IReadLogicalDocumentVersion
{
    Task<LogicalDocumentContent> OpenAsync(
        ReadLogicalDocumentVersionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The lookups for several versions of one Case, made together; the bytes
    /// are still read one at a time. A caller that reads many versions of one
    /// Case in turn (a report's pinned images) asks once, then opens each handle
    /// when it needs that version, so it holds one source at a time and pays the
    /// lookups once instead of once for each version.
    /// </summary>
    /// <remarks>
    /// The default prepares nothing: each handle is the single read. A reader
    /// that can share its lookups overrides it. Either way a handle answers
    /// exactly as <see cref="OpenAsync"/> does for the same request, including
    /// how an unavailable version fails, and it fails when it is opened, not
    /// when it is prepared.
    /// </remarks>
    Task<IReadOnlyList<PreparedLogicalDocumentRead>> PrepareAsync(
        ActionActor actor,
        Guid caseId,
        IReadOnlyList<LogicalDocumentVersionRead> versions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(versions);
        return Task.FromResult<IReadOnlyList<PreparedLogicalDocumentRead>>(
            [.. versions.Select(version => new PreparedLogicalDocumentRead(
                token => OpenAsync(
                    new ReadLogicalDocumentVersionRequest(
                        actor,
                        version.DocumentId,
                        version.VersionId,
                        IntakeAssetId: null,
                        caseId,
                        IntakeReceiptId: null,
                        version.ExpectedSha256,
                        version.ExpectedContentLength),
                    token)))]);
    }
}

/// <summary>One document version of a Case to read, and the hash and length it must have.</summary>
public sealed record LogicalDocumentVersionRead(
    Guid DocumentId, Guid VersionId, string ExpectedSha256, long ExpectedContentLength);

/// <summary>
/// One version whose lookups are done. <see cref="OpenAsync"/> reads its bytes,
/// verified against the hash and length it was prepared with, and may be
/// called each time the bytes are needed.
/// </summary>
public sealed class PreparedLogicalDocumentRead
{
    private readonly Func<CancellationToken, Task<LogicalDocumentContent>> open;

    public PreparedLogicalDocumentRead(Func<CancellationToken, Task<LogicalDocumentContent>> open)
    {
        ArgumentNullException.ThrowIfNull(open);
        this.open = open;
    }

    public Task<LogicalDocumentContent> OpenAsync(CancellationToken cancellationToken) =>
        open(cancellationToken);
}

public enum DocumentSemanticRole
{
    OriginalSource,
    Instruction,
    Image,
    Correspondence,
    EngineerReport,
    AuditReport,

    /// <summary>
    /// Market research findings produced outside Pegasus and filed against the
    /// Case: evidence beside the valuation, never read as a value.
    /// </summary>
    MarketResearch,
    Other
}

public enum DocumentSource
{
    Intake,
    StaffUpload,
    ExternalCorrespondence,
    Generated,
    Automation
}

public enum DocumentCustodyStatus
{
    Pending,
    Confirmed,
    Failed
}

/// <param name="IsRecognisedEstimate">
/// Whether the Worker, reading this version once after it was filed, found it
/// to be an estimate (<see cref="Pegasus.Core.Assessment.EstimateFormats"/>);
/// null until it has been read.
/// </param>
public sealed record DocumentVersion(
    Guid Id,
    Guid DocumentId,
    int Version,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256,
    DocumentCustodyStatus CustodyStatus,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy,
    bool IsCurrent,
    bool IsLogicallyRemoved,
    string? RemovalReason,
    bool? IsRecognisedEstimate = null);

public sealed record DocumentOccurrence(
    Guid Id,
    Guid CaseId,
    Guid DocumentId,
    Guid VersionId,
    DocumentSemanticRole SemanticRole,
    DocumentSource Source,
    string SourceOccurrenceIdentity,
    DateTimeOffset RecordedAtUtc,
    IReadOnlyList<ImageTagAssignment> Tags,
    int Ordinal = 0);

public sealed record CaseDocument(
    Guid Id,
    Guid CaseId,
    IReadOnlyList<DocumentOccurrence> Occurrences,
    IReadOnlyList<DocumentVersion> Versions);

/// <summary>
/// A case file as the operator sees it: one occurrence and the single version it
/// names, once that version is the current one and has not been logically
/// removed. <see cref="CaseFiles.Live"/> narrows that to confirmed custody;
/// <see cref="CaseFiles.Current"/> keeps a version whose custody is still in
/// flight or has failed, so a surface can show the operator that it is there.
/// </summary>
public sealed record CaseFile(DocumentOccurrence Occurrence, DocumentVersion Version);

/// <summary>
/// The one owner of "which of a case's documents are live files".
/// </summary>
/// <remarks>
/// The rule — join an occurrence to the version it names, then require current,
/// not logically removed, custody confirmed — was written out separately in the
/// evidence gallery, the case export, the report projection and a custody guard
/// before this existed, and the Evidence tab was about to make a fifth copy. It
/// is the operator's own rule: "if they show here, they should be on box."
///
/// Giving it one owner is also what keeps the Evidence tab's count and its rows
/// agreeing. They disagreed while the count read the raw document list: removing
/// a file — the very action this surface offers — left the tab saying one and
/// the panel showing none.
/// </remarks>
public static class CaseFiles
{
    /// <summary>
    /// Every file the Case currently has, whatever its custody has reached:
    /// the occurrence joined to the current, not logically removed version it
    /// names. This is what an operator surface lists, because a file that is
    /// still being stored or whose storage failed is on the Case and the
    /// operator must see it rather than have the tab silently omit it. Each
    /// entry carries <see cref="DocumentVersion.CustodyStatus"/>, so the
    /// surface says which state it is in.
    /// </summary>
    public static IReadOnlyList<CaseFile> Current(IEnumerable<CaseDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        return
        [
            .. documents.SelectMany(document => document.Occurrences
                .Select(occurrence => new
                {
                    Occurrence = occurrence,
                    Version = document.Versions.FirstOrDefault(version =>
                        version.Id == occurrence.VersionId
                        && version.IsCurrent
                        && !version.IsLogicallyRemoved)
                }))
                .Where(entry => entry.Version is not null)
                .Select(entry => new CaseFile(entry.Occurrence, entry.Version!))
        ];
    }

    /// <summary>
    /// The subset whose bytes are durably held: what may be read, sent, or
    /// projected into a report. "If they show here, they should be on box."
    /// </summary>
    public static IReadOnlyList<CaseFile> Live(IEnumerable<CaseDocument> documents) =>
    [
        .. Current(documents)
            .Where(file => file.Version.CustodyStatus == DocumentCustodyStatus.Confirmed)
    ];
}

public sealed record AddCaseDocumentCommand(
    Guid CaseId,
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content,
    DocumentSemanticRole SemanticRole,
    DocumentSource Source,
    string SourceOccurrenceIdentity,
    ActionActor Actor,
    string OperationKey,
    long ExpectedCaseVersion,
    string EditLeaseToken);

public sealed record AddCaseDocumentResult(
    DocumentOccurrence Occurrence,
    DocumentVersion Version,
    bool IsReplay);

public sealed record DownloadCaseDocumentQuery(
    Guid CaseId,
    Guid OccurrenceId,
    Guid VersionId,
    ActionActor Actor,
    string OperationKey);

public sealed record GetCaseDocumentMetadataQuery(
    Guid CaseId,
    Guid OccurrenceId,
    Guid VersionId,
    ActionActor Actor);

public sealed record CaseDocumentMetadata(
    Guid CaseId,
    Guid OccurrenceId,
    Guid DocumentId,
    Guid VersionId,
    string FileName,
    string MediaType,
    long ContentLength,
    string Sha256);

public sealed class DocumentDownload(
    Stream content,
    string fileName,
    string mediaType,
    long contentLength,
    string sha256) : IAsyncDisposable
{
    public Stream Content { get; } = content ?? throw new ArgumentNullException(nameof(content));

    public string FileName { get; } = fileName;

    public string MediaType { get; } = mediaType;

    public long ContentLength { get; } = contentLength;

    public string Sha256 { get; } = sha256;

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public sealed record LogicallyRemoveDocumentCommand(
    Guid CaseId,
    Guid OccurrenceId,
    ActionActor Actor,
    string Reason,
    string OperationKey,
    long ExpectedCaseVersion,
    string EditLeaseToken);

public sealed record MarkAsOriginalReportCommand(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string EditLeaseToken,
    Guid DocumentOccurrenceId,
    Guid DocumentVersionId);

public sealed record OriginalReportRecorded(
    Guid CaseId,
    Guid DocumentOccurrenceId,
    string FileName,
    long CaseVersion);

public interface IAddCaseDocument
{
    Task<AddCaseDocumentResult> ExecuteAsync(
        AddCaseDocumentCommand command,
        CancellationToken cancellationToken = default);
}

public interface IDownloadCaseDocument
{
    Task<DocumentDownload?> ExecuteAsync(
        DownloadCaseDocumentQuery query,
        CancellationToken cancellationToken = default);
}

public interface IGetCaseDocumentMetadata
{
    Task<CaseDocumentMetadata?> ExecuteAsync(
        GetCaseDocumentMetadataQuery query,
        CancellationToken cancellationToken = default);
}

public interface ILogicallyRemoveDocument
{
    Task ExecuteAsync(
        LogicallyRemoveDocumentCommand command,
        CancellationToken cancellationToken = default);
}

public interface IMarkAsOriginalReportStore
{
    /// <summary>
    /// Records the role and fills the Original report cells from
    /// <paramref name="reading"/> in the same transaction; a null reading
    /// still records the role.
    /// </summary>
    Task<OriginalReportRecorded> MarkAsOriginalReportAsync(
        MarkAsOriginalReportCommand command,
        OriginalReportReading? reading,
        CancellationToken cancellationToken = default);
}

/// <summary>A file an intake receipt filed on a Case, as the Case's document.</summary>
public sealed record FiledOriginalReportCandidate(
    Guid IntakeAssetId,
    Guid DocumentOccurrenceId,
    Guid DocumentVersionId);

/// <summary>A receipt's file to look for on the Case: its asset and the hash of its bytes.</summary>
public sealed record FiledOriginalReportLookup(Guid IntakeAssetId, string Sha256);

/// <summary>
/// Whether the Case is an open Audit awaiting its report, and which of the
/// asked-for files are filed on it. The two are told apart so a caller can
/// wait for a filing that has not happened yet rather than give up.
/// </summary>
public sealed record FiledOriginalReportCandidates(
    bool CaseAwaitsReport,
    IReadOnlyList<FiledOriginalReportCandidate> Filed);

public sealed record RecordRecognisedOriginalReport(
    Guid CaseId,
    Guid IntakeReceiptId,
    Guid DocumentOccurrenceId,
    Guid DocumentVersionId,
    ActionActor Actor,
    string OperationKey);

/// <summary>
/// The Case side of recognising a filed original report (FRD-16): which of a
/// receipt's files are on a Case that awaits its report, and the recording
/// of the one recognised, as the system, under the same rules as a staff Mark.
/// </summary>
public interface IRecogniseOriginalReportStore
{
    /// <summary>
    /// Which of <paramref name="assets"/> are filed on the Case as current,
    /// non-image documents, found by the hash of their bytes whichever route
    /// filed them, and whether the Case is an open Audit whose original report
    /// is missing at all.
    /// </summary>
    Task<FiledOriginalReportCandidates> FindAwaitingCandidatesAsync(
        Guid caseId,
        Guid receiptId,
        IReadOnlyCollection<FiledOriginalReportLookup> assets,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the role and fills the Original report cells from
    /// <paramref name="reading"/>. Null when the Case no longer awaits its
    /// report or the document changed; a live staff edit defers it with
    /// <see cref="Pegasus.Core.Intake.IntakeDependencyUnavailableException"/>.
    /// </summary>
    Task<OriginalReportRecorded?> RecordRecognisedAsync(
        RecordRecognisedOriginalReport command,
        OriginalReportReading reading,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Durable content storage for managed case document versions, keyed by the
/// immutable case and document-version identities. Implementations verify the
/// SHA-256 and length on both write and read, and treat a store of identical
/// content as a successful replay rather than a conflict.
/// </summary>
public interface IDocumentContentStore
{
    Task StoreAsync(
        Guid caseId,
        string caseReference,
        Guid versionId,
        ReadOnlyMemory<byte> content,
        string expectedSha256,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        Guid caseId,
        string caseReference,
        Guid versionId,
        string expectedSha256,
        long expectedLength,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        Guid caseId,
        string caseReference,
        Guid versionId,
        CancellationToken cancellationToken);

    async Task<DocumentContentWriteResult> StoreVersionAsync(
        ManagedDocumentContentAddress address,
        ReadOnlyMemory<byte> content,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        await StoreAsync(
            address.CaseId,
            address.CaseReference,
            address.VersionId,
            content,
            expectedSha256,
            cancellationToken);
        return new(DocumentContentWriteDisposition.Created, null);
    }

    /// <summary>
    /// Stores one managed version from a caller-owned stream. The stream has
    /// exactly <paramref name="contentLength"/> bytes and remains owned by the
    /// caller; an implementation verifies that length and
    /// <paramref name="expectedSha256"/> without retaining the document in
    /// process memory.
    /// </summary>
    Task<DocumentContentWriteResult> StoreVersionAsync(
        ManagedDocumentContentAddress address,
        Stream content,
        long contentLength,
        string expectedSha256,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadVersionAsync(
        ManagedDocumentContentAddress address,
        string expectedSha256,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        return OpenReadAsync(
            address.CaseId,
            address.CaseReference,
            address.VersionId,
            expectedSha256,
            expectedLength,
            cancellationToken);
    }

    /// <summary>
    /// The contents of several versions of one case, in the order asked for.
    ///
    /// A remote store resolves a case's folder and re-proves each
    /// file's ancestry on every single read, so N files cost N times the whole
    /// resolution. Asking for the set lets it resolve once and fetch the
    /// contents together. The default is the per-version read, so a store with
    /// no cheaper route needs nothing; the SHA-256 and length of each version
    /// are verified exactly as they are on a single read.
    ///
    /// Every version is held in memory before any is returned, so this is for
    /// a caller that wants the bytes — an archive built from them — and not for
    /// one that streams a version straight to its destination under a size
    /// bound. That caller keeps <see cref="OpenReadVersionAsync"/>.
    /// </summary>
    async Task<IReadOnlyList<ReadOnlyMemory<byte>>> ReadVersionsAsync(
        IReadOnlyList<ManagedDocumentContentRead> reads,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reads);
        var contents = new ReadOnlyMemory<byte>[reads.Count];
        for (var index = 0; index < reads.Count; index++)
        {
            var read = reads[index];
            await using var content = await OpenReadVersionAsync(
                read.Address, read.ExpectedSha256, read.ExpectedLength, cancellationToken);
            var bytes = GC.AllocateUninitializedArray<byte>(checked((int)read.ExpectedLength));
            await content.ReadExactlyAsync(bytes, cancellationToken);
            contents[index] = bytes;
        }
        return contents;
    }
}

/// <summary>
/// One version to read: the address plus the custody hash and length it must
/// verify against — the three arguments of a single managed read, so a set of
/// them can be asked for at once.
/// </summary>
public sealed record ManagedDocumentContentRead(
    ManagedDocumentContentAddress Address,
    string ExpectedSha256,
    long ExpectedLength);

/// <summary>
/// A Case's managed versions read through the document content cache first:
/// a warm version costs one cached-object read, and a cold one is read from
/// Box once and cached for the next time. The export and the report read
/// their photographs this way when the cache is composed.
/// </summary>
public interface IReadCachedDocumentVersions
{
    /// <summary>
    /// The contents in the order asked for, each verified against its custody
    /// hash and length, exactly as
    /// <see cref="IDocumentContentStore.ReadVersionsAsync"/> returns them.
    /// </summary>
    Task<IReadOnlyList<ReadOnlyMemory<byte>>> ReadVersionsAsync(
        IReadOnlyList<ManagedDocumentContentRead> reads,
        CancellationToken cancellationToken);
}

/// <summary>
/// What one read-cache copy is kept under: a document version or a retained
/// intake asset, never both. The cache is keyed by the same identity a read
/// asks for, so a copy is found by the caller that filed it.
/// </summary>
public sealed record DocumentContentCacheKey(Guid? DocumentVersionId, Guid? IntakeAssetId)
{
    public static DocumentContentCacheKey ForVersion(Guid documentVersionId) =>
        new(documentVersionId, null);

    public static DocumentContentCacheKey ForIntakeAsset(Guid intakeAssetId) =>
        new(null, intakeAssetId);
}

/// <summary>
/// Writes the read-cache copy of content a caller holds and has just filed to
/// custody, so the first read of it is a cache hit instead of a Box read.
/// </summary>
/// <remarks>
/// The copy is an optimisation and never the record. A publish that fails, or
/// takes longer than five seconds, is logged and forgotten: it never throws
/// to the caller, so it can never fail the filing it follows, and it never
/// holds it for longer than that. Once one publish has failed or timed out, the
/// publisher's remaining publishes are skipped. The publisher is scoped: one
/// request, one queued work item, or one whole run of the Worker's
/// reconciliation timer, which can file up to 50 pending versions. So a store
/// that is down costs that scope one wait and not one for each file. The next
/// read of a file that was not published then misses and publishes as it
/// always did.
///
/// A caller publishes only after custody has confirmed the file and its own
/// transaction has committed, because the cache row refers to the version or
/// asset row. Each call uses its own database context, so several may run at
/// the same time.
/// </remarks>
public interface IDocumentContentCachePublisher
{
    /// <summary>
    /// Publishes <paramref name="content"/>, which must be a readable, seekable
    /// stream of exactly <paramref name="contentLength"/> bytes whose SHA-256 is
    /// <paramref name="sha256"/>. The publisher checks both itself and refuses
    /// to publish bytes that differ. The stream stays the caller's.
    /// </summary>
    Task PublishAsync(
        DocumentContentCacheKey key,
        Stream content,
        string sha256,
        long contentLength,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publishes the copy of an artifact intake retained under
    /// <paramref name="storageKey"/>, for a caller that filed the file without
    /// holding its bytes: a custody adapter read them from the retained copy and
    /// the version they belong to was only recorded afterwards. The publisher
    /// reads the retained copy itself and checks it exactly as
    /// <see cref="PublishAsync(DocumentContentCacheKey, Stream, string, long, CancellationToken)"/>
    /// does. Box is not read.
    /// </summary>
    Task PublishRetainedIntakeCopyAsync(
        DocumentContentCacheKey key,
        string storageKey,
        string sha256,
        long contentLength,
        CancellationToken cancellationToken);
}

public static class DocumentContentCachePublisherExtensions
{
    /// <summary>Publishes bytes a caller holds in memory.</summary>
    public static async Task PublishAsync(
        this IDocumentContentCachePublisher publisher,
        DocumentContentCacheKey key,
        ReadOnlyMemory<byte> content,
        string sha256,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        await using var stream = StreamOf(content);
        await publisher.PublishAsync(key, stream, sha256, content.Length, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// A read-only stream over bytes held in memory. It shares the array when the
    /// memory is one, so the bytes are not copied.
    /// </summary>
    public static MemoryStream StreamOf(ReadOnlyMemory<byte> content) =>
        System.Runtime.InteropServices.MemoryMarshal.TryGetArray(content, out var segment)
            ? new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(content.ToArray(), writable: false);
}

public sealed record ManagedDocumentContentAddress(
    Guid CaseId,
    string CaseReference,
    string? CaseRootRemoteId,
    Guid OccurrenceId,
    int OccurrenceOrdinal,
    Guid DocumentId,
    Guid VersionId,
    int Version,
    DocumentSemanticRole SemanticRole,
    string FileName,
    string MediaType,
    string? BoxFileId = null,
    string? BoxVersionId = null);

public enum DocumentContentWriteDisposition
{
    Created,
    Replay
}

public sealed record DocumentContentWriteResult(
    DocumentContentWriteDisposition Disposition,
    string? RemoteId,
    string? BoxVersionId = null);
