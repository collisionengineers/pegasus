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
    Generated
}

public enum DocumentCustodyStatus
{
    Pending,
    Confirmed,
    Failed
}

public enum BoxFileRequestStatus
{
    Pending,
    Active,
    Unavailable,
    Deactivated,
    Failed,
    Unknown
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
    string? ThirdPartyVehicleConfirmationReason);

public sealed record CaseDocument(
    Guid Id,
    Guid CaseId,
    IReadOnlyList<DocumentOccurrence> Occurrences,
    IReadOnlyList<DocumentVersion> Versions);
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

public sealed record CreateBoxFileRequestCommand(
    Guid CaseId,
    ActionActor Actor,
    string OperationKey,
    DateTimeOffset? ExpiresAtUtc,
    long ExpectedCaseVersion,
    string EditLeaseToken);

public sealed record BoxFileRequest(
    Guid Id,
    Guid CaseId,
    BoxFileRequestStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? DeactivatedAtUtc,
    long Version);

public sealed class BoxFileRequestSecret
{
    public BoxFileRequestSecret(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        Url = url;
    }

    public string Url { get; }

    public override string ToString() => "[REDACTED]";
}

public sealed record CreateBoxFileRequestResult(
    BoxFileRequest FileRequest,
    BoxFileRequestSecret? Secret,
    bool IsReplay);

public sealed record RevokeBoxFileRequestCommand(
    Guid CaseId,
    Guid FileRequestId,
    ActionActor Actor,
    string Reason,
    string OperationKey,
    long ExpectedFileRequestVersion,
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

public interface ICreateBoxFileRequest
{
    Task<CreateBoxFileRequestResult> ExecuteAsync(
        CreateBoxFileRequestCommand command,
        CancellationToken cancellationToken = default);
}

public interface IRevokeBoxFileRequest
{
    Task<BoxFileRequest> ExecuteAsync(
        RevokeBoxFileRequestCommand command,
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
}
