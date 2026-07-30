using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tasks;

public sealed class CreateCaseTask(
    ICaseTaskStore store,
    ICaseTaskAssigneeDirectory assignees) : ICreateCaseTask
{
    private readonly ICaseTaskStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICaseTaskAssigneeDirectory _assignees =
        assignees ?? throw new ArgumentNullException(nameof(assignees));

    public async Task<CaseTaskRecord> ExecuteAsync(
        CreateCaseTaskRequest request,
        CancellationToken cancellationToken)
    {
        CaseTaskRules.ValidateCreate(request);
        await RequireEligibleAssigneeUnlessReplayAsync(
            request.CaseId,
            request.OperationKey,
            request.AssigneeId,
            cancellationToken);
        return await _store.CreateAsync(request, cancellationToken);
    }

    private async Task RequireEligibleAssigneeUnlessReplayAsync(
        Guid caseId,
        string operationKey,
        Guid? assigneeId,
        CancellationToken cancellationToken)
    {
        if (assigneeId is null
            || await _store.HasOperationAsync(caseId, operationKey, cancellationToken))
        {
            return;
        }

        CaseTaskRules.RequireEligibleAssignee(
            await _assignees.GetAsync(assigneeId.Value, cancellationToken));
    }
}

public sealed class AssignCaseTask(
    ICaseTaskStore store,
    ICaseTaskAssigneeDirectory assignees) : IAssignCaseTask
{
    private readonly ICaseTaskStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICaseTaskAssigneeDirectory _assignees =
        assignees ?? throw new ArgumentNullException(nameof(assignees));

    public async Task<CaseTaskRecord> ExecuteAsync(
        AssignCaseTaskRequest request,
        CancellationToken cancellationToken)
    {
        CaseTaskRules.ValidateExisting(request);
        CaseTaskRules.ValidateAssigneeId(request.AssigneeId, nameof(request));
        if (request.AssigneeId is not null
            && !await _store.HasOperationAsync(
                request.CaseId,
                request.OperationKey,
                cancellationToken))
        {
            CaseTaskRules.RequireEligibleAssignee(
                await _assignees.GetAsync(request.AssigneeId.Value, cancellationToken));
        }

        return await _store.AssignAsync(request, cancellationToken);
    }
}

public sealed class CompleteCaseTask(ICaseTaskStore store) : ICompleteCaseTask
{
    private readonly ICaseTaskStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<CaseTaskRecord> ExecuteAsync(
        CompleteCaseTaskRequest request,
        CancellationToken cancellationToken)
    {
        CaseTaskRules.ValidateExisting(request);
        return _store.CompleteAsync(request, cancellationToken);
    }
}

public sealed class CancelCaseTask(ICaseTaskStore store) : ICancelCaseTask
{
    private readonly ICaseTaskStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<CaseTaskRecord> ExecuteAsync(
        CancelCaseTaskRequest request,
        CancellationToken cancellationToken)
    {
        CaseTaskRules.ValidateExisting(request);
        return _store.CancelAsync(request, cancellationToken);
    }
}

public static class CaseTaskRules
{
    public static void ValidateCreate(CreateCaseTaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCommon(
            request.CaseId,
            request.TaskId,
            request.ExpectedCaseVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            request.EditLeaseToken);
        RequireText(
            request.Description,
            "A task description is required.",
            500,
            nameof(request));
        ValidateAssigneeId(request.AssigneeId, nameof(request));
    }

    public static void ValidateExisting(ExistingCaseTaskMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCommon(
            request.CaseId,
            request.TaskId,
            request.ExpectedCaseVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            request.EditLeaseToken);
        if (request.ExpectedTaskVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The expected task version cannot be negative.");
        }
    }

    public static void RequireOpen(CaseTaskRecord task, string action)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.State != CaseTaskState.Open)
        {
            throw new InvalidOperationException(
                $"Only an open case task can be {action}.");
        }
    }
    public static void RequireNonTerminal(CaseLifecycleState state)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
        if (CaseLifecycleRules.IsTerminal(state))
        {
            throw new InvalidOperationException(
                "Case tasks cannot be changed while the case is closed. Reopen the case first.");
        }
    }


    public static void RequireEligibleAssignee(CaseTaskAssigneeStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (!status.Exists)
        {
            throw new InvalidOperationException("The case-task assignee does not exist.");
        }
        if (!status.IsEnabled)
        {
            throw new InvalidOperationException("The case-task assignee is disabled.");
        }
    }

    public static void ValidateAssigneeId(Guid? assigneeId, string parameterName)
    {
        if (assigneeId == Guid.Empty)
        {
            throw new ArgumentException(
                "An assignee must be null or a non-empty staff identifier.",
                parameterName);
        }
    }

    private static void ValidateCommon(
        Guid caseId,
        Guid taskId,
        long expectedCaseVersion,
        ActionActor actor,
        string operationKey,
        string reason,
        string editLeaseToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("A task identifier is required.", nameof(taskId));
        }
        if (expectedCaseVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedCaseVersion),
                "The expected case version cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        RequireText(operationKey, "An operation key is required.", 100, nameof(operationKey));
        RequireText(reason, "A reason is required.", 500, nameof(reason));
        RequireText(
            editLeaseToken,
            "An active edit lease token is required.",
            128,
            nameof(editLeaseToken));
    }

    private static void RequireText(
        string value,
        string message,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }
        if (value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters.");
        }
    }
}
