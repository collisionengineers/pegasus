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
        var engineersValue = await WriteEngineersValueAsync(
            context,
            workflow,
            request.Actor,
            entity,
            previousSource: null,
            now,
            cancellationToken);
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
            engineersValue,
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
        var engineersValue = await WriteEngineersValueAsync(
            context,
            workflow,
            request.Actor,
            entity,
            before.Details.Source,
            now,
            cancellationToken);
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
            engineersValue,
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
            .ToArrayAsync(cancellationToken);
        return entities.OrderByDescending(OrderKey).Select(Map).ToArray();
    }

    /// <summary>
    /// The one order valuations are read in: the entered local date and time,
    /// newest first, with the audit time and the stable identity breaking
    /// exact ties. The table's row order and the case's current Engineer's
    /// Value are the same question, so they are never asked two ways.
    /// </summary>
    private static (DateOnly Date, TimeOnly Time, DateTimeOffset RecordedAtUtc, Guid Id) OrderKey(
        CaseValuationEntity item) => (item.Date, item.Time, item.RecordedAtUtc, item.Id);

    /// <summary>
    /// <c>assessment.values.engineer</c> is the one owner of the Engineer's
    /// Value the product consumes: Send to Claude's target percentage, the
    /// rendered report, and the Assessment screen all read that field.
    /// Recording or correcting an Engineer's Value row therefore writes it in
    /// this same transaction, from the case's latest Engineer's Value row, so
    /// the Valuations table stays the entry surface and never becomes a
    /// second owner. A row edited away from Engineer's Value re-resolves the
    /// field from the rows that remain; when none remain the field is removed
    /// so no stale Engineer's Value survives its last source row.
    /// </summary>
    private static async Task<EngineersValueChange?> WriteEngineersValueAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ActionActor actor,
        CaseValuationEntity saved,
        ValuationSource? previousSource,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var source = Map(saved).Details.Source;
        if (source != ValuationSource.EngineersValue
            && previousSource != ValuationSource.EngineersValue)
        {
            return null;
        }

        AssessmentPolicy.RequireFindingConfirmationAuthority(actor);
        if (!Enum.TryParse<CaseLifecycleState>(workflow.State, out var state)
            || !AssessmentPolicy.IsWritableState(state))
        {
            throw new InvalidOperationException(
                "An Engineer's Value can be recorded only while the assessment is open: "
                + "a Not ready, Review, or Report preparation case.");
        }

        var engineersValue = ValuationSource.EngineersValue.ToString();
        var others = await context.CaseValuations
            .Where(item => item.CaseId == workflow.CaseId
                && item.Source == engineersValue
                && item.Id != saved.Id)
            .ToArrayAsync(cancellationToken);
        var latest = (source == ValuationSource.EngineersValue ? others.Append(saved) : others)
            .OrderByDescending(OrderKey)
            .FirstOrDefault();
        var existing = await context.CaseAssessmentFields.SingleOrDefaultAsync(
            item => item.CaseId == workflow.CaseId
                && item.FieldPath == AssessmentVocabulary.ValueEngineer,
            cancellationToken);
        var before = existing?.Value;
        if (latest is null)
        {
            if (existing is null)
            {
                return null;
            }

            context.CaseAssessmentFields.Remove(existing);
            return new(before, After: null);
        }

        var selected = Map(latest);
        var value = ValuationPolicy.EngineersValueField(selected.Details)
            ?? throw new InvalidDataException(
                "The selected Engineer's Value row does not carry an Engineer's Value.");
        var recordedBy = selected.LastEditedBy ?? selected.RecordedBy;
        var recordedAtUtc = selected.LastEditedAtUtc ?? selected.RecordedAtUtc;
        var written = AssessmentFieldWriter.Write(
            context,
            workflow.Case,
            workflow.CaseId,
            existing,
            AssessmentVocabulary.ValueEngineer,
            value,
            ActorKind.Staff,
            recordedBy,
            recordedAtUtc,
            confirmedBy: recordedBy);
        return new(before, written.Value);
    }

    private sealed record EngineersValueChange(string? Before, string? After);

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
        EngineersValueChange? engineersValue,
        DateTimeOffset now)
    {
        var beforeVersion = workflow.Version - 1;
        var roles = JsonSerializer.Serialize(
            actor.Roles.OrderBy(role => role),
            SerializerOptions);
        var resultJson = JsonSerializer.Serialize(result, SerializerOptions);
        var beforeJson = before is null && engineersValue?.Before is null
            ? null
            : JsonSerializer.Serialize(
                new { Valuation = before, EngineersValue = engineersValue?.Before },
                SerializerOptions);
        var afterJson = JsonSerializer.Serialize(
            new { Valuation = result, EngineersValue = engineersValue?.After },
            SerializerOptions);
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
            AfterJson = afterJson,
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
