using System.Data.Common;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

public static class IntakeEnvelopeLimits
{
    /// <summary>
    /// One file uploaded through the staff form or a public request link,
    /// which arrives inside one bounded multipart HTTP request.
    /// </summary>
    /// <remarks>
    /// Exactly 100 MiB, set by C07 item 5 (residual INTK-052) as the single
    /// per-file cap the manual and public channels share. This class is the
    /// one owner of that figure: host, ingress and per-request
    /// <c>DocumentRequests</c> settings may tighten it and may never raise it.
    ///
    /// The Provider API does not follow this cap. Its files arrive inline as
    /// base64 in one request body, so they are bounded by
    /// <see cref="MaximumProviderApiFileLength"/> instead.
    /// </remarks>
    public const int MaximumContentLength = 100 * 1024 * 1024;

    /// <summary>
    /// One received mailbox message, envelope and every attachment together.
    /// </summary>
    /// <remarks>
    /// A received instruction is not an uploaded file. The staff form takes
    /// one file, so <see cref="MaximumContentLength"/> bounds one file; an
    /// instruction email carries the covering message plus the 2–20+ documents
    /// and photographs of the job, and applying the one-file figure to the
    /// whole envelope refused real QDOS instructions outright — a 16.69 MB
    /// forward was rejected as <c>message_too_large</c> on 2026-08-05, against
    /// the 10 MiB one-file bound then in force, without ever being read.
    ///
    /// This bound is deliberately permissive rather than a capacity claim.
    /// Exchange Online will not carry a message anywhere near it, the reader
    /// still enforces its own nesting, entity and decoded-byte limits, and
    /// the poll materializes a message in memory — so the practical ceiling
    /// is far lower and is set by the Worker instance, not by this number.
    /// It exists so that a genuine instruction is read and decided rather
    /// than refused at the door.
    /// </remarks>
    public const long MaximumMailboxContentLength = 750L * 1024 * 1024;

    /// <summary>
    /// The most files one staff Upload submission may select as a single
    /// group. Mirrors the 2–20+ documents a real QDOS instruction envelope
    /// carries (see <see cref="MaximumMailboxContentLength"/>), so a staff
    /// member reproducing that job manually is not capped below it.
    ///
    /// C07 item 5 (residual INTK-052) retained 20 unchanged while the per-file
    /// cap rose, so the file count and the byte budget are now independent
    /// facts rather than two halves of one multiplication.
    /// </summary>
    public const int MaximumBatchFileCount = 20;

    /// <summary>
    /// One Provider API submission, decoded: every attached file together.
    ///
    /// It is not the mailbox bound, because the whole envelope arrives inline
    /// as base64 in one request body and is held in memory to be decoded. It
    /// is not the manual per-file bound either, because a real instruction
    /// carries the documents and photographs of a job: the mailbox note above
    /// records a genuine 16.69 MB QDOS instruction, so this is set comfortably
    /// above that. C07 item 5 (residual INTK-052) left it unchanged.
    /// </summary>
    public const int MaximumProviderApiEnvelopeLength = 30 * 1024 * 1024;

    /// <summary>
    /// One file inside a Provider API submission.
    /// </summary>
    /// <remarks>
    /// The channel's decoded envelope is the effective ceiling for one file as
    /// well as for the batch, so the per-file bound is the envelope itself. It
    /// is stated separately, and used separately, so that the Provider API can
    /// never inherit the manual channel's larger
    /// <see cref="MaximumContentLength"/> the next time that cap moves
    /// (C07 item 5, residual INTK-052).
    /// </remarks>
    public const int MaximumProviderApiFileLength = MaximumProviderApiEnvelopeLength;

    /// <summary>
    /// The request body that carries it. Base64 costs a third again, plus the
    /// declared fields and JSON structure around them.
    /// </summary>
    public const int MaximumProviderApiRequestLength = 42 * 1024 * 1024;

    /// <summary>
    /// The multipart request body budget for one whole Upload submission.
    /// </summary>
    /// <remarks>
    /// Pinned by C07 item 5 (residual INTK-052) at exactly 200 MiB plus the
    /// fixed multipart overhead, and deliberately not derived from
    /// <see cref="MaximumBatchFileCount"/> times
    /// <see cref="MaximumContentLength"/>: raising the per-file cap while
    /// deriving this figure would hand one request a body budget far past what
    /// the Web instance can hold. Every file may be at its individual cap; the
    /// batch as a whole may not, and is refused by this budget first.
    /// </remarks>
    public const long MaximumBatchContentLength = (200L * 1024 * 1024) + MultipartOverhead;

