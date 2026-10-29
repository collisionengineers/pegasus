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
    CaseTransitionDestination Destination)
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

public interface ICaseArchiveStore
{
    Task<CaseWorkflowRecord> ArchiveAsync(
        ArchiveCaseRequest request,
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
