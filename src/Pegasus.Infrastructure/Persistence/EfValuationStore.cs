using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Reads Case valuations and applied valuations, and owns the valuation writes
/// the Case workspace save performs (guide cards, adoption, Engineer's Value,
/// report staleness).
/// </summary>
public sealed class EfValuationStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IValuationStore, IAppliedValuationStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

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
    /// the Engineer's Value row and field, the basis card's retail and trade
    /// fields, the applied snapshot and its action-history entry. No stamp is
    /// checked: every writer of a guide card moves the Case version, which the
    /// save has already checked.
    /// </summary>
    internal static async Task<ValuationAdopted> AdoptAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string operationKey,
        ValuationCalculationSelection selection,
        IReadOnlyList<CaseValuationEntity> writtenBySave,
        bool claimantVatRegistered,
        long? caseMileageInMiles,
        long resultingCaseVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // The adoption belongs to the Case's current work: the Audit once it exists.
        var workId = await CaseWorkScope.CurrentIdAsync(context, workflow.CaseId, cancellationToken);
        var posted = await RequiredGuideAsync(
            context,
            workId,
            selection.GuideValuationId,
            cancellationToken);
        var guideEntity = writtenBySave.LastOrDefault(item => item.Source == posted.Source) ?? posted;
        var basis = await ReadBasisAsync(
            context,
            workId,
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
            WorkId = workId,
            Source = ValuationSource.EngineersValue.ToString(),
            Date = DateOnly.FromDateTime(now.UtcDateTime),
            Time = TimeOnly.FromDateTime(now.UtcDateTime),
            // The Case's own mileage (operator, 24 September 2026): a guide
            // card carries none. The value needs it, as the lookup does.
            Mileage = caseMileageInMiles,
            RetailValue = accepted,
            TradeValue = 0m,
            RecordedBy = actor.SubjectId,
            RecordedAtUtc = now,
        };
        context.CaseValuations.Add(adopted);
        ValuationPolicy.ValidateDetails(Map(adopted).Details);
        var engineersValue = await WriteEngineersValueAsync(
            context,
            workflow,
            workId,
            actor,
            adopted,
            cancellationToken);
        // The report's Retail value and Trade value are the basis card's
        // (operator, 24 September 2026), recorded with the Engineer's Value
        // from the card as this save leaves it, so they change only when a
        // Save adopts again.
        var basisValues = await WriteAdoptedBasisValuesAsync(
            context,
            workId,
            actor,
            ValuationCalculationPolicy.AdoptedBasisFields(calculation, guideEntity.TradeValue),
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
        var snapshotHash = AppliedSnapshotHash(
            workflow.CaseId,
            snapshot.CaseVersion,
            snapshot.GuideValuationId,
            snapshot.GuideValuationStampUtc,
            snapshot.Calculation,
            accepted,
            reason);
        var entity = new AppliedValuationSnapshotEntity
        {
            Id = Guid.NewGuid(),
            WorkId = workId,
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
        var result = Map(entity, snapshot, workflow.CaseId);
        AddActionHistory(
            context,
            actor,
            "case_applied_valuation",
            result.Id,
            "valuation_applied",
            operationKey,
            reason,
            engineersValue?.Before is null && basisValues.Values.All(change => change.Before is null)
                ? null
                : JsonSerializer.Serialize(
                    new
                    {
                        EngineersValue = engineersValue?.Before,
                        BasisValues = basisValues.ToDictionary(pair => pair.Key, pair => pair.Value.Before),
                    },
                    SerializerOptions),
            JsonSerializer.Serialize(
                new
                {
                    AppliedValuation = result,
                    EngineersValue = engineersValue?.After,
                    BasisValues = basisValues.ToDictionary(pair => pair.Key, pair => pair.Value.After),
                },
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
        CaseWorkSelector work,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workId = await CaseWorkScope.ResolveIdAsync(context, caseId, work, cancellationToken);
        var entities = await context.Set<AppliedValuationSnapshotEntity>()
            .AsNoTracking()
            .Where(item => item.WorkId == workId)
            .ToArrayAsync(cancellationToken);
        return entities
            .OrderByDescending(item => item.AcceptedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => Map(item, null, caseId))
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
        var workId = await CaseWorkScope.CurrentIdAsync(context, caseId, cancellationToken);
        var guide = await RequiredGuideAsync(
            context,
            workId,
            guideValuationId,
            cancellationToken);
        var claimantVatField = await context.CaseAssessmentFields.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.WorkId == workId
                    && item.FieldPath == AssessmentVocabulary.SettlementClaimantVatRegistered,
                cancellationToken);
        return await ReadBasisAsync(
            context,
            workId,
            guide,
            string.Equals(claimantVatField?.Value, "true", StringComparison.Ordinal),
            cancellationToken);
    }

    private static async Task<ValuationCalculationBasis> ReadBasisAsync(
        PegasusDbContext context,
        Guid workId,
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
            .Where(item => item.WorkId == workId)
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
                    .SelectMany(item => ReadSnapshot(item).Calculation.Additions)
                    .Where(addition => addition.PresetId != Guid.Empty)
                    .DistinctBy(addition => (addition.PresetId, addition.PresetVersion))
            ]
        };
    }

    private static async Task<CaseValuationEntity> RequiredGuideAsync(
        PegasusDbContext context,
        Guid workId,
        Guid guideValuationId,
        CancellationToken cancellationToken)
    {
        var entity = await context.CaseValuations.SingleOrDefaultAsync(
            item => item.Id == guideValuationId && item.WorkId == workId,
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

    private static AppliedValuationSnapshot ReadSnapshot(AppliedValuationSnapshotEntity entity) =>
        JsonSerializer.Deserialize<AppliedValuationSnapshot>(entity.SnapshotJson, SerializerOptions)
            ?? throw new InvalidDataException(
                "The persisted applied valuation snapshot is invalid.");

    // The record's CaseId is the Case (plan 1.3): the caller names it, since
    // the row keys on its work.
    private static AppliedValuation Map(
        AppliedValuationSnapshotEntity entity,
        AppliedValuationSnapshot? snapshot,
        Guid caseId)
    {
        snapshot ??= ReadSnapshot(entity);
        return new(
            entity.Id,
            caseId,
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

    /// <summary>
    /// The one hash of an applied valuation, including the Case version the
    /// adoption produced: returning to an earlier calculation in a later save
    /// is a new adoption rather than a repeat of the first.
    /// </summary>
    internal static string AppliedSnapshotHash(
        Guid caseId,
        long caseVersion,
        Guid guideValuationId,
        DateTimeOffset guideValuationStampUtc,
        ValuationCalculation calculation,
        decimal accepted,
        string reason) =>
        Hash(new
        {
            CaseId = caseId,
            CaseVersion = caseVersion,
            GuideValuationId = guideValuationId,
            GuideValuationStampUtc = guideValuationStampUtc,
            Calculation = calculation,
            Accepted = accepted,
            Reason = reason,
        });

    /// <summary>
    /// An applied valuation re-pointed at copied guide cards (Create audit's
    /// copy, whose guide cards carry new ids): the frozen snapshot with its
    /// guide id mapped, and the hash recomputed by <see cref="AppliedSnapshotHash"/>.
    /// </summary>
    internal static (string SnapshotJson, string SnapshotHash) RepointAppliedSnapshot(
        AppliedValuationSnapshotEntity entity,
        Guid caseId,
        IReadOnlyDictionary<Guid, Guid> guideValuationIds)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(guideValuationIds);
        var source = ReadSnapshot(entity);
        if (!guideValuationIds.TryGetValue(source.GuideValuationId, out var guideValuationId))
        {
            throw new InvalidDataException(
                "The applied valuation names a guide valuation that was not copied.");
        }

        // The adoption hashed the proposal at its own scale, which the stored
        // decimal(18,2) column does not keep, so the frozen proposal is hashed.
        var snapshot = source with { GuideValuationId = guideValuationId };
        return (
            JsonSerializer.Serialize(snapshot, SerializerOptions),
            AppliedSnapshotHash(
                caseId,
                snapshot.CaseVersion,
                snapshot.GuideValuationId,
                snapshot.GuideValuationStampUtc,
                snapshot.Calculation,
                snapshot.Calculation.Proposal,
                entity.Reason));
    }

    public async Task<IReadOnlyList<CaseValuation>> ListForCaseAsync(
        Guid caseId,
        CaseWorkSelector work,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workId = await CaseWorkScope.ResolveIdAsync(context, caseId, work, cancellationToken);
        var entities = await context.CaseValuations
            .AsNoTracking()
            .Where(item => item.WorkId == workId)
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
    /// Value the product consumes: Send to Claude's target percentage and the
    /// rendered report read that field. An adoption therefore writes it in
    /// this same transaction, from the case's latest Engineer's Value row, so
    /// the Valuations table stays the entry surface and never becomes a
    /// second owner.
    /// </summary>
    private static async Task<EngineersValueChange?> WriteEngineersValueAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        Guid workId,
        ActionActor actor,
        CaseValuationEntity saved,
        CancellationToken cancellationToken)
    {
        AssessmentPolicy.RequireFindingConfirmationAuthority(actor);
        if (!Enum.TryParse<CaseLifecycleState>(workflow.State, out var state)
            || !AssessmentPolicy.IsWritableState(state))
        {
            throw new InvalidOperationException(
                "An Engineer's Value cannot be recorded in the Case's current state.");
        }

        var engineersValue = ValuationSource.EngineersValue.ToString();
        var others = await context.CaseValuations
            .Where(item => item.WorkId == workId
                && item.Source == engineersValue
                && item.Id != saved.Id)
            .ToArrayAsync(cancellationToken);
        var latest = others.Append(saved)
            .OrderByDescending(OrderKey)
            .First();
        var existing = await context.CaseAssessmentFields.SingleOrDefaultAsync(
            item => item.WorkId == workId
                && item.FieldPath == AssessmentVocabulary.ValueEngineer,
            cancellationToken);
        var before = existing?.Value;
        var selected = Map(latest);
        var value = ValuationPolicy.EngineersValueField(selected.Details)
            ?? throw new InvalidDataException(
                "The selected Engineer's Value row does not carry an Engineer's Value.");
        var recordedBy = selected.LastEditedBy ?? selected.RecordedBy;
        var recordedAtUtc = selected.LastEditedAtUtc ?? selected.RecordedAtUtc;
        var written = AssessmentFieldWriter.Write(
            context,
            workId,
            existing,
            AssessmentVocabulary.ValueEngineer,
            value,
            ActorKind.Staff,
            recordedBy,
            recordedAtUtc,
            confirmedBy: recordedBy);
        return new(before, written.Value);
    }

    /// <summary>
    /// Writes the basis values an adoption records
    /// (<see cref="ValuationCalculationPolicy.AdoptedBasisFields"/>) as
    /// confirmed staff findings through the one field writer, and removes one
    /// the basis card does not carry, so no figure from an earlier basis
    /// survives.
    /// </summary>
    private static async Task<Dictionary<string, (string? Before, string? After)>> WriteAdoptedBasisValuesAsync(
        PegasusDbContext context,
        Guid workId,
        ActionActor actor,
        IReadOnlyList<KeyValuePair<string, string?>> values,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changes = new Dictionary<string, (string? Before, string? After)>(StringComparer.Ordinal);
        foreach (var (path, value) in values)
        {
            var existing = await context.CaseAssessmentFields.SingleOrDefaultAsync(
                item => item.WorkId == workId && item.FieldPath == path,
                cancellationToken);
            var before = existing?.Value;
            if (value is null)
            {
                if (existing is not null)
                {
                    context.CaseAssessmentFields.Remove(existing);
                }
            }
            else
            {
                AssessmentFieldWriter.Write(
                    context,
                    workId,
                    existing,
                    path,
                    value,
                    ActorKind.Staff,
                    actor.SubjectId,
                    now,
                    confirmedBy: actor.SubjectId);
            }
            changes[path] = (before, value);
        }
        return changes;
    }

    internal sealed record EngineersValueChange(string? Before, string? After);

    private static async Task<CaseReportValuationDependencies> ReadReportDependenciesAsync(
        PegasusDbContext context,
        Guid workId,
        CancellationToken cancellationToken)
    {
        var usesGlasses = await context.CaseValuations.AsNoTracking().AnyAsync(
            item => item.WorkId == workId
                && item.Source == nameof(ValuationSource.Glasses),
            cancellationToken);
        var engineersValue = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.WorkId == workId
                && item.FieldPath == AssessmentVocabulary.ValueEngineer)
            .Select(item => item.Value)
            .SingleOrDefaultAsync(cancellationToken);
        var applied = await context.Set<AppliedValuationSnapshotEntity>().AsNoTracking()
            .Where(item => item.WorkId == workId)
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
        Guid workId,
        ValuationDetails details,
        CancellationToken cancellationToken)
    {
        if (details.GuideMonth is not { } guideMonth)
        {
            return null;
        }

        var source = details.Source.ToString();
        return await context.CaseValuations
            .Where(item => item.WorkId == workId && item.Source == source && item.GuideMonth == guideMonth)
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
            entity.Work.CaseId,
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
    /// guide month (<see cref="FindReplacedAsync"/>). A card whose figures are
    /// already the recorded ones is left untouched, because rewriting it would
    /// move its last-written stamp. The Case save owns the version, the
    /// workflow event and the history line, so this writes only the rows and
    /// their action-history entries.
    /// </summary>
    internal static async Task<GuideEntriesRecorded> RecordGuideEntriesAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        Guid workId,
        ActionActor actor,
        string operationKey,
        IReadOnlyList<ValuationDetails> entries,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var beforeDependencies = await ReadReportDependenciesAsync(context, workId, cancellationToken);
        var recorded = new List<ValuationDetails>(entries.Count);
        var written = new List<CaseValuationEntity>(entries.Count);
        foreach (var details in entries)
        {
            var replaced = await FindReplacedAsync(context, workId, details, cancellationToken);
            var before = replaced is null ? null : Map(replaced);
            if (before is not null && ValuationPolicy.IsUnchanged(details, before.Details))
            {
                continue;
            }

            var entity = replaced ?? new CaseValuationEntity
            {
                Id = Guid.NewGuid(),
                WorkId = workId,
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
                throw new ValuationPresetException(ValuationPresetError.VersionConflict);
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
                throw new ValuationPresetException(ValuationPresetError.VersionConflict);
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
            throw new ValuationPresetException(ValuationPresetError.VersionConflict);
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