    /// <summary>
    /// Fixed slack for multipart boundaries and non-file form fields,
    /// independent of how many files are in the batch.
    /// </summary>
    public const long MultipartOverhead = 64 * 1024;
}

/// <summary>
/// What processing did with a received source.
/// </summary>
/// <remarks>
/// There is no decision meaning "a human has not pressed the button yet".
/// The requirements are explicit that definitive authorised intake
/// creates exactly one instructed Case idempotently and that the allocation
/// decision adds no universal manual acceptance gate, and the operator notes
/// send only ambiguous provider, instruction-type or case evidence — and any
/// unidentified e-mail — to <see cref="NeedsSorting"/>. So a definitive
/// instruction is <see cref="CaseCreated"/> with the reference already
/// allocated, ambiguity is <see cref="NeedsSorting"/>, and a reasoned refusal
/// is <see cref="BlockedIntake"/>.
///
/// <see cref="CaseCreated"/> is a processing decision — the instruction is
/// definitive enough to allocate on — not proof that a Case exists. The
/// allocation/link projection alone says whether one does.
/// </remarks>
public enum IntakeDecision
{
    CaseCreated,
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
    SystemDefault,

    /// <summary>
    /// A value the authenticated Principal stated over the Provider API
    /// (API-01). It is neither something a document said nor something a person
    /// here keyed, and the case record must not report it as either.
    /// </summary>
    ProviderDeclaration
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
    Automation,
    ProviderApi
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

/// <summary>
/// What kind of place in a source a locator names. One enumeration, because a
/// candidate is read from exactly one kind of place: a whole document, a page,
/// a table cell, a PDF form field, a bounded text region, or a part of an
/// e-mail message.
/// </summary>
public enum IntakeLocatorKind
{
    Document,
    Page,
    TableCell,
    FormField,
    Region,
    MessagePart
}

/// <summary>
/// Which part of a transported message a fragment came from. An outer
/// transport envelope, the message a sender actually wrote, the history quoted
/// beneath it and an attachment are four different pieces of evidence about
/// four possibly different senders, and collapsing them is how a forwarding
/// desk comes to be recorded as the instructing party.
/// </summary>
public enum IntakeMessagePart
{
    None,
    OuterTransport,
    CurrentBody,
    QuotedHistory,
    Attachment
}

/// <summary>
/// The smallest useful statement of WHERE in a source something was read.
///
/// One locator serves every shape the intake pipeline actually meets — page,
/// table cell, PDF form field, bounded text region and message part — because a
/// second document model is exactly what the stream is forbidden to grow. The
/// fields a given kind does not use stay null; nothing infers a page from a
/// cell or a cell from a label.
/// </summary>
/// <param name="Table">
/// The table's own identity within its source (its ordinal, as the reader met
/// it). Paired with <paramref name="Row"/> and <paramref name="Column"/> it
/// makes <see cref="Cell"/>, the frozen projection's cell string.
/// </param>
/// <param name="Region">
/// A bounded region of the source text or page, written as the reader chose to
/// bound it. Never a global positional line number: a region is meaningful only
/// against the source it names.
/// </param>
/// <param name="Occurrence">
/// Which repetition of the same evidence this is within its source, counted
/// from zero. Identical bytes are evidence twice when a document says a thing
/// twice.
/// </param>
public sealed record IntakeSourceLocator(
    IntakeLocatorKind Kind,
    int? Page = null,
    int? Table = null,
    int? Row = null,
    int? Column = null,
    string? FormField = null,
    string? Region = null,
    IntakeMessagePart MessagePart = IntakeMessagePart.None,
    int Occurrence = 0,
    string? Sha256 = null,
    string? DocumentRole = null)
{
    /// <summary>
    /// The cell string the frozen <see cref="SourceFieldCandidate"/> projection
    /// carries, or null when this locator does not name a cell. Written in one
    /// place so a store and a page cannot spell it differently.
    /// </summary>
    public string? Cell => Table is null || Row is null || Column is null
        ? null
        : $"T{Table}R{Row}C{Column}";

    public static IntakeSourceLocator ForPage(int page, int occurrence = 0) =>
        new(IntakeLocatorKind.Page, Page: page, Occurrence: occurrence);

    public static IntakeSourceLocator ForCell(
        int table,
        int row,
        int column,
        int? page = null,
        int occurrence = 0) =>
        new(
            IntakeLocatorKind.TableCell,
            Page: page,
            Table: table,
            Row: row,
            Column: column,
            Occurrence: occurrence);

    public static IntakeSourceLocator ForFormField(
        string formField,
        int? page = null,
        string? region = null,
        int occurrence = 0) =>
        new(
            IntakeLocatorKind.FormField,
            Page: page,
            FormField: formField,
            Region: region,
            Occurrence: occurrence);

    public static IntakeSourceLocator ForMessagePart(
        IntakeMessagePart messagePart,
        string? region = null,
        int occurrence = 0) =>
        new(
            IntakeLocatorKind.MessagePart,
            Region: region,
            MessagePart: messagePart,
            Occurrence: occurrence);
}

/// <param name="Locator">
/// Where in the source the fragment was read. Null when the reader could offer
/// nothing better than the whole source, which is honest rather than guessed.
/// </param>
public sealed record IntakeContentFragment(
    IntakeEvidenceSource Source,
    string SourceLabel,
    string Text,
    IntakeSourceLocator? Locator = null);

public enum IntakeSenderIdentityKind
{
    Transport,
    AttachedOriginal,
    InlineForwardedOriginal
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
    string ReaderVersion = "1",
    IReadOnlyList<IntakeAttachmentDescriptor>? Attachments = null)
{
    public IReadOnlyList<IntakeAssetCandidate> AssetCandidates => Assets ?? [];

    public IReadOnlyList<ScannedPdfOcrCandidate> ScannedPdfPages => OcrCandidates ?? [];

    public IReadOnlyList<IntakeAttachmentDescriptor> AttachmentRecords => Attachments ?? [];
}

public sealed record IntakeAttachmentDescriptor(
    string FileName,
    string MediaType,
    long? ContentLength,
    int Ordinal = 0,
    string? SourceLabel = null);

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

/// <param name="Value">
/// The value as the extraction engine bounded it — trimmed, with runs of
/// whitespace collapsed. It is not a normalized value: no field is
/// canonicalized here.
/// </param>
/// <param name="RawValue">
/// The source text exactly as it was printed, before the engine trimmed or
/// collapsed anything. Null when it is identical to <paramref name="Value"/>.
/// Normalization never destroys the source value, so the raw form is what an
/// operator reviewing a conflict is shown.
/// </param>
public sealed record InstructionFieldCandidate(
    string Value,
    IntakeEvidenceSource Source,
    string SourceLabel,
    IntakeSourceLocator? Locator = null,
    string? RawValue = null)
{
    /// <summary>The printed value: the raw text when one was kept, else the bounded value.</summary>
    public string SourceValue => RawValue ?? Value;
}

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
    DateOnly? InspectionDate = null,
    // Below here: fields no extraction policy reads today. A provider that
    // declares its instruction over the API (API-01) states them directly, and
    // the draft is the one carrier every downstream owner already reads, so
    // they belong here rather than in a second pre-case record.
    string? VehicleMileageUnit = null,
    string? VatStatus = null,
    string? ClaimantAddress = null,
    string? ClaimantContactNumber = null,
    string? FileHandlerName = null,
    string? FileHandlerEmailAddress = null,
    string? FileHandlerPhoneNumber = null,
    string? Notes = null);

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
    long? ManualAssociationVersion = null,
    MailClassificationResult? MailClassificationDecision = null,
    CaseMatchEvaluationResult? CaseMatchDecision = null,
    IntakeAllocationState? AllocationState = null,
    string? AcceptedCaseReference = null,
    string? ManualLinkedCaseReference = null,
    ActorKind? ManualAssociationActorKind = null)
{
    public IReadOnlyList<IntakeAssetRecord> AssetRecords => Assets ?? [];

    public IReadOnlyList<ScannedPdfOcrCandidate> ScannedPdfPages => OcrCandidates ?? [];

    public Guid? CurrentCaseId =>
        ManualAssociationVersion is null ? AcceptedCaseId : ManualLinkedCaseId;

    /// <summary>
    /// Whether the current case association was an explicit staff decision
    /// rather than the pipeline's automatic one — the automatic paths record
    /// their association under a system-worker actor. Owned here, beside the
    /// rest of the association derivation, so no surface re-derives
    /// provenance from raw actor identity.
    /// </summary>
    public bool AssociationWasStaffDecision =>
        CurrentCaseId is not null && ManualAssociationActorKind == ActorKind.Staff;

    /// <summary>
    /// Whether unlinking this receipt cancels the case it is currently linked
    /// to. True when that case is the one this receipt's own acceptance
    /// created: unlinking then takes the case's only source away. A receipt
    /// since relinked to some other case is not that case's source, so
    /// unlinking it leaves that case alone. Derived here beside the rest of the
    /// association rules so no surface works it out again from raw fields
    /// (INTK-029).
    /// </summary>
    public bool UnlinkCancelsCase =>
        AcceptedCaseId is not null && AcceptedCaseId == CurrentCaseId;

    public string? CurrentCaseReference =>
        ManualAssociationVersion is null
            ? AcceptedCaseReference
            : ManualLinkedCaseReference ?? AcceptedCaseReference;
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
    MailRouteEvaluationResult? MailRouteDecision = null,
    MailClassificationResult? MailClassificationDecision = null,
    CaseMatchEvaluationResult? CaseMatchDecision = null,
    IReadOnlyList<IntakeSearchDocument>? SearchDocuments = null)
{
    public IReadOnlyList<IntakeAssetRecord> AssetRecords => Assets ?? [];

    public IReadOnlyList<ScannedPdfOcrCandidate> ScannedPdfPages => OcrCandidates ?? [];

    public IReadOnlyList<IntakeSearchDocument> SearchDocumentRecords => SearchDocuments ?? [];
}

