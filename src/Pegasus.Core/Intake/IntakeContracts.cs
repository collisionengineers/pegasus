using Pegasus.Core.Cases;

namespace Pegasus.Core.Intake;

public enum IntakeDecision
{
    DraftReady,
    NeedsSorting,
    Unsupported,
    OcrRequired,
    TechnicalFailure
}

public enum IntakeEvidenceSource
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

public enum IntakeEvidenceStrength
{
    Strong,
    Weak
}

public enum IntakeEvidenceFinding
{
    SupportsPrincipal,
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

public enum InstructionPolicyApplicability
{
    Applicable,
    NotApplicable,
    Indeterminate
}

public enum MailRouteDisposition
{
    Accepted,
    NoMatch,
    NeedsSorting
}

public enum MailRouteKind
{
    DirectProvider,
    Intermediary
}

public sealed record MailRoutePredicateResult(
    string Key,
    bool Matched,
    string Detail);

public sealed record MailRouteSelection(
    string RouteOwnerCode,
    MailRouteKind Kind,
    string WorkProviderCode);

public sealed record MailRouteEvaluationResult(
    MailRouteDisposition Disposition,
    MailRouteSelection? SelectedRoute,
    IReadOnlyList<MailRoutePredicateResult> Predicates,
    string Reason,
    string PolicyKey,
    int PolicyVersion);

public interface IMailRoutePolicy
{
    MailRouteEvaluationResult Evaluate(IntakeSourceReadResult readResult);
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

public sealed class IntakeArtifactRetentionException : Exception
{
    public IntakeArtifactRetentionException(Exception innerException)
        : base("The intake source could not be retained.", innerException)
    {
    }
}

public sealed record IntakeSource(
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content,
    DateTimeOffset ReceivedAtUtc,
    string Actor,
    IntakeSourceIdentity SourceIdentity);

public sealed record IntakeContentFragment(
    IntakeEvidenceSource Source,
    string SourceLabel,
    string Text);

public sealed record IntakeTransportEvidence(
    IntakeEvidenceSource Source,
    string Value);

public sealed record IntakeSourceIssue(
    string Code,
    string Reason,
    IntakeEvidenceSource Source);

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
    bool IsIncomplete = false,
    string ReaderKey = "unspecified_reader",
    string ReaderVersion = "1")
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

public sealed record IntakeEvidence(
    IntakeEvidenceSource Source,
    IntakeEvidenceStrength Strength,
    IntakeEvidenceFinding Finding,
    string Signal,
    string Detail);

public sealed record InstructionFieldCandidate(
    string Value,
    IntakeEvidenceSource Source,
    string SourceLabel);

public sealed record InstructionReviewField(
    string Name,
    string? SuggestedValue,
    IReadOnlyList<InstructionFieldCandidate> Candidates,
    bool IsDefaulted,
    bool HasConflict);

public sealed record InstructionDraft(
    string? SuggestedPrincipalCode,
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

public sealed record IntakeReceipt(
    Guid Id,
    string SourceFileName,
    string MediaType,
    long SourceLength,
    string SourceHash,
    IntakeSourceIdentity SourceIdentity,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset ProcessedAtUtc,
    IntakeDecision Decision,
    string DecisionReason,
    IReadOnlyList<IntakeEvidence> Evidence,
    IReadOnlyList<InstructionReviewField> Fields,
    InstructionDraft? InstructionDraft,
    IReadOnlyList<string> MissingFields,
    string? FailureCode,
    string? FailureReason,
    bool IsDuplicate,
    string SourceReaderKey,
    string SourceReaderVersion,
    string? ExtractionPolicyKey,
    int? ExtractionPolicyVersion,
    IReadOnlyList<IntakeAssetRecord>? Assets = null,
    IReadOnlyList<ScannedPdfOcrCandidate>? OcrCandidates = null)
{
    public IReadOnlyList<IntakeAssetRecord> AssetRecords => Assets ?? [];

    public IReadOnlyList<ScannedPdfOcrCandidate> ScannedPdfPages => OcrCandidates ?? [];
}

public sealed record IntakeReceiptDraft(
    string SourceFileName,
    string MediaType,
    long SourceLength,
    string SourceHash,
    IntakeSourceIdentity SourceIdentity,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset ProcessedAtUtc,
    string Actor,
    IntakeDecision Decision,
    string DecisionReason,
    IReadOnlyList<IntakeEvidence> Evidence,
    IReadOnlyList<InstructionReviewField> Fields,
    InstructionDraft? InstructionDraft,
    IReadOnlyList<string> MissingFields,
    string? FailureCode,
    string? FailureReason,
    string SourceReaderKey,
    string SourceReaderVersion,
    string? ExtractionPolicyKey,
    int? ExtractionPolicyVersion,
    IReadOnlyList<IntakeAssetRecord>? Assets = null,
    IReadOnlyList<ScannedPdfOcrCandidate>? OcrCandidates = null)
{
    public IReadOnlyList<IntakeAssetRecord> AssetRecords => Assets ?? [];

    public IReadOnlyList<ScannedPdfOcrCandidate> ScannedPdfPages => OcrCandidates ?? [];
}

public sealed record IntakeQueueCounts(int DraftReady, int NeedsSorting);

public sealed record IntakeReceiptSummary(
    Guid Id,
    string SourceFileName,
    DateTimeOffset ReceivedAtUtc,
    IntakeDecision Decision,
    string? FailureReason);

public sealed record InstructionExtractionResult(
    InstructionPolicyApplicability Applicability,
    IReadOnlyList<IntakeEvidence> Evidence,
    IReadOnlyList<InstructionReviewField> Fields,
    InstructionDraft? InstructionDraft,
    IReadOnlyList<string> MissingFields,
    string PolicyKey,
    int PolicyVersion);

public interface IInstructionExtractionPolicy
{
    InstructionExtractionResult Extract(
        IntakeSourceReadResult readResult,
        DateTimeOffset processedAtUtc);
}

public interface IIntakeSourceReader
{
    Task<IntakeSourceReadResult> ReadAsync(IntakeSource source, CancellationToken cancellationToken);
}

public static class IntakeExceptionPolicy
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
    : Exception("The retained intake artifact failed integrity validation.");

public interface IIntakeReceiptStore
{
    Task<IntakeReceipt?> FindBySourceIdentityAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken);

    Task<IntakeReceipt> StoreAsync(IntakeReceiptDraft draft, CancellationToken cancellationToken);
}

public interface IIntakeReceiptQueries
{
    Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<IntakeReceiptSummary>> ListAsync(
        IntakeDecision? decision,
        CancellationToken cancellationToken);

    Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IntakeAssetRecord?> GetAssetAsync(
        Guid receiptId,
        Guid assetId,
        CancellationToken cancellationToken);
}

public sealed record ResolveIntakeRequest(
    Guid ReceiptId,
    long ExpectedVersion,
    string Actor,
    string OperationKey,
    string Reason,
    InstructionDraft? CorrectedDraft);

public sealed record ReevaluateIntakeRequest(
    Guid ReceiptId,
    long ExpectedVersion,
    string Actor,
    string OperationKey,
    string Reason);

public sealed record AcceptIntakeRequest(
    Guid ReceiptId,
    long ExpectedVersion,
    string Actor,
    string OperationKey,
    CaseType CaseType,
    string PrincipalCode,
    CaseCompleteness Completeness,
    AuditAssessment? StandaloneAuditAssessment = null);

public sealed record LinkIntakeRequest(
    Guid ReceiptId,
    Guid CaseId,
    long ExpectedIntakeVersion,
    long ExpectedCaseVersion,
    string Actor,
    string OperationKey,
    string Reason);

public sealed record ReverseIntakeLinkRequest(
    Guid ReceiptId,
    Guid CaseId,
    long ExpectedIntakeVersion,
    long ExpectedCaseVersion,
    string Actor,
    string OperationKey,
    string Reason);

public interface IResolveIntake
{
    Task<IntakeReceipt> ExecuteAsync(ResolveIntakeRequest request, CancellationToken cancellationToken);
}

public interface IReevaluateIntake
{
    Task<IntakeReceipt> ExecuteAsync(ReevaluateIntakeRequest request, CancellationToken cancellationToken);
}

public interface IAcceptIntake
{
    Task<CaseAcceptanceOutcome> ExecuteAsync(AcceptIntakeRequest request, CancellationToken cancellationToken);
}

public interface ILinkIntake
{
    Task ExecuteAsync(LinkIntakeRequest request, CancellationToken cancellationToken);
}

public interface IReverseIntakeLink
{
    Task ExecuteAsync(ReverseIntakeLinkRequest request, CancellationToken cancellationToken);
}
