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

    internal static async Task<CaseWorkflowConfiguration> ReadAsync(
        PegasusDbContext context, CancellationToken cancellationToken)
    {
        var entity = await context.Set<WorkflowConfigurationEntity>().AsNoTracking()
            .SingleAsync(item => item.Id == AdministrationPolicyModelConfiguration.WorkflowPolicyKey,
                cancellationToken);
        return Map(entity);
    }

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
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageWorkflowConfiguration);
        if (request.ChaseIntervalDays is < 1 or > 365)
            throw new ArgumentOutOfRangeException(nameof(request));
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
        await EfEditScopeStore.RequireAsync(context, EditScopeKind.NamedConfiguration,
            GetWorkflowConfiguration.RecordId, entity.Version, request.ExpectedVersion,
            request.Actor, request.EditLeaseToken, timeProvider.GetUtcNow(), cancellationToken);
        entity.RequireInstructions = request.RequireInstructions;
        entity.RequireImages = request.RequireImages;
        entity.ChaseIntervalDays = request.ChaseIntervalDays;
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

        EfEditScopeStore.Complete(context, EditScopeKind.NamedConfiguration, GetWorkflowConfiguration.RecordId);
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
        if (snapshot.PolicyVersion != checked(request.ExpectedVersion + 1)
            || snapshot.RequireInstructions != request.RequireInstructions
            || snapshot.RequireImages != request.RequireImages
            || snapshot.ChaseIntervalDays != request.ChaseIntervalDays)
        {
            throw new WorkflowConfigurationOperationConflictException();
        }

        return Map(snapshot);
    }

    private static WorkflowConfigurationSnapshot Snapshot(WorkflowConfigurationEntity entity) => new(
        entity.Id,
        entity.Version,
        entity.RequireInstructions,
        entity.RequireImages,
        entity.ChaseIntervalDays);

    private static CaseWorkflowConfiguration Map(WorkflowConfigurationEntity entity) =>
        Map(Snapshot(entity));

    private static CaseWorkflowConfiguration Map(WorkflowConfigurationSnapshot snapshot) => new(
        snapshot.PolicyKey,
        snapshot.PolicyVersion)
    {
        RequireInstructions = snapshot.RequireInstructions,
        RequireImages = snapshot.RequireImages,
        ChaseIntervalDays = snapshot.ChaseIntervalDays
    };

    private sealed record WorkflowConfigurationSnapshot(
        string PolicyKey,
        int PolicyVersion,
        bool RequireInstructions,
        bool RequireImages,
        int ChaseIntervalDays);
}