/// <summary>
/// One queryable projection of text the canonical intake reader already produced.
/// A null attachment name denotes the root message body; named rows are attachment
/// content. Empty text records that an attachment was retained but not searchable.
/// </summary>
public sealed record IntakeSearchDocument(
    string SourceLabel,
    string? AttachmentFileName,
    string? Text,
    int? AttachmentOrdinal = null)
{
    public bool IsSearchable => !string.IsNullOrWhiteSpace(Text);
}

/// <summary>
/// How much received material is waiting for a person.
/// </summary>
/// <remarks>
/// Both counts exclude receipts that already produced a case. Before this,
/// neither the counts nor the filtered list applied any such filter, so every
/// intake count was cumulative for all time and creating a case from a receipt
/// never decremented anything.
/// </remarks>
public sealed record IntakeQueueCounts(int NeedsSorting, int BlockedIntake = 0);

/// <summary>
/// One row of the Inbox.
/// </summary>
/// <remarks>
/// Sender and subject are what an operator recognises a message by. The row
/// used to carry only <c>SourceFileName</c>, which for mailbox material is a
/// stored hex <c>.eml</c> name — an identifier, not a description. Where a
/// manual upload genuinely has no sender or subject, the file name is what
/// there is, and the surface says "Manual upload" rather than inventing one.
///
/// <paramref name="CaseReference"/> is present when this message produced or
/// was linked to a case, so the row can say which one instead of leaving the
/// operator to open it and find out.
/// </remarks>
public sealed record IntakeReceiptSummary(
    Guid Id,
    string SourceFileName,
    DateTimeOffset ReceivedAtUtc,
    IntakeDecision Decision,
    string? FailureReason,
    string? Sender = null,
    string? Subject = null,
    Guid? CaseId = null,
    string? CaseReference = null,
    IntakeAllocationState? AllocationState = null);

