using Pegasus.Core.Actors;
using Pegasus.Core.Intake;
using Pegasus.Core.Identity;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;

namespace Pegasus.Core.Triage;

public enum TriageState
{
    Open,
    AwaitingInformation,
    FindingRecorded,
    Completed,
    Cancelled
}

public enum RoadworthinessFinding
{
    Roadworthy,
    Unroadworthy
}

public enum AssessmentFinding
{
    Repairable,
    TotalLoss
}

/// <summary>
/// The accepted route evidence a Triage Case was opened from. A Triage Case a
/// member of staff created directly has none.
/// </summary>
public sealed record TriageOrigin(
    Guid ReceiptId,
    IntakeSourceIdentity SourceIdentity,
    string SourceHash,
    Guid EvaluationRevisionId);

/// <summary>
/// A Triage Case: a Case of type <see cref="CaseType.Triage"/> whose
/// specialised lifecycle lives in its Triage subtype. <see cref="CaseId"/> is
/// the Case identity, <see cref="Reference"/> its <c>t.</c> Case/PO and
/// <see cref="PrincipalId"/> its established Principal.
/// <see cref="LinkedInstructionCaseId"/> is the later definitive instructed
/// Case it is associated with, if any.
/// </summary>
public sealed record TriageRecord(
    Guid CaseId,
    TriageOrigin? Origin,
    string NormalizedVehicleRegistration,
    TriageState State,
    Guid? AssigneeId,
    Guid? LinkedInstructionCaseId,
    long Version,
    string Reference,
    Guid PrincipalId);


public sealed class TriageVersionConflictException(
    Guid caseId,
    long expectedVersion,
    long actualVersion)
    : InvalidOperationException(
        $"Triage '{caseId}' is at version {actualVersion}, not expected version {expectedVersion}.")
{
    public Guid CaseId { get; } = caseId;

    public long ExpectedVersion { get; } = expectedVersion;

    public long ActualVersion { get; } = actualVersion;
}

public sealed class TriageOperationConflictException(Guid caseId, string operationKey)
    : InvalidOperationException(
        $"Operation '{operationKey}' was already applied to triage '{caseId}' with different inputs.")
{
    public Guid CaseId { get; } = caseId;

    public string OperationKey { get; } = operationKey;
}
public sealed class TriageResponseEvidenceAlreadyLinkedException(
    Guid caseId,
    Exception? innerException = null)
    : InvalidOperationException(
        $"Triage '{caseId}' already has current response evidence.",
        innerException)
{
    public Guid CaseId { get; } = caseId;
}




public sealed record CreateTriageFromIntakeRequest(
    TriageOrigin Origin,
    string NormalizedVehicleRegistration,
    IntakeEvidence AcceptedMatchEvidence,
    ActionActor Actor,
    string OperationKey);

public sealed record TriageMutationRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason)
{
    public string EditLeaseToken { get; init; } = string.Empty;
}

public sealed record AssignTriageRequest(
    Guid CaseId,
    long ExpectedVersion,
    Guid AssigneeId,
    ActionActor Actor,
    string OperationKey,
    string Reason)
{
    public string EditLeaseToken { get; init; } = string.Empty;
}

/// <summary>
/// Adds one operator note to the Triage's permanent history.
/// </summary>
/// <remarks>
/// A note is not a second kind of record: it is an entry in the same
/// attributed, versioned, replay-safe history every state change writes, so it
/// carries an expected version, an operation key and its text, and it is
/// appended — never edited and never replaced.
/// </remarks>
public sealed record AddTriageNoteRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Note)
{
    public string EditLeaseToken { get; init; } = string.Empty;
}

public static class TriageNotes
{
    /// <summary>
    /// The history event type an operator note is written as, matching the
    /// Case timeline's own name for the same thing.
    /// </summary>
    public const string EventType = "operator_note";

