using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one transaction behind one Case edit — the one Save (23 September
/// 2026). Case facts, assessment fields, the damage impacts, the repair
/// specification the editor shows, the guide cards, an applied valuation,
/// the two factual completeness controls and the sign-off Engineer are
/// written together inside a single transaction that commits once, under the
/// Case's workflow lock, so a Case save either records the whole authorized
/// snapshot or none of it. It owns that transaction outright: it never calls the case-data,
/// assessment, estimate or valuation stores' commands, because each of those
/// owns a transaction of its own and composing them would make a partial
/// write possible again. It writes through their transaction-local routines.
/// </summary>
public sealed class EfCaseWorkspaceStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    IEnumerable<IPrincipalCaseMatchPolicy>? caseMatchPolicies = null) : ICaseWorkspaceStore
{
    private const string EventType = "case_workspace_saved";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<SaveCaseWorkspaceResult> SaveAsync(
        SaveCaseWorkspaceRequest request,
        CancellationToken cancellationToken,
        IReadOnlyList<SignOffEngineerProfile>? signOffProfiles = null) =>
        StoreConcurrencyRetry.ExecuteAsync(
            token => SaveOnceAsync(request, signOffProfiles, token),
            cancellationToken);

    private async Task<SaveCaseWorkspaceResult> SaveOnceAsync(
        SaveCaseWorkspaceRequest request,
        IReadOnlyList<SignOffEngineerProfile>? signOffProfiles,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request = CaseWorkspacePolicy.ValidateAndNormalize(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        // The same lock every claim, heartbeat and lifecycle write takes, and
        // taken before the replay probe, so a second save under one operation
        // key waits for the first and replays it.
        await EfCaseWorkflowStore.AcquireWorkflowMutationLockAsync(context, request.CaseId, cancellationToken);

        var requestHash = RequestHash(request);
        if (await CaseOperationReplay.FindAsync(
                context,
                request.CaseId,
                request.OperationKey,
                requestHash,
                cancellationToken))
        {
            // A replay has read neither the configuration nor the work yet.
            var replayWorkId = await CaseWorkScope.ResolveIdAsync(context, request.CaseId, request.Work, cancellationToken);
            var replayConfiguration = await EfWorkflowConfigurationStore.ReadAsync(context, cancellationToken);
            return await ProjectAsync(
                context,
                request.CaseId,
                replayWorkId,
                replayConfiguration,
                await ReplayedEstimateIdAsync(context, request.CaseId, request.OperationKey, cancellationToken),
                wasReplay: true,
                cancellationToken);
        }

        var workflow = await context.CaseWorkflows
            .Include(item => item.DueWork)
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");

        var now = UtcNow();
        CaseMutationGuard.RequireLease(workflow, request.Actor, request.EditLeaseToken, now);
        CaseMutationGuard.RequireVersionUnderLease(workflow, request.ExpectedVersion);
        AssessmentPolicy.RequireOriginalReportScope(
            CaseWorkspacePolicy.AssessmentFields(request).Keys,
            CaseTypeCodes.Parse(workflow.Case.Type));
        ArchivedCaseGuard.RequireMutable(workflow);
        // Read the persisted configuration under the same transaction as the
        // guarded Case so the readiness written below is never based on a
        // configuration snapshot from before this edit began.
        var configuration = await EfWorkflowConfigurationStore.ReadAsync(
            context,
            cancellationToken);
        if (!Enum.TryParse<CaseLifecycleState>(workflow.State, out var state)
            || !AssessmentPolicy.IsWritableState(state))
        {
            throw new InvalidOperationException(
                "The Case cannot be saved in its current state.");
        }

        // The save writes the work the request names, resolved after the
        // guards: Create audit takes the same workflow lock and bumps the
        // version, so a save prepared against the Inspection is refused as
        // stale. Matching, the accepted deadline, due work, the completeness
        // gate and the state switch are the Case's and move only while the
        // Case has no Audit (decision M): an edit of the Inspection from its
        // view once the Audit exists moves none of them, since the Case's
        // state is the Audit's (operator, 2 October 2026).
        var currentWorkId = await CaseWorkScope.CurrentIdAsync(context, request.CaseId, cancellationToken);
        var workId = request.Work == CaseWorkSelector.Primary ? request.CaseId : currentWorkId;
        var movesCase = currentWorkId == request.CaseId;
        var snapshot = await EfCaseDataStore.SnapshotQuery(context, tracking: true)
            .SingleOrDefaultAsync(item => item.WorkId == workId, cancellationToken)
            ?? throw new InvalidDataException(
                "The accepted case has no typed data snapshot for its current work.");

        var dueWorkVersionBeforeSave = workflow.DueWork?.Version;
        var beforeData = CaseDataFieldWriter.ReadEditable(snapshot) with
        {
            DueBy = CaseDuePolicy.Resolve(
                workflow.DueWork?.DueBy,
                snapshot.Work.Case.AcceptedInspectionDeadline)
        };
        var beforeReportData = EffectiveReportData(snapshot.Fields);
        var registrationBefore = CaseDataFieldWriter.Registration(snapshot);
        var beforeMileageField = CaseDataFieldValues.CurrentField(
            snapshot.Fields,
            CaseDataFieldNames.VehicleMileage);
        // Read before the save rewrites the row in place.
        var beforeMileageOrigin = beforeMileageField is null
            ? null
            : EfCaseDataStore.Source(beforeMileageField);
        var beforeCompleteness = Completeness(snapshot);
        var data = CaseDataPolicy.Normalize(
            CaseWorkspacePolicy.Overlay(beforeData, request));
        if (data.ClaimSourceId != beforeData.ClaimSourceId)
        {
            await ClaimSourceChoice.RequireActiveAsync(context, data.ClaimSourceId, cancellationToken);
        }
        if (data != beforeData)
        {
            CaseDataFieldWriter.ApplyEditableData(context, snapshot, data, request.Actor, now);
            if (movesCase)
            {
                CaseMatchIndexProjector.Apply(
                    context,
                    await context.CaseMatchIndex.SingleOrDefaultAsync(
                        item => item.CaseId == request.CaseId,
                        cancellationToken),
                    CaseMatchIndexProjector.Project(
                        snapshot.Work.Case,
                        snapshot.Fields,
                        caseMatchPolicies ?? [],
                        now));
            }
        }
        var afterReportData = EffectiveReportData(snapshot.Fields);

        if (movesCase && request.Inspection is not null)
        {
            snapshot.Work.Case.AcceptedInspectionDeadline = data.InspectionDeadline;
        }

        var dueByChanged = movesCase && request.Overview is not null && data.DueBy != beforeData.DueBy;

        var assessmentFields = await context.CaseAssessmentFields
            .Where(item => item.WorkId == workId)
            .ToListAsync(cancellationToken);
        var beforeAssessment = assessmentFields.ToDictionary(
            item => item.FieldPath, item => (string?)item.Value, StringComparer.Ordinal);
        var removedLookupFacts = await CaseDataFieldWriter.RemoveLookupFactsOnRegistrationChangeAsync(
            context, snapshot, registrationBefore, cancellationToken);
        assessmentFields.RemoveAll(item => removedLookupFacts.Contains(item));
        var requestedFields = CaseWorkspacePolicy.AssessmentFields(request);
        if (request.ExpectedVersion < workflow.Version && workId == currentWorkId)
        {
            // System work moved the version under this lease: a lookup that
            // filled the work since the page read it keeps what it filled.
            requestedFields = CaseWorkspacePolicy.KeepSystemFills(
                request.CaseId,
                request.ExpectedVersion,
                workflow.Version,
                requestedFields,
                await EfVehicleLookupWorkStore.FilledSinceAsync(
                    context, request.CaseId, request.ExpectedVersion, cancellationToken),
                assessmentFields.ToDictionary(
                    item => item.FieldPath, item => (string?)item.Value, StringComparer.Ordinal));
        }
        CaseValuationEntity? postedGuide = null;
        if (request.Valuation is { Adoption: { } chosen } chosenValuation)
        {
            // The report's Retail and Trade follow the guide card the
            // calculation is chosen against (operator, 8 October 2026).
            var withReportValues = new Dictionary<string, string?>(requestedFields, StringComparer.Ordinal);
            var reportValues = await EfValuationStore.ReportValuesAsync(
                context, workId, chosen, chosenValuation.GuideEntries ?? [], cancellationToken);
            postedGuide = reportValues.PostedGuide;
            foreach (var (path, value) in reportValues.Values)
            {
                withReportValues[path] = value;
            }
            requestedFields = withReportValues;
        }
        var (fieldsToWrite, merged) = AssessmentWriteSet.Build(requestedFields, assessmentFields);
        var (beforeFields, afterFields) = AssessmentWriteSet.Apply(
            context,
            workId,
            assessmentFields,
            fieldsToWrite,
            request.Actor,
            now);
        var afterAssessment = merged.ToDictionary(
            item => item.Key,
            item => (string?)item.Value,
            StringComparer.Ordinal);
        var afterMileageField = CaseDataFieldValues.CurrentField(
            snapshot.Fields,
            CaseDataFieldNames.VehicleMileage);
        var beforeMileageSource = CaseVehicleMileageSourcePolicy.Resolve(
            beforeMileageOrigin?.Kind,
            beforeMileageOrigin is not null);
        var afterMileageSource = CaseVehicleMileageSourcePolicy.Resolve(
            afterMileageField is null
                ? null
                : EfCaseDataStore.ParseSourceKind(afterMileageField.SourceKind),
            afterMileageField is not null);

        IReadOnlyList<CaseAssetPreparation>? beforePreparedImages = null;
        IReadOnlyList<CaseAssetPreparation>? preparedImages = null;
        if (request.ImagePreparation is { Edits: { Count: > 0 } preparationEdits })
        {
            // This is deliberately the preparation store's transaction-local
            // routine, rather than its public command. The workspace owns the
            // transaction, the single Case version increment and
            // the single history entry for all of the submitted changes.
            beforePreparedImages = await EfCaseAssetPreparationStore.LoadCurrentAsync(
                context, request.CaseId, cancellationToken);
            preparedImages = await EfCaseAssetPreparationStore.PrepareSaveAsync(
                context,
                workflow,
                new SaveCaseAssetPreparationRequest(
                    request.CaseId,
                    request.ExpectedVersion,
                    request.Actor,
                    request.OperationKey,
                    request.Reason,
                    request.EditLeaseToken,
                    preparationEdits),
                now,
                cancellationToken);
        }

        var wordingChanged = request.ReportWording is { } wording
            && await SaveReportWordingAsync(
                context, request, workId, wording.Blocks ?? [], now, cancellationToken);

        // The repair specification the editor shows is written by the
        // estimate store's own editor routine, under this transaction's one
        // version, workflow event and history line. One the operator left as
        // it was is not rewritten.
        var estimateEdit = request.Estimate is { } estimateSection
            ? await EfRepairSpecificationStore.ApplyEditAsync(
                context,
                workId,
                estimateSection.ToSaveEstimateRequest(request),
                now,
                leaveUnchanged: true,
                cancellationToken)
            : null;
        var estimate = estimateEdit is { Changed: true } ? estimateEdit.Entity : null;

        // The guide source cards are recorded by this save (23 September
        // 2026), and a calculation the operator changed is recorded against
        // the basis card as the save leaves it: the valuation store's
        // transaction-local writers, under this transaction's one version,
        // workflow event and history line. The Engineer's Value box is an
        // ordinary field, and Retail and Trade follow the basis card; both
        // were written above with the rest.
        var guideEntries = request.Valuation is { } valuationSection
            ? await EfValuationStore.RecordGuideEntriesAsync(
                context,
                workflow,
                workId,
                request.Actor,
                request.OperationKey,
                valuationSection.GuideEntries ?? [],
                now,
                cancellationToken)
            : null;
        var adopted = request.Valuation?.Adoption is { } adoption
            ? await EfValuationStore.AdoptAsync(
                context,
                workflow,
                workId,
                request.Actor,
                request.OperationKey,
                adoption,
                guideEntries?.Written ?? [],
                postedGuide,
                string.Equals(
                    afterAssessment.GetValueOrDefault(AssessmentVocabulary.SettlementClaimantVatRegistered),
                    "true",
                    StringComparison.Ordinal),
                afterAssessment.GetValueOrDefault(AssessmentVocabulary.ValueEngineer),
                CaseMileageInMiles(snapshot.Fields),
                checked(workflow.Version + 1),
                now,
                cancellationToken)
            : null;

        var signOffEngineerProfiles = signOffProfiles
            ?? await EfStaffAccountQueries.ListSignOffEngineersAsync(context, cancellationToken);
        var beforeSignOffEngineerId = CaseSignOffEngineerResolver.Resolve(
            workflow.SignOffEngineerId,
            workflow.AssignedEngineerId,
            signOffEngineerProfiles)?.StaffId;
        if (request.Report?.SignOffEngineerId is { } signOffEngineerId)
        {
            workflow.SignOffEngineerId = signOffEngineerId;
        }

        if (movesCase && request.Completeness is { } completeness)
        {
            snapshot.Work.Case.InstructionComplete =
                completeness.InstructionComplete ?? snapshot.Work.Case.InstructionComplete;
            snapshot.Work.Case.ImagesComplete =
                completeness.ImagesComplete ?? snapshot.Work.Case.ImagesComplete;
        }

        // Readiness is evaluated from the row that was just written,
        // never from anything the caller claimed, and never forced to false as
        // a side effect of editing an unrelated fact.
        var afterCompleteness = Completeness(snapshot);
        var evaluation = CaseCompletenessPolicy.Evaluate(afterCompleteness, configuration);
        snapshot.CompletenessPolicyKey = evaluation.PolicyKey;
        snapshot.CompletenessPolicyVersion = evaluation.PolicyVersion;
        snapshot.CompletenessPolicySatisfied = evaluation.SatisfiesPolicy;
        if (movesCase
            && workflow.AssignedEngineerId is null
            && state is CaseLifecycleState.NotReady or CaseLifecycleState.Review)
        {
            if (evaluation.SatisfiesPolicy)
            {
                var enteringReview = state != CaseLifecycleState.Review;
                workflow.State = nameof(CaseLifecycleState.Review);
                CaseChaseState.Stop(workflow);
                if (enteringReview)
                {
                    workflow.StateEnteredAtUtc = now;
                }
            }
            else
            {
                if (state != CaseLifecycleState.NotReady)
                {
                    workflow.StateEnteredAtUtc = now;
                }

                workflow.State = nameof(CaseLifecycleState.NotReady);
                await CaseDueWorkScheduler.ScheduleAsync(
                    context,
                    workflow,
                    snapshot.Work.Case.AcceptedInspectionDeadline,
                    now,
                    cancellationToken);
            }
        }

        if (dueByChanged)
        {
            CaseDueWorkScheduler.ApplyStaffDueBy(
                context,
                workflow,
                data.DueBy,
                snapshot.Work.Case.AcceptedInspectionDeadline,
                dueWorkVersionBeforeSave);
        }
        else if (movesCase)
        {
            CaseDueWorkScheduler.ProjectDueBy(
                context,
                workflow,
                snapshot.Work.Case.AcceptedInspectionDeadline,
                dueWorkVersionBeforeSave);
        }

        var beforeVersion = workflow.Version;
        CaseMutationGuard.Complete(workflow);
        var afterJson = JsonSerializer.Serialize(
            new
            {
                Data = data,
                Completeness = afterCompleteness,
                Fields = afterFields,
                EstimateLines = estimateEdit?.AfterLines,
                Estimate = estimate is null
                    ? null
                    : new { estimate.Id, estimate.Version, estimate.Name },
                ImagePreparation = preparedImages,
                Valuations = guideEntries?.Recorded,
                AppliedValuation = adopted
            },
            JsonOptions);
        CaseMutationHistory.Add(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            CaseWorkspaceChangeSummary.Describe(
                beforeData,
                data,
                beforeFields,
                afterFields,
                estimateChanged: estimate is not null,
                imagesPrepared: preparedImages?.Count ?? 0,
                request.Reason,
                wordingChanged,
                guideEntries?.Recorded,
                valuationAdopted: adopted is not null),
            EventType,
            requestHash,
            beforeVersion,
            workflow.Version,
            JsonSerializer.Serialize(
                new
                {
                    Data = beforeData,
                    Completeness = beforeCompleteness,
                    Fields = beforeFields,
                    EstimateLines = estimateEdit?.BeforeLines
                },
                JsonOptions),
            afterJson,
            $"{CaseWorkspacePolicy.PolicyKey}/v{CaseWorkspacePolicy.PolicyVersion}",
            now);
        await EfVehicleLookupWorkStore.RecordEstimateDispositionAsync(
            context,
            workflow,
            beforeMileageOrigin,
            afterMileageField,
            request.Actor,
            beforeVersion,
            now,
            cancellationToken);
        var freshness = CaseReportFreshness.ClassifyWorkspace(
            beforeReportData,
            afterReportData,
            wordingChanged,
            beforeAssessment,
            afterAssessment,
            beforeSignOffEngineerId,
            CaseSignOffEngineerResolver.Resolve(
                workflow.SignOffEngineerId,
                workflow.AssignedEngineerId,
                signOffEngineerProfiles)?.StaffId,
            beforePreparedImages,
            preparedImages,
            beforeMileageSource,
            afterMileageSource);
        // The report is marked stale once per save, by the first rule that
        // finds it stale: the workspace's own, then the specification's (an
        // edit of the current one changes the breakdown a report pinned),
        // then the valuation's. Only the edited work's report moves, never
        // the other work's (operator, 2 October 2026).
        if (freshness.IsStale)
        {
            await EfCaseReportGenerationStore.MarkWorkStaleAsync(
                context,
                request.CaseId,
                workId,
                freshness.ReasonCode!,
                now,
                cancellationToken);
        }
        else if (estimateEdit is { Changed: true, ChangesCurrent: true })
        {
            await EfCaseReportGenerationStore.MarkWorkStaleAsync(
                context,
                request.CaseId,
                workId,
                "current_estimate_saved",
                now,
                cancellationToken);
        }
        else if (guideEntries is not null
            && (guideEntries.Recorded.Count > 0 || adopted is not null))
        {
            await EfValuationStore.MarkStaleIfNeededAsync(
                context,
                request.CaseId,
                workId,
                guideEntries.Before,
                adopted is null ? guideEntries.After : EfValuationStore.WithApplied(guideEntries.After, adopted),
                now,
                cancellationToken);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CaseVersionConflictException(
                request.CaseId,
                request.ExpectedVersion,
                request.ExpectedVersion + 1);
        }
        catch (DbUpdateException exception) when (IsWorkflowEventVersionCollision(exception))
        {
            // Another mutation took the same next version between this
            // transaction's read and its commit. The unique
            // (CaseId, AfterVersion) index caught it, and the caller must
            // re-read rather than have its write silently ordered after work
            // it never saw.
            throw new CaseVersionConflictException(
                request.CaseId,
                request.ExpectedVersion,
                request.ExpectedVersion + 1);
        }

        return await ProjectAsync(
            context,
            request.CaseId,
            workId,
            configuration,
            estimateEdit?.Entity.Id,
            wasReplay: false,
            cancellationToken);
    }

    /// <summary>
    /// The work the save wrote as the save left it, with the specification
    /// the save carried (<paramref name="estimateId"/>) rather than whichever
    /// Draft is latest.
    /// </summary>
    private static async Task<SaveCaseWorkspaceResult> ProjectAsync(
        PegasusDbContext context,
        Guid caseId,
        Guid workId,
        CaseWorkflowConfiguration configuration,
        Guid? estimateId,
        bool wasReplay,
        CancellationToken cancellationToken)
    {
        var snapshot = await EfCaseDataStore.SnapshotQuery(context, tracking: false)
            .SingleAsync(item => item.WorkId == workId, cancellationToken);
        var workflow = await context.CaseWorkflows.AsNoTracking()
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleAsync(item => item.CaseId == caseId, cancellationToken);
        var estimate = estimateId is null
            ? null
            : await context.CaseRepairSpecifications
                .AsNoTracking()
                .Include(item => item.Lines)
                .SingleOrDefaultAsync(item => item.WorkId == workId && item.Id == estimateId, cancellationToken);
        var fields = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.WorkId == workId)
            .OrderBy(item => item.FieldPath)
            .ToArrayAsync(cancellationToken);
        // The specification's own lines, read with it above: a line cannot
        // outlive its specification, so a specification not found has none.
        var lines = estimate is null
            ? []
            : estimate.Lines.OrderBy(line => line.Position).ToArray();
        return new(
            EfCaseDataStore.ApplyConfiguration(EfCaseDataStore.Map(snapshot, workflow), configuration),
            EfCaseAssessmentStore.Map(
                workflow,
                fields,
                lines,
                snapshot.Fields.ToArray(),
                snapshot.OriginReceivedAtUtc),
            estimate is null ? null : EfRepairSpecificationStore.Map(estimate),
            wasReplay);
    }

