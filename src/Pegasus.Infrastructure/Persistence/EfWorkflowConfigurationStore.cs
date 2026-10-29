using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfWorkflowConfigurationStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    IMemoryCache cache) : IWorkflowConfigurationStore
{
    private const string AggregateType = "workflow_configuration";
    private const string EventKind = "workflow_configuration_updated";

    // The current configuration is kept for this long. Web's update evicts its
    // own copy after it commits; the Worker composes this store too and its copy
    // cannot be evicted from Web, so a Configuration change reaches Worker intake
    // within this lifetime (R342). The reads inside a store's own transaction use
    // ReadAsync and are never cached.
    internal static readonly TimeSpan CurrentLifetime = TimeSpan.FromSeconds(30);

    // A private key object, so no other IMemoryCache user can collide with the entry.
    private static readonly object CurrentKey = new();

    internal static async Task<CaseWorkflowConfiguration> ReadAsync(
        PegasusDbContext context, CancellationToken cancellationToken)
    {
        var entity = await context.Set<WorkflowConfigurationEntity>().AsNoTracking()
            .SingleAsync(item => item.Id == AdministrationPolicyModelConfiguration.WorkflowPolicyKey,
                cancellationToken);
        return Map(entity);
    }

    public Task<CaseWorkflowConfiguration> GetCurrentAsync(
        CancellationToken cancellationToken) =>
        EvictableCacheEntry.GetOrReadAsync(
            cache, CurrentKey, CurrentLifetime, slidingWindow: null, ReadCurrentAsync, cancellationToken);

    private async Task<CaseWorkflowConfiguration> ReadCurrentAsync(
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
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);

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
            throw new WorkflowConfigurationVersionConflictException();
        }

        var before = Snapshot(entity);
        entity.RequireInstructions = request.RequireInstructions;
        entity.RequireImages = request.RequireImages;
        entity.ChaseIntervalDays = request.ChaseIntervalDays;
        entity.UnidentifiedTargetDays = request.UnidentifiedTargetDays;
        entity.TriageTargetDays = request.TriageTargetDays;
        entity.HeldTargetDays = request.HeldTargetDays;
        entity.ReviewTargetDays = request.ReviewTargetDays;
        entity.AiDraftTargetDays = request.AiDraftTargetDays;
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
            Reason = null,
            BeforeJson = JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after),
            PolicyVersion = $"{entity.Id}/v{entity.Version}"
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        EvictableCacheEntry.Evict(cache, CurrentKey);
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
            || history.AfterJson is null)
        {
            throw new WorkflowConfigurationOperationConflictException();
        }

        var snapshot = JsonSerializer.Deserialize<WorkflowConfigurationSnapshot>(history.AfterJson)
            ?? throw new WorkflowConfigurationOperationConflictException();
        if (snapshot.PolicyVersion != checked(request.ExpectedVersion + 1)
            || snapshot.RequireInstructions != request.RequireInstructions
            || snapshot.RequireImages != request.RequireImages
            || snapshot.ChaseIntervalDays != request.ChaseIntervalDays
            || snapshot.UnidentifiedTargetDays != request.UnidentifiedTargetDays
            || snapshot.TriageTargetDays != request.TriageTargetDays
            || snapshot.HeldTargetDays != request.HeldTargetDays
            || snapshot.ReviewTargetDays != request.ReviewTargetDays
            || snapshot.AiDraftTargetDays != request.AiDraftTargetDays)
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
        entity.ChaseIntervalDays,
        entity.UnidentifiedTargetDays,
        entity.TriageTargetDays,
        entity.HeldTargetDays,
        entity.ReviewTargetDays,
        entity.AiDraftTargetDays);

    internal static CaseWorkflowConfiguration Map(WorkflowConfigurationEntity entity) =>
        Map(Snapshot(entity));

    private static CaseWorkflowConfiguration Map(WorkflowConfigurationSnapshot snapshot) => new(
        snapshot.PolicyKey,
        snapshot.PolicyVersion)
    {
        RequireInstructions = snapshot.RequireInstructions,
        RequireImages = snapshot.RequireImages,
        ChaseIntervalDays = snapshot.ChaseIntervalDays,
        UnidentifiedTargetDays = snapshot.UnidentifiedTargetDays,
        TriageTargetDays = snapshot.TriageTargetDays,
        HeldTargetDays = snapshot.HeldTargetDays,
        ReviewTargetDays = snapshot.ReviewTargetDays,
        AiDraftTargetDays = snapshot.AiDraftTargetDays
    };

    private sealed record WorkflowConfigurationSnapshot(
        string PolicyKey,
        int PolicyVersion,
        bool RequireInstructions,
        bool RequireImages,
        int ChaseIntervalDays,
        int UnidentifiedTargetDays = 0,
        int TriageTargetDays = 1,
        int HeldTargetDays = 7,
        int ReviewTargetDays = 1,
        int AiDraftTargetDays = 1);
}