    /// <summary>
    /// A note is the entry's reason, so it is bounded by what a Triage history
    /// entry holds — the same 500 characters every other entry's reason is
    /// bounded by, and what `TriageHistory.Reason` stores. Stating a larger
    /// bound here would accept a note the store then refuses. A longer note
    /// needs that column widened first.
    /// </summary>
    public const int MaximumLength = TriageReasonLength;

    internal const int TriageReasonLength = 500;
}

public interface IAddTriageNote
{
    Task<TriageRecord> ExecuteAsync(
        AddTriageNoteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record RecordTriageFindingRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    RoadworthinessFinding? Roadworthiness,
    AssessmentFinding? Assessment,
    Guid? SupersedesFindingId)
{
    public string EditLeaseToken { get; init; } = string.Empty;
}

/// <summary>
/// Associates a Triage Case (<see cref="CaseId"/>) with, or removes its
/// association from, a later definitive instructed Case
/// (<see cref="InstructionCaseId"/>). <see cref="ExpectedCaseVersion"/> and
/// <see cref="CaseEditLeaseToken"/> are the instructed Case's.
/// </summary>
public sealed record TriageCaseLinkRequest(
    Guid CaseId,
    Guid InstructionCaseId,
    long ExpectedTriageVersion,
    long ExpectedCaseVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string CaseEditLeaseToken)
{
    public string EditLeaseToken { get; init; } = string.Empty;
}

public sealed record TriageResponseEvidenceLinkRequest(
    Guid CaseId,
    Guid PollOutcomeId,
    Guid SentEvidenceId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason)
{
    public string EditLeaseToken { get; init; } = string.Empty;
}

public sealed record TriageResponseEvidenceUnlinkRequest(
    Guid CaseId,
    Guid SentEvidenceId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason)
{
    public string EditLeaseToken { get; init; } = string.Empty;
}

public interface ICreateTriageFromIntake
{
    Task<TriageRecord> ExecuteAsync(
        CreateTriageFromIntakeRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Whether a receipt's Principal is established, and which it is. A request
/// is classified into a Triage Case only once its Principal is established, so
/// intake asks this before it opens one: without an established Principal the
/// request does not qualify and is held as Unidentified.
/// </summary>
public interface ITriagePrincipalGate
{
    /// <summary>
    /// The one active Principal the receipt's instruction established, or null
    /// when the receipt established none.
    /// </summary>
    Task<Guid?> GetEstablishedPrincipalIdAsync(Guid receiptId, CancellationToken cancellationToken);
}

public interface IAssignTriage
{
    Task<TriageRecord> ExecuteAsync(AssignTriageRequest request, CancellationToken cancellationToken);
}

public interface IUnassignTriage
{
    Task<TriageRecord> ExecuteAsync(TriageMutationRequest request, CancellationToken cancellationToken);
}

public interface IRecordTriageFinding
{
    Task<TriageRecord> ExecuteAsync(
        RecordTriageFindingRequest request,
        CancellationToken cancellationToken);
}

public interface ISupersedeTriageFinding
{
    Task<TriageRecord> ExecuteAsync(
        RecordTriageFindingRequest request,
        CancellationToken cancellationToken);
}

public interface ILinkTriageResponseEvidence
{
    Task ExecuteAsync(
        TriageResponseEvidenceLinkRequest request,
        CancellationToken cancellationToken);
}

public interface IUnlinkTriageResponseEvidence
{
    Task ExecuteAsync(
        TriageResponseEvidenceUnlinkRequest request,
        CancellationToken cancellationToken);
}
public interface IAwaitTriageInformation
{
    Task<TriageRecord> ExecuteAsync(TriageMutationRequest request, CancellationToken cancellationToken);
}


public interface ICompleteTriage
{
    Task<TriageRecord> ExecuteAsync(TriageMutationRequest request, CancellationToken cancellationToken);
}

public interface ICancelTriage
{
    Task<TriageRecord> ExecuteAsync(TriageMutationRequest request, CancellationToken cancellationToken);
}

public interface IReopenTriage
{
    Task<TriageRecord> ExecuteAsync(TriageMutationRequest request, CancellationToken cancellationToken);
}

public interface ILinkTriageCase
{
    Task ExecuteAsync(TriageCaseLinkRequest request, CancellationToken cancellationToken);
}

public interface IUnlinkTriageCase
{
    Task ExecuteAsync(TriageCaseLinkRequest request, CancellationToken cancellationToken);
}

public sealed record TriageFinding(
    Guid Id,
    Guid CaseId,
    RoadworthinessFinding? Roadworthiness,
    AssessmentFinding? Assessment,
    Guid? SupersedesFindingId,
    string Actor,
    string OperationKey,
    string Reason,
    DateTimeOffset RecordedAtUtc);

public sealed record TriageResponseEvidenceLink(
    Guid CaseId,
    Guid SentEvidenceId,
    string Actor,
    string OperationKey,
    string Reason,
    DateTimeOffset LinkedAtUtc);

public sealed record TriageResponseEvidenceCandidate(
    Guid PollOutcomeId,
    Guid SentEvidenceId,
    string MailboxAddress,
    string SentFolderIdentity,
    string ImmutableItemIdentity,
    string InternetMessageIdentity,
    string ConversationIdentity,
    string ReplyChainIdentity,
    DateTimeOffset SentAtUtc,
    DateTimeOffset DiscoveredAtUtc);

public sealed record TriageSentEvidenceReference(
    Guid SentEvidenceId,
    string MessageIdentity);

public sealed record TriageHistoryEntry(
    Guid Id,
    Guid CaseId,
    string EventType,
    string Actor,
    string ActorKind,
    string Reason,
    string OperationKey,
    DateTimeOffset OccurredAtUtc,
    long BeforeVersion,
    long AfterVersion,
    TriageState AfterState,
    Guid? AfterAssigneeId,
    Guid? AfterLinkedInstructionCaseId)
{
    /// <summary>
    /// The operator-facing name for <see cref="Actor"/> — a raw staff subject id
    /// on every current mutation path — resolved by <c>GetTriage</c>. Defaults to
    /// the same honest fallback a missing account gets, so a caller that forgets
    /// to populate it never renders the raw subject id.
    /// </summary>
    public string ActorDisplayName { get; init; } = ActorDisplayNames.UnknownStaff;
}

/// <summary>
/// A Triage queue row. <see cref="Reference"/> is the Triage Case's own
/// <c>t.</c> Case/PO; <see cref="ClaimNumber"/> is the originating instruction
/// draft's provider claim number, which is a fact about the sender and not an
/// identifier of this Triage Case.
/// </summary>
public sealed record TriageSummary(
    Guid CaseId,
    string NormalizedVehicleRegistration,
    TriageState State,
    Guid? AssigneeId,
    Guid? LinkedInstructionCaseId,
    DateTimeOffset CreatedAtUtc,
    long Version,
    string Reference,
    string? Provider,
    string? ClaimNumber = null,
    Guid? PrincipalId = null);

public sealed record TriageDetail(
    TriageRecord Record,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<TriageFinding> Findings,
    IReadOnlyList<TriageResponseEvidenceLink> ResponseEvidence,
    IReadOnlyList<TriageHistoryEntry> History,
    IReadOnlyList<TriageResponseEvidenceCandidate> ResponseEvidenceCandidates,
    /// <summary>
    /// The code of the Triage Case's Principal, read in the same round trip as
    /// the record.
    /// </summary>
    string? PrincipalCode = null)
{
    /// <summary>The Triage Case's files: its standard Case documents.</summary>
    public IReadOnlyList<CaseDocument> Documents { get; init; } = [];

    /// <summary>The Triage Case's Box folder custody, as every Case has one.</summary>
    public CaseCustodyState CustodyState { get; init; } = CaseCustodyState.Pending;

    /// <summary>The Box folder of the Triage Case once custody confirmed it.</summary>
    public string? CustodyFolderRemoteId { get; init; }
}

/// <summary>
/// A decoded keyset position in the Triage list: the newest-first order is
/// <c>CreatedAtUtc</c> descending with the Case identity as the tie-break, so a
/// position is exactly that pair. Both are absent on the first page. This is
/// the store's own currency — the opaque cursor that carries it between
/// requests is minted above the store, by <see cref="ListTriagePage"/>.
/// </summary>
public sealed record TriageListPosition(DateTimeOffset CreatedAtUtc, Guid CaseId);

/// <summary>
/// One keyset page and the position the next page continues from. A null
/// <see cref="NextPosition"/> means this page reached the end.
/// </summary>
public sealed record TriageListSlice(
    IReadOnlyList<TriageSummary> Items,
    TriageListPosition? NextPosition);

public interface ITriageQueries
{
    Task<IReadOnlyList<TriageSummary>> ListAsync(
        TriageState? state,
        CancellationToken cancellationToken);

    Task<int> CountAsync(TriageState? state, CancellationToken cancellationToken);

    /// <summary>
    /// The keyset continuation behind the Triage list: at most
    /// <paramref name="limit"/> rows strictly after <paramref name="after"/> in
    /// the newest-first order, with the position the caller continues from. The
    /// database applies both the filter and the bound, so a later page never
    /// reads the rows before it and a row inserted between requests never
    /// shifts a page boundary.
    /// </summary>
    Task<TriageListSlice> ListPageAsync(
        TriageState? state,
        TriageListPosition? after,
        int limit,
        CancellationToken cancellationToken) =>
        Task.FromException<TriageListSlice>(
            new NotSupportedException("Triage keyset continuation is not available."));

    Task<TriageDetail?> GetAsync(Guid caseId, CancellationToken cancellationToken);

    /// <summary>
    /// The Triage Case this receipt opened, if it opened one. Mirrors
    /// <c>IImageIntakeQueries.GetByOriginReceiptAsync</c>: an origin receipt
    /// has at most one, and the Unidentified supersession rule needs to ask.
    /// </summary>
    Task<TriageSummary?> GetByOriginReceiptAsync(
        Guid originReceiptId,
        CancellationToken cancellationToken);
}

public interface ITriageResponseEvidenceCandidateQueries
{
    Task<IReadOnlyList<TriageSentEvidenceReference>> ListSentEvidenceReferencesAsync(
        Guid caseId,
        int maximumResults,
        CancellationToken cancellationToken);
}

/// <summary>
/// The historical post-operation result for an exact committed request.
/// A replay probe returns <see langword="null"/> only for an unseen operation key;
/// a committed key with a different request fingerprint throws
/// <see cref="TriageOperationConflictException"/>.
/// </summary>
public sealed record TriageOperationReplay(TriageRecord Result);

/// <summary>
/// An automatic association candidate: the Triage Case (<see cref="CaseId"/>
/// at <see cref="TriageVersion"/>) and the one definitive instructed Case
/// (<see cref="InstructionCaseId"/> at <see cref="InstructionCaseVersion"/>)
/// the Principal's match policy identifies.
/// </summary>
public sealed record TriageCaseLinkCandidate(
    Guid CaseId, long TriageVersion, Guid InstructionCaseId, long InstructionCaseVersion,
    string MatchPolicyKey, int MatchPolicyVersion);

public sealed record TriageCasePairingResult(
    int Candidates, int Linked, int Failures, string? FirstFailure = null);

public interface ITriageCasePairing
{
    Task<TriageCasePairingResult> PairTriageAsync(Guid triageCaseId, CancellationToken cancellationToken);
    Task<TriageCasePairingResult> PairAcceptedCaseAsync(Guid caseId, CancellationToken cancellationToken);
    Task<TriageCasePairingResult> ReconcileAsync(int maximumItems, CancellationToken cancellationToken);
}

/// <summary>
/// Persists triage lifecycle mutations. Implementations must enforce the supplied version
/// and operation key atomically, because the aggregate is read for transition validation
/// before each mutation. Replay probes must verify the complete request fingerprint and
/// return the historical post-operation result.
/// </summary>
public interface ITriageStore : ITriageQueries, ITriageResponseEvidenceCandidateQueries
{
    Task<IReadOnlyList<TriageCaseLinkCandidate>> ListAutomaticLinkCandidatesAsync(
        Guid? triageCaseId, Guid? instructionCaseId, int maximumItems, CancellationToken cancellationToken);

    Task<bool> LinkAutomaticallyAsync(
        TriageCaseLinkCandidate candidate, ActionActor actor, CancellationToken cancellationToken);

    Task<TriageOperationReplay?> ProbeRecordFindingReplayAsync(
        RecordTriageFindingRequest request,
        CancellationToken cancellationToken);

    Task<TriageOperationReplay?> ProbeSupersedeFindingReplayAsync(
        RecordTriageFindingRequest request,
        CancellationToken cancellationToken);

    Task<TriageOperationReplay?> ProbeStateChangeReplayAsync(
        TriageMutationRequest request,
        TriageState targetState,
        CancellationToken cancellationToken);

    Task<TriageOperationReplay?> ProbeLinkResponseEvidenceReplayAsync(
        TriageResponseEvidenceLinkRequest request,
        CancellationToken cancellationToken);

    Task<TriageOperationReplay?> ProbeUnlinkResponseEvidenceReplayAsync(
        TriageResponseEvidenceUnlinkRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// The house probe-then-write pair for a note, so a retried note append
    /// returns the committed result instead of writing the same note twice.
    /// </summary>
    Task<TriageOperationReplay?> ProbeAddNoteReplayAsync(
        AddTriageNoteRequest request,
        CancellationToken cancellationToken) =>
        Task.FromException<TriageOperationReplay?>(
            new NotSupportedException("Triage note replay probing is not available."));

    Task<TriageRecord> AddNoteAsync(
        AddTriageNoteRequest request,
        CancellationToken cancellationToken) =>
        Task.FromException<TriageRecord>(
            new NotSupportedException("Triage notes are not available."));

    Task<TriageRecord> CreateAsync(
        CreateTriageFromIntakeRequest request,
        CancellationToken cancellationToken);

    Task<TriageRecord> AssignAsync(
        AssignTriageRequest request,
        CancellationToken cancellationToken);

    Task<TriageRecord> UnassignAsync(
        TriageMutationRequest request,
        CancellationToken cancellationToken);

    Task<TriageRecord> RecordFindingAsync(
        RecordTriageFindingRequest request,
        CancellationToken cancellationToken);

    Task<TriageRecord> SupersedeFindingAsync(
        RecordTriageFindingRequest request,
        CancellationToken cancellationToken);

    Task LinkResponseEvidenceAsync(
        TriageResponseEvidenceLinkRequest request,
        CancellationToken cancellationToken);

    Task UnlinkResponseEvidenceAsync(
        TriageResponseEvidenceUnlinkRequest request,
        CancellationToken cancellationToken);

    Task<TriageRecord> ChangeStateAsync(
        TriageMutationRequest request,
        TriageState targetState,
        CancellationToken cancellationToken);

    Task LinkCaseAsync(TriageCaseLinkRequest request, CancellationToken cancellationToken);

    Task UnlinkCaseAsync(TriageCaseLinkRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// "Assign to me" (Work Centre P8) on an unassigned Triage: the ordinary assignment
/// with the actor as the assignee, carrying the same version, lease and operation
/// key; the reason is fixed because the action is its own record.
/// </summary>
public sealed record AssignTriageToMeRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey)
{
    public string EditLeaseToken { get; init; } = string.Empty;
}

public interface IAssignTriageToMe
{
    Task<TriageRecord> ExecuteAsync(AssignTriageToMeRequest request, CancellationToken cancellationToken);
}