    /// <summary>
    /// The Case's accepted mileage in miles, as this save leaves it: a
    /// calculated Engineer's Value records the Case's own mileage, never a
    /// guide card's (operator, 24 September 2026), and none while the Case has
    /// none (operator, 26 September 2026).
    /// </summary>
    private static long? CaseMileageInMiles(IReadOnlyList<CaseDataFieldEntity> fields)
    {
        if (!long.TryParse(
                CaseDataFieldValues.Accepted(fields, CaseDataFieldNames.VehicleMileage),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var reading))
        {
            return null;
        }
        var unit = CaseOdometer.TryParseUnit(
            CaseDataFieldValues.Accepted(fields, CaseDataFieldNames.VehicleMileageUnit),
            out var recorded)
            ? recorded
            : CaseOdometerUnit.Miles;
        return (long)Math.Round(
            CaseOdometer.Display(reading, unit, CaseOdometerUnit.Miles),
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The specification a replayed save wrote, as its own history entry
    /// recorded it; null when the save carried none it changed.
    /// </summary>
    private static async Task<Guid?> ReplayedEstimateIdAsync(
        PegasusDbContext context,
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var after = await EfRepairSpecificationStore.ReplayedCaseAfterJsonAsync(
            context, caseId, operationKey, EventType, cancellationToken);
        if (after is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(after);
        return document.RootElement.TryGetProperty("estimate", out var estimate)
            && estimate.ValueKind == JsonValueKind.Object
            && estimate.TryGetProperty("id", out var id)
            && id.TryGetGuid(out var estimateId)
            ? estimateId
            : null;
    }

    /// <summary>
    /// The Engineer's changes to the report's narrative blocks (v28 P30). A
    /// row is held only for a block whose heading, wording, order or presence
    /// the Engineer changed, so a block left out of the submission is reset to
    /// tracking its fields rather than deleted: the Case's row history is
    /// append-and-amend, like every other Case-owned table.
    /// </summary>
    private static async Task<bool> SaveReportWordingAsync(
        PegasusDbContext context,
        SaveCaseWorkspaceRequest request,
        Guid workId,
        IReadOnlyList<CaseReportWording> blocks,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await context.CaseReportWordings
            .Where(item => item.WorkId == workId)
            .ToListAsync(cancellationToken);
        var submitted = blocks.ToDictionary(block => block.Key, StringComparer.Ordinal);
        var changed = false;

        foreach (var row in existing.Where(item => !submitted.ContainsKey(item.BlockKey)))
        {
            // A manual paragraph left out is one the Engineer deleted; a
            // standard block left out tracks its fields again.
            var included = !row.Manual;
            if (row.Title is null && row.Text is null && row.Order is null && row.Included == included)
            {
                continue;
            }
            row.Title = null;
            row.Text = null;
            row.Order = null;
            row.Included = included;
            row.UpdatedBy = request.Actor.SubjectId;
            row.UpdatedAtUtc = now;
            changed = true;
        }

        foreach (var block in blocks)
        {
            var row = existing.SingleOrDefault(item =>
                string.Equals(item.BlockKey, block.Key, StringComparison.Ordinal));
            if (row is null)
            {
                // A block that changed nothing needs no row of its own.
                if (block.Title is null && block.Text is null && block.Order is null
                    && block.Included && !block.Manual)
                {
                    continue;
                }
                context.CaseReportWordings.Add(new CaseReportWordingEntity
                {
                    Id = Guid.NewGuid(),
                    WorkId = workId,
                    BlockKey = block.Key,
                    Title = block.Title,
                    Text = block.Text,
                    Order = block.Order,
                    Included = block.Included,
                    Manual = block.Manual,
                    UpdatedBy = request.Actor.SubjectId,
                    UpdatedAtUtc = now,
                });
                changed = true;
                continue;
            }
            if (row.Title == block.Title && row.Text == block.Text && row.Order == block.Order
                && row.Included == block.Included && row.Manual == block.Manual)
            {
                continue;
            }
            row.Title = block.Title;
            row.Text = block.Text;
            row.Order = block.Order;
            row.Included = block.Included;
            row.Manual = block.Manual;
            row.UpdatedBy = request.Actor.SubjectId;
            row.UpdatedAtUtc = now;
            changed = true;
        }

        return changed;
    }

    private static CaseCompleteness Completeness(CaseDataSnapshotEntity snapshot) => new(
        snapshot.Work.Case.InstructionComplete,
        snapshot.Work.Case.ImagesComplete);

    private static string RequestHash(SaveCaseWorkspaceRequest request)
    {
        var material = JsonSerializer.Serialize(
            new
            {
                Command = "save_case_workspace",
                request.CaseId,
                request.ExpectedVersion,
                ActorKind = request.Actor.Kind.ToString(),
                request.Actor.SubjectId,
                Roles = request.Actor.Roles
                    .OrderBy(role => role)
                    .Select(role => role.ToString())
                    .ToArray(),
                request.OperationKey,
                request.Reason,
                request.EditLeaseToken,
                request.Work,
                request.Overview,
                request.Inspection,
                request.Vehicle,
                request.Damage,
                request.ImagePreparation,
                request.Valuation,
                request.Estimate,
                request.Settlement,
                request.Report,
                request.ReportWording,
                request.Completeness
            },
            JsonOptions);
        return CaseOperationReplay.Hash(material);
    }

    private static bool IsWorkflowEventVersionCollision(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains(
            "IX_CaseWorkflowEvents_CaseId_AfterVersion",
            StringComparison.Ordinal) == true;

    private DateTimeOffset UtcNow()
    {
        var now = timeProvider.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }

    private static CaseEditableData EffectiveReportData(
        IReadOnlyList<CaseDataFieldEntity> fields)
    {
        string? Current(string fieldName) => CaseDataFieldValues.Current(fields, fieldName);
        long? CurrentLong(string fieldName) => Current(fieldName) is { } value
            ? long.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture)
            : null;
        DateOnly? CurrentDate(string fieldName) => Current(fieldName) is { } value
            ? DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
        CaseInspectionMode? CurrentInspectionMode() =>
            Current(CaseDataFieldNames.InspectionMode) is { } value
                ? CaseDataFieldWriter.ParseInspectionMode(value)
                : null;

        return new(
            ClaimantName: Current(CaseDataFieldNames.ClaimantName),
            ClaimNumber: Current(CaseDataFieldNames.ClaimNumber),
            VehicleRegistration: Current(CaseDataFieldNames.VehicleRegistration),
            VehicleMake: Current(CaseDataFieldNames.VehicleMake),
            VehicleModel: Current(CaseDataFieldNames.VehicleModel),
            VehicleMileage: CurrentLong(CaseDataFieldNames.VehicleMileage),
            VehicleMileageUnit: Current(CaseDataFieldNames.VehicleMileageUnit),
            IncidentDate: CurrentDate(CaseDataFieldNames.IncidentDate),
            InspectionDate: CurrentDate(CaseDataFieldNames.InspectionDate),
            InspectionAddress: Current(CaseDataFieldNames.InspectionAddress),
            InspectionMode: CurrentInspectionMode(),
            VehicleYear: Current(CaseDataFieldNames.VehicleYear));
    }
}

/// <summary>
/// A Case's Claim source must be an active Claim Source contact when the Case is
/// created with it or the choice changes: a form rendered before the record was
/// deactivated, or an id that is not a Claim Source, fails closed.
/// </summary>
internal static class ClaimSourceChoice
{
    public static async Task RequireActiveAsync(
        PegasusDbContext context, Guid? claimSourceId, CancellationToken cancellationToken)
    {
        if (claimSourceId is { } id && !await context.Organizations.AsNoTracking().AnyAsync(
                item => item.Id == id && item.Active && item.Role == "claim_source", cancellationToken))
        {
            throw new InvalidOperationException("The selected claim source is not an active Claim source record.");
        }
    }
}
