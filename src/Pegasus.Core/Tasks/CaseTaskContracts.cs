using Pegasus.Core.Identity;

namespace Pegasus.Core.Tasks;

public enum CaseTaskState
{
    Open,
    Completed,
    Cancelled
}

public sealed record CaseTaskRecord(
    Guid Id,
    Guid CaseId,
    string Description,
    Guid? AssigneeId,
    CaseTaskState State,
    long Version,
    long CaseVersion);

public sealed class CaseTaskVersionConflictException(
    Guid taskId,
    long expectedVersion,
    long actualVersion)
    : InvalidOperationException(
        $"Case task '{taskId}' is at version {actualVersion}, not expected version {expectedVersion}.")
{
    public Guid TaskId { get; } = taskId;
    public long ExpectedVersion { get; } = expectedVersion;
    public long ActualVersion { get; } = actualVersion;
}

public sealed record CaseTaskAssigneeStatus(
    bool Exists,
    bool IsEnabled);

public sealed record CreateCaseTaskRequest(
    Guid CaseId,
    Guid TaskId,
    long ExpectedCaseVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    string Description,
    Guid? AssigneeId = null);

public abstract record ExistingCaseTaskMutationRequest(
    Guid CaseId,
    Guid TaskId,
    long ExpectedCaseVersion,
    long ExpectedTaskVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken);

public sealed record AssignCaseTaskRequest(
    Guid CaseId,
    Guid TaskId,
    long ExpectedCaseVersion,
    long ExpectedTaskVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid? AssigneeId)
    : ExistingCaseTaskMutationRequest(
        CaseId,
        TaskId,
        ExpectedCaseVersion,
        ExpectedTaskVersion,
        Actor,
        OperationKey,
        Reason,
        EditLeaseToken);

public sealed record CompleteCaseTaskRequest(
    Guid CaseId,
    Guid TaskId,
    long ExpectedCaseVersion,
    long ExpectedTaskVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken)
    : ExistingCaseTaskMutationRequest(
        CaseId,
        TaskId,
        ExpectedCaseVersion,
        ExpectedTaskVersion,
        Actor,
        OperationKey,
        Reason,
        EditLeaseToken);

public sealed record CancelCaseTaskRequest(
    Guid CaseId,
    Guid TaskId,
    long ExpectedCaseVersion,
    long ExpectedTaskVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken)
    : ExistingCaseTaskMutationRequest(
        CaseId,
        TaskId,
        ExpectedCaseVersion,
        ExpectedTaskVersion,
        Actor,
        OperationKey,
        Reason,
        EditLeaseToken);

/// <summary>
/// Atomic persistence port for case-task mutations. Implementations own the case/task
/// concurrency checks, active edit-lease check, idempotent replay and permanent history write.
/// </summary>
public interface ICaseTaskStore
{
    Task<bool> HasOperationAsync(
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken);

    Task<CaseTaskRecord> CreateAsync(
        CreateCaseTaskRequest request,
        CancellationToken cancellationToken);

    Task<CaseTaskRecord> AssignAsync(
        AssignCaseTaskRequest request,
        CancellationToken cancellationToken);

    Task<CaseTaskRecord> CompleteAsync(
        CompleteCaseTaskRequest request,
        CancellationToken cancellationToken);

    Task<CaseTaskRecord> CancelAsync(
        CancelCaseTaskRequest request,
        CancellationToken cancellationToken);
}

public interface ICaseTaskQueries
{
    Task<IReadOnlyList<CaseTaskRecord>> ListAsync(
        Guid caseId,
        CancellationToken cancellationToken);
}

public interface ICaseTaskAssigneeDirectory
{
    Task<CaseTaskAssigneeStatus> GetAsync(
        Guid staffId,
        CancellationToken cancellationToken);
}

public interface ICreateCaseTask
{
    Task<CaseTaskRecord> ExecuteAsync(
        CreateCaseTaskRequest request,
        CancellationToken cancellationToken);
}

public interface IAssignCaseTask
{
    Task<CaseTaskRecord> ExecuteAsync(
        AssignCaseTaskRequest request,
        CancellationToken cancellationToken);
}

public interface ICompleteCaseTask
{
    Task<CaseTaskRecord> ExecuteAsync(
        CompleteCaseTaskRequest request,
        CancellationToken cancellationToken);
}

public interface ICancelCaseTask
{
    Task<CaseTaskRecord> ExecuteAsync(
        CancelCaseTaskRequest request,
        CancellationToken cancellationToken);
}
