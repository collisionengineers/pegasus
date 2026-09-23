using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
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
                replay.EventType == "valuation_replaced" ? "valuation_replaced" : eventKind,
                requestHash,
                request.CaseId,
                request.OperationKey);
        }

        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);
        var beforeDependencies = await ReadReportDependenciesAsync(
            context,
            request.CaseId,
            cancellationToken);
        // A card for the same source and guide month is replaced in place: the
        // Valuations table does not version rows, so the earlier figures survive
        // only in the history entry written below.
        var replaced = await FindReplacedAsync(context, request.CaseId, request.Details, cancellationToken);
        var before = replaced is null ? null : Map(replaced);
        var entity = replaced ?? new CaseValuationEntity
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            Case = workflow.Case,
            Source = request.Details.Source.ToString(),
            RecordedBy = request.Actor.SubjectId,
            RecordedAtUtc = now,
        };
        Write(entity, request.Details, replaced is null ? null : request.Actor.SubjectId, now);
        if (replaced is null)
        {
            context.CaseValuations.Add(entity);
        }

        var result = Map(entity);
        var engineersValue = await WriteEngineersValueAsync(
            context,
            workflow,
            request.Actor,
            entity,
            previousSource: before?.Details.Source,
            now,
            cancellationToken);
        AddHistory(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            request.Reason,
            replaced is null ? eventKind : "valuation_replaced",
            requestHash,
            result,
            before,
            engineersValue,
            now);
        var afterDependencies = WithEngineersValue(beforeDependencies, engineersValue) with
        {
            UsesGlassesValuationGuide = beforeDependencies.UsesGlassesValuationGuide
                || request.Details.Source == ValuationSource.Glasses,
        };
        await MarkStaleIfNeededAsync(
            context,
            request.CaseId,
            beforeDependencies,
            afterDependencies,
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
        var beforeDependencies = await ReadReportDependenciesAsync(
            context,
            request.CaseId,
            cancellationToken);
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
        var anotherGlassesGuideExists = await context.CaseValuations.AsNoTracking().AnyAsync(
            item => item.CaseId == request.CaseId
                && item.Id != entity.Id
                && item.Source == nameof(ValuationSource.Glasses),
            cancellationToken);
        var afterDependencies = WithEngineersValue(beforeDependencies, engineersValue) with
        {
            UsesGlassesValuationGuide = request.Details.Source == ValuationSource.Glasses
                || anotherGlassesGuideExists,
        };
        await MarkStaleIfNeededAsync(
            context,
            request.CaseId,
            beforeDependencies,
            afterDependencies,
            now,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Adopts a calculated valuation as the Case's Engineer's Value inside the
    /// Case save's own transaction (23 September 2026: one Save, which adopts
    /// only a calculation the operator changed). The basis card, the
    /// maintained presets and the Engineer's finding authority are rechecked
    /// here — the form is the request, never the authority. The basis is the
    /// card as this save leaves it: when the save recorded the basis source's
    /// card for a new guide month, that new card is the one on screen. The
    /// claimant's VAT position is the one this save records. The Case save
    /// owns the version, the workflow event and the history line; this writes
    /// the Engineer's Value row and field, the applied snapshot and its
    /// action-history entry. No stamp is checked: every writer of a guide
    /// card moves the Case version, which the save has already checked.
    /// </summary>
    internal static async Task<ValuationAdopted> AdoptAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string operationKey,
        ValuationCalculationSelection selection,
        IReadOnlyList<CaseValuationEntity> writtenBySave,
        bool claimantVatRegistered,
        long resultingCaseVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var posted = await RequiredGuideAsync(
            context,
            workflow.CaseId,
            selection.GuideValuationId,
            cancellationToken);
        var guideEntity = writtenBySave.LastOrDefault(item => item.Source == posted.Source) ?? posted;
        var basis = await ReadBasisAsync(
            context,
            workflow.CaseId,
            guideEntity,
            claimantVatRegistered,
            cancellationToken);
        var calculation = ValuationCalculationPolicy.Calculate(
            ValuationCalculationPolicy.Resolve(selection, basis));
        var accepted = ValuationCalculationPolicy.AcceptedValue(calculation);

        // The Valuations table stays the one entry surface of
        // assessment.values.engineer: the adoption writes an Engineer's Value
        // row and the existing field owner resolves the confirmed field from
        // it, so applying a calculation and typing a value cannot become two
        // owners of the same number.
        var adopted = new CaseValuationEntity
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Case = workflow.Case,
            Source = ValuationSource.EngineersValue.ToString(),
            Date = DateOnly.FromDateTime(now.UtcDateTime),
            Time = TimeOnly.FromDateTime(now.UtcDateTime),
            Mileage = guideEntity.Mileage,
            RetailValue = accepted,
            TradeValue = 0m,
            RecordedBy = actor.SubjectId,
            RecordedAtUtc = now,
        };
        ValuationPolicy.ValidateDetails(Map(adopted).Details);
        context.CaseValuations.Add(adopted);
        var engineersValue = await WriteEngineersValueAsync(
            context,
            workflow,
            actor,
            adopted,
            previousSource: null,
            now,
            cancellationToken);

        var snapshot = new AppliedValuationSnapshot(
            resultingCaseVersion,
            basis.GuideValuationId,
            basis.GuideValuationStampUtc,
            calculation);
        var snapshotJson = JsonSerializer.Serialize(snapshot, SerializerOptions);
        const string reason = ValuationCalculationPolicy.AppliedReason;

        // The Case version this adoption produced is part of what it is, so
        // returning to an earlier calculation in a later save is a new
        // adoption rather than a repeat of the first.
        var snapshotHash = Hash(new
        {
            workflow.CaseId,
            snapshot.CaseVersion,
            snapshot.GuideValuationId,
            snapshot.GuideValuationStampUtc,
            snapshot.Calculation,
            Accepted = accepted,
            Reason = reason,
        });
        var entity = new AppliedValuationSnapshotEntity
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            SnapshotJson = snapshotJson,
            CalculationPolicyVersion = ValuationCalculationPolicy.PolicyStamp,
            GeneratedByKind = actor.Kind.ToString(),
            GeneratedBySubjectId = actor.SubjectId,
            SnapshotHash = snapshotHash,
            AcceptedEngineerValue = accepted,
            AcceptedBy = actor.SubjectId,
            AcceptedAtUtc = now,
            Reason = reason,
            PolicyVersion = $"{ValuationPolicy.PolicyKey}/v{ValuationPolicy.PolicyVersion}",
        };
        context.Set<AppliedValuationSnapshotEntity>().Add(entity);
        var result = Map(entity, snapshot);
        AddActionHistory(
            context,
            actor,
            "case_applied_valuation",
            result.Id,
            "valuation_applied",
            operationKey,
            reason,
            engineersValue?.Before is null
                ? null
                : JsonSerializer.Serialize(new { EngineersValue = engineersValue.Before }, SerializerOptions),
            JsonSerializer.Serialize(
                new { AppliedValuation = result, EngineersValue = engineersValue?.After },
                SerializerOptions),
            ValuationCalculationPolicy.PolicyStamp,
            now);
        return new(result, engineersValue);
    }

    /// <summary>An adoption a Case save recorded, and the Engineer's Value field either side of it.</summary>
    internal sealed record ValuationAdopted(AppliedValuation Applied, EngineersValueChange? EngineersValue)
    {
        /// <summary>The report's valuation dependencies once this adoption stands.</summary>
        public CaseReportValuationDependencies Apply(CaseReportValuationDependencies dependencies) =>
            WithEngineersValue(dependencies, EngineersValue) with
            {
                AppliedValuationId = Applied.Id,
                AcceptedEngineerValue = Applied.AcceptedEngineerValue,
                AppliedValuationReason = Applied.Reason,
            };
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
    /// moment it was last written, the claimant's own VAT position, the
    /// maintained presets, and the additions this Case has already recorded.
    /// The preview and the adoption read exactly this, so the figures on
    /// screen and the figures recorded come from one place.
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
        var claimantVatField = await context.CaseAssessmentFields.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CaseId == caseId
                    && item.FieldPath == AssessmentVocabulary.SettlementClaimantVatRegistered,
                cancellationToken);
        return await ReadBasisAsync(
            context,
            caseId,
            guide,
            string.Equals(claimantVatField?.Value, "true", StringComparison.Ordinal),
            cancellationToken);
    }

    private static async Task<ValuationCalculationBasis> ReadBasisAsync(
        PegasusDbContext context,
        Guid caseId,
        CaseValuationEntity guideEntity,
        bool claimantVatRegistered,
        CancellationToken cancellationToken)
    {
        var guide = Map(guideEntity);
        // Every preset row, disabled and removed included: the selection
        // rules that refuse them live in Core, so the read stays a read.
        var presets = await context.Set<ValuationPresetEntity>()
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
        var snapshots = await context.Set<AppliedValuationSnapshotEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .ToArrayAsync(cancellationToken);
        return new(
            guide.ValuationId,
            StampOf(guide),
            // The calculation starts from retail; a card recorded without one
            // is never offered as the basis, so this refuses only a stale page.
            guide.Details.RetailValue
                ?? throw new ArgumentException("The chosen guide valuation has no retail value to calculate from."),
            claimantVatRegistered,
            [.. presets.Select(EfValuationPresetStore.Map)])
        {
            RecordedAdditions =
            [
                .. snapshots
                    .SelectMany(item => Map(item, null).Calculation.Additions)
                    .Where(addition => addition.PresetId != Guid.Empty)
                    .DistinctBy(addition => (addition.PresetId, addition.PresetVersion))
            ]
        };
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
                "An Engineer's Value cannot be recorded in the Case's current state.");
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

    private static async Task<CaseReportValuationDependencies> ReadReportDependenciesAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var usesGlasses = await context.CaseValuations.AsNoTracking().AnyAsync(
            item => item.CaseId == caseId
                && item.Source == nameof(ValuationSource.Glasses),
            cancellationToken);
        var engineersValue = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.CaseId == caseId
                && item.FieldPath == AssessmentVocabulary.ValueEngineer)
            .Select(item => item.Value)
            .SingleOrDefaultAsync(cancellationToken);
        var applied = await context.Set<AppliedValuationSnapshotEntity>().AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderByDescending(item => item.AcceptedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new
            {
                item.Id,
                item.AcceptedEngineerValue,
                item.Reason,
            })
            .FirstOrDefaultAsync(cancellationToken);
        return new(
            usesGlasses,
            engineersValue,
            applied?.Id,
            applied?.AcceptedEngineerValue,
            applied?.Reason);
    }

    private static CaseReportValuationDependencies WithEngineersValue(
        CaseReportValuationDependencies dependencies,
        EngineersValueChange? change) => change is null
            ? dependencies
            : dependencies with { EngineersValue = change.After };

    internal static async Task MarkStaleIfNeededAsync(
        PegasusDbContext context,
        Guid caseId,
        CaseReportValuationDependencies before,
        CaseReportValuationDependencies after,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var freshness = CaseReportFreshness.ClassifyValuation(before, after);
        if (freshness.IsStale)
        {
            await EfCaseReportGenerationStore.MarkStaleAsync(
                context,
                caseId,
                freshness.ReasonCode!,
                now,
                cancellationToken);
        }
    }

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
        ArchivedCaseGuard.RequireMutable(workflow);
        if (!Enum.TryParse<CaseLifecycleState>(workflow.State, out var state)
            || !AssessmentPolicy.IsWritableState(state))
        {
            throw new InvalidOperationException(
                "The assessment is read-only in the current case state.");
        }
        CaseMutationGuard.RequireLease(workflow, actor, lease, now);
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

    /// <summary>
    /// The existing card the incoming figures replace (<see cref="ValuationPolicy.Replaces"/>):
    /// same source, same guide month, on this Case. Null when the figures are a new card.
    /// </summary>
    internal static async Task<CaseValuationEntity?> FindReplacedAsync(
        PegasusDbContext context,
        Guid caseId,
        ValuationDetails details,
        CancellationToken cancellationToken)
    {
        if (details.GuideMonth is not { } guideMonth)
        {
            return null;
        }

        var source = details.Source.ToString();
        return await context.CaseValuations
            .Where(item => item.CaseId == caseId && item.Source == source && item.GuideMonth == guideMonth)
            .OrderByDescending(item => item.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    internal static void Write(
        CaseValuationEntity entity,
        ValuationDetails details,
        string? editedBy,
        DateTimeOffset now)
    {
        entity.Source = details.Source.ToString();
        entity.Date = details.Date;
        entity.Time = details.Time;
        entity.GuideMonth = details.GuideMonth;
        entity.Mileage = details.Mileage;
        entity.RetailValue = details.RetailValue;
        entity.TradeValue = details.TradeValue;
        if (editedBy is not null)
        {
            entity.LastEditedBy = editedBy;
            entity.LastEditedAtUtc = now;
        }
    }

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

    /// <summary>
    /// The guide source cards a Case save records, written inside the Case
    /// save's own transaction (23 September 2026: the source cards have no Save
    /// of their own). Each card replaces the same source's card for the same
    /// guide month, as <see cref="SaveAsync"/> does. A card whose figures are
    /// already the recorded ones is left untouched, because rewriting it would
    /// move its last-written stamp. The Case save owns the version, the
    /// workflow event and the history line, so this writes only the rows and
    /// their action-history entries.
    /// </summary>
    internal static async Task<GuideEntriesRecorded> RecordGuideEntriesAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string operationKey,
        IReadOnlyList<ValuationDetails> entries,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var beforeDependencies = await ReadReportDependenciesAsync(context, workflow.CaseId, cancellationToken);
        var recorded = new List<ValuationDetails>(entries.Count);
        var written = new List<CaseValuationEntity>(entries.Count);
        foreach (var details in entries)
        {
            var replaced = await FindReplacedAsync(context, workflow.CaseId, details, cancellationToken);
            var before = replaced is null ? null : Map(replaced);
            if (before is not null && ValuationPolicy.IsUnchanged(details, before.Details))
            {
                continue;
            }

            var entity = replaced ?? new CaseValuationEntity
            {
                Id = Guid.NewGuid(),
                CaseId = workflow.CaseId,
                Case = workflow.Case,
                Source = details.Source.ToString(),
                RecordedBy = actor.SubjectId,
                RecordedAtUtc = now,
            };
            Write(entity, details, replaced is null ? null : actor.SubjectId, now);
            if (replaced is null)
            {
                context.CaseValuations.Add(entity);
            }

            var result = Map(entity);
            AddActionHistory(
                context,
                actor,
                "case_valuation",
                result.ValuationId,
                replaced is null ? "valuation_created" : "valuation_replaced",
                operationKey,
                "Valuation recorded.",
                before is null ? null : JsonSerializer.Serialize(new { Valuation = before }, SerializerOptions),
                JsonSerializer.Serialize(new { Valuation = result }, SerializerOptions),
                $"{ValuationPolicy.PolicyKey}/v{ValuationPolicy.PolicyVersion}",
                now);
            recorded.Add(details);
            written.Add(entity);
        }

        return new(
            recorded,
            written,
            beforeDependencies,
            beforeDependencies with
            {
                UsesGlassesValuationGuide = beforeDependencies.UsesGlassesValuationGuide
                    || recorded.Any(details => details.Source == ValuationSource.Glasses),
            });
    }

    /// <summary>The guide cards a Case save wrote, their rows, and the report's valuation dependencies either side of them.</summary>
    internal sealed record GuideEntriesRecorded(
        IReadOnlyList<ValuationDetails> Recorded,
        IReadOnlyList<CaseValuationEntity> Written,
        CaseReportValuationDependencies Before,
        CaseReportValuationDependencies After);

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

    /// <summary>One action-history entry for a valuation write.</summary>
    private static void AddActionHistory(
        PegasusDbContext context,
        ActionActor actor,
        string aggregateType,
        Guid aggregateId,
        string eventKind,
        string operationKey,
        string reason,
        string? beforeJson,
        string afterJson,
        string policyVersion,
        DateTimeOffset now) =>
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = aggregateType,
            AggregateId = aggregateId.ToString("D"),
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(actor.Roles.OrderBy(role => role), SerializerOptions),
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            PolicyVersion = policyVersion,
        });

    /// <summary>
    /// The history a valuation command records: the replayable workflow event,
    /// the action-history entry with its before/after payload, and the Case
    /// history line. (A Case save's cards and adoption record only the
    /// action-history entry: the save owns the workflow event.)
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
        AddActionHistory(
            context, actor, aggregateType, aggregateId, eventKind, operationKey,
            reason.Trim(), beforeJson, afterJson, policyVersion, now);
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
    private const string RemovedEventKind = "valuation_preset_removed";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ValuationPreset>> ListAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.Set<ValuationPresetEntity>()
            .AsNoTracking()
            .Where(item => item.RemovedAtUtc == null)
            .ToArrayAsync(cancellationToken);

        // Disabled presets are listed too: history keeps naming them, and
        // the selection rule that refuses them lives in Core, not in the
        // read. A removed preset is the one thing the list drops, because it
        // is no longer maintained at all.
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
        if (entity?.RemovedAtUtc is not null)
        {
            throw new ValuationPresetException(ValuationPresetError.Removed);
        }

        // A removed preset keeps its label. The label is unique across the
        // whole table, so it stays reserved rather than becoming available to
        // a second record that history would then be unable to tell apart.
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

    /// <summary>
    /// Removes one preset from the maintained list after checking its expected
    /// version and replay key. The preset row remains so recorded valuations
    /// and history stay readable; only <c>RemovedAtUtc</c> is written.
    /// </summary>
    public async Task<ValuationPreset> RemoveAsync(
        RemoveValuationPresetRequest request,
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

        var entity = await context.Set<ValuationPresetEntity>().SingleOrDefaultAsync(
                item => item.Id == request.PresetId,
                cancellationToken)
            ?? throw new ValuationPresetException(ValuationPresetError.NotFound);
        if (entity.RemovedAtUtc is not null)
        {
            throw new ValuationPresetException(ValuationPresetError.Removed);
        }
        if (entity.Version != request.ExpectedVersion)
        {
            throw new ValuationPresetException(
                ValuationPresetError.VersionConflict,
                entity.Version);
        }

        var now = timeProvider.GetUtcNow();
        var before = Map(entity);
        entity.RemovedAtUtc = now;
        entity.UpdatedBy = request.Actor.SubjectId;
        entity.UpdatedAtUtc = now;
        entity.Version = checked(entity.Version + 1);
        entity.ConcurrencyToken = Guid.NewGuid();
        var after = Map(entity);
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = AggregateType,
            AggregateId = entity.Id.ToString("D"),
            EventKind = RemovedEventKind,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                request.Actor.Roles.OrderBy(role => role),
                SerializerOptions),
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = request.OperationKey,
            Reason = request.Reason,
            BeforeJson = JsonSerializer.Serialize(before, SerializerOptions),
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

    private static ValuationPreset Replay(
        RemoveValuationPresetRequest request,
        ActionHistoryEntity history)
    {
        if (history.EventKind != RemovedEventKind
            || history.AggregateId != request.PresetId.ToString("D")
            || history.ActorSubjectId != request.Actor.SubjectId
            || history.AfterJson is null)
        {
            throw new ValuationPresetException(ValuationPresetError.OperationConflict);
        }

        var replayed = JsonSerializer.Deserialize<ValuationPreset>(
                history.AfterJson,
                SerializerOptions)
            ?? throw new ValuationPresetException(ValuationPresetError.OperationConflict);
        if (replayed.Version != checked(request.ExpectedVersion + 1)
            || replayed.RemovedAtUtc is null)
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
        entity.UpdatedAtUtc)
    {
        RemovedAtUtc = entity.RemovedAtUtc,
    };
}