public sealed record InstructionExtractionResult(
    InstructionPolicyApplicability Applicability,
    IReadOnlyList<IntakeEvidence> Evidence,
    IReadOnlyList<InstructionReviewField> Fields,
    InstructionDraft? InstructionDraft,
    IReadOnlyList<string> MissingFields,
    string PolicyKey,
    int PolicyVersion);

public sealed record EstablishedPrincipalContext(
    string PrincipalCode,
    string PolicyKey,
    int PolicyVersion);

public interface IInstructionExtractionPolicy
{
    string PrincipalCode { get; }

    InstructionExtractionResult Extract(
        IntakeSourceReadResult readResult,
        DateTimeOffset processedAtUtc,
        EstablishedPrincipalContext principalContext);
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

    /// <summary>
    /// Faults worth a bounded retry rather than an immediate terminal outcome:
    /// the named intake conflicts, the dependency-unavailable fault adapters
    /// translate to, and raw I/O, timeout and database faults, including any
    /// of those wrapped by another exception, which is how EF surfaces a
    /// deadlock or dropped connection. Retryable processing must remain in
    /// processing rather than allocating a terminal decision or an
    /// Unidentified reference on the first attempt.
    /// </summary>
    public static bool IsTransientFailure(Exception exception) =>
        exception is IntakeArtifactRetentionException
            or IntakeOperationConflictException
            or IntakeVersionConflictException
            or IntakeDependencyUnavailableException
            or IOException
            or TimeoutException
            or DbException
        || (exception.InnerException is { } inner && IsTransientFailure(inner));
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
    Task<IntakeReceipt?> FindBySourceIdentityAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken) =>
        Task.FromResult<IntakeReceipt?>(null);

    Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One page of received items, newest first, filtered and counted at the store.
    /// </summary>
    /// <remarks>
    /// Paging belongs here rather than above it. The port used to return a
    /// hard-capped list that the use case then paged inside, so the reported total
    /// was the cap: at twenty-five a page exactly four pages existed however much
    /// had been received, and everything older was unreachable.
    /// </remarks>
    Task<IntakeListPage> ListAsync(
        IntakeDecision? decision,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// One keyset page of received items, newest first: strictly after
    /// <paramref name="after"/> in (ReceivedAtUtc DESC, Id DESC) order, or from
    /// the newest row when it is null.
    ///
    /// Offset paging cannot be made stable for a caller that pages over time. A
    /// receipt arriving while a connector reads page after page shifts every
    /// later row by one, so a row is silently skipped; a receipt resolved out of
    /// the filter shifts them back and a row is delivered twice. The sort key
    /// plus the id names an exact row instead of a position in a list that
    /// moves. <see cref="ListAsync"/> stays the right shape for a staff screen,
    /// which wants a total and a page number.
    /// </summary>
    Task<KeysetPage<IntakeReceiptSummary>> ListByCursorAsync(
        IntakeDecision? decision,
        KeysetPosition? after,
        int limit,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "This intake receipt query does not support keyset continuation.");

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

/// <param name="ContentType">
/// The stored source media type. Presentation is each endpoint's decision:
/// the Source download forces an octet-stream attachment regardless, and the
/// image view serves only a true image type inline.
/// </param>
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
    DateOnly? AcceptedInspectionDeadline = null,
    Guid? AllocationAttemptId = null,
    DateTimeOffset? AllocationCompletedAtUtc = null);

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

