using Pegasus.Core.Intake;

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
    long Version);
public sealed record TriageEditLease(
    Guid TriageId,
    string Token,
    string Holder,
    long Version,
    DateTimeOffset ExpiresAtUtc);

public sealed record ClaimTriageEditLeaseRequest(
    Guid TriageId,
    long ExpectedVersion,
    string Actor,
    string OperationKey);

public sealed record RenewTriageEditLeaseRequest(
    Guid TriageId,
    long ExpectedVersion,
    string Actor,
    string LeaseToken);

public sealed record ReleaseTriageEditLeaseRequest(
    Guid TriageId,
    string Actor,
    string LeaseToken);


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

public sealed class TriageEditLeaseConflictException(Guid triageId)
    : InvalidOperationException($"Triage '{triageId}' is currently being edited by another actor.")
{
    public Guid TriageId { get; } = triageId;
}

public sealed class TriageEditLeaseExpiredException(Guid triageId)
    : InvalidOperationException($"The edit lease for triage '{triageId}' is no longer valid.")
{
    public Guid TriageId { get; } = triageId;
}
public sealed class TriageOperationConflictException(Guid triageId, string operationKey)
    : InvalidOperationException(
        $"Operation '{operationKey}' was already applied to triage '{triageId}' with different inputs.")
{
    public Guid TriageId { get; } = triageId;

    public string OperationKey { get; } = operationKey;
}


public interface ILeaseTriageForEdit
{
    Task<TriageEditLease> ClaimAsync(
        ClaimTriageEditLeaseRequest request,
        CancellationToken cancellationToken);

    Task<TriageEditLease> RenewAsync(
        RenewTriageEditLeaseRequest request,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        ReleaseTriageEditLeaseRequest request,
        CancellationToken cancellationToken);
}


public sealed record CreateTriageFromIntakeRequest(
    TriageOrigin Origin,
    string NormalizedVehicleRegistration,
    string Actor,
    string OperationKey);

public sealed record TriageMutationRequest(
    Guid TriageId,
    long ExpectedVersion,
    string Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken);

public sealed record AssignTriageRequest(
    Guid TriageId,
    long ExpectedVersion,
    Guid AssigneeId,
    string Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken);

public sealed record RecordTriageFindingRequest(
    Guid TriageId,
    long ExpectedVersion,
    string Actor,
    string OperationKey,
    string Reason,
    RoadworthinessFinding? Roadworthiness,
    AssessmentFinding? Assessment,
    Guid? SupersedesFindingId,
    string EditLeaseToken);

public sealed record TriageCaseLinkRequest(
    Guid TriageId,
    Guid CaseId,
    long ExpectedTriageVersion,
    long ExpectedCaseVersion,
    string Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken);

public sealed record TriageResponseEvidenceLinkRequest(
    Guid TriageId,
    Guid SentEvidenceId,
    long ExpectedVersion,
    string Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken);

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
        TriageResponseEvidenceLinkRequest request,
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

public sealed record TriageSummary(
    Guid Id,
    string NormalizedVehicleRegistration,
    TriageState State,
    Guid? AssigneeId,
    Guid? LinkedCaseId,
    DateTimeOffset CreatedAtUtc,
    long Version);

public sealed record TriageDetail(
    TriageRecord Record,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<TriageFinding> Findings,
    IReadOnlyList<TriageResponseEvidenceLink> ResponseEvidence);

public interface ITriageQueries
{
    Task<IReadOnlyList<TriageSummary>> ListAsync(
        TriageState? state,
        CancellationToken cancellationToken);

    Task<TriageDetail?> GetAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Persists triage lifecycle mutations. Implementations must enforce the supplied version
/// and operation key atomically, because the aggregate is read for transition validation
/// before each mutation.
/// </summary>
public interface ITriageStore : ITriageQueries, ILeaseTriageForEdit
{
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
        TriageResponseEvidenceLinkRequest request,
        CancellationToken cancellationToken);

    Task<TriageRecord> ChangeStateAsync(
        TriageMutationRequest request,
        TriageState targetState,
        CancellationToken cancellationToken);

    Task LinkCaseAsync(TriageCaseLinkRequest request, CancellationToken cancellationToken);

    Task UnlinkCaseAsync(TriageCaseLinkRequest request, CancellationToken cancellationToken);
}
