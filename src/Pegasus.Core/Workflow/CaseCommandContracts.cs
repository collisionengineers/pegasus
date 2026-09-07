using Pegasus.Core.Identity;

namespace Pegasus.Core.Workflow;

public sealed record CaseArchive(
    DateTimeOffset ArchivedAtUtc,
    ActionActor ArchivedBy,
    string Reason);

public sealed class CaseArchivedException(Guid caseId)
    : InvalidOperationException($"Case '{caseId}' is archived and read-only.")
{
    public Guid CaseId { get; } = caseId;
}

public sealed record ArchiveCaseRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken)
    : CaseMutationRequest(
        CaseId,
        ExpectedVersion,
        Actor,
        OperationKey,
        Reason,
        EditLeaseToken);

public enum CaseTransitionDestination
{
    Review,
    ReportPreparation
}

public sealed record TransitionCaseRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    CaseTransitionDestination Destination,
    CaseReadinessEvidence? Readiness = null)
    : CaseMutationRequest(
        CaseId,
        ExpectedVersion,
        Actor,
        OperationKey,
        Reason,
        EditLeaseToken);

public sealed record CaseArchiveReadiness(
    bool IsCustodyConfirmed,
    bool HasBlockingExternalWork);

public interface ICaseArchiveReadinessQueries
{
    Task<bool> HasCaseMutationOperationAsync(
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken);

    Task<CaseArchiveReadiness> GetArchiveReadinessAsync(
        Guid caseId,
        CancellationToken cancellationToken);
}

public interface ICaseArchiveStore
{
    Task<CaseWorkflowRecord> ArchiveAsync(
        ArchiveCaseRequest request,
        CancellationToken cancellationToken);
}

public interface IAcquireCaseEditLease
{
    Task<CaseEditLease> ExecuteAsync(
        ClaimCaseEditLeaseRequest request,
        CancellationToken cancellationToken);
}

public interface IRenewCaseEditLease
{
    Task<CaseEditLease> ExecuteAsync(
        RenewCaseEditLeaseRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Keeps the lease its holder already has from lapsing under an open editor.
/// Unlike renewal it carries no operation key and leaves no replay record:
/// FRD-01 counts a heartbeat as telemetry, and an editor produces one every
/// minute for as long as the page stays open.
/// </summary>
public interface IHeartbeatCaseEditLease
{
    Task<CaseEditLease> ExecuteAsync(
        HeartbeatCaseEditLeaseRequest request,
        CancellationToken cancellationToken);
}

public interface IReleaseCaseEditLease
{
    Task ExecuteAsync(
        ReleaseCaseEditLeaseRequest request,
        CancellationToken cancellationToken);
}

public interface IClearCaseEditLease
{
    Task<ClearCaseEditLeaseResult> ExecuteAsync(
        ClearCaseEditLeaseRequest request,
        CancellationToken cancellationToken);
}

public interface IHoldCase
{
    Task<CaseWorkflowRecord> ExecuteAsync(
        PutCaseOnHoldRequest request,
        CancellationToken cancellationToken);
}

public interface IReleaseCase
{
    Task<CaseWorkflowRecord> ExecuteAsync(
        CaseMutationRequest request,
        CancellationToken cancellationToken);
}

public interface ITransitionCase
{
    Task<CaseWorkflowRecord> ExecuteAsync(
        TransitionCaseRequest request,
        CancellationToken cancellationToken);
}

public interface IArchiveCase
{
    Task<CaseWorkflowRecord> ExecuteAsync(
        ArchiveCaseRequest request,
        CancellationToken cancellationToken);
}