public sealed class IntakeDependencyUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class IntakeAssociationConflictException(string message) : Exception(message);

/// <summary>
/// API-01 is create-only: a declared provider instruction whose identity facts
/// match existing Case work is refused rather than allocated or associated. The
/// envelope is still durably received — the refusal happens in processing, so
/// the submission terminates under this one code and no Case, PO, association
/// or Case mutation is produced. Updating an existing Case through the API
/// awaits a separate authorised contract (FRD-09, operator decision 2026-09-02).
/// </summary>
public sealed class ProviderExistingCaseMatchException()
    : Exception("The provider submission matches existing Case work; API-01 cannot update it.")
{
    public const string FailureCode = "provider_existing_case_match";
}

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

public enum SourceCandidateDisposition { Usable, Missing, Ambiguous, Conflicting }
public sealed record SourceFieldCandidate(
    Guid Id, Guid ReceiptId, Guid? DocumentId, Guid? DocumentVersionId, Guid? IntakeAssetId, string Sha256,
    int Occurrence, string DocumentRole, string PartyRole, string ReferenceRole,
    string Field, string? RawValue, string? NormalizedValue, string? Unit, string? Currency,
    string SourceLabel, int? Page, string? Cell, string? FormField, string? Region,
    string ReaderVersion, string PolicyVersion, SourceCandidateDisposition Disposition);
public interface ISourceCandidateQueries
{
    Task<IReadOnlyList<SourceFieldCandidate>> GetAsync(
        ActionActor actor, Guid receiptId, Guid? documentVersionId, Guid? intakeAssetId,
        CancellationToken cancellationToken);
}
