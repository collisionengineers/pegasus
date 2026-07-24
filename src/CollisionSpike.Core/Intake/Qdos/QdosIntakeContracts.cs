namespace CollisionSpike.Core.Intake.Qdos;

public enum QdosIntakeDecision
{
    DraftReady,
    NeedsSorting,
    Unsupported,
    OcrRequired,
    TechnicalFailure
}

public enum QdosEvidenceSource
{
    EmailBody,
    PdfContent,
    DocumentContent,
    ImageContent,
    Sender,
    Subject,
    FileName,
    MimeType,
    SystemDefault
}

public enum QdosEvidenceStrength
{
    Strong,
    Weak
}

public enum QdosEvidenceFinding
{
    SupportsQdos,
    ContradictsTransport,
    ExtractedField,
    ConflictingField,
    MissingField,
    Information
}

public enum IntakeSourceReadStatus
{
    Readable,
    Unsupported,
    TechnicalFailure
}

public enum IntakeSourceChannel
{
    ManualUpload
}

public sealed record IntakeSourceIdentity(
    IntakeSourceChannel Channel,
    string ExternalReceiptToken);

public sealed class IntakeSourceIdentityConflictException : Exception
{
    public IntakeSourceIdentityConflictException()
        : base("The source identity is already associated with different content.")
    {
    }
}

public sealed record QdosIntakeSource(
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content,
    DateTimeOffset ReceivedAtUtc,
    string Actor,
    IntakeSourceIdentity SourceIdentity);

public sealed record IntakeContentFragment(
    QdosEvidenceSource Source,
    string SourceLabel,
    string Text);

public sealed record IntakeTransportEvidence(
    QdosEvidenceSource Source,
    string Value);

public sealed record IntakeSourceIssue(
    string Code,
    string Reason,
    QdosEvidenceSource Source);

public enum IntakeAssetKind
{
    Source,
    Attachment,
    InlineImage,
    EmbeddedImage
}

public enum IntakeAssetDisposition
{
    Source,
    Attachment,
    Inline,
    Embedded
}

public sealed record IntakeAssetBounds(
    double Left,
    double Bottom,
    double Right,
    double Top);

public sealed record IntakeAssetCandidate(
    string SourceLabel,
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content,
    IntakeAssetKind Kind,
    IntakeAssetDisposition Disposition,
    int? PageNumber = null,
    IntakeAssetBounds? Bounds = null,
    int? WidthPixels = null,
    int? HeightPixels = null);

public sealed record ScannedPdfOcrCandidate(
    string SourceLabel,
    int PageNumber);

public sealed record IntakeSourceReadResult(
    IntakeSourceReadStatus Status,
    IReadOnlyList<IntakeContentFragment> Content,
    IReadOnlyList<IntakeTransportEvidence> TransportEvidence,
    IReadOnlyList<IntakeSourceIssue> Issues,
    bool RequiresOcr,
    string? FailureCode = null,
    string? FailureReason = null,
    IReadOnlyList<IntakeAssetCandidate>? Assets = null,
    IReadOnlyList<ScannedPdfOcrCandidate>? OcrCandidates = null,
    bool IsIncomplete = false)
{
    public IReadOnlyList<IntakeAssetCandidate> AssetCandidates => Assets ?? [];

    public IReadOnlyList<ScannedPdfOcrCandidate> ScannedPdfPages => OcrCandidates ?? [];
}

public sealed record IntakeAssetRecord(
    Guid Id,
    string SourceLabel,
    string FileName,
    string MediaType,
    IntakeAssetKind Kind,
    IntakeAssetDisposition Disposition,
    long ContentLength,
    string ContentHash,
    string StorageKey,
    int? PageNumber,
    IntakeAssetBounds? Bounds,
    int? WidthPixels,
    int? HeightPixels);

public sealed record QdosEvidence(
    QdosEvidenceSource Source,
    QdosEvidenceStrength Strength,
    QdosEvidenceFinding Finding,
    string Signal,
    string Detail);

public sealed record QdosFieldCandidate(
    string Value,
    QdosEvidenceSource Source,
    string SourceLabel);

