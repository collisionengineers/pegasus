using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfApprovedOutlookCategoryStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IApprovedOutlookCategoryStore, IApprovedOutlookCategoryResolver
{
    private const string AggregateType = "approved_outlook_category";
    private const string EventKind = "approved_outlook_category_updated";

    public async Task<IReadOnlyList<ApprovedOutlookCategory>> ListAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return (await context.Set<ApprovedOutlookCategoryEntity>().AsNoTracking()
            .OrderBy(item => item.DisplayName).ThenBy(item => item.Id).ToListAsync(cancellationToken))
            .Select(Map).ToArray();
    }

    public async Task<ApprovedOutlookCategory?> ResolveActiveAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var active = ApprovedOutlookCategoryState.Active.ToString();
        var entity = await context.Set<ApprovedOutlookCategoryEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == categoryId && item.State == active, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<ApprovedOutlookCategory> UpdateAsync(
        UpdateApprovedOutlookCategoryRequest request,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // Serialize every update/replay for one category before reading its operation
        // history. A concurrent retry with the same key then observes the committed
        // history, while a competing key observes the committed version.
        var entity = await FindForUpdateAsync(context, request.CategoryId, cancellationToken);
        var replay = await context.ActionHistory.AsNoTracking().SingleOrDefaultAsync(
            item => item.AggregateType == AggregateType && item.CorrelationId == request.OperationKey,
            cancellationToken);
        if (replay is not null)
        {
            var result = Replay(request, replay);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }

        var normalizedName = request.DisplayName.ToUpperInvariant();
        if (await context.Set<ApprovedOutlookCategoryEntity>().AsNoTracking().AnyAsync(
            item => item.NormalizedDisplayName == normalizedName && item.Id != request.CategoryId,
            cancellationToken))
            throw new ApprovedOutlookCategoryUpdateException(ApprovedOutlookCategoryUpdateError.DuplicateDisplayName);

        Snapshot? before = null;
        if (request.ExpectedVersion == 0)
        {
            if (entity is not null)
                throw new ApprovedOutlookCategoryUpdateException(ApprovedOutlookCategoryUpdateError.VersionConflict, entity.Version);
            entity = new ApprovedOutlookCategoryEntity
            {
                Id = request.CategoryId, DisplayName = request.DisplayName,
                NormalizedDisplayName = normalizedName, State = request.State.ToString(), Version = 1
            };
            context.Set<ApprovedOutlookCategoryEntity>().Add(entity);
        }
        else
        {
            if (entity is null) throw new ApprovedOutlookCategoryUpdateException(ApprovedOutlookCategoryUpdateError.NotFound);
            if (entity.Version != request.ExpectedVersion)
                throw new ApprovedOutlookCategoryUpdateException(ApprovedOutlookCategoryUpdateError.VersionConflict, entity.Version);
            before = TakeSnapshot(entity);
            entity.DisplayName = request.DisplayName;
            entity.NormalizedDisplayName = normalizedName;
            entity.State = request.State.ToString();
            entity.Version = checked(entity.Version + 1);
        }

        var after = TakeSnapshot(entity);
        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(), AggregateType = AggregateType, AggregateId = entity.Id.ToString("D"),
            EventKind = EventKind, ActorKind = request.Actor.Kind.ToString(), ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(request.Actor.Roles.OrderBy(role => role).Select(role => role.ToString())),
            OccurredAtUtc = timeProvider.GetUtcNow(), Outcome = "succeeded", CorrelationId = request.OperationKey,
            Reason = request.Reason, BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after), PolicyVersion = $"approved-outlook-category/v{entity.Version}"
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(after);
    }

    private static Task<ApprovedOutlookCategoryEntity?> FindForUpdateAsync(
        PegasusDbContext context,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var categories = context.Set<ApprovedOutlookCategoryEntity>();
        return context.Database.IsSqlServer()
            ? categories.FromSqlInterpolated(
                    $"SELECT * FROM [dbo].[ApprovedOutlookCategories] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {categoryId}")
                .SingleOrDefaultAsync(cancellationToken)
            : categories.SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken);
    }

    private static ApprovedOutlookCategory Replay(UpdateApprovedOutlookCategoryRequest request, ActionHistoryEntity history)
    {
        var snapshot = history.AfterJson is null ? null : JsonSerializer.Deserialize<Snapshot>(history.AfterJson);
        if (snapshot is null || history.AggregateId != request.CategoryId.ToString("D")
            || history.EventKind != EventKind || history.ActorKind != request.Actor.Kind.ToString()
            || history.ActorSubjectId != request.Actor.SubjectId || history.Reason != request.Reason
            || snapshot.DisplayName != request.DisplayName || snapshot.State != request.State
            || snapshot.Version != checked(request.ExpectedVersion + 1))
            throw new ApprovedOutlookCategoryUpdateException(ApprovedOutlookCategoryUpdateError.OperationConflict);
        return Map(snapshot);
    }

    private static Snapshot TakeSnapshot(ApprovedOutlookCategoryEntity entity) =>
        new(entity.Id, entity.DisplayName, Enum.Parse<ApprovedOutlookCategoryState>(entity.State), entity.Version);
    private static ApprovedOutlookCategory Map(ApprovedOutlookCategoryEntity entity) => Map(TakeSnapshot(entity));
    private static ApprovedOutlookCategory Map(Snapshot snapshot) => new(snapshot.Id, snapshot.DisplayName, snapshot.State, snapshot.Version);
    private sealed record Snapshot(Guid Id, string DisplayName, ApprovedOutlookCategoryState State, int Version);
}
