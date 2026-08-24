using Pegasus.Core.Identity;

namespace Pegasus.Core.Documents;

public enum DocumentSemanticRole
{
    OriginalSource,
    Instruction,
    Image,
    Correspondence,
    EngineerReport,
    AuditReport,
    Other
}

public enum DocumentSource
{
    Intake,
    StaffUpload,
    RequestUpload,
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
    string? RemovalReason);

public sealed record DocumentOccurrence(
    Guid Id,
    Guid CaseId,
    Guid DocumentId,
    Guid VersionId,
    DocumentSemanticRole SemanticRole,
    DocumentSource Source,
    string SourceOccurrenceIdentity,
    DateTimeOffset RecordedAtUtc,
    DateTimeOffset? ThirdPartyVehicleConfirmedAtUtc,
    string? ThirdPartyVehicleConfirmationReason,
    int Ordinal = 0);

public sealed record CaseDocument(
    Guid Id,
    Guid CaseId,
    IReadOnlyList<DocumentOccurrence> Occurrences,
    IReadOnlyList<DocumentVersion> Versions);

/// <summary>
/// A case file as the operator sees it: one occurrence and the single version it
/// names, once that version is the current one, has not been logically removed,
/// and its custody is confirmed.
/// </summary>
public sealed record CaseFile(DocumentOccurrence Occurrence, DocumentVersion Version);

/// <summary>
/// The one owner of "which of a case's documents are live files".
/// </summary>
/// <remarks>
/// The rule — join an occurrence to the version it names, then require current,
/// not logically removed, custody confirmed — was written out separately in the
/// evidence gallery, the EVA hand-off, the report projection and a custody guard
/// before this existed, and the Evidence tab was about to make a fifth copy. It
/// is the operator's own rule: "if they show here, they should be on box."
///
/// Giving it one owner is also what keeps the Evidence tab's count and its rows
/// agreeing. They disagreed while the count read the raw document list: removing
/// a file — the very action this surface offers — left the tab saying one and
/// the panel showing none (DOCS-012).
/// </remarks>
public static class CaseFiles
{
    public static IReadOnlyList<CaseFile> Live(IEnumerable<CaseDocument> documents)
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
                        && !version.IsLogicallyRemoved
                        && version.CustodyStatus == DocumentCustodyStatus.Confirmed)
                }))
                .Where(entry => entry.Version is not null)
                .Select(entry => new CaseFile(entry.Occurrence, entry.Version!))
        ];
    }
}
public sealed record CaseDocumentState(Guid CaseId, long CaseVersion);

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

public sealed record ExportCaseDocumentsCommand(
    Guid CaseId,
    IReadOnlyList<DocumentExportSelection> Selections,
    ActionActor Actor,
    string OperationKey,
    long MaximumArchiveBytes,
    long ExpectedCaseVersion,
    string EditLeaseToken);

public sealed record DocumentExportSelection(Guid OccurrenceId, Guid VersionId);

public sealed record DocumentExportManifestEntry(
    string FileName,
    Guid OccurrenceId,
    Guid VersionId,
    DocumentSemanticRole SemanticRole,
    long ContentLength,
    string Sha256);

public sealed class DocumentExport(
    Stream content,
    string fileName,
    IReadOnlyList<DocumentExportManifestEntry> manifest) : IAsyncDisposable
{
    public Stream Content { get; } = content ?? throw new ArgumentNullException(nameof(content));

    public string FileName { get; } = fileName;

    public IReadOnlyList<DocumentExportManifestEntry> Manifest { get; } = manifest;

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

public sealed record ConfirmThirdPartyVehicleEvidenceCommand(
    Guid CaseId,
    Guid OccurrenceId,
    ActionActor Actor,
    string Reason,
    string OperationKey,
    long ExpectedCaseVersion,
    string EditLeaseToken);

public interface ICaseDocumentStateQueries
{
    Task<CaseDocumentState?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken = default);
}

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

/// <summary>
/// Export was attempted on a case that is not in <c>Review</c>.
/// </summary>
/// <remarks>
/// The operator's rule (2026-08-04) is that a case exports only in Review. A
/// disabled button is presentation; this is the condition itself, so it holds
/// for every caller rather than only for the one that renders the button.
/// </remarks>
public sealed class CaseNotInReviewException(Guid caseId)
    : InvalidOperationException("A case can only be exported while it is in Review.")
{
    public Guid CaseId { get; } = caseId;
}

public interface IExportCaseDocuments
{
    Task<DocumentExport> ExecuteAsync(
        ExportCaseDocumentsCommand command,
        CancellationToken cancellationToken = default);
}

public interface ILogicallyRemoveDocument
{
    Task ExecuteAsync(
        LogicallyRemoveDocumentCommand command,
        CancellationToken cancellationToken = default);
}

public interface IConfirmThirdPartyVehicleEvidence
{
    Task ExecuteAsync(
        ConfirmThirdPartyVehicleEvidenceCommand command,
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
}

public sealed record ManagedDocumentContentAddress(
    Guid CaseId,
    string CaseReference,
    Guid OccurrenceId,
    int OccurrenceOrdinal,
    Guid DocumentId,
    Guid VersionId,
    int Version,
    DocumentSemanticRole SemanticRole,
    string FileName,
    string MediaType);

public enum DocumentContentWriteDisposition
{
    Created,
    Replay
}

public sealed record DocumentContentWriteResult(
    DocumentContentWriteDisposition Disposition,
    string? RemoteId);
