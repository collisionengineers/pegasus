using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

public static class IntakeEnvelopeLimits
{
    public const int MaximumContentLength = 10 * 1024 * 1024;
}

public enum IntakeDecision
{
    DraftReady,
    NeedsSorting,
    BlockedIntake,
    Unsupported,
    OcrRequired,
    TechnicalFailure,
    ImageIntakeRegistered
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
    StaffCorrection,
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
    Information,
    AcceptedTriageMatch
}

public enum IntakeSourceReadStatus
{
    Readable,
    Unsupported,
    TechnicalFailure
}

public enum IntakeSourceChannel
{
    ManualUpload,
    Mailbox,
    Automation
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
public sealed record MailRouteIdentity(
    string Address,
    string SourceLabel);


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
    int PolicyVersion,
    IReadOnlyList<MailRouteIdentity> TransportIdentities,
    IReadOnlyList<MailRouteIdentity> OriginalIdentities,
    MailRouteIdentity? EffectiveSender);

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

    public IntakeSourceIdentityConflictException(
        string existingSourceHash,
        string presentedSourceHash)
        : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(existingSourceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(presentedSourceHash);
        ExistingSourceHash = existingSourceHash;
        PresentedSourceHash = presentedSourceHash;
    }

    public string? ExistingSourceHash { get; }

    public string? PresentedSourceHash { get; }
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

public enum IntakeSenderIdentityKind
{
    Transport,
    AttachedOriginal
}

public sealed record IntakeTransportEvidence(
    IntakeEvidenceSource Source,
    string Value,
    IntakeSenderIdentityKind SenderIdentityKind = IntakeSenderIdentityKind.Transport,
    string? SourceLabel = null);

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
    string Detail,
    string? MatcherKey = null,
    int? MatcherVersion = null);

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
    string? InspectionAddress,
    DateOnly? InspectionDate = null);

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
    IReadOnlyList<ScannedPdfOcrCandidate>? OcrCandidates = null,
    MailRouteEvaluationResult? MailRouteDecision = null,
    long Version = 0,
    Guid? AcceptedCaseId = null,
    Guid? ManualLinkedCaseId = null,
    long? ManualAssociationVersion = null)
{
    public IReadOnlyList<IntakeAssetRecord> AssetRecords => Assets ?? [];

    public IReadOnlyList<ScannedPdfOcrCandidate> ScannedPdfPages => OcrCandidates ?? [];

    public Guid? CurrentCaseId =>
        ManualAssociationVersion is null ? AcceptedCaseId : ManualLinkedCaseId;
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
    IReadOnlyList<ScannedPdfOcrCandidate>? OcrCandidates = null,
    MailRouteEvaluationResult? MailRouteDecision = null)
{
    public IReadOnlyList<IntakeAssetRecord> AssetRecords => Assets ?? [];

    public IReadOnlyList<ScannedPdfOcrCandidate> ScannedPdfPages => OcrCandidates ?? [];
}

public sealed record IntakeQueueCounts(int DraftReady, int NeedsSorting, int BlockedIntake = 0);

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
public sealed record IntakeTriageMatch(
    IntakeEvidenceSource Source,
    string Signal,
    string Detail,
    string MatcherKey,
    int MatcherVersion);

public interface IIntakeTriageMatcher
{
    IReadOnlyList<IntakeTriageMatch> Match(
        IntakeSourceReadResult readResult,
        InstructionDraft draft);
}

public sealed class NoAcceptedIntakeTriageMatcher : IIntakeTriageMatcher
{
    public IReadOnlyList<IntakeTriageMatch> Match(
        IntakeSourceReadResult readResult,
        InstructionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(draft);
        return [];
    }
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

public enum StagedArtifactDisposition
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Unmatched = 3,
    Orphan = 4
}

public sealed record StagedArtifactInventoryItem(
    string StorageKey,
    string ContentHash,
    long ContentLength,
    DateTimeOffset FirstSeenAtUtc,
    StagedArtifactDisposition Disposition,
    string ConcurrencyToken);

public sealed record IntakeQuarantineArtifact(
    string StorageKey,
    string ContentHash,
    long ContentLength);

public interface IIntakeQuarantineArtifactStore
{
    Task<IntakeQuarantineArtifact> StoreStreamAsync(
        Stream content,
        long contentLength,
        CancellationToken cancellationToken);

    Task VerifyAsync(
        IntakeQuarantineArtifact artifact,
        CancellationToken cancellationToken);
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

    async Task<StagedArtifactInventoryItem> StageAsync(
        Guid stagedReceiptId,
        string contentHash,
        ReadOnlyMemory<byte> content,
        DateTimeOffset firstSeenAtUtc,
        CancellationToken cancellationToken)
    {
        var storageKey = await StoreAsync(contentHash, content, cancellationToken);
        return new(
            storageKey,
            contentHash,
            content.Length,
            firstSeenAtUtc,
            StagedArtifactDisposition.Pending,
            string.Empty);
    }

    Task<StagedArtifactInventoryItem?> GetStagedAsync(
        string storageKey,
        CancellationToken cancellationToken) =>
        Task.FromResult<StagedArtifactInventoryItem?>(null);

