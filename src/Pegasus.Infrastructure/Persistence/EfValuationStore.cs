using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Persists Case valuations under the same case-version, edit-lease,
/// operation replay and permanent-history guards as named estimates.
/// </summary>
public sealed class EfValuationStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IValuationStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<CaseValuation> SaveAsync(
        SaveValuationRequest request,
        CancellationToken cancellationToken)
    {
        request = ValuationPolicy.ValidateSave(request);
        const string eventKind = "valuation_created";
        var requestHash = Hash(request);

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
            return RequireExactReplay(
                replay,
                eventKind,
                requestHash,
                request.CaseId,
                request.OperationKey);
        }

        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);
        var entity = new CaseValuationEntity
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            Case = workflow.Case,
            Source = request.Details.Source.ToString(),
            Date = request.Details.Date,
            Time = request.Details.Time,
            Mileage = request.Details.Mileage,
            RetailValue = request.Details.RetailValue,
            TradeValue = request.Details.TradeValue,
            RecordedBy = request.Actor.SubjectId,
            RecordedAtUtc = now,
        };
        context.CaseValuations.Add(entity);
        var result = Map(entity);
        AddHistory(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            request.Reason,
            eventKind,
            requestHash,
            result,
            before: null,
            now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<CaseValuation> EditAsync(
        EditValuationRequest request,
        CancellationToken cancellationToken)
    {
        request = ValuationPolicy.ValidateEdit(request);
        const string eventKind = "valuation_updated";
        var requestHash = Hash(request);

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
            return RequireExactReplay(
                replay,
                eventKind,
                requestHash,
                request.CaseId,
                request.OperationKey);
        }

        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);
        var entity = await context.CaseValuations.SingleOrDefaultAsync(
            item => item.Id == request.ValuationId && item.CaseId == request.CaseId,
            cancellationToken)
            ?? throw new InvalidOperationException("The valuation was not found on this case.");
        var before = Map(entity);
        entity.Source = request.Details.Source.ToString();
        entity.Date = request.Details.Date;
        entity.Time = request.Details.Time;
        entity.Mileage = request.Details.Mileage;
        entity.RetailValue = request.Details.RetailValue;
        entity.TradeValue = request.Details.TradeValue;
        entity.LastEditedBy = request.Actor.SubjectId;
        entity.LastEditedAtUtc = now;
        var result = Map(entity);
        AddHistory(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            request.Reason,
            eventKind,
            requestHash,
            result,
            before,
            now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<CaseValuation>> ListForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.CaseValuations
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.Time)
            .ThenByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return entities.Select(Map).ToArray();
    }

    private static Task<CaseWorkflowEventEntity?> FindReplayAsync(
        PegasusDbContext context,
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken) =>
        context.CaseWorkflowEvents.AsNoTracking().SingleOrDefaultAsync(
            item => item.CaseId == caseId && item.OperationKey == operationKey,
            cancellationToken);

    private static CaseValuation RequireExactReplay(
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

        return JsonSerializer.Deserialize<CaseValuation>(replay.ResultJson, SerializerOptions)
            ?? throw new InvalidDataException("The persisted valuation replay result is invalid.");
    }

    private static async Task<CaseWorkflowEntity> RequiredWorkflowAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken) =>
        await context.CaseWorkflows
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
        ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");

    private static void Guard(
        CaseWorkflowEntity workflow,
        long expectedVersion,
        ActionActor actor,
        string lease,
        DateTimeOffset now)
    {
        CaseMutationGuard.RequireVersion(workflow, expectedVersion);
        CaseMutationGuard.RequireLease(workflow, actor, lease, now);
        ArchivedCaseGuard.RequireMutable(workflow);
        workflow.Version++;
        CaseMutationGuard.ClearLease(workflow);
    }

    private DateTimeOffset Now()
    {
        var now = timeProvider.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }

    private static string Hash<T>(T request) =>
        Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, SerializerOptions))));

    private static CaseValuation Map(CaseValuationEntity entity)
    {
        if (!Enum.TryParse<ValuationSource>(entity.Source, out var source)
            || !ValuationSources.IsSupported(source))
        {
            throw new InvalidDataException(
                $"Unknown persisted valuation source '{entity.Source}'.");
        }

        return new(
            entity.Id,
            entity.CaseId,
            new(
                source,
                entity.Date,
                entity.Time,
                entity.Mileage,
                entity.RetailValue,
                entity.TradeValue),
            entity.RecordedBy,
            entity.RecordedAtUtc,
            entity.LastEditedBy,
            entity.LastEditedAtUtc);
    }

    private static void AddHistory(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string operationKey,
        string reason,
        string eventKind,
        string requestHash,
        CaseValuation result,
        CaseValuation? before,
        DateTimeOffset now)
    {
        var beforeVersion = workflow.Version - 1;
        var roles = JsonSerializer.Serialize(
            actor.Roles.OrderBy(role => role),
            SerializerOptions);
        var resultJson = JsonSerializer.Serialize(result, SerializerOptions);
        var beforeJson = before is null
            ? null
            : JsonSerializer.Serialize(before, SerializerOptions);
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
            ActorRolesJson = roles,
            Reason = reason.Trim(),
            OccurredAtUtc = now,
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version,
            ResultJson = resultJson,
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case_valuation",
            AggregateId = result.ValuationId.ToString("D"),
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = roles,
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason.Trim(),
            BeforeJson = beforeJson,
            AfterJson = resultJson,
            PolicyVersion = $"{ValuationPolicy.PolicyKey}/v{ValuationPolicy.PolicyVersion}",
        });
        context.CaseHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Case = workflow.Case,
            EventType = eventKind,
            Actor = actor.SubjectId,
            Reason = reason.Trim(),
            OccurredAtUtc = now,
            OperationKey = operationKey,
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version,
        });
    }
}
