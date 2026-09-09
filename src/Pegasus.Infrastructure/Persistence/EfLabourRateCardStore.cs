using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfLabourRateCardStore(
    IDbContextFactory<PegasusDbContext> contextFactory, TimeProvider timeProvider) : ILabourRateCardStore
{
    public async Task<IReadOnlyList<LabourRateCard>> ListAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.LabourRateCards.AsNoTracking().OrderBy(x => x.Label).ThenBy(x => x.Id)
            .Select(x => new LabourRateCard(x.Id, x.Label, x.PanelRate, x.Active, x.Version))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<LabourRateCard> SaveAsync(SaveLabourRateCardRequest request, CancellationToken cancellationToken)
    {
        request = LabourRateCardAdministration.Validate(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await context.ActionHistory.AsNoTracking().SingleOrDefaultAsync(
            x => x.AggregateType == "labour_rate_card" && x.CorrelationId == request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            var saved = replay.AfterJson is null ? null : JsonSerializer.Deserialize<LabourRateCard>(replay.AfterJson);
            if (saved is null || saved.Id != request.Id || saved.Version != request.ExpectedVersion + 1
                || saved.Name != request.Name || saved.HourlyRate != request.HourlyRate || saved.Enabled != request.Enabled
                || replay.ActorKind != request.Actor.Kind.ToString() || replay.ActorSubjectId != request.Actor.SubjectId
                || replay.Reason != request.Reason)
                throw new LabourRateCardConflictException("This form was already used for another change.");
            return saved;
        }
        var entity = await context.LabourRateCards.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if ((entity?.Version ?? 0) != request.ExpectedVersion)
            throw new LabourRateCardConflictException("The labour-rate card changed. Reload it before saving.");
        if (await context.LabourRateCards.AnyAsync(x => x.Id != request.Id && x.Label == request.Name, cancellationToken))
            throw new ArgumentException("A labour-rate card already has that name.");
        var before = entity is null ? null : Map(entity);
        if (entity is not null)
            await EfEditScopeStore.RequireAsync(context, EditScopeKind.LabourRateCard, request.Id,
                entity.Version, request.ExpectedVersion, request.Actor, request.EditLeaseToken,
                timeProvider.GetUtcNow(), cancellationToken);
        if (entity is null)
        {
            entity = new LabourRateCardEntity { Id = request.Id, Label = request.Name, UpdatedBy = request.Actor.SubjectId };
            context.LabourRateCards.Add(entity);
        }
        entity.Label = request.Name;
        entity.PanelRate = request.HourlyRate;
        entity.Active = request.Enabled;
        entity.Version++;
        entity.ConcurrencyToken = Guid.NewGuid();
        entity.UpdatedBy = request.Actor.SubjectId;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        var after = Map(entity);
        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(), AggregateType = "labour_rate_card", AggregateId = entity.Id.ToString("D"),
            EventKind = "labour_rate_card_saved", ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId, ActorRolesJson = JsonSerializer.Serialize(request.Actor.Roles),
            OccurredAtUtc = entity.UpdatedAtUtc, Outcome = "succeeded", CorrelationId = request.OperationKey,
            Reason = request.Reason, BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after), PolicyVersion = "labour-rate-card/v1"
        });
        if (before is not null) EfEditScopeStore.Complete(context, EditScopeKind.LabourRateCard, request.Id);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return after;
    }

    private static LabourRateCard Map(LabourRateCardEntity entity) =>
        new(entity.Id, entity.Label, entity.PanelRate, entity.Active, entity.Version);
}
