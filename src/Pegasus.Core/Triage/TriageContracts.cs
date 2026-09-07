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

public sealed record TriageSummary(
    Guid Id,
    string NormalizedVehicleRegistration,
    TriageState State,
    Guid? AssigneeId,
    Guid? LinkedCaseId,
    DateTimeOffset CreatedAtUtc,
    long Version,
    string? Reference,
    string? Provider);

public sealed record TriageDetail(
    TriageRecord Record,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<TriageFinding> Findings,
    IReadOnlyList<TriageResponseEvidenceLink> ResponseEvidence,
    IReadOnlyList<TriageHistoryEntry> History,
    IReadOnlyList<TriageResponseEvidenceCandidate> ResponseEvidenceCandidates);

public interface ITriageQueries
{
    Task<IReadOnlyList<TriageSummary>> ListAsync(
        TriageState? state,
        CancellationToken cancellationToken);

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
