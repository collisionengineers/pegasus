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
    DateTimeOffset RecordedAtUtc);

public sealed record CaseDocument(
    Guid Id,
    Guid CaseId,
    IReadOnlyList<DocumentOccurrence> Occurrences,
    IReadOnlyList<DocumentVersion> Versions);

public sealed record AddCaseDocumentCommand(
    Guid CaseId,
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content,
    DocumentSemanticRole SemanticRole,
    DocumentSource Source,
    string SourceOccurrenceIdentity,
    string Actor,
    string OperationKey,
    long? ExpectedCaseVersion);

public sealed record AddCaseDocumentResult(
    DocumentOccurrence Occurrence,
    DocumentVersion Version,
    bool IsReplay);

public sealed record DownloadCaseDocumentQuery(
    Guid CaseId,
    Guid OccurrenceId,
    Guid VersionId,
    string Actor);

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
    string Actor,
    string OperationKey);

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
    string Actor,
    string Reason,
    string OperationKey,
    long ExpectedCaseVersion);

public sealed record CreateBoxFileRequestCommand(
    Guid CaseId,
    string Actor,
    string OperationKey,
    DateTimeOffset? ExpiresAtUtc);

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
    string Actor,
    string Reason,
    string OperationKey,
    long ExpectedVersion);

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
