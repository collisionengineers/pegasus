using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfLabourRateCardStore(
    IDbContextFactory<PegasusDbContext> contextFactory, TimeProvider timeProvider) : ILabourRateCardStore
{
    public async Task<IReadOnlyList<LabourRateCard>> ListAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var cards = await context.LabourRateCards.AsNoTracking()
            .Where(x => LabourRateCardAdministration.Names.Contains(x.Label))
            .Select(x => new LabourRateCard(x.Id, x.Label, x.PanelRate, x.Version))
            .ToArrayAsync(cancellationToken);
        return LabourRateCardAdministration.Names.SelectMany(name => cards.Where(card => card.Name == name)).ToArray();
    }

    public async Task<LabourRateCard> SaveAsync(SaveLabourRateCardRequest request, CancellationToken cancellationToken)
    {
        request = LabourRateCardAdministration.Validate(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        var replay = await context.ActionHistory.AsNoTracking().SingleOrDefaultAsync(
            x => x.AggregateType == "labour_rate_card" && x.CorrelationId == request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            var saved = replay.AfterJson is null ? null : JsonSerializer.Deserialize<LabourRateCard>(replay.AfterJson);
            if (saved is null || saved.Id != request.Id || saved.Version != request.ExpectedVersion + 1
                || saved.HourlyRate != request.HourlyRate
                || replay.ActorKind != request.Actor.Kind.ToString() || replay.ActorSubjectId != request.Actor.SubjectId
                || replay.Reason != request.Reason)
                throw new LabourRateCardConflictException("This form was already used for another change.");
            return saved;
        }
        var entity = await context.LabourRateCards.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity is null || entity.Version != request.ExpectedVersion)
            throw new LabourRateCardConflictException("The labour-rate card changed. Reload it before saving.");
        var before = Map(entity);
        entity.PanelRate = request.HourlyRate;
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
            Reason = request.Reason, BeforeJson = JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after), PolicyVersion = "labour-rate-card/v2"
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return after;
    }

    private static LabourRateCard Map(LabourRateCardEntity entity) =>
        new(entity.Id, entity.Label, entity.PanelRate, entity.Version);
}