public sealed record QdosReviewField(
    string Name,
    string? SuggestedValue,
    IReadOnlyList<QdosFieldCandidate> Candidates,
    bool IsDefaulted,
    bool HasConflict);

public sealed record QdosTypedDraft(
    string PrincipalCode,
    string? ClaimantName,
    string? ClaimNumber,
    string? VehicleRegistration,
    string? VehicleMake,
    string? VehicleModel,
    long? VehicleMileage,
    string? AccidentCircumstances,
    DateOnly? DateOfIncident,
    DateOnly? InstructionDate,
    string? InspectionAddress);

public sealed record QdosIntakeRecord(
    Guid Id,
    string SourceFileName,
    string MediaType,
    long SourceLength,
    string SourceHash,
    IntakeSourceIdentity SourceIdentity,
    DateTimeOffset ReceivedAtUtc,
    QdosIntakeDecision Decision,
    string DecisionReason,
    IReadOnlyList<QdosEvidence> Evidence,
    IReadOnlyList<QdosReviewField> Fields,
    QdosTypedDraft? TypedDraft,
    IReadOnlyList<string> MissingFields,
    string? FailureCode,
    string? FailureReason,
    bool IsDuplicate,
    IReadOnlyList<IntakeAssetRecord>? Assets = null,
    IReadOnlyList<ScannedPdfOcrCandidate>? OcrCandidates = null)
{
    public IReadOnlyList<IntakeAssetRecord> AssetRecords => Assets ?? [];

    public IReadOnlyList<ScannedPdfOcrCandidate> ScannedPdfPages => OcrCandidates ?? [];
}

public sealed record QdosIntakeDraft(
    string SourceFileName,
    string MediaType,
    long SourceLength,
    string SourceHash,
    IntakeSourceIdentity SourceIdentity,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset ProcessedAtUtc,
    string Actor,
    QdosIntakeDecision Decision,
    string DecisionReason,
    IReadOnlyList<QdosEvidence> Evidence,
    IReadOnlyList<QdosReviewField> Fields,
    QdosTypedDraft? TypedDraft,
    IReadOnlyList<string> MissingFields,
    string? FailureCode,
    string? FailureReason,
    IReadOnlyList<IntakeAssetRecord>? Assets = null,
    IReadOnlyList<ScannedPdfOcrCandidate>? OcrCandidates = null)
{
    public IReadOnlyList<IntakeAssetRecord> AssetRecords => Assets ?? [];

    public IReadOnlyList<ScannedPdfOcrCandidate> ScannedPdfPages => OcrCandidates ?? [];
}

public sealed record QdosQueueCounts(int DraftReady, int NeedsSorting);

public sealed record QdosIntakeSummary(
    Guid Id,
    string SourceFileName,
    DateTimeOffset ReceivedAtUtc,
    QdosIntakeDecision Decision,
    string? FailureReason);

public interface IQdosIntakeSourceReader
{
    Task<IntakeSourceReadResult> ReadAsync(QdosIntakeSource source, CancellationToken cancellationToken);
}

public static class QdosIntakeExceptionPolicy
{
    public static bool IsRecoverable(Exception exception) =>
        exception is not OperationCanceledException
            and not OutOfMemoryException
            and not AccessViolationException;
}

public interface IIntakeArtifactStore
{
    Task<string> StoreAsync(
        string contentHash,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);

    Task<ReadOnlyMemory<byte>?> ReadAsync(
        string storageKey,
        CancellationToken cancellationToken);
}

public sealed class IntakeArtifactIntegrityException()
    : Exception("The retained intake artifact failed integrity validation.")
{
}

public interface IQdosIntakeStore
{
    Task<QdosIntakeRecord?> FindBySourceIdentityAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken);

    Task<QdosIntakeRecord> StoreAsync(QdosIntakeDraft draft, CancellationToken cancellationToken);
}

public interface IQdosIntakeQueries
{
    Task<QdosQueueCounts> GetCountsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<QdosIntakeSummary>> ListAsync(
        QdosIntakeDecision? decision,
        CancellationToken cancellationToken);

    Task<QdosIntakeRecord?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IntakeAssetRecord?> GetAssetAsync(
        Guid receiptId,
        Guid assetId,
        CancellationToken cancellationToken);
}
