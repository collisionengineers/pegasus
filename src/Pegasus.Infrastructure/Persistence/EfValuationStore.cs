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
    TimeProvider timeProvider) : IValuationStore, IAppliedValuationStore
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
            return RequireExactReplay<CaseValuation>(
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
            GuideMonth = request.Details.GuideMonth,
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
        // Manual valuation evidence (guide figures, and the Engineer's Value
        // field it may write) is frozen report input: the save stales the
        // current generation in this same transaction. Replay returned before
        // any mutation; a lookup or draft action never reaches this path.
        await EfCaseReportGenerationStore.MarkStaleAsync(
            context,
            request.CaseId,
            "valuation_recorded",
            now,
            cancellationToken);
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
            return RequireExactReplay<CaseValuation>(
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
        entity.GuideMonth = request.Details.GuideMonth;
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
        // Same stale rule as the save: an edited valuation changes frozen
        // report inputs (figures, guide month, and the Engineer's Value field
        // it may rewrite), and the staleness commits with the edit.
        await EfCaseReportGenerationStore.MarkStaleAsync(
            context,
            request.CaseId,
            "valuation_edited",
            now,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Adopts a calculated valuation as the Case's Engineer's Value. The
    /// selected guide card, the maintained presets, the claimant's own VAT
    /// position, the Case version, the edit lease and the Engineer's finding
    /// authority are all rechecked here — the form is the request, never the
    /// authority — and the accepted value and its whole ordered calculation
    /// are then written in one serializable transaction.
    /// </summary>
    public async Task<AppliedValuation> ApplyAsync(
        ApplyValuationRequest request,
        CancellationToken cancellationToken)
    {
        request = ValuationCalculationPolicy.ValidateApply(request);
        const string eventKind = "valuation_applied";
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
            return RequireExactReplay<AppliedValuation>(
                replay,
                eventKind,
                requestHash,
                request.CaseId,
                request.OperationKey);
        }

        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);

        var guideEntity = await RequiredGuideAsync(
            context,
            request.CaseId,
            request.Selection.GuideValuationId,
            cancellationToken);
        var basis = await ReadBasisAsync(context, request.CaseId, guideEntity, cancellationToken);
        if (basis.GuideValuationStampUtc != request.GuideValuationStampUtc)
        {
            throw new InvalidOperationException(
                "The selected guide valuation changed after the calculation was prepared.");
        }

        var calculation = ValuationCalculationPolicy.Calculate(
            ValuationCalculationPolicy.Resolve(request.Selection, basis));
        var accepted = ValuationCalculationPolicy.AcceptedValue(request, calculation);

        // The Valuations table stays the one entry surface of
        // assessment.values.engineer: the adoption writes an Engineer's Value
        // row and the existing field owner resolves the confirmed field from
        // it, so applying a calculation and typing a value cannot become two
        // owners of the same number.
        var adopted = new CaseValuationEntity
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            Case = workflow.Case,
            Source = ValuationSource.EngineersValue.ToString(),
            Date = DateOnly.FromDateTime(now.UtcDateTime),
            Time = TimeOnly.FromDateTime(now.UtcDateTime),
            Mileage = guideEntity.Mileage,
            RetailValue = accepted,
            TradeValue = 0m,
            RecordedBy = request.Actor.SubjectId,
            RecordedAtUtc = now,
        };
        ValuationPolicy.ValidateDetails(Map(adopted).Details);
        context.CaseValuations.Add(adopted);
        var engineersValue = await WriteEngineersValueAsync(
            context,
            workflow,
            request.Actor,
            adopted,
            previousSource: null,
            now,
            cancellationToken);

        var snapshot = new AppliedValuationSnapshot(
            workflow.Version,
            basis.GuideValuationId,
            basis.GuideValuationStampUtc,
            calculation);
        var snapshotJson = JsonSerializer.Serialize(snapshot, SerializerOptions);
        var reason = request.Reason.Trim();

        // The hash is what the case makes of the calculation, not when it was
        // made: the Case version is deliberately left out so that adopting
        // the same figures from the same card for the same reason a second
        // time is caught here rather than recorded twice.
        var snapshotHash = Hash(new
        {
            request.CaseId,
            snapshot.GuideValuationId,
            snapshot.GuideValuationStampUtc,
            snapshot.Calculation,
            Accepted = accepted,
            Reason = reason,
        });
        if (await context.Set<AppliedValuationSnapshotEntity>().AnyAsync(
                item => item.CaseId == request.CaseId && item.SnapshotHash == snapshotHash,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "This valuation calculation and reason were already applied to this case.");
        }

        var entity = new AppliedValuationSnapshotEntity
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            SnapshotJson = snapshotJson,
            CalculationPolicyVersion = ValuationCalculationPolicy.PolicyStamp,
            GeneratedByKind = request.Actor.Kind.ToString(),
            GeneratedBySubjectId = request.Actor.SubjectId,
            SnapshotHash = snapshotHash,
            AcceptedEngineerValue = accepted,
            AcceptedBy = request.Actor.SubjectId,
            AcceptedAtUtc = now,
            Reason = reason,
            PolicyVersion = $"{ValuationPolicy.PolicyKey}/v{ValuationPolicy.PolicyVersion}",
        };
        context.Set<AppliedValuationSnapshotEntity>().Add(entity);
        var result = Map(entity, snapshot);
        AddHistory(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            request.Reason,
            eventKind,
            requestHash,
            "case_applied_valuation",
            result.Id,
            JsonSerializer.Serialize(result, SerializerOptions),
            engineersValue?.Before is null
                ? null
                : JsonSerializer.Serialize(
                    new { EngineersValue = engineersValue.Before },
                    SerializerOptions),
            JsonSerializer.Serialize(
                new { AppliedValuation = result, EngineersValue = engineersValue?.After },
                SerializerOptions),
            ValuationCalculationPolicy.PolicyStamp,
            now);
        // The applied Engineer value is a frozen report input: adopting a new
        // one marks the Case's current generation stale in this same
        // transaction, so neither the adoption nor the staleness can land
        // without the other.
        await EfCaseReportGenerationStore.MarkStaleAsync(
            context,
            request.CaseId,
            "valuation_applied",
            now,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Every adoption this case has recorded, newest first. Earlier rows stay
    /// exactly as they were applied: a correction adds a row, it never edits
    /// one.
    /// </summary>
    public async Task<IReadOnlyList<AppliedValuation>> ListAppliedAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.Set<AppliedValuationSnapshotEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .ToArrayAsync(cancellationToken);
        return entities
            .OrderByDescending(item => item.AcceptedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => Map(item, null))
            .ToArray();
    }

    /// <summary>
    /// The facts a calculation is measured against, each read from its own
    /// owner rather than from the form: the selected guide card and the
    /// moment it was last written, the claimant's own VAT position, and the
    /// maintained presets. The preview and the adoption read exactly this, so
    /// the figures on screen and the figures recorded come from one place.
    /// </summary>
    public async Task<ValuationCalculationBasis> ReadBasisAsync(
        Guid caseId,
        Guid guideValuationId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var guide = await RequiredGuideAsync(
            context,
            caseId,
            guideValuationId,
            cancellationToken);
        return await ReadBasisAsync(context, caseId, guide, cancellationToken);
    }

    private static async Task<ValuationCalculationBasis> ReadBasisAsync(
        PegasusDbContext context,
        Guid caseId,
        CaseValuationEntity guideEntity,
        CancellationToken cancellationToken)
    {
        var guide = Map(guideEntity);
        var claimantVatField = await context.CaseAssessmentFields.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CaseId == caseId
                    && item.FieldPath == AssessmentVocabulary.SettlementClaimantVatRegistered,
                cancellationToken);
        var presets = await context.Set<ValuationPresetEntity>()
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
        return new(
            guide.ValuationId,
            StampOf(guide),
            guide.Details.RetailValue,
            string.Equals(claimantVatField?.Value, "true", StringComparison.Ordinal),
            [.. presets.Select(EfValuationPresetStore.Map)]);
    }

    private static async Task<CaseValuationEntity> RequiredGuideAsync(
        PegasusDbContext context,
        Guid caseId,
        Guid guideValuationId,
        CancellationToken cancellationToken)
    {
        var entity = await context.CaseValuations.SingleOrDefaultAsync(
            item => item.Id == guideValuationId && item.CaseId == caseId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The selected guide valuation was not found on this case.");
        if (Map(entity).Details.Source == ValuationSource.EngineersValue)
        {
            throw new InvalidOperationException(
                "An Engineer's Value cannot be the guide basis of another Engineer's Value.");
        }

        return entity;
    }

    /// <summary>
    /// A recorded card carries no version of its own, so the moment it was
    /// last written is what a calculation pins itself to.
    /// </summary>
    private static DateTimeOffset StampOf(CaseValuation valuation) =>
        valuation.LastEditedAtUtc ?? valuation.RecordedAtUtc;

    private static AppliedValuation Map(
        AppliedValuationSnapshotEntity entity,
        AppliedValuationSnapshot? snapshot)
    {
        snapshot ??= JsonSerializer.Deserialize<AppliedValuationSnapshot>(
                entity.SnapshotJson,
                SerializerOptions)
            ?? throw new InvalidDataException(
                "The persisted applied valuation snapshot is invalid.");
        return new(
            entity.Id,
            entity.CaseId,
            snapshot.CaseVersion,
            snapshot.GuideValuationId,
            snapshot.GuideValuationStampUtc,
            snapshot.Calculation,
            entity.AcceptedEngineerValue,
            entity.AcceptedBy,
            entity.AcceptedAtUtc,
            entity.Reason,
            entity.CalculationPolicyVersion);
    }

    private sealed record AppliedValuationSnapshot(
        long CaseVersion,
        Guid GuideValuationId,
        DateTimeOffset GuideValuationStampUtc,
        ValuationCalculation Calculation);

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

    internal sealed record EngineersValueChange(string? Before, string? After);

    private static Task<CaseWorkflowEventEntity?> FindReplayAsync(
        PegasusDbContext context,
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken) =>
        context.CaseWorkflowEvents.AsNoTracking().SingleOrDefaultAsync(
            item => item.CaseId == caseId && item.OperationKey == operationKey,
            cancellationToken);

    private static TResult RequireExactReplay<TResult>(
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

        return JsonSerializer.Deserialize<TResult>(replay.ResultJson, SerializerOptions)
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

    internal static CaseValuation Map(CaseValuationEntity entity)
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
                entity.TradeValue,
                entity.GuideMonth),
            entity.RecordedBy,
            entity.RecordedAtUtc,
            entity.LastEditedBy,
            entity.LastEditedAtUtc);
    }

    internal static void AddHistory(
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
        DateTimeOffset now) =>
        AddHistory(
            context,
            workflow,
            actor,
            operationKey,
            reason,
            eventKind,
            requestHash,
            "case_valuation",
            result.ValuationId,
            JsonSerializer.Serialize(result, SerializerOptions),
            before is null && engineersValue?.Before is null
                ? null
                : JsonSerializer.Serialize(
                    new { Valuation = before, EngineersValue = engineersValue?.Before },
                    SerializerOptions),
            JsonSerializer.Serialize(
                new { Valuation = result, EngineersValue = engineersValue?.After },
                SerializerOptions),
            $"{ValuationPolicy.PolicyKey}/v{ValuationPolicy.PolicyVersion}",
            now);

    /// <summary>
    /// The one history shape every valuation write records: the replayable
    /// workflow event, the action-history entry with its before/after payload,
    /// and the Case history line. Recording a card and adopting a calculated
    /// Engineer's Value differ only in what they put in those payloads.
    /// </summary>
    private static void AddHistory(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string operationKey,
        string reason,
        string eventKind,
        string requestHash,
        string aggregateType,
        Guid aggregateId,
        string resultJson,
        string? beforeJson,
        string afterJson,
        string policyVersion,
        DateTimeOffset now)
    {
        var beforeVersion = workflow.Version - 1;
        var roles = JsonSerializer.Serialize(
            actor.Roles.OrderBy(role => role),
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
            AggregateType = aggregateType,
            AggregateId = aggregateId.ToString("D"),
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
            PolicyVersion = policyVersion,
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

/// <summary>
/// Persists the maintained valuation additions an Engineer may select. The
/// approved five arrive with the schema, so this store never seeds: it reads
/// what is there and records the Administrator's own changes against it,
/// version by version.
/// </summary>
public sealed class EfValuationPresetStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IValuationPresetStore
{
    private const string AggregateType = "valuation_preset";
    private const string EventKind = "valuation_preset_saved";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ValuationPreset>> ListAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.Set<ValuationPresetEntity>()
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        // Disabled presets are listed too: history keeps naming them, and
        // the selection rule that refuses them lives in Core, not in the read.
        return [.. entities
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .Select(Map)];
    }

    public async Task<ValuationPreset> SaveAsync(
        SaveValuationPresetRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.ActionHistory.AsNoTracking().SingleOrDefaultAsync(
            item => item.AggregateType == AggregateType
                && item.CorrelationId == request.OperationKey,
            cancellationToken);
        if (replay is not null)
        {
            var replayed = Replay(request, replay);
            await transaction.CommitAsync(cancellationToken);
            return replayed;
        }

        var entities = await context.Set<ValuationPresetEntity>().ToArrayAsync(cancellationToken);
        var entity = entities.SingleOrDefault(item => item.Id == request.PresetId);
        if (entities.Any(item => item.Id != request.PresetId
            && string.Equals(item.Label, request.Label, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValuationPresetException(ValuationPresetError.DuplicateLabel);
        }

        var now = timeProvider.GetUtcNow();
        ValuationPreset? before = null;
        if (request.ExpectedVersion == 0)
        {
            if (entity is not null)
            {
                throw new ValuationPresetException(
                    ValuationPresetError.VersionConflict,
                    entity.Version);
            }

            entity = new()
            {
                Id = request.PresetId,
                Label = request.Label,
                SuggestedAmount = request.SuggestedAmount,
                Active = request.Active,
                UpdatedBy = request.Actor.SubjectId,
                UpdatedAtUtc = now,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid(),
            };
            context.Set<ValuationPresetEntity>().Add(entity);
        }
        else
        {
            if (entity is null)
            {
                throw new ValuationPresetException(ValuationPresetError.NotFound);
            }
            if (entity.Version != request.ExpectedVersion)
            {
                throw new ValuationPresetException(
                    ValuationPresetError.VersionConflict,
                    entity.Version);
            }

            before = Map(entity);
            entity.Label = request.Label;
            entity.SuggestedAmount = request.SuggestedAmount;
            entity.Active = request.Active;
            entity.UpdatedBy = request.Actor.SubjectId;
            entity.UpdatedAtUtc = now;
            entity.Version = checked(entity.Version + 1);
            entity.ConcurrencyToken = Guid.NewGuid();
        }

        var after = Map(entity);
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = AggregateType,
            AggregateId = entity.Id.ToString("D"),
            EventKind = EventKind,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                request.Actor.Roles.OrderBy(role => role),
                SerializerOptions),
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = request.OperationKey,
            Reason = request.Reason,
            BeforeJson = before is null
                ? null
                : JsonSerializer.Serialize(before, SerializerOptions),
            AfterJson = JsonSerializer.Serialize(after, SerializerOptions),
            PolicyVersion = ValuationCalculationPolicy.PolicyStamp,
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return after;
    }

    private static ValuationPreset Replay(
        SaveValuationPresetRequest request,
        ActionHistoryEntity history)
    {
        if (history.EventKind != EventKind
            || history.AggregateId != request.PresetId.ToString("D")
            || history.ActorSubjectId != request.Actor.SubjectId
            || history.Reason != request.Reason
            || history.AfterJson is null)
        {
            throw new ValuationPresetException(ValuationPresetError.OperationConflict);
        }

        var replayed = JsonSerializer.Deserialize<ValuationPreset>(
                history.AfterJson,
                SerializerOptions)
            ?? throw new ValuationPresetException(ValuationPresetError.OperationConflict);
        if (replayed.Version != checked(request.ExpectedVersion + 1)
            || !string.Equals(replayed.Label, request.Label, StringComparison.Ordinal)
            || replayed.SuggestedAmount != request.SuggestedAmount
            || replayed.Active != request.Active)
        {
            throw new ValuationPresetException(ValuationPresetError.OperationConflict);
        }

        return replayed;
    }

    internal static ValuationPreset Map(ValuationPresetEntity entity) => new(
        entity.Id,
        entity.Label,
        entity.SuggestedAmount,
        entity.Active,
        entity.Version,
        entity.UpdatedBy,
        entity.UpdatedAtUtc);
}
