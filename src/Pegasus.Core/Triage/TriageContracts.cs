using System.Globalization;
using System.Text.RegularExpressions;
using Pegasus.Core.Actors;
using Pegasus.Core.Intake;
using Pegasus.Core.Identity;

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
/// The permanent Triage reference: `T-` followed by the global allocation
/// sequence zero-padded to five digits, expanding past `T-99999` without
/// reuse. The sequence is global — not per principal, per vehicle or per year
/// — so a reference identifies exactly one Triage for the life of the system.
/// It is allocated once at creation, is never reset and is never reused, so a
/// number consumed by a failed creation simply leaves a gap.
/// </summary>
public static class TriageReferenceFormat
{
    public const string Prefix = "T-";

    private static readonly Regex Canonical = new(
        "^T-[0-9]{5,}$",
        RegexOptions.CultureInvariant);

    public static string Format(long sequence)
    {
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                "A Triage reference sequence starts at 1.");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}{sequence:00000}");
    }

    public static bool TryParse(string? value, out long sequence)
    {
        sequence = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (!Canonical.IsMatch(candidate)
            || !long.TryParse(
                candidate.AsSpan(Prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out sequence)
            || sequence <= 0)
        {
            sequence = 0;
            return false;
        }

        return true;
    }
}

public sealed record TriageOrigin(
    Guid ReceiptId,
    IntakeSourceIdentity SourceIdentity,
    string SourceHash,
    Guid EvaluationRevisionId);

public sealed record TriageRecord(
    Guid Id,
    TriageOrigin Origin,
    string NormalizedVehicleRegistration,
    TriageState State,
    Guid? AssigneeId,
    Guid? LinkedCaseId,
    long Version,
    string? Reference = null,
    Guid? PrincipalId = null);


public sealed class TriageVersionConflictException(
    Guid triageId,
    long expectedVersion,
    long actualVersion)
    : InvalidOperationException(
        $"Triage '{triageId}' is at version {actualVersion}, not expected version {expectedVersion}.")
{
    public Guid TriageId { get; } = triageId;

    public long ExpectedVersion { get; } = expectedVersion;

    public long ActualVersion { get; } = actualVersion;
}

public sealed class TriageOperationConflictException(Guid triageId, string operationKey)
    : InvalidOperationException(
        $"Operation '{operationKey}' was already applied to triage '{triageId}' with different inputs.")
{
    public Guid TriageId { get; } = triageId;

    public string OperationKey { get; } = operationKey;
}
public sealed class TriageResponseEvidenceAlreadyLinkedException(
    Guid triageId,
    Exception? innerException = null)
    : InvalidOperationException(
        $"Triage '{triageId}' already has current response evidence.",
        innerException)
{
    public Guid TriageId { get; } = triageId;
}




public sealed record CreateTriageFromIntakeRequest(
    TriageOrigin Origin,
    string NormalizedVehicleRegistration,
    IntakeEvidence AcceptedMatchEvidence,
    ActionActor Actor,
    string OperationKey);

public sealed record TriageMutationRequest(
    Guid TriageId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public sealed record AssignTriageRequest(
    Guid TriageId,
    long ExpectedVersion,
    Guid AssigneeId,
    ActionActor Actor,
    string OperationKey,
    string Reason);

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
    Guid TriageId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Note);

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
    Guid TriageId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    RoadworthinessFinding? Roadworthiness,
    AssessmentFinding? Assessment,
    Guid? SupersedesFindingId);

public sealed record TriageCaseLinkRequest(
    Guid TriageId,
    Guid CaseId,
    long ExpectedTriageVersion,
    long ExpectedCaseVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string CaseEditLeaseToken);

public sealed record TriageResponseEvidenceLinkRequest(
    Guid TriageId,
    Guid PollOutcomeId,
    Guid SentEvidenceId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public sealed record TriageResponseEvidenceUnlinkRequest(
    Guid TriageId,
    Guid SentEvidenceId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public interface ICreateTriageFromIntake
{
    Task<TriageRecord> ExecuteAsync(
        CreateTriageFromIntakeRequest request,
        CancellationToken cancellationToken);
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
    Guid TriageId,
    RoadworthinessFinding? Roadworthiness,
    AssessmentFinding? Assessment,
    Guid? SupersedesFindingId,
    string Actor,
    string OperationKey,
    string Reason,
    DateTimeOffset RecordedAtUtc);

public sealed record TriageResponseEvidenceLink(
    Guid TriageId,
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
    Guid TriageId,
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
    Guid? AfterLinkedCaseId)
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
/// A Triage queue row. <see cref="Reference"/> is the Triage's own permanent
/// T reference; <see cref="ClaimNumber"/> is the originating instruction
/// draft's provider claim number, which is a fact about the sender and not an
/// identifier of this Triage. The two were the same field before the T
/// reference existed.
/// </summary>
/// <remarks>
/// Every persisted Triage carries a reference, so <see cref="Reference"/> is
/// only nullable to keep the two out-of-stream in-memory test fixtures that
/// still pass <c>Reference: null</c> compiling; tightening it to a required
/// member is a follow-up on those fixtures' owner.
/// </remarks>
public sealed record TriageSummary(
    Guid Id,
    string NormalizedVehicleRegistration,
    TriageState State,
    Guid? AssigneeId,
    Guid? LinkedCaseId,
    DateTimeOffset CreatedAtUtc,
    long Version,
    string? Reference,
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
    /// The code of the principal the originating receipt established, read in
    /// the same round trip as the record. Null is the operator-visible
    /// `Not known` state.
    /// </summary>
    string? PrincipalCode = null);

/// <summary>
/// A decoded keyset position in the Triage list: the newest-first order is
/// <c>CreatedAtUtc</c> descending with the identity as the tie-break, so a
/// position is exactly that pair. Both are absent on the first page. This is
/// the store's own currency — the opaque cursor that carries it between
/// requests is minted above the store, by <see cref="ListTriagePage"/>.
/// </summary>
public sealed record TriageListPosition(DateTimeOffset CreatedAtUtc, Guid Id);

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

    Task<TriageDetail?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The Triage this receipt opened, if it opened one. Mirrors
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
        Guid triageId,
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
/// Persists triage lifecycle mutations. Implementations must enforce the supplied version
/// and operation key atomically, because the aggregate is read for transition validation
/// before each mutation. Replay probes must verify the complete request fingerprint and
/// return the historical post-operation result.
/// </summary>
public interface ITriageStore : ITriageQueries, ITriageResponseEvidenceCandidateQueries
{
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
