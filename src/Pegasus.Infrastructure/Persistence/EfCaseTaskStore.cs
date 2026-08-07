using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfCaseTaskStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : ICaseTaskStore, ICaseTaskQueries, ICaseTaskAssigneeDirectory
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<bool> HasOperationAsync(
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CaseWorkflowEvents.AsNoTracking().AnyAsync(
            item => item.CaseId == caseId && item.OperationKey == operationKey.Trim(),
            cancellationToken);
    }
    public async Task<IReadOnlyList<CaseTaskRecord>> ListAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var caseVersion = await context.CaseWorkflows
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => (long?)item.Version)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");
        var tasks = await context.CaseTasks
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderBy(item => item.State == nameof(CaseTaskState.Open) ? 0
                : item.State == nameof(CaseTaskState.Completed) ? 1 : 2)
            .ThenBy(item => item.Description)
            .ThenBy(item => item.Id)
            .Take(500)
            .ToArrayAsync(cancellationToken);
        return tasks.Select(item => Map(item, caseVersion)).ToArray();
    }


    public async Task<CaseTaskAssigneeStatus> GetAsync(
        Guid staffId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var staff = await context.Users.AsNoTracking()
            .Where(item => item.Id == staffId)
            .Select(item => new { item.IsEnabled })
            .SingleOrDefaultAsync(cancellationToken);
        return staff is null
            ? new(false, false)
            : new(true, staff.IsEnabled);
    }

    public async Task<CaseTaskRecord> CreateAsync(
        CreateCaseTaskRequest request,
        CancellationToken cancellationToken)
    {
        CaseTaskRules.ValidateCreate(request);
        var eventKind = "case_task_created";
        var requestHash = RequestHash(
            eventKind,
            request.CaseId,
            request.TaskId,
            request.ExpectedCaseVersion,
            expectedTaskVersion: null,
            request.Actor,
            request.OperationKey,
            request.Reason,
            request.EditLeaseToken,
            request.Description,
            request.AssigneeId);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindReplayAsync(
            context,
            request.CaseId,
            request.OperationKey,
            cancellationToken);
        if (replay is not null)
        {
            return RequireExactReplay(replay, eventKind, requestHash, request.CaseId, request.OperationKey);
        }

        var workflow = await LoadWorkflowAsync(context, request.CaseId, cancellationToken);
        RequireCaseVersion(workflow, request.ExpectedCaseVersion);
        ArchivedCaseGuard.RequireMutable(workflow);
        CaseTaskRules.RequireNonTerminal(ParseLifecycleState(workflow.State));
        RequireLease(workflow, request.Actor, request.EditLeaseToken, timeProvider.GetUtcNow());
        if (await context.CaseTasks.AnyAsync(item => item.Id == request.TaskId, cancellationToken))
        {
            throw new InvalidOperationException("The case-task identifier is already in use.");
        }
        await RequireEligibleAssigneeAsync(context, request.AssigneeId, cancellationToken);

        var task = new CaseTaskEntity
        {
            Id = request.TaskId,
            CaseId = request.CaseId,
            Workflow = workflow,
            Description = request.Description.Trim(),
            AssigneeId = request.AssigneeId,
            State = nameof(CaseTaskState.Open),
            Version = 0
        };
        context.CaseTasks.Add(task);
        var beforeCaseVersion = workflow.Version;
        workflow.Version = checked(workflow.Version + 1);
        ClearLease(workflow);
        var result = Map(task, workflow.Version);
        AddHistory(
            context,
            workflow,
            request.Actor,
            request.OperationKey.Trim(),
            request.Reason.Trim(),
            eventKind,
            requestHash,
            beforeCaseVersion,
            result,
            before: null,
            timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task<CaseTaskRecord> AssignAsync(
        AssignCaseTaskRequest request,
        CancellationToken cancellationToken)
    {
        CaseTaskRules.ValidateExisting(request);
        CaseTaskRules.ValidateAssigneeId(request.AssigneeId, nameof(request));
        return MutateAsync(
            request,
            "case_task_assigned",
            request.AssigneeId,
            async (context, task, token) =>
            {
                await RequireEligibleAssigneeAsync(context, request.AssigneeId, token);
                task.AssigneeId = request.AssigneeId;
            },
            cancellationToken);
    }

    public Task<CaseTaskRecord> CompleteAsync(
        CompleteCaseTaskRequest request,
        CancellationToken cancellationToken)
    {
        CaseTaskRules.ValidateExisting(request);
        return MutateAsync(
            request,
            "case_task_completed",
            assigneeId: null,
            static (context, task, token) =>
            {
                task.State = nameof(CaseTaskState.Completed);
                return Task.CompletedTask;
            },
            cancellationToken);
    }

    public Task<CaseTaskRecord> CancelAsync(
        CancelCaseTaskRequest request,
        CancellationToken cancellationToken)
    {
        CaseTaskRules.ValidateExisting(request);
        return MutateAsync(
            request,
            "case_task_cancelled",
            assigneeId: null,
            static (context, task, token) =>
            {
                task.State = nameof(CaseTaskState.Cancelled);
                return Task.CompletedTask;
            },
            cancellationToken);
    }

    private async Task<CaseTaskRecord> MutateAsync<TRequest>(
        TRequest request,
        string eventKind,
        Guid? assigneeId,
        Func<PegasusDbContext, CaseTaskEntity, CancellationToken, Task> apply,
        CancellationToken cancellationToken)
        where TRequest : ExistingCaseTaskMutationRequest
    {
        var requestHash = RequestHash(
            eventKind,
            request.CaseId,
            request.TaskId,
            request.ExpectedCaseVersion,
            request.ExpectedTaskVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            request.EditLeaseToken,
            description: null,
            assigneeId);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindReplayAsync(
            context,
            request.CaseId,
            request.OperationKey,
            cancellationToken);
        if (replay is not null)
        {
            return RequireExactReplay(replay, eventKind, requestHash, request.CaseId, request.OperationKey);
        }

        var workflow = await LoadWorkflowAsync(context, request.CaseId, cancellationToken);
        RequireCaseVersion(workflow, request.ExpectedCaseVersion);
        ArchivedCaseGuard.RequireMutable(workflow);
        CaseTaskRules.RequireNonTerminal(ParseLifecycleState(workflow.State));
        RequireLease(workflow, request.Actor, request.EditLeaseToken, timeProvider.GetUtcNow());
        var task = await context.CaseTasks.SingleOrDefaultAsync(
            item => item.Id == request.TaskId && item.CaseId == request.CaseId,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Case task '{request.TaskId}' was not found on case '{request.CaseId}'.");
        RequireTaskVersion(task, request.ExpectedTaskVersion);
        var before = Map(task, workflow.Version);
        CaseTaskRules.RequireOpen(before, eventKind switch
        {
            "case_task_assigned" => "assigned, reassigned or unassigned",
            "case_task_completed" => "completed",
            "case_task_cancelled" => "cancelled",
            _ => "changed"
        });

        await apply(context, task, cancellationToken);
        var beforeCaseVersion = workflow.Version;
        task.Version = checked(task.Version + 1);
        workflow.Version = checked(workflow.Version + 1);
        ClearLease(workflow);
        var result = Map(task, workflow.Version);
        AddHistory(
            context,
            workflow,
            request.Actor,
            request.OperationKey.Trim(),
            request.Reason.Trim(),
            eventKind,
            requestHash,
            beforeCaseVersion,
            result,
            before,
            timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static async Task<CaseWorkflowEntity> LoadWorkflowAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken) =>
        await context.CaseWorkflows.SingleOrDefaultAsync(
            item => item.CaseId == caseId,
            cancellationToken)
        ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");

    private static Task<CaseWorkflowEventEntity?> FindReplayAsync(
        PegasusDbContext context,
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken) =>
        context.CaseWorkflowEvents.AsNoTracking().SingleOrDefaultAsync(
            item => item.CaseId == caseId && item.OperationKey == operationKey.Trim(),
            cancellationToken);

    private static CaseTaskRecord RequireExactReplay(
        CaseWorkflowEventEntity replay,
        string eventKind,
        string requestHash,
        Guid caseId,
        string operationKey)
    {
        if (!string.Equals(replay.EventType, eventKind, StringComparison.Ordinal)
            || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(replay.RequestHash),
                Convert.FromHexString(requestHash))
            || replay.ResultJson is null)
        {
            throw new CaseOperationConflictException(caseId, operationKey);
        }

        return JsonSerializer.Deserialize<CaseTaskRecord>(replay.ResultJson, SerializerOptions)
            ?? throw new InvalidDataException("The persisted case-task replay result is invalid.");
    }

    private static async Task RequireEligibleAssigneeAsync(
        PegasusDbContext context,
        Guid? assigneeId,
        CancellationToken cancellationToken)
    {
        if (assigneeId is null)
        {
            return;
        }

        var staff = await context.Users
            .Where(item => item.Id == assigneeId.Value)
            .Select(item => new { item.IsEnabled })
            .SingleOrDefaultAsync(cancellationToken);
        CaseTaskRules.RequireEligibleAssignee(
            staff is null
                ? new(false, false)
                : new(true, staff.IsEnabled));
    }

    private static void RequireCaseVersion(CaseWorkflowEntity workflow, long expectedVersion) =>
        CaseMutationGuard.RequireVersion(workflow, expectedVersion);

    private static void RequireTaskVersion(CaseTaskEntity task, long expectedVersion)
    {
        if (task.Version != expectedVersion)
        {
            throw new CaseTaskVersionConflictException(task.Id, expectedVersion, task.Version);
        }
    }

    private static void RequireLease(
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string token,
        DateTimeOffset now) =>
        CaseMutationGuard.RequireLease(workflow, actor, token, now);

    private static void ClearLease(CaseWorkflowEntity workflow) =>
        CaseMutationGuard.ClearLease(workflow);

    private static void AddHistory(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string operationKey,
        string reason,
        string eventKind,
        string requestHash,
        long beforeCaseVersion,
        CaseTaskRecord result,
        CaseTaskRecord? before,
        DateTimeOffset occurredAtUtc)
    {
        var resultJson = JsonSerializer.Serialize(result, SerializerOptions);
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = eventKind,
            OperationKey = operationKey,
            RequestHash = requestHash,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(actor.Roles.OrderBy(role => role)),
            Reason = reason,
            OccurredAtUtc = occurredAtUtc,
            BeforeVersion = beforeCaseVersion,
            AfterVersion = workflow.Version,
            ResultJson = resultJson
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case_task",
            AggregateId = result.Id.ToString("D"),
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                actor.Roles.OrderBy(role => role).Select(role => role.ToString()),
                SerializerOptions),
            OccurredAtUtc = occurredAtUtc,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = before is null
                ? null
                : JsonSerializer.Serialize(before, SerializerOptions),
            AfterJson = resultJson,
            PolicyVersion = "case-task-v1"
        });
    }

    private static CaseTaskRecord Map(CaseTaskEntity task, long caseVersion) =>
        new(
            task.Id,
            task.CaseId,
            task.Description,
            task.AssigneeId,
            Enum.Parse<CaseTaskState>(task.State),
            task.Version,
            caseVersion);

    private static CaseLifecycleState ParseLifecycleState(string value) =>
        Enum.TryParse<CaseLifecycleState>(value, ignoreCase: false, out var state)
        && Enum.IsDefined(state)
            ? state
            : throw new InvalidDataException(
                $"Unknown persisted case lifecycle state '{value}'.");

    private static string RequestHash(
        string eventKind,
        Guid caseId,
        Guid taskId,
        long expectedCaseVersion,
        long? expectedTaskVersion,
        ActionActor actor,
        string operationKey,
        string reason,
        string editLeaseToken,
        string? description,
        Guid? assigneeId) =>
        Hash(JsonSerializer.Serialize(new
        {
            eventKind,
            caseId,
            taskId,
            expectedCaseVersion,
            expectedTaskVersion,
            actorKind = actor.Kind.ToString(),
            actorSubjectId = actor.SubjectId,
            actorRoles = actor.Roles.OrderBy(role => role).Select(role => role.ToString()),
            operationKey = operationKey.Trim(),
            reason = reason.Trim(),
            editLeaseTokenHash = Hash(editLeaseToken),
            description = description?.Trim(),
            assigneeId
        }, SerializerOptions));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
