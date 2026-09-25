using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Repair specifications and named estimates share one table and one
/// aggregate. The estimate methods are the named-estimate path, where a case
/// holds several Drafts and Accepted estimates and exactly one is Current;
/// each writes the Case history and replays by operation key.
/// </summary>
public sealed class EfRepairSpecificationStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IRepairSpecificationStore
{
    internal const string SourceHashReplayEventType = "estimate_source_replay_bound";
    private const string CaseAggregateType = "case";

    /// <summary>
    /// The one serializer settings object this aggregate uses, for the
    /// request hash, the history payloads and the estimate's own JSON
    /// columns alike.
    /// </summary>
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record SourceHashReplaySnapshot(
        string SourceSha256,
        Guid EstimateId = default);

    public async Task RequireImportAuthorityAsync(ImportRawEstimateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EstimatePolicy.RequireImportActor(request.Actor);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        CaseMutationGuard.Require(workflow, request.Actor, request.ExpectedVersion, request.EditLeaseToken, Now());
        RequireAssessmentEditable(workflow);
    }

    public async Task<EstimateImportResult?> ProbeSourceHashReplayAsync(
        Guid caseId,
        string operationKey,
        string sourceSha256,
        CancellationToken cancellationToken)
    {
        var normalizedSha256 = ImportRawEstimate.NormalizeSha256(sourceSha256);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var binding = await context.ActionHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.AggregateType == CaseAggregateType
                    && item.AggregateId == caseId.ToString("D")
                    && item.CorrelationId == operationKey
                    && item.EventKind == SourceHashReplayEventType,
                cancellationToken);
        return binding is null
            ? null
            : await ReadSourceHashReplayAsync(context, binding, normalizedSha256, cancellationToken);
    }

    public async Task<EstimateImportResult> BindSourceHashReplayAsync(
        Guid caseId,
        string operationKey,
        string sourceSha256,
        Guid estimateId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var normalizedSha256 = ImportRawEstimate.NormalizeSha256(sourceSha256);
        if (estimateId == Guid.Empty)
        {
            throw new ArgumentException("An estimate result is required.", nameof(estimateId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        _ = await RequiredWorkflowForUpdateAsync(context, caseId, cancellationToken);
        var workId = await CaseWorkScope.CurrentIdAsync(context, caseId, cancellationToken);
        var existing = await context.ActionHistory
            .SingleOrDefaultAsync(
                item => item.AggregateType == CaseAggregateType
                    && item.AggregateId == caseId.ToString("D")
                    && item.CorrelationId == operationKey,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.EventKind == SourceHashReplayEventType)
            {
                return await ReadSourceHashReplayAsync(context, existing, normalizedSha256, cancellationToken);
            }

            if (existing.EventKind != "estimate_created"
                || ReadHistoryEstimateId(existing) != estimateId)
            {
                throw new CaseOperationConflictException(caseId, operationKey);
            }

            var linkedEstimate = await RequiredEstimateOfCaseAsync(context, caseId, estimateId, cancellationToken);
            if (!string.Equals(linkedEstimate.CreationOperationKey, operationKey, StringComparison.Ordinal)
                || !string.Equals(linkedEstimate.SourceSha256, normalizedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new CaseOperationConflictException(caseId, operationKey);
            }

            return new(estimateId);
        }

        var estimate = await RequiredEstimateAsync(context, workId, estimateId, cancellationToken);
        if (!string.Equals(estimate.SourceSha256, normalizedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new CaseOperationConflictException(caseId, operationKey);
        }

        var now = Now();
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = CaseAggregateType,
            AggregateId = caseId.ToString("D"),
            EventKind = SourceHashReplayEventType,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(actor.Roles.OrderBy(role => role), JsonOptions),
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = ImportRawEstimate.ImportReason,
            BeforeJson = JsonSerializer.Serialize(new SourceHashReplaySnapshot(normalizedSha256), JsonOptions),
            AfterJson = JsonSerializer.Serialize(new SourceHashReplaySnapshot(normalizedSha256, estimateId), JsonOptions),
            PolicyVersion = $"{RepairSpecificationPolicy.PolicyKey}/source-replay"
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(estimateId);
    }

    public Task<RepairSpecificationVersion> SaveEstimateAsync(
        SaveEstimateRequest request, CancellationToken cancellationToken) =>
        PersistEstimateAsync(request, importedDocument: false, cancellationToken);

    /// <summary>
    /// The Estimate Apply command: the saved Draft and both scaling snapshots
    /// share this one serializable transaction and one operation key. The page
    /// saves the specification with the Case before it asks.
    /// </summary>
    public async Task<RepairSpecificationVersion> ScaleAsync(
        ScaleRepairSpecificationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EngineerValue is not { } engineerValue || engineerValue <= 0m)
        {
            throw new InvalidOperationException("A confirmed Engineer's Value is required before scaling.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        // The Engineer's Value is read by the act, not posted: the hash is the
        // operator's intent, so a replay is judged on what was asked.
        var replayHash = Hash(request with { EngineerValue = null });
        if (await CaseOperationReplay.FindAsync(
                context, request.CaseId, request.OperationKey, replayHash, cancellationToken))
        {
            return await ReplayedAsync(context, request.CaseId, request.OperationKey, cancellationToken);
        }

        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);
        var workId = await CaseWorkScope.CurrentIdAsync(context, request.CaseId, cancellationToken);
        var specificationId = request.SpecificationId;
        var entity = await RequiredEstimateAsync(context, workId, specificationId, cancellationToken);
        var edited = Map(entity);
        EstimatePolicy.ValidateEditable(edited, request.Actor);
        entity.LastOperationKey = request.OperationKey;

        var result = RepairSpecificationScaling.Scale(
            edited,
            engineerValue * request.TargetPercentOfValue / 100m,
            request.Floors);
        var reason = RepairSpecificationWording.Scaled(result, request.TargetPercentOfValue);

        var latest = await context.CaseRepairSpecificationSnapshots
            .Where(item => item.SpecificationId == specificationId)
            .OrderByDescending(item => item.Number)
            .FirstOrDefaultAsync(cancellationToken);
        var beforeScaling = EfRepairSpecificationSnapshotStore.Freeze(
            context, workId, edited, request.Actor, RepairSpecificationSnapshotKind.BeforeScaling,
            "Before scaling", now, latest);

        context.CaseEstimateLines.RemoveRange(entity.Lines.ToArray());
        entity.Lines.Clear();
        ApplyDetails(entity, result.Details);
        AddLines(context, entity, result.Lines, request.Actor, now);
        RecordBreakdown(entity);
        var scaled = Map(entity);
        EfRepairSpecificationSnapshotStore.Freeze(
            context, workId, scaled, request.Actor, RepairSpecificationSnapshotKind.Scaled,
            reason, now, beforeScaling);
        if (entity.IsCurrent)
        {
            await EfCaseReportGenerationStore.MarkStaleAsync(
                context, request.CaseId, "current_estimate_saved", now, cancellationToken);
        }
        AddHistory(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            reason,
            "estimate_scaled",
            replayHash,
            new { entity.Id, entity.Version, entity.Name, Lines = entity.Lines.Count },
            now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    /// <summary>Restores a frozen version and both restore marks in one transaction.</summary>
    public async Task<RepairSpecificationVersion> RestoreSnapshotAsync(
        RestoreRepairSpecificationSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = Hash(request);
        if (await CaseOperationReplay.FindAsync(context, request.CaseId, request.OperationKey, requestHash, cancellationToken))
        {
            return await ReplayedAsync(context, request.CaseId, request.OperationKey, cancellationToken);
        }

        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);
        var workId = await CaseWorkScope.CurrentIdAsync(context, request.CaseId, cancellationToken);
        var entity = await RequiredEstimateAsync(context, workId, request.SpecificationId, cancellationToken);
        var specification = Map(entity);
        EstimatePolicy.ValidateEditable(specification, request.Actor);
        var versionRow = await context.CaseRepairSpecificationSnapshots
            .SingleOrDefaultAsync(
                item => item.WorkId == workId && item.Id == request.SnapshotId,
                cancellationToken)
            ?? throw new KeyNotFoundException("The version was not found.");
        if (versionRow.SpecificationId != request.SpecificationId)
        {
            throw new InvalidOperationException("The version belongs to another repair specification.");
        }

        var version = EfRepairSpecificationSnapshotStore.Map(versionRow);
        var latest = await context.CaseRepairSpecificationSnapshots
            .Where(item => item.SpecificationId == request.SpecificationId)
            .OrderByDescending(item => item.Number)
            .FirstOrDefaultAsync(cancellationToken);
        var beforeRestore = EfRepairSpecificationSnapshotStore.Freeze(
            context, workId,
            specification,
            request.Actor,
            RepairSpecificationSnapshotKind.BeforeRestore,
            $"Before v{version.Number} was restored",
            now,
            latest);

        context.CaseEstimateLines.RemoveRange(entity.Lines.ToArray());
        entity.Lines.Clear();
        ApplyDetails(entity, version.Details);
        ApplySupplementary(entity, version.Supplementary);
        AddLines(
            context,
            entity,
            version.Lines.Select(RepairSpecificationScaling.ToInput).ToArray(),
            request.Actor,
            now);
        entity.LastOperationKey = request.OperationKey;
        RecordBreakdown(entity);
        var restored = Map(entity);
        EfRepairSpecificationSnapshotStore.Freeze(
            context, workId,
            restored,
            request.Actor,
            RepairSpecificationSnapshotKind.Restored,
            $"Restored from v{version.Number} ({version.Origin})",
            now,
            beforeRestore);
        if (entity.IsCurrent)
        {
            await EfCaseReportGenerationStore.MarkStaleAsync(
                context, request.CaseId, "current_estimate_saved", now, cancellationToken);
        }
        AddHistory(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            $"Restored from v{version.Number} ({version.Origin})",
            "estimate_restored",
            requestHash,
            new { entity.Id, entity.Version, entity.Name, Lines = entity.Lines.Count },
            now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    /// <summary>Restores the latest scale pair and records terminal removal in the same transaction.</summary>
    public async Task<RepairSpecificationVersion> RemoveScalingAsync(
        RemoveRepairSpecificationScalingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = Hash(request);
        if (await CaseOperationReplay.FindAsync(context, request.CaseId, request.OperationKey, requestHash, cancellationToken))
        {
            return await ReplayedAsync(context, request.CaseId, request.OperationKey, cancellationToken);
        }

        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var latestScale = await context.CaseRepairSpecificationSnapshots
            .Where(item => item.SpecificationId == request.SpecificationId
                && (item.Kind == RepairSpecificationSnapshotKind.Scaled.ToString()
                    || item.Kind == RepairSpecificationSnapshotKind.ScalingRemoved.ToString()))
            .OrderByDescending(item => item.Number)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestScale is null || latestScale.Kind != RepairSpecificationSnapshotKind.Scaled.ToString())
        {
            throw new InvalidOperationException("The repair specification has no removable scaling.");
        }
        var latestSnapshot = await context.CaseRepairSpecificationSnapshots
            .Where(item => item.SpecificationId == request.SpecificationId)
            .OrderByDescending(item => item.Number)
            .FirstAsync(cancellationToken);
        var beforeRow = await context.CaseRepairSpecificationSnapshots
            .Where(item => item.SpecificationId == request.SpecificationId
                && item.Kind == RepairSpecificationSnapshotKind.BeforeScaling.ToString()
                && item.Number < latestScale.Number)
            .OrderByDescending(item => item.Number)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The repair specification has no version before scaling.");

        var now = Now();
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);
        var workId = await CaseWorkScope.CurrentIdAsync(context, request.CaseId, cancellationToken);
        var entity = await RequiredEstimateAsync(context, workId, request.SpecificationId, cancellationToken);
        EstimatePolicy.ValidateEditable(Map(entity), request.Actor);
        var before = EfRepairSpecificationSnapshotStore.Map(beforeRow);

        context.CaseEstimateLines.RemoveRange(entity.Lines.ToArray());
        entity.Lines.Clear();
        ApplyDetails(entity, before.Details);
        ApplySupplementary(entity, before.Supplementary);
        AddLines(
            context,
            entity,
            before.Lines.Select(RepairSpecificationScaling.ToInput).ToArray(),
            request.Actor,
            now);
        entity.LastOperationKey = request.OperationKey;
        RecordBreakdown(entity);
        var restored = Map(entity);
        EfRepairSpecificationSnapshotStore.Freeze(
            context, workId,
            restored,
            request.Actor,
            RepairSpecificationSnapshotKind.ScalingRemoved,
            "Scaling removed",
            now,
            latestSnapshot);
        if (entity.IsCurrent)
        {
            await EfCaseReportGenerationStore.MarkStaleAsync(
                context, request.CaseId, "current_estimate_saved", now, cancellationToken);
        }
        AddHistory(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            "Scaling removed",
            "estimate_scaling_removed",
            requestHash,
            new { entity.Id, entity.Version, entity.Name, Lines = entity.Lines.Count },
            now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public Task<RepairSpecificationVersion> SaveImportedEstimateAsync(
        SaveEstimateRequest request, CancellationToken cancellationToken) =>
        PersistEstimateAsync(EstimatePolicy.ValidateImportedSave(request), importedDocument: true, cancellationToken);

    private async Task<RepairSpecificationVersion> PersistEstimateAsync(
        SaveEstimateRequest request,
        bool importedDocument,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        var workId = await CaseWorkScope.CurrentIdAsync(context, request.CaseId, cancellationToken);
        if (importedDocument)
        {
            CaseMutationGuard.Require(workflow, request.Actor, request.ExpectedVersion, request.EditLeaseToken, now);
            RequireAssessmentEditable(workflow);
            // The workflow lock and source census share this transaction: two
            // concurrent completions cannot create two Drafts for one Case/hash.
            var existingImport = await context.CaseRepairSpecifications.Include(item => item.Lines)
                .FirstOrDefaultAsync(item => item.WorkId == workId
                    && item.SourceSha256 == request.Source.Sha256, cancellationToken);
            if (existingImport is not null)
            {
                return Map(existingImport);
            }
        }
        var requestHash = Hash(request);
        if (await CaseOperationReplay.FindAsync(context, request.CaseId, request.OperationKey, requestHash, cancellationToken))
        {
            return await ReplayedAsync(context, request.CaseId, request.OperationKey, cancellationToken);
        }
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);

        var edit = await ApplyEditAsync(context, workflow, request, now, leaveUnchanged: false, cancellationToken);
        var entity = edit.Entity;
        request = edit.Evidenced;
        if (importedDocument)
        {
            // v1 of an imported specification is the import itself (v28 P43).
            EfRepairSpecificationSnapshotStore.Freeze(
                context, workId, Map(entity), request.Actor, RepairSpecificationSnapshotKind.Imported,
                "Imported " + Pegasus.Core.Assessment.RepairSpecificationRouteWords.Of(request.Source.Route), now);
        }
        if (edit.EditingCurrent)
        {
            // Editing the current estimate changes the breakdown a frozen
            // report pinned; a Draft-only save never stales a generation.
            await EfCaseReportGenerationStore.MarkStaleAsync(
                context, request.CaseId, "current_estimate_saved", now, cancellationToken);
        }
        AddHistory(context, workflow, request.Actor, request.OperationKey, request.Reason,
            request.EventType ?? edit.EventType, requestHash, new { entity.Id, entity.Version, entity.Name, Lines = request.Lines.Count }, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    /// <summary>What one editor save did to a specification, inside its caller's transaction.</summary>
    internal sealed record EstimateEdit(
        CaseRepairSpecificationEntity Entity,
        string EventType,
        bool EditingCurrent,
        bool Changed,
        SaveEstimateRequest Evidenced,
        object? BeforeLines,
        object? AfterLines);

    /// <summary>
    /// The one routine that writes a specification from an editor save, inside
    /// the caller's transaction and under the caller's guard: it creates the
    /// Draft or replaces an editable one's header, lines and supplementary
    /// statement, carries each line's evidence, resolves a newly chosen
    /// labour-rate card and records the breakdown. The estimate command and
    /// the Case save both write through it, so the two cannot record different
    /// things for the same edit. With <paramref name="leaveUnchanged"/> an edit
    /// that is the specification exactly as recorded writes nothing.
    /// </summary>
    internal static async Task<EstimateEdit> ApplyEditAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        SaveEstimateRequest request,
        DateTimeOffset now,
        bool leaveUnchanged,
        CancellationToken cancellationToken)
    {
        // Every edit writes the Case's current work: the Audit once it exists.
        var workId = await CaseWorkScope.CurrentIdAsync(context, workflow.CaseId, cancellationToken);
        CaseRepairSpecificationEntity entity;
        RepairSpecificationVersion? existing = null;
        string eventType;
        var editingCurrent = false;
        if (request.EstimateId is { } estimateId)
        {
            entity = await RequiredEstimateAsync(context, workId, estimateId, cancellationToken);
            editingCurrent = entity.IsCurrent;
            existing = Map(entity);
            EstimatePolicy.ValidateEditable(existing, request.Actor);
            eventType = "estimate_updated";
        }
        else
        {
            entity = new CaseRepairSpecificationEntity
            {
                Id = Guid.NewGuid(),
                WorkId = workId,
                Version = await NextVersionAsync(context, workId, cancellationToken),
                State = RepairSpecificationState.Draft.ToString(),
                SourceRoute = request.Source.Route.ToString(),
                CreatedBy = request.Actor.SubjectId,
                CreationOperationKey = request.OperationKey,
                CreatedAtUtc = now,
                Name = request.Details.Name,
                AiJobId = request.AiJobId,
            };
            eventType = "estimate_created";
        }
        request = EstimatePolicy.ApplyEditorEvidence(request, existing, now);
        if (request.SelectedRateCardId is { } rateCardId)
        {
            var card = await context.LabourRateCards.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == rateCardId, cancellationToken);
            if (card is null || !card.Active || card.Version != request.SelectedRateCardVersion)
                throw new InvalidOperationException("The selected labour-rate card changed or is disabled. Select a current card.");
            request = request with { Details = request.Details with
            {
                LabourRate = card.PanelRate,
                Rate = new EstimateRateSnapshot(card.Id, card.Version, card.PanelRate)
            } };
        }
        else if (request.SelectedRateCardVersion is not null)
            throw new ArgumentException("Select a labour-rate card for the specified version.");
        if (leaveUnchanged && EstimatePolicy.IsUnchanged(request, existing))
        {
            return new(entity, eventType, editingCurrent, Changed: false, request, null, null);
        }

        if (existing is null)
        {
            context.CaseRepairSpecifications.Add(entity);
        }
        var beforeLines = entity.Lines.OrderBy(line => line.Position).Select(EstimateLineWriter.Evidence).ToArray();
        if (existing is not null)
        {
            context.CaseEstimateLines.RemoveRange(entity.Lines.ToArray());
            entity.Lines.Clear();
        }
        entity.SourceRoute = request.Source.Route.ToString();
        entity.SourceArtifactReference = request.Source.ArtifactReference;
        entity.SourceVersion = request.Source.SourceVersion;
        entity.SourceSha256 = request.Source.Sha256;
        entity.AiJobId = request.AiJobId ?? entity.AiJobId;
        entity.LastOperationKey = request.OperationKey;
        ApplyDetails(entity, request.Details);
        ApplySupplementary(entity, request.Supplementary);
        AddLines(context, entity, request.Lines, request.Actor, now);
        RecordBreakdown(entity);
        var afterLines = entity.Lines.OrderBy(line => line.Position).Select(EstimateLineWriter.Evidence).ToArray();
        return new(entity, eventType, editingCurrent, Changed: true, request, beforeLines, afterLines);
    }

    public async Task<RepairSpecificationVersion> DuplicateEstimateAsync(
        DuplicateEstimateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = Hash(request);
        if (await CaseOperationReplay.FindAsync(context, request.CaseId, request.OperationKey, requestHash, cancellationToken))
        {
            return await ReplayedAsync(context, request.CaseId, request.OperationKey, cancellationToken);
        }
        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);
        var workId = await CaseWorkScope.CurrentIdAsync(context, request.CaseId, cancellationToken);
        var original = await RequiredEstimateAsync(context, workId, request.EstimateId, cancellationToken);
        EstimatePolicy.ValidateDuplicate(Map(original));

        // A copy is the staff member's own working estimate: it keeps the figures
        // and lines but not the document provenance or the AI job of the
        // original, and its name is bounded like any typed name.
        var name = original.Name + EstimatePolicy.CopySuffix;
        var entity = new CaseRepairSpecificationEntity
        {
            Id = Guid.NewGuid(),
            WorkId = workId,
            Version = await NextVersionAsync(context, workId, cancellationToken),
            State = RepairSpecificationState.Draft.ToString(),
            SourceRoute = RepairSpecificationSourceRoute.Manual.ToString(),
            CreatedBy = request.Actor.SubjectId,
            CreationOperationKey = request.OperationKey,
            CreatedAtUtc = now,
            LastOperationKey = request.OperationKey,
            Name = name.Length <= EstimatePolicy.MaximumNameLength
                ? name
                : name[..EstimatePolicy.MaximumNameLength],
        };
        // The header the copy keeps is the original's own canonical header —
        // rate snapshot, discounts, VAT categories and percentage — applied
        // through the single owner of that mapping.
        ApplyDetails(entity, ReadDetails(original) with { Name = entity.Name });
        context.CaseRepairSpecifications.Add(entity);
        foreach (var line in original.Lines.OrderBy(item => item.Position))
        {
            context.CaseEstimateLines.Add(
                CloneLine(line, entity, request.Actor, now));
        }
        RecordBreakdown(entity);
        AddHistory(context, workflow, request.Actor, request.OperationKey, request.Reason,
            "estimate_duplicated", requestHash,
            new { entity.Id, entity.Version, entity.Name, SourceEstimateId = original.Id }, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<RepairSpecificationVersion> DiscardEstimateAsync(
        DiscardEstimateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = Hash(request);
        if (await CaseOperationReplay.FindAsync(context, request.CaseId, request.OperationKey, requestHash, cancellationToken))
        {
            return await ReplayedAsync(context, request.CaseId, request.OperationKey, cancellationToken);
        }
        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);
        var workId = await CaseWorkScope.CurrentIdAsync(context, request.CaseId, cancellationToken);
        var entity = await RequiredEstimateAsync(context, workId, request.EstimateId, cancellationToken);
        EstimatePolicy.ValidateDiscard(Map(entity));
        var discardingCurrent = entity.IsCurrent;
        entity.State = RepairSpecificationState.Discarded.ToString();
        entity.DiscardedBy = request.Actor.SubjectId;
        entity.DiscardedAtUtc = now;
        entity.DiscardReason = RequiredReason(request.Reason);
        entity.LastOperationKey = request.OperationKey;
        if (discardingCurrent)
        {
            // Discarding the current estimate removes the breakdown a frozen
            // report pinned; discarding a Draft changes nothing it froze.
            await EfCaseReportGenerationStore.MarkStaleAsync(
                context, request.CaseId, "current_estimate_discarded", now, cancellationToken);
        }
        AddHistory(context, workflow, request.Actor, request.OperationKey, request.Reason,
            "estimate_discarded", requestHash, new { entity.Id, entity.Version, entity.Name }, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<RepairSpecificationVersion> SetCurrentEstimateAsync(
        SetCurrentEstimateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = Hash(request);
        if (await CaseOperationReplay.FindAsync(context, request.CaseId, request.OperationKey, requestHash, cancellationToken))
        {
            return await ReplayedAsync(context, request.CaseId, request.OperationKey, cancellationToken);
        }
        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedVersion, request.Actor, request.EditLeaseToken, now);
        var workId = await CaseWorkScope.CurrentIdAsync(context, request.CaseId, cancellationToken);
        var beforeEstimate = await ReadReportEstimateDependenciesAsync(
            context,
            workId,
            cancellationToken);
        var entity = await RequiredEstimateAsync(context, workId, request.EstimateId, cancellationToken);

        // "Use estimate" is the Engineer's acceptance of a Draft, and the
        // calculation basis is the one totals owner's figures at this moment.
        var candidate = Map(entity);
        EstimatePolicy.ValidateSetCurrent(candidate, request.Actor);
        if (candidate.State == RepairSpecificationState.Draft)
        {
            // One calculation: the accepted basis and the frozen breakdown
            // are the same run of the one totals owner, never two.
            var totals = EstimateTotals.Compute(candidate);
            Accept(entity, EstimatePolicy.BasisFor(totals), request.Actor, now);
            RecordBreakdown(entity, totals);
        }

        // The previous Current is cleared in the same transaction; the
        // filtered unique index refuses two Current rows on one case.
        var previous = await context.CaseRepairSpecifications
            .Where(item => item.WorkId == workId && item.IsCurrent && item.Id != entity.Id)
            .ToListAsync(cancellationToken);
        foreach (var item in previous)
        {
            item.IsCurrent = false;
        }
        if (previous.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        entity.IsCurrent = true;
        entity.LastOperationKey = request.OperationKey;
        await MarkEstimateStaleIfNeededAsync(
            context,
            request.CaseId,
            beforeEstimate,
            new(entity.Id, entity.Version),
            now,
            cancellationToken);
        AddHistory(context, workflow, request.Actor, request.OperationKey, request.Reason,
            "estimate_set_current", requestHash,
            new { entity.Id, entity.Version, entity.Name, Previous = previous.Select(item => item.Id).ToArray() }, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<IReadOnlyList<RepairSpecificationVersion>> ListEstimatesAsync(
        Guid caseId,
        CaseWorkSelector work,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workId = await CaseWorkScope.ResolveIdAsync(context, caseId, work, cancellationToken);
        var entities = await context.CaseRepairSpecifications.AsNoTracking().Include(item => item.Lines)
            .Where(item => item.WorkId == workId)
            .OrderBy(item => item.Version)
            .ToArrayAsync(cancellationToken);
        return entities.Select(Map).ToArray();
    }

    /// <summary>
    /// The keyset-paged sibling of <see cref="ListEstimatesAsync"/>:
    /// newest version first, then estimate id. Projects the
    /// bounded <see cref="CaseEstimatePageItem"/> header
    /// and never includes <see cref="CaseRepairSpecificationEntity.Lines"/> —
    /// a case can carry many superseded versions, each with an unbounded
    /// line list a keyset page never needs.
    /// </summary>
    public async Task<IReadOnlyList<CaseEstimatePageItem>> ListByCursorAsync(
        Guid caseId,
        int? afterVersion,
        Guid? afterId,
        int fetchCount,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workId = await CaseWorkScope.CurrentIdAsync(context, caseId, cancellationToken);
        var rows = context.CaseRepairSpecifications.AsNoTracking()
            .Where(item => item.WorkId == workId);
        if (afterId is { } id)
        {
            var afterValue = afterVersion!.Value;
            rows = rows.Where(item =>
                item.Version < afterValue
                || (item.Version == afterValue && item.Id < id));
        }

        var entities = await rows
            .OrderByDescending(item => item.Version)
            .ThenByDescending(item => item.Id)
            .Take(fetchCount)
            .ToArrayAsync(cancellationToken);
        return entities.Select(MapPageItem).ToArray();
    }

    public async Task<RepairSpecificationVersion?> GetVersionAsync(
        Guid caseId, Guid specificationId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CaseRepairSpecifications.AsNoTracking().Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Work.CaseId == caseId && item.Id == specificationId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<RepairSpecificationVersion?> GetCurrentAcceptedAsync(
        Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workId = await CaseWorkScope.CurrentIdAsync(context, caseId, cancellationToken);
        var entity = await AcceptedQuery(context, workId).AsNoTracking().Include(item => item.Lines)
            .SingleOrDefaultAsync(cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<RepairSpecificationVersion?> GetCurrentDraftAsync(
        Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var workId = await CaseWorkScope.CurrentIdAsync(context, caseId, cancellationToken);
        var entity = await DraftQuery(context, workId).AsNoTracking().Include(item => item.Lines)
            .SingleOrDefaultAsync(cancellationToken);
        return entity is null ? null : Map(entity);
    }

    /// <summary>
    /// The current-draft and current-accepted predicates are the single
    /// owner of "what row is the current specification for a case", shared
    /// with <see cref="EfCaseAssessmentStore"/>'s legacy implicit-draft path
    /// so the two stores never diverge on what "current" means. With named
    /// estimates a case may hold several drafts; the current draft is the
    /// latest one, and the current accepted specification is the estimate
    /// marked Current.
    /// </summary>
    internal static IQueryable<CaseRepairSpecificationEntity> DraftQuery(
        PegasusDbContext context, Guid workId) => context.CaseRepairSpecifications
        .Where(item => item.WorkId == workId
            && item.State == RepairSpecificationState.Draft.ToString())
        .OrderByDescending(item => item.Version)
        .Take(1);

    internal static IQueryable<CaseRepairSpecificationEntity> AcceptedQuery(
        PegasusDbContext context, Guid workId) => context.CaseRepairSpecifications
        .Where(item => item.WorkId == workId && item.IsCurrent);

    internal static async Task<int> NextVersionAsync(
        PegasusDbContext context, Guid workId, CancellationToken cancellationToken) =>
        (await context.CaseRepairSpecifications
            .Where(item => item.WorkId == workId)
            .MaxAsync(item => (int?)item.Version, cancellationToken) ?? 0) + 1;

    /// <summary>
    /// The one shape a repair specification takes when a legacy assessment
    /// save implicitly opens it (no explicit source evidence yet, actor
    /// authority already checked by the caller).
    /// </summary>
    internal static CaseRepairSpecificationEntity NewLegacyDraft(
        Guid workId, int version, string createdBy, string operationKey, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        WorkId = workId,
        Version = version,
        State = RepairSpecificationState.Draft.ToString(),
        SourceRoute = RepairSpecificationSourceRoute.LegacyUnresolved.ToString(),
        CreatedBy = createdBy,
        CreationOperationKey = operationKey,
        CreatedAtUtc = now,
        Name = DefaultName(version),
        VatPercent = EstimatePolicy.DefaultVatPercent,
    };

    private static string DefaultName(int version) =>
        string.Create(CultureInfo.InvariantCulture, $"Estimate {version}");

    /// <summary>
    /// The one place a Draft estimate header takes its edited values. The Case
    /// workspace save applies the same header inside its own transaction, so a
    /// header saved through the workspace and one saved through the estimate
    /// command cannot end up meaning different things.
    /// </summary>
    internal static void ApplyDetails(CaseRepairSpecificationEntity entity, EstimateDetails details)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(details);
        entity.Name = details.Name;
        // A header change invalidates the breakdown the row last recorded.
        // Every path that has the lines to recompute it calls RecordBreakdown
        // straight after; one that does not leaves no stale figures behind.
        entity.CalculationBreakdownJson = null;
        // One rate column. The rate snapshot's hourly rate is the estimate's
        // labour rate — EstimateDetails.HourlyRate reads it that way — so the
        // snapshot adds only the rate card it was taken from, never a second
        // copy of the rate itself.
        entity.LabourRate = details.Rate?.HourlyRate ?? details.LabourRate;
        entity.RateCardId = details.Rate?.RateCardId;
        entity.RateCardVersion = details.Rate?.RateCardVersion;
        entity.RegionalUplift = details.RegionalUplift;
        entity.OtherCosts = details.OtherCosts;
        entity.VatPercent = details.VatPercent;

        // Discounts are fractions in Core and percentages in the column the
        // schema named; four decimal places survive the conversion exactly.
        entity.PartsDiscountPercent = Percent(details.Discounts?.Parts);
        entity.MaterialsDiscountPercent = Percent(details.Discounts?.Materials);
        entity.SpecialistDiscountPercent = Percent(details.Discounts?.Specialist);
        entity.OverallDiscountPercent = Percent(details.Discounts?.Overall);

        // The effective policy is always recorded, so an estimate saved
        // without an explicit one keeps the meaning EstimateDetails gives it
        // rather than reading back as an unrecorded status. The four
        // applicable flags are written only for a hand-made override: their
        // presence is what "the operator chose these categories" means.
        var vat = details.VatPolicy;
        entity.RepairerVatStatus = vat.RepairerStatus.ToString();
        entity.LabourVatApplicable = Applicable(vat, EstimateVatCategories.Labour);
        entity.PartsVatApplicable = Applicable(vat, EstimateVatCategories.Parts);
        entity.MaterialsVatApplicable = Applicable(vat, EstimateVatCategories.Materials);
        entity.SpecialistVatApplicable = Applicable(vat, EstimateVatCategories.Specialist);
    }

    private static decimal? Percent(decimal? fraction) => fraction * 100m;

    private static void ApplySupplementary(
        CaseRepairSpecificationEntity entity,
        RepairSpecificationSupplementary? supplementary)
    {
        entity.SupplementaryOfSpecificationId = supplementary?.OfSpecificationId;
        entity.SupplementaryReason = supplementary?.Reason;
        entity.SupplementaryExplainOnReport = supplementary?.ExplainOnReport ?? false;
        entity.SupplementaryStatement = supplementary?.Statement;
    }

    private static bool? Applicable(EstimateVatPolicy policy, EstimateVatCategories category) =>
        policy.CategoriesOverridden ? policy.Charges(category) : null;

    /// <summary>
    /// The reverse of <see cref="ApplyDetails"/>: the canonical header as the
    /// row records it. A row that names no applicable categories carries the
    /// status's own defaults; a row whose status was never recorded reads
    /// back as <see cref="RepairerVatStatus.Unknown"/>, whose totals charge
    /// VAT on nothing until the status or the categories are recorded (it
    /// never gates Use repair spec, v28 P10).
    /// </summary>
    private static EstimateDetails ReadDetails(CaseRepairSpecificationEntity entity)
    {
        var status = Enum.Parse<RepairerVatStatus>(entity.RepairerVatStatus);
        var overridden = entity.LabourVatApplicable is not null
            || entity.PartsVatApplicable is not null
            || entity.MaterialsVatApplicable is not null
            || entity.SpecialistVatApplicable is not null;
        var vat = overridden
            ? new EstimateVatPolicy(
                status,
                Category(entity.LabourVatApplicable, EstimateVatCategories.Labour)
                    | Category(entity.PartsVatApplicable, EstimateVatCategories.Parts)
                    | Category(entity.MaterialsVatApplicable, EstimateVatCategories.Materials)
                    | Category(entity.SpecialistVatApplicable, EstimateVatCategories.Specialist),
                true)
            : EstimateVatPolicy.For(status);

        var discounts = entity.PartsDiscountPercent is null
            && entity.MaterialsDiscountPercent is null
            && entity.SpecialistDiscountPercent is null
            && entity.OverallDiscountPercent is null
            ? null
            : new EstimateDiscounts(
                Fraction(entity.PartsDiscountPercent),
                Fraction(entity.MaterialsDiscountPercent),
                Fraction(entity.SpecialistDiscountPercent),
                Fraction(entity.OverallDiscountPercent));

        var rate = entity.RateCardId is null && entity.RateCardVersion is null
            ? null
            : new EstimateRateSnapshot(
                entity.RateCardId, entity.RateCardVersion, entity.LabourRate ?? 0m);

        return new(
            entity.Name, entity.LabourRate, entity.OtherCosts, entity.VatPercent,
            discounts, vat, rate, entity.RegionalUplift);
    }

    private static EstimateVatCategories Category(bool? applicable, EstimateVatCategories category) =>
        applicable == true ? category : EstimateVatCategories.None;

    private static decimal Fraction(decimal? percent) => percent is { } value ? value / 100m : 0m;

    /// <summary>
    /// Freezes the one totals owner's own output on the row: the raw and
    /// printed breakdowns and the calculation policy version it stamped. The
    /// off-pattern values it retained are written on the lines that carry
    /// them, so neither fact is stored twice.
    /// </summary>
    private static void RecordBreakdown(CaseRepairSpecificationEntity entity) =>
        RecordBreakdown(entity, EstimateTotals.Compute(Map(entity)));

    private static void RecordBreakdown(CaseRepairSpecificationEntity entity, EstimateTotals totals)
    {
        entity.CalculationBreakdownJson = JsonSerializer.Serialize(
            new EstimateCalculationBreakdown(
                totals.CalculationPolicyVersion, totals.VatPercent, totals.Raw, totals.Printed),
            JsonOptions);
        var anomalies = totals.OffPattern.ToLookup(anomaly => anomaly.Position);
        foreach (var line in entity.Lines)
        {
            line.CurrentValuesJson = anomalies.Contains(line.Position)
                ? JsonSerializer.Serialize(anomalies[line.Position].ToArray(), JsonOptions)
                : null;
        }
    }

    private static void Accept(
        CaseRepairSpecificationEntity entity, RepairCalculationBasis basis, ActionActor actor, DateTimeOffset now)
    {
        entity.CalculationLabour = basis.Labour;
        entity.CalculationParts = basis.Parts;
        entity.CalculationPaintMaterials = basis.PaintMaterials;
        entity.CalculationSpecialistOther = basis.SpecialistOther;
        entity.RepairerVatRegistered = basis.RepairerVatRegistered;
        entity.CalculationVat = basis.Vat;
        entity.CalculationTotal = basis.Total;
        entity.CalculationPolicyVersion = basis.PolicyVersion;
        entity.State = RepairSpecificationState.Accepted.ToString();
        entity.AcceptedBy = actor.SubjectId;
        entity.AcceptedAtUtc = now;
    }

    private static void AddLines(
        PegasusDbContext context, CaseRepairSpecificationEntity target,
        IReadOnlyList<EstimateLineInput> lines, ActionActor actor, DateTimeOffset now)
    {
        var position = 0;
        foreach (var line in lines)
        {
            position++;
            context.CaseEstimateLines.Add(NewLine(line, position, target, actor, now));
        }
    }

    /// <summary>The recorded result of a Case operation already applied under this key, if any.</summary>
    internal static Task<string?> ReplayedCaseAfterJsonAsync(
        PegasusDbContext context, Guid caseId, string operationKey, string eventKind, CancellationToken cancellationToken) =>
        context.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == "case" && item.AggregateId == caseId.ToString("D")
                && item.CorrelationId == operationKey && item.EventKind == eventKind)
            .Select(item => item.AfterJson)
            .SingleOrDefaultAsync(cancellationToken);

    private static async Task<RepairSpecificationVersion> ReplayedAsync(
        PegasusDbContext context, Guid caseId, string operationKey, CancellationToken cancellationToken)
    {
        // LastOperationKey moves on every edit. The append-only operation
        // history keeps the result identity even after K2 has been followed
        // by K3; a replay reads that aggregate now, never reapplies K2.
        var eventType = await context.CaseWorkflowEvents.AsNoTracking()
            .Where(item => item.CaseId == caseId && item.OperationKey == operationKey)
            .Select(item => item.EventType).SingleAsync(cancellationToken);
        var after = await ReplayedCaseAfterJsonAsync(context, caseId, operationKey, eventType, cancellationToken);
        using var result = JsonDocument.Parse(after
            ?? throw new InvalidOperationException("The estimate operation has no recorded result identity."));
        var estimateId = result.RootElement.GetProperty("id").GetGuid();
        return Map(await context.CaseRepairSpecifications.AsNoTracking().Include(item => item.Lines)
            .SingleAsync(item => item.Work.CaseId == caseId && item.Id == estimateId, cancellationToken));
    }

    private static async Task<EstimateImportResult> ReadSourceHashReplayAsync(
        PegasusDbContext context,
        ActionHistoryEntity binding,
        string sourceSha256,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(binding.AggregateId, out var caseId)
            || binding.EventKind != SourceHashReplayEventType
            || binding.Outcome != "Succeeded"
            || binding.AfterJson is null)
        {
            throw new InvalidDataException("The source-hash replay binding is invalid.");
        }

        SourceHashReplaySnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<SourceHashReplaySnapshot>(
                binding.AfterJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The source-hash replay binding is invalid.", exception);
        }

        if (snapshot is null
            || snapshot.EstimateId == Guid.Empty
            || snapshot.SourceSha256 is null
            || !CaseOperationReplay.FixedTimeEquals(snapshot.SourceSha256, sourceSha256))
        {
            throw new CaseOperationConflictException(caseId, binding.CorrelationId);
        }

        var estimate = await context.CaseRepairSpecifications.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Work.CaseId == caseId && item.Id == snapshot.EstimateId,
                cancellationToken);
        if (estimate is null)
        {
            throw new InvalidDataException("The source-hash replay binding does not point to an estimate on its case.");
        }

        return new(snapshot.EstimateId);
    }

    private static Guid? ReadHistoryEstimateId(ActionHistoryEntity history)
    {
        if (string.IsNullOrWhiteSpace(history.AfterJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(history.AfterJson);
        return document.RootElement.TryGetProperty("id", out var id)
            && id.ValueKind == JsonValueKind.String
            && id.TryGetGuid(out var estimateId)
            ? estimateId
            : null;
    }

    // A write finds its estimate in the current work only: an estimate of the
    // Inspection is read-only once the Audit exists, so it is not found here.
    private static async Task<CaseRepairSpecificationEntity> RequiredEstimateAsync(
        PegasusDbContext context, Guid workId, Guid estimateId, CancellationToken cancellationToken) =>
        await context.CaseRepairSpecifications.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == estimateId && item.WorkId == workId, cancellationToken)
        ?? throw new InvalidOperationException("The estimate was not found on this case.");

    private static async Task<CaseRepairSpecificationEntity> RequiredEstimateOfCaseAsync(
        PegasusDbContext context, Guid caseId, Guid estimateId, CancellationToken cancellationToken) =>
        await context.CaseRepairSpecifications.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == estimateId && item.Work.CaseId == caseId, cancellationToken)
        ?? throw new InvalidOperationException("The estimate was not found on this case.");

    private static async Task<CaseReportEstimateDependencies> ReadReportEstimateDependenciesAsync(
        PegasusDbContext context,
        Guid workId,
        CancellationToken cancellationToken)
    {
        var current = await context.CaseRepairSpecifications.AsNoTracking()
            .Where(item => item.WorkId == workId && item.IsCurrent)
            .Select(item => new { item.Id, item.Version })
            .SingleOrDefaultAsync(cancellationToken);
        return current is null
            ? new(null, null)
            : new(current.Id, current.Version);
    }

    private static async Task MarkEstimateStaleIfNeededAsync(
        PegasusDbContext context,
        Guid caseId,
        CaseReportEstimateDependencies before,
        CaseReportEstimateDependencies after,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var freshness = CaseReportFreshness.ClassifyEstimate(before, after);
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

    private static async Task<CaseWorkflowEntity> RequiredWorkflowAsync(
        PegasusDbContext context, Guid caseId, CancellationToken cancellationToken) =>
        await context.CaseWorkflows.Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
        ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");

    private static async Task<CaseWorkflowEntity> RequiredWorkflowForUpdateAsync(
        PegasusDbContext context, Guid caseId, CancellationToken cancellationToken)
    {
        if (context.Database.IsSqlServer())
        {
            var lockedCaseId = await context.CaseWorkflows
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM [CaseWorkflows] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [CaseId] = {caseId}
                    """)
                .AsNoTracking()
                .Select(item => (Guid?)item.CaseId)
                .SingleOrDefaultAsync(cancellationToken);
            if (lockedCaseId is null)
            {
                throw new KeyNotFoundException($"Case '{caseId}' was not found.");
            }
        }

        return await RequiredWorkflowAsync(context, caseId, cancellationToken);
    }

    private static void Guard(
        CaseWorkflowEntity workflow, long expectedVersion, ActionActor actor, string lease, DateTimeOffset now)
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

    private static void RequireAssessmentEditable(CaseWorkflowEntity workflow)
    {
        if (AssessmentAccessPolicy.IsReadOnly(new(Enum.Parse<CaseLifecycleState>(workflow.State))))
        {
            throw new InvalidOperationException("The assessment is read-only in the current case state.");
        }
    }

    private static string RequiredReason(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A reason is required.") : value.Trim();

    private DateTimeOffset Now()
    {
        var now = timeProvider.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }

    private static string Hash<T>(T request) =>
        CaseOperationReplay.Hash(JsonSerializer.Serialize(request, JsonOptions));

    /// <summary>
    /// A duplicate is the staff member's own working estimate, so it keeps the
    /// figures and drops where they came from.
    /// </summary>
    private static CaseEstimateLineEntity CloneLine(
        CaseEstimateLineEntity line, CaseRepairSpecificationEntity target, ActionActor actor,
        DateTimeOffset now) =>
        NewLine(new(
            line.LineType, line.GuideCode, line.Description, line.WorkUnits, line.Price,
            line.Unpriced, line.PartNumber, line.Betterment, line.Status,
            line.EvidenceLabel, line.Justification, line.PaintWorkUnits, line.Quantity,
            line.Materials,
            null, null, null, null, null, null, null),
            line.Position, target, actor, now);

    private static EstimateLineOrigin? ReadOrigin(CaseEstimateLineEntity line) =>
        line.OriginalValuesJson is { Length: > 0 } json
            ? JsonSerializer.Deserialize<EstimateLineOrigin>(json, JsonOptions)
            : null;

    private static CaseEstimateLineEntity NewLine(
        EstimateLineInput line, int position, CaseRepairSpecificationEntity target,
        ActionActor actor, DateTimeOffset now) =>
        EstimateLineWriter.NewLine(line, position, target.WorkId, target, actor, now);

    internal static RepairSpecificationVersion Map(CaseRepairSpecificationEntity entity)
    {
        var details = ReadDetails(entity);
        var breakdown = ReadBreakdown(entity);
        return new(
            entity.Id, entity.Work.CaseId, entity.Version,
            Enum.Parse<RepairSpecificationState>(entity.State),
            new(Enum.Parse<RepairSpecificationSourceRoute>(entity.SourceRoute),
                entity.SourceArtifactReference, entity.SourceVersion, entity.SourceSha256),
            entity.Lines.OrderBy(line => line.Position).Select(line => new CaseEstimateLineRecord(
                line.Id, line.Position, line.LineType, line.GuideCode, line.Description,
                line.WorkUnits, line.Price, line.Unpriced, line.PartNumber, line.Betterment,
                line.Status, line.EvidenceLabel, line.Justification,
                Enum.Parse<ActorKind>(line.RecordedByKind), line.RecordedBy, line.RecordedAtUtc,
                line.PaintWorkUnits, line.Quantity,
                line.Materials, ReadOrigin(line), line.SourceDocumentIdentity,
                line.SourceDocumentVersionId, line.SourceDocumentSha256, line.SourceRowIdentity,
                line.AmendedBy, line.AmendedAtUtc)).ToArray(),
            MapBasis(entity, details),
            entity.CreatedBy, entity.CreatedAtUtc, entity.AcceptedBy, entity.AcceptedAtUtc,
            entity.SupersedesSpecificationId, entity.SupersessionReason,
            details,
            entity.IsCurrent, entity.AiJobId, entity.DiscardReason,
            breakdown is null
                ? null
                : new(
                    breakdown.Raw,
                    breakdown.Printed,
                    details.VatPolicy,
                    breakdown.VatPercent,
                    breakdown.CalculationPolicyVersion,
                    []),
            entity.SupplementaryOfSpecificationId is { } supplementaryOf
                ? new(supplementaryOf, entity.SupplementaryReason ?? string.Empty,
                    entity.SupplementaryExplainOnReport, entity.SupplementaryStatement ?? string.Empty)
                : null);
    }

    /// <summary>
    /// The accepted calculation basis as the row froze it: the four typed
    /// component columns, and — when the acceptance recorded one — the
    /// printed breakdown and the VAT categories that produced them.
    /// </summary>
    private static RepairCalculationBasis? MapBasis(
        CaseRepairSpecificationEntity entity, EstimateDetails details) =>
        entity.CalculationLabour is { } labour
            ? new(labour, entity.CalculationParts!.Value, entity.CalculationPaintMaterials!.Value,
                entity.CalculationSpecialistOther!.Value, entity.RepairerVatRegistered!.Value,
                entity.CalculationVat!.Value, entity.CalculationTotal!.Value,
                entity.CalculationPolicyVersion!,
                details.VatPolicy,
                ReadBreakdown(entity)?.Printed)
            : null;

    private static EstimateCalculationBreakdown? ReadBreakdown(CaseRepairSpecificationEntity entity) =>
        entity.CalculationBreakdownJson is { Length: > 0 } json
            ? JsonSerializer.Deserialize<EstimateCalculationBreakdown>(json, JsonOptions)
            : null;

    /// <summary>
    /// The bounded <see cref="CaseEstimatePageItem"/> sibling of <see
    /// cref="Map"/>, read without
    /// <c>entity.Lines</c> ever being included.
    /// </summary>
    internal static CaseEstimatePageItem MapPageItem(CaseRepairSpecificationEntity entity) => new(
        entity.Id, entity.Work.CaseId, entity.Version,
        Enum.Parse<RepairSpecificationState>(entity.State),
        new(Enum.Parse<RepairSpecificationSourceRoute>(entity.SourceRoute),
            entity.SourceArtifactReference, entity.SourceVersion, entity.SourceSha256),
        entity.Name,
        entity.IsCurrent,
        MapBasis(entity, ReadDetails(entity)));

    private static void AddHistory(
        PegasusDbContext context, CaseWorkflowEntity workflow, ActionActor actor,
        string operationKey, string reason, string eventType, string requestHash, object after,
        DateTimeOffset now) =>
        CaseMutationHistory.Add(
            context,
            workflow,
            actor,
            operationKey,
            RequiredReason(reason),
            eventType,
            requestHash,
            workflow.Version - 1,
            workflow.Version,
            "{}",
            JsonSerializer.Serialize(after, JsonOptions),
            $"{RepairSpecificationPolicy.PolicyKey}/v{RepairSpecificationPolicy.PolicyVersion}",
            now);
}

/// <summary>
/// The persisted form of one run of <see cref="EstimateTotals.Compute"/>:
/// the unrounded arithmetic, its printed projection, and the calculation
/// policy version that produced them. Nothing here is re-derived on read —
/// the row states what the one totals owner computed when the estimate was
/// saved or accepted.
/// </summary>
internal sealed record EstimateCalculationBreakdown(
    int CalculationPolicyVersion,
    decimal VatPercent,
    EstimateRawTotals Raw,
    EstimatePrintedTotals Printed);

/// <summary>
/// The one owner of an estimate's line rows. Replacing the lines of a Draft is
/// a whole-list operation — positions are contiguous and start at one — and
/// every line carries the provenance of the actor that saved it; a Draft's
/// lines become the Case's accepted lines when an Engineer makes it Current.
/// The estimate commands and the Case workspace save write lines through
/// here, so the two routes cannot record different provenance for the same
/// edit.
/// </summary>
internal static class EstimateLineWriter
{
    public static CaseEstimateLineEntity NewLine(
        EstimateLineInput line,
        int position,
        Guid workId,
        CaseRepairSpecificationEntity? specification,
        ActionActor actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(actor);
        return new()
        {
            Id = Guid.NewGuid(),
            WorkId = workId,
            RepairSpecificationId = specification?.Id,
            RepairSpecification = specification,
            Position = position,
            LineType = line.Type,
            GuideCode = line.GuideCode,
            Description = line.Description,
            WorkUnits = line.WorkUnits,
            PaintWorkUnits = line.PaintWorkUnits,
            Quantity = line.Quantity,
            Price = line.Price,
            Unpriced = line.Unpriced,
            PartNumber = line.PartNumber,
            Betterment = line.Betterment,
            Status = line.Status,
            EvidenceLabel = line.EvidenceLabel,
            Justification = line.Justification,
            Materials = line.Materials,
            // The editor's operation vocabulary, projected from the persisted
            // line type by the one mapping that owns it. The line type stays
            // the value every rule reads; this column only makes the row
            // legible in the operation's own words.
            Operation = EstimateOperations.FromLineType(line.Type).ToString(),
            OriginalValuesJson = line.Origin is null
                ? null
                : JsonSerializer.Serialize(line.Origin, EfRepairSpecificationStore.JsonOptions),
            SourceDocumentIdentity = line.SourceDocumentIdentity,
            SourceDocumentVersionId = line.SourceDocumentVersionId,
            SourceDocumentSha256 = line.SourceDocumentSha256,
            SourceRowIdentity = line.SourceRowIdentity,
            AmendedBy = line.AmendedBy,
            AmendedAtUtc = line.AmendedAtUtc,
            RecordedByKind = actor.Kind.ToString(),
            RecordedBy = actor.SubjectId,
            RecordedAtUtc = now
        };
    }

    /// <summary>
    /// Replaces every line of one estimate and returns the before/after
    /// evidence the history record carries. <paramref name="tracked"/> is
    /// updated in place so the caller's projection sees the new rows.
    /// </summary>
    public static (object Before, object After) Replace(
        PegasusDbContext context,
        Guid workId,
        CaseRepairSpecificationEntity? specification,
        List<CaseEstimateLineEntity> tracked,
        IReadOnlyList<EstimateLineInput> replacement,
        ActionActor actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tracked);
        ArgumentNullException.ThrowIfNull(replacement);
        var before = tracked.Select(Evidence).ToArray();
        context.CaseEstimateLines.RemoveRange(tracked);
        tracked.Clear();
        var position = 0;
        foreach (var line in replacement)
        {
            position++;
            var entity = NewLine(line, position, workId, specification, actor, now);
            context.CaseEstimateLines.Add(entity);
            tracked.Add(entity);
        }

        return (before, tracked.Select(Evidence).ToArray());
    }

    public static object Evidence(CaseEstimateLineEntity line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new
        {
            line.Position,
            line.LineType,
            line.GuideCode,
            line.Description,
            line.WorkUnits,
            line.PaintWorkUnits,
            line.Quantity,
            line.Price,
            line.Unpriced,
            line.PartNumber,
            line.Betterment,
            line.Status,
            line.EvidenceLabel,
            line.Justification
        };
    }
}
