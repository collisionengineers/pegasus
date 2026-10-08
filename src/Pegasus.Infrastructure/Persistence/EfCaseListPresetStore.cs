using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Persists the shared Case list presets (MI-04) the way valuation presets
/// are kept: serializable writes, an operation key that replays, a version
/// check and a soft removal recorded in action history.
/// </summary>
internal sealed class EfCaseListPresetStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : ICaseListPresetStore
{
    private const string AggregateType = "case_list_preset";
    private const string SavedEventKind = "case_list_preset_saved";
    private const string RemovedEventKind = "case_list_preset_removed";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<CaseListPreset>> ListAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.Set<CaseListPresetEntity>()
            .AsNoTracking()
            .Where(item => item.RemovedAtUtc == null)
            .ToArrayAsync(cancellationToken);
        return [.. entities
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .Select(Map)];
    }

    public async Task<CaseListPreset> SaveAsync(SaveCaseListPresetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var replay = await ReplayAsync(context, request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            var replayed = Replay(replay, SavedEventKind, request.PresetId, request.Actor.SubjectId, request.ExpectedVersion);
            if (!string.Equals(replayed.Name, request.Name, StringComparison.Ordinal)
                || !replayed.ColumnKeys.SequenceEqual(request.ColumnKeys, StringComparer.Ordinal))
            {
                throw new CaseListPresetException(CaseListPresetError.OperationConflict);
            }

            await transaction.CommitAsync(cancellationToken);
            return replayed;
        }

        var entities = await context.Set<CaseListPresetEntity>().ToArrayAsync(cancellationToken);
        var entity = entities.SingleOrDefault(item => item.Id == request.PresetId);
        if (entity?.RemovedAtUtc is not null)
        {
            throw new CaseListPresetException(CaseListPresetError.Removed);
        }
        if (entities.Any(item => item.Id != request.PresetId
            && item.RemovedAtUtc is null
            && string.Equals(item.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new CaseListPresetException(CaseListPresetError.DuplicateName);
        }

        var now = timeProvider.GetUtcNow();
        CaseListPreset? before = null;
        if (request.ExpectedVersion == 0)
        {
            if (entity is not null)
            {
                throw new CaseListPresetException(CaseListPresetError.VersionConflict);
            }

            entity = new()
            {
                Id = request.PresetId,
                Name = request.Name,
                ColumnKeysJson = JsonSerializer.Serialize(request.ColumnKeys, SerializerOptions),
                UpdatedBy = request.Actor.SubjectId,
                UpdatedAtUtc = now,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid(),
            };
            context.Set<CaseListPresetEntity>().Add(entity);
        }
        else
        {
            if (entity is null)
            {
                throw new CaseListPresetException(CaseListPresetError.NotFound);
            }
            if (entity.Version != request.ExpectedVersion)
            {
                throw new CaseListPresetException(CaseListPresetError.VersionConflict);
            }

            before = Map(entity);
            entity.Name = request.Name;
            entity.ColumnKeysJson = JsonSerializer.Serialize(request.ColumnKeys, SerializerOptions);
            entity.UpdatedBy = request.Actor.SubjectId;
            entity.UpdatedAtUtc = now;
            entity.Version = checked(entity.Version + 1);
            entity.ConcurrencyToken = Guid.NewGuid();
        }

        var after = Map(entity);
        AddHistory(context, request.Actor, entity.Id, SavedEventKind, request.OperationKey, before, after, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return after;
    }

    public async Task<CaseListPreset> RemoveAsync(RemoveCaseListPresetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var replay = await ReplayAsync(context, request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            var replayed = Replay(replay, RemovedEventKind, request.PresetId, request.Actor.SubjectId, request.ExpectedVersion);
            if (replayed.RemovedAtUtc is null)
            {
                throw new CaseListPresetException(CaseListPresetError.OperationConflict);
            }

            await transaction.CommitAsync(cancellationToken);
            return replayed;
        }

        var entity = await context.Set<CaseListPresetEntity>()
                .SingleOrDefaultAsync(item => item.Id == request.PresetId, cancellationToken)
            ?? throw new CaseListPresetException(CaseListPresetError.NotFound);
        if (entity.RemovedAtUtc is not null)
        {
            throw new CaseListPresetException(CaseListPresetError.Removed);
        }
        if (entity.Version != request.ExpectedVersion)
        {
            throw new CaseListPresetException(CaseListPresetError.VersionConflict);
        }

        var now = timeProvider.GetUtcNow();
        var before = Map(entity);
        entity.RemovedAtUtc = now;
        entity.UpdatedBy = request.Actor.SubjectId;
        entity.UpdatedAtUtc = now;
        entity.Version = checked(entity.Version + 1);
        entity.ConcurrencyToken = Guid.NewGuid();
        var after = Map(entity);
        AddHistory(context, request.Actor, entity.Id, RemovedEventKind, request.OperationKey, before, after, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return after;
    }

    private static Task<ActionHistoryEntity?> ReplayAsync(
        PegasusDbContext context,
        string operationKey,
        CancellationToken cancellationToken) =>
        context.ActionHistory.AsNoTracking().SingleOrDefaultAsync(
            item => item.AggregateType == AggregateType && item.CorrelationId == operationKey,
            cancellationToken);

    private static CaseListPreset Replay(
        ActionHistoryEntity history,
        string eventKind,
        Guid presetId,
        string actorSubjectId,
        long expectedVersion)
    {
        if (history.EventKind != eventKind
            || history.AggregateId != presetId.ToString("D")
            || history.ActorSubjectId != actorSubjectId
            || history.AfterJson is null)
        {
            throw new CaseListPresetException(CaseListPresetError.OperationConflict);
        }

        var replayed = JsonSerializer.Deserialize<CaseListPreset>(history.AfterJson, SerializerOptions)
            ?? throw new CaseListPresetException(CaseListPresetError.OperationConflict);
        return replayed.Version == checked(expectedVersion + 1)
            ? replayed
            : throw new CaseListPresetException(CaseListPresetError.OperationConflict);
    }

    private static void AddHistory(
        PegasusDbContext context,
        Pegasus.Core.Identity.ActionActor actor,
        Guid presetId,
        string eventKind,
        string operationKey,
        CaseListPreset? before,
        CaseListPreset after,
        DateTimeOffset now) =>
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = AggregateType,
            AggregateId = presetId.ToString("D"),
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(actor.Roles.OrderBy(role => role), SerializerOptions),
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, SerializerOptions),
            AfterJson = JsonSerializer.Serialize(after, SerializerOptions),
            PolicyVersion = CaseListPresetPolicy.PolicyStamp,
        });

    private static CaseListPreset Map(CaseListPresetEntity entity) => new(
        entity.Id,
        entity.Name,
        JsonSerializer.Deserialize<string[]>(entity.ColumnKeysJson, SerializerOptions)
            ?? throw new InvalidDataException($"Case list preset '{entity.Id}' has no column list."),
        entity.Version,
        entity.UpdatedBy,
        entity.UpdatedAtUtc)
    {
        RemovedAtUtc = entity.RemovedAtUtc,
    };
}