    Task<IReadOnlyList<StagedArtifactInventoryItem>> ListStagedAsync(
        int maximumItems,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StagedArtifactInventoryItem>>([]);

    Task<StagedArtifactInventoryItem?> TrySetStagedDispositionAsync(
        string storageKey,
        string expectedConcurrencyToken,
        StagedArtifactDisposition disposition,
        CancellationToken cancellationToken) =>
        Task.FromResult<StagedArtifactInventoryItem?>(null);

    Task<bool> DeleteCompletedStagedAsync(
        string storageKey,
        string expectedConcurrencyToken,
        CancellationToken cancellationToken) =>
        Task.FromResult(false);
}

public sealed class IntakeArtifactIntegrityException()
    : Exception("The retained intake artifact failed integrity validation.");

public interface IIntakeReceiptStore
{
    Task<IntakeReceipt?> FindBySourceIdentityAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken);

    Task<IntakeReceipt> StoreAsync(IntakeReceiptDraft draft, CancellationToken cancellationToken);

    Task<IntakeReceipt> ReplaceEvaluationAsync(
        IntakeReceiptDraft draft,
        CancellationToken cancellationToken);
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

public sealed record ListIntakeQuery(
    ActionActor Actor,
    IntakeDecision? Decision,
    int Page,
    int PageSize);

public sealed record IntakeListPage(
    IReadOnlyList<IntakeReceiptSummary> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 1
        : (int)Math.Ceiling((double)TotalCount / PageSize);
}

public interface IListIntake
{
    Task<IntakeListPage> ExecuteAsync(
        ListIntakeQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record GetIntakeQuery(Guid ReceiptId, ActionActor Actor);

public interface IGetIntake
{
    Task<IntakeReceipt?> ExecuteAsync(
        GetIntakeQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record DownloadIntakeSourceQuery(Guid ReceiptId, ActionActor Actor);

public sealed record IntakeSourceDownload(
    ReadOnlyMemory<byte> Content,
    string FileName,
    string ContentType,
    long ContentLength,
    string Sha256);

public interface IDownloadIntakeSource
{
    Task<IntakeSourceDownload?> ExecuteAsync(
        DownloadIntakeSourceQuery query,
        CancellationToken cancellationToken = default);
}

public enum IntakeResolutionKind
{
    CorrectDraft,
    Block
}

public sealed record ResolveIntakeRequest(
    Guid ReceiptId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    IntakeResolutionKind Kind,
    InstructionDraft? CorrectedDraft);

public sealed record ReevaluateIntakeRequest(
    Guid ReceiptId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public sealed record AcceptIntakeRequest(
    Guid ReceiptId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    CaseType CaseType,
    string PrincipalCode,
    CaseCompleteness Completeness,
    Guid? StandaloneAuditEvidenceId = null,
    DateOnly? AcceptedInspectionDeadline = null);

public sealed record LinkIntakeRequest(
    Guid ReceiptId,
    Guid CaseId,
    long ExpectedIntakeVersion,
    long ExpectedCaseVersion,
    string EditLeaseToken,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public sealed record ReverseIntakeLinkRequest(
    Guid ReceiptId,
    Guid CaseId,
    long ExpectedIntakeVersion,
    long ExpectedCaseVersion,
    string EditLeaseToken,
    ActionActor Actor,
    string OperationKey,
    string Reason);

/// <summary>
/// The pipeline's automatic Image-intake association: a system-worker actor,
/// no staff edit lease, and the same serializable replay-protected
/// association write as the manual link. The store must enforce Image-intake
/// case eligibility inside the transaction.
/// </summary>
public sealed record AutomaticIntakeLinkRequest(
    Guid ReceiptId,
    Guid CaseId,
    long ExpectedCaseVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public interface IIntakeMutationStore
{
    Task<IntakeReceipt> ResolveAsync(
        ResolveIntakeRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);

    Task<IntakeReceipt> ScheduleReevaluationAsync(
        ReevaluateIntakeRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);

    Task LinkAsync(
        LinkIntakeRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);

    Task ReverseLinkAsync(
        ReverseIntakeLinkRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);

    Task AutoLinkAsync(
        AutomaticIntakeLinkRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);
}

public sealed class IntakeOperationConflictException()
    : Exception("The intake operation key was already used for different command details.");

public sealed class IntakeVersionConflictException()
    : Exception("The intake or case changed after it was loaded.");

public sealed class IntakeAssociationConflictException(string message) : Exception(message);

public interface IResolveIntake
{
    Task<IntakeReceipt> ExecuteAsync(
        ResolveIntakeRequest request,
        CancellationToken cancellationToken = default);
}

public interface IReevaluateIntake
{
    Task<IntakeReceipt> ExecuteAsync(
        ReevaluateIntakeRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAcceptIntake
{
    Task<CaseAcceptanceOutcome> ExecuteAsync(
        AcceptIntakeRequest request,
        CancellationToken cancellationToken);
}

public interface ILinkIntake
{
    Task ExecuteAsync(
        LinkIntakeRequest request,
        CancellationToken cancellationToken = default);
}

public interface IReverseIntakeLink
{
    Task ExecuteAsync(
        ReverseIntakeLinkRequest request,
        CancellationToken cancellationToken = default);
}
