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
    string Reason);

public sealed record AssignTriageRequest(
    Guid TriageId,
    long ExpectedVersion,
    Guid AssigneeId,
    string Actor,
    string OperationKey,
    string Reason);

public sealed record RecordTriageFindingRequest(
    Guid TriageId,
    long ExpectedVersion,
    string Actor,
    string OperationKey,
    string Reason,
    RoadworthinessFinding? Roadworthiness,
    AssessmentFinding? Assessment,
    Guid? SupersedesFindingId = null);

public sealed record TriageCaseLinkRequest(
    Guid TriageId,
    Guid CaseId,
    long ExpectedTriageVersion,
    long ExpectedCaseVersion,
    string Actor,
    string OperationKey,
    string Reason);

public sealed record TriageResponseEvidenceLinkRequest(
    Guid TriageId,
    Guid SentEvidenceId,
    long ExpectedVersion,
    string Actor,
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
        TriageResponseEvidenceLinkRequest request,
        CancellationToken cancellationToken);
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
