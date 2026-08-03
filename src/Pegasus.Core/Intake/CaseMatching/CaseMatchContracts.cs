using Pegasus.Core.Workflow;

namespace Pegasus.Core.Intake;

/// <summary>
/// Normalized match keys extracted from an inbound message by a provider's case-match
/// policy. The incident date is never a positive key: it participates only as an
/// eliminator (requirements: a mismatch between accepted incident dates may eliminate a
/// candidate; a matching incident date proves nothing alone).
/// </summary>
public sealed record CaseMatchKeys(
    string? DurableClaimToken,
    string? NormalizedVrm,
    string? NormalizedSurname,
    string? NormalizedFirstInitial,
    DateOnly? IncidentDate)
{
    public bool HasAnyKey =>
        DurableClaimToken is not null
        || NormalizedVrm is not null
        || NormalizedSurname is not null;
}

/// <summary>
/// The accepted case-data values a provider policy normalizes into index keys. Write
/// side and read side share the provider's one normalization grammar so they can never
/// drift.
/// </summary>
public sealed record CaseMatchSourceData(
    string? ClaimNumber,
    string? VehicleRegistration,
    string? ClaimantName,
    DateOnly? IncidentDate);

public sealed record CaseMatchIndexKeys(
    string? DurableClaimToken,
    string? NormalizedVrm,
    string? NormalizedSurname,
    string? NormalizedFirstInitial,
    DateOnly? IncidentDate);

public sealed record CaseMatchCandidate(
    Guid CaseId,
    string WorkProviderCode,
    string? DurableClaimToken,
    string? NormalizedVrm,
    string? NormalizedSurname,
    string? NormalizedFirstInitial,
    DateOnly? IncidentDate,
    CaseLifecycleState State,
    Guid? ReplacementCaseId);

public interface ICaseMatchCandidateQueries
{
    /// <summary>
    /// Returns every case of the provider matching ANY populated key. All lifecycle
    /// states are eligible (operator decision 2026-08-03: staff do not archive; a
    /// post-report case is simply post-report stage).
    /// </summary>
    Task<IReadOnlyList<CaseMatchCandidate>> FindByAnyKeyAsync(
        string workProviderCode,
        CaseMatchKeys keys,
        CancellationToken cancellationToken);

    /// <summary>
    /// Fetches one case's index identity and lifecycle state, used to evaluate a
    /// Created in error replacement on its own keys. Null when the case has no
    /// match-index row.
    /// </summary>
    Task<CaseMatchCandidate?> FindByCaseIdAsync(
        Guid caseId,
        CancellationToken cancellationToken);
}

/// <summary>
/// A route-owned case-match policy (ADR-0008: each route policy owns its own evidence
/// precedence and case-association rules). The policy owns the provider's extraction
/// labels and normalization grammars; the shared eliminator orchestrator owns the
/// accepted decision procedure.
/// </summary>
public interface IProviderCaseMatchPolicy
{
    string WorkProviderCode { get; }
    string PolicyKey { get; }
    int PolicyVersion { get; }
    CaseMatchKeys ExtractMatchKeys(IntakeSourceReadResult readResult);
    CaseMatchIndexKeys DeriveIndexKeys(CaseMatchSourceData caseData);
}

public enum CaseMatchOutcome
{
    UniqueMatch,
    NoMatch,
    NoKeys,
    Ambiguous
}

public sealed record CaseMatchCandidateEvaluation(
    Guid CaseId,
    IReadOnlyList<string> HitKeys,
    IReadOnlyList<string> Eliminations,
    Guid? RedirectedFromCaseId = null);

public sealed record CaseMatchEvaluationResult(
    CaseMatchOutcome Outcome,
    Guid? MatchedCaseId,
    Guid? RedirectedFromCaseId,
    CaseMatchKeys Keys,
    IReadOnlyList<CaseMatchCandidateEvaluation> Candidates,
    string Reason,
    string PolicyKey,
    int PolicyVersion);

public sealed record AutomaticCaseAssociationRequest(
    Guid IntakeReceiptId,
    Guid CaseId,
    string MatchPolicyKey,
    int MatchPolicyVersion,
    string Actor,
    string OperationKey,
    string Reason);

public enum AutomaticCaseAssociationOutcome
{
    Associated,
    AlreadyAssociated
}

/// <summary>
/// The durable write for an unambiguous automatic match. Distinct from the staff
/// LinkIntake path, which demands an edit lease and throws on an active association;
/// the automatic write is idempotent and no-ops when any association row exists —
/// active, or deliberately reversed by staff, which must never be silently
/// re-linked. It also yields to an archived case or a live staff edit lease.
/// Reversal stays the staff ReverseIntakeLink path.
/// </summary>
public interface IAutomaticCaseAssociationStore
{
    Task<AutomaticCaseAssociationOutcome> AssociateFromMatchAsync(
        AutomaticCaseAssociationRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);
}
