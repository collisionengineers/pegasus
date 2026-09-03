using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfWorkflowConfigurationStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IWorkflowConfigurationStore
{
    private const string AggregateType = "workflow_configuration";
    private const string EventKind = "workflow_configuration_updated";

    public async Task<CaseWorkflowConfiguration> GetCurrentAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<WorkflowConfigurationEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == AdministrationPolicyModelConfiguration.WorkflowPolicyKey,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The current workflow configuration has not been initialized.");
        return Map(entity);
    }

    public async Task<CaseWorkflowConfiguration> UpdateAsync(
        UpdateWorkflowConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.ActionHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.AggregateType == AggregateType
                    && item.CorrelationId == request.OperationKey,
                cancellationToken);
        if (replay is not null)
        {
            var replayed = Replay(request, replay);
            await transaction.CommitAsync(cancellationToken);
            return replayed;
        }

        var entity = await context.Set<WorkflowConfigurationEntity>()
            .SingleOrDefaultAsync(
                item => item.Id == AdministrationPolicyModelConfiguration.WorkflowPolicyKey,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The current workflow configuration has not been initialized.");
        if (entity.Version != request.ExpectedVersion)
        {
            throw new WorkflowConfigurationVersionConflictException(
                request.ExpectedVersion,
                entity.Version);
        }

        var before = Snapshot(entity);
        entity.Version = checked(entity.Version + 1);
        var after = Snapshot(entity);

        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = AggregateType,
            AggregateId = entity.Id,
            EventKind = EventKind,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                request.Actor.Roles.OrderBy(role => role).Select(role => role.ToString())),
            OccurredAtUtc = timeProvider.GetUtcNow(),
            Outcome = "succeeded",
            CorrelationId = request.OperationKey,
            Reason = request.Reason,
            BeforeJson = JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after),
            PolicyVersion = $"{entity.Id}/v{entity.Version}"
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    private static CaseWorkflowConfiguration Replay(
        UpdateWorkflowConfigurationRequest request,
        ActionHistoryEntity history)
    {
        if (history.AggregateId != AdministrationPolicyModelConfiguration.WorkflowPolicyKey
            || history.EventKind != EventKind
            || history.ActorKind != request.Actor.Kind.ToString()
            || history.ActorSubjectId != request.Actor.SubjectId
            || history.Reason != request.Reason
            || history.AfterJson is null)
        {
            throw new WorkflowConfigurationOperationConflictException();
        }

        var snapshot = JsonSerializer.Deserialize<WorkflowConfigurationSnapshot>(history.AfterJson)
            ?? throw new WorkflowConfigurationOperationConflictException();
        if (snapshot.PolicyVersion != checked(request.ExpectedVersion + 1))
        {
            throw new WorkflowConfigurationOperationConflictException();
        }

        return Map(snapshot);
    }

    private static WorkflowConfigurationSnapshot Snapshot(WorkflowConfigurationEntity entity) => new(
        entity.Id,
        entity.Version);

    private static CaseWorkflowConfiguration Map(WorkflowConfigurationEntity entity) =>
        Map(Snapshot(entity));

    private static CaseWorkflowConfiguration Map(WorkflowConfigurationSnapshot snapshot) => new(
        snapshot.PolicyKey,
        snapshot.PolicyVersion);

    private sealed record WorkflowConfigurationSnapshot(
        string PolicyKey,
        int PolicyVersion);
}
