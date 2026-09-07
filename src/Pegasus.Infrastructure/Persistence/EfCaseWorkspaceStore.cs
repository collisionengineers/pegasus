using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one transaction behind one Case edit. Case facts, assessment fields,
/// the damage impacts, the Draft estimate header and lines, the two factual
/// completeness controls and the sign-off Engineer are written together inside
/// a single serializable transaction that commits once, so a Case save either
/// records the whole authorized snapshot or none of it. It owns that
/// transaction outright: it never calls the case-data, assessment or estimate
/// stores, because each of those owns a transaction of its own and composing
/// them would make a partial write possible again.
/// </summary>
public sealed class EfCaseWorkspaceStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider,
    ICaseWorkflowConfiguration workflowConfiguration,
    IEnumerable<IProviderCaseMatchPolicy>? caseMatchPolicies = null) : ICaseWorkspaceStore
{
    private const string EventType = "case_workspace_saved";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SaveCaseWorkspaceResult> SaveAsync(
        SaveCaseWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request = CaseWorkspacePolicy.ValidateAndNormalize(request);
        var configuration = await workflowConfiguration.GetCurrentAsync(cancellationToken);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var requestHash = RequestHash(request);
        if (await CaseOperationReplay.FindAsync(
                context,
                request.CaseId,
                request.OperationKey,
                requestHash,
                cancellationToken))
        {
            return await ProjectAsync(context, request.CaseId, wasReplay: true, cancellationToken);
        }

        var snapshot = await EfCaseDataStore.SnapshotQuery(context, tracking: true)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        var workflow = await context.CaseWorkflows
            .Include(item => item.DueWork)
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleAsync(item => item.CaseId == request.CaseId, cancellationToken);

        CaseMutationGuard.RequireVersion(workflow, request.ExpectedVersion);
        var now = UtcNow();
        CaseMutationGuard.RequireLease(workflow, request.Actor, request.EditLeaseToken, now);
        ArchivedCaseGuard.RequireMutable(workflow);
        if (!Enum.TryParse<CaseLifecycleState>(workflow.State, out var state)
            || !AssessmentPolicy.IsWritableState(state))
        {
            throw new InvalidOperationException(
                "The Case can be saved only on a Not ready, Review, or Report preparation case.");
        }

        var beforeData = CaseDataFieldWriter.ReadEditable(snapshot);
        var beforeCompleteness = Completeness(snapshot);
        var data = CaseDataPolicy.Normalize(
            CaseWorkspacePolicy.Overlay(beforeData, request));
        if (data != beforeData)
        {
            CaseDataFieldWriter.ApplyEditableData(context, snapshot, data, request.Actor, now);
            CaseMatchIndexProjector.Apply(
                context,
                await context.CaseMatchIndex.SingleOrDefaultAsync(
                    item => item.CaseId == request.CaseId,
                    cancellationToken),
                CaseMatchIndexProjector.Project(
                    snapshot.Case,
                    snapshot.Fields,
                    caseMatchPolicies ?? [],
                    now));
        }

        if (request.Inspection is not null)
        {
            snapshot.Case.AcceptedInspectionDeadline = data.InspectionDeadline;
        }

        var assessmentFields = await context.CaseAssessmentFields
            .Where(item => item.CaseId == request.CaseId)
            .ToListAsync(cancellationToken);
        var requestedFields = CaseWorkspacePolicy.AssessmentFields(request);
        var (fieldsToWrite, merged) = AssessmentWriteSet.Build(requestedFields, assessmentFields);
        AssessmentPolicy.ValidateMergedState(requestedFields, merged);
        var (beforeFields, afterFields) = AssessmentWriteSet.Apply(
            context,
            workflow.Case,
            request.CaseId,
            assessmentFields,
            fieldsToWrite,
            request.Actor,
            now);

        var (estimate, beforeLines, afterLines) = await SaveEstimateAsync(
            context,
            request,
            workflow,
            now,
            cancellationToken);

        if (request.Report?.SignOffEngineerId is { } signOffEngineerId)
        {
            workflow.SignOffEngineerId = signOffEngineerId;
        }

        if (request.Completeness is { } completeness)
        {
            snapshot.Case.InstructionComplete =
                completeness.InstructionComplete ?? snapshot.Case.InstructionComplete;
            snapshot.Case.ImagesComplete =
                completeness.ImagesComplete ?? snapshot.Case.ImagesComplete;
        }

        // CASE-046: readiness is evaluated from the row that was just written,
        // never from anything the caller claimed, and never forced to false as
        // a side effect of editing an unrelated fact.
        var afterCompleteness = Completeness(snapshot);
        var evaluation = CaseCompletenessPolicy.Evaluate(afterCompleteness, configuration);
        snapshot.CompletenessPolicyKey = evaluation.PolicyKey;
        snapshot.CompletenessPolicyVersion = evaluation.PolicyVersion;
        snapshot.CompletenessPolicySatisfied = evaluation.SatisfiesPolicy;
        if (workflow.AssignedEngineerId is null
            && state is CaseLifecycleState.NotReady or CaseLifecycleState.Review)
        {
            if (evaluation.SatisfiesPolicy)
            {
                workflow.State = nameof(CaseLifecycleState.Review);
                CaseChaseState.Stop(workflow);
            }
            else
            {
                workflow.State = nameof(CaseLifecycleState.NotReady);
                CaseDueWorkScheduler.Schedule(
                    context,
                    workflow,
                    snapshot.Case.AcceptedInspectionDeadline,
                    now);
            }
        }

        var beforeVersion = workflow.Version;
        CaseMutationGuard.Complete(workflow);
        CaseMutationHistory.Add(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            request.Reason,
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
                    EstimateLines = beforeLines
                },
                JsonOptions),
            JsonSerializer.Serialize(
                new
                {
                    Data = data,
                    Completeness = afterCompleteness,
                    Fields = afterFields,
                    EstimateLines = afterLines,
                    Estimate = estimate is null
                        ? null
                        : new { estimate.Id, estimate.Version, estimate.Name }
                },
                JsonOptions),
            $"{CaseWorkspacePolicy.PolicyKey}/v{CaseWorkspacePolicy.PolicyVersion}",
            now);
        // The workspace save covers narrative, content and settlement facts
        // a frozen report pins: the Case's current generation goes stale in
        // this same transaction, so the change and the staleness it causes
        // commit together or not at all. Engineer notes are a separate
        // command and never reach this path.
        await EfCaseReportGenerationStore.MarkStaleAsync(
            context,
            request.CaseId,
            "case_workspace_saved",
            now,
            cancellationToken);

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

        return await ProjectAsync(context, request.CaseId, wasReplay: false, cancellationToken);
    }

    /// <summary>
    /// The Draft estimate the workspace edits. A Case with an accepted
    /// estimate and no open Draft refuses the whole save: correcting an
    /// accepted estimate is the explicit reasoned-correction command, not a
    /// side effect of editing the Case.
    /// </summary>
    private static async Task<(
        CaseRepairSpecificationEntity? Estimate,
        object? BeforeLines,
        object? AfterLines)> SaveEstimateAsync(
        PegasusDbContext context,
        SaveCaseWorkspaceRequest request,
        CaseWorkflowEntity workflow,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (request.Estimate is not { } section)
        {
            return (null, null, null);
        }

        var draft = await EfRepairSpecificationStore.DraftQuery(context, request.CaseId)
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(cancellationToken);
        if (draft is null)
        {
            if (await EfRepairSpecificationStore.AcceptedQuery(context, request.CaseId)
                    .AnyAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "An accepted repair specification is immutable; start a reasoned correction "
                    + "draft before editing its lines.");
            }

            var version = await EfRepairSpecificationStore.NextVersionAsync(
                context,
                request.CaseId,
                cancellationToken);
            draft = EfRepairSpecificationStore.NewLegacyDraft(
                request.CaseId,
                workflow.Case,
                version,
                request.Actor.SubjectId,
                request.OperationKey,
                now);
            context.CaseRepairSpecifications.Add(draft);
        }

        if (section.EstimateId is { } estimateId && estimateId != draft.Id)
        {
            throw new InvalidOperationException(
                "The submitted estimate is not the Case's open Draft.");
        }

        EstimatePolicy.ValidateEditable(EfRepairSpecificationStore.Map(draft), request.Actor);
        if (section.Details is { } details)
        {
            EfRepairSpecificationStore.ApplyDetails(draft, details);
        }

        object? beforeLines = null;
        object? afterLines = null;
        if (section.Lines is { } lines)
        {
            var tracked = draft.Lines.OrderBy(line => line.Position).ToList();
            (beforeLines, afterLines) = EstimateLineWriter.Replace(
                context,
                request.CaseId,
                workflow.Case,
                draft,
                tracked,
                lines,
                request.Actor,
                now);
            draft.Lines.Clear();
            foreach (var line in tracked)
            {
                draft.Lines.Add(line);
            }
        }

        draft.LastOperationKey = request.OperationKey;
        return (draft, beforeLines, afterLines);
    }

    private static async Task<SaveCaseWorkspaceResult> ProjectAsync(
        PegasusDbContext context,
        Guid caseId,
        bool wasReplay,
        CancellationToken cancellationToken)
    {
        var snapshot = await EfCaseDataStore.SnapshotQuery(context, tracking: false)
            .SingleAsync(item => item.CaseId == caseId, cancellationToken);
        var workflow = await context.CaseWorkflows.AsNoTracking()
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleAsync(item => item.CaseId == caseId, cancellationToken);
        var estimate = await EfRepairSpecificationStore.DraftQuery(context, caseId)
            .AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(cancellationToken);
        var fields = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderBy(item => item.FieldPath)
            .ToArrayAsync(cancellationToken);
        var estimateId = estimate?.Id;
        var lines = estimateId is null
            ? []
            : await context.CaseEstimateLines.AsNoTracking()
                .Where(item => item.CaseId == caseId && item.RepairSpecificationId == estimateId)
                .OrderBy(item => item.Position)
                .ToArrayAsync(cancellationToken);
        return new(
            EfCaseDataStore.Map(snapshot, workflow),
            EfCaseAssessmentStore.Map(
                workflow,
                fields,
                lines,
                snapshot.Fields.ToArray()),
            estimate is null ? null : EfRepairSpecificationStore.Map(estimate),
            wasReplay);
    }

    private static CaseCompleteness Completeness(CaseDataSnapshotEntity snapshot) => new(
        snapshot.Case.InstructionComplete,
        snapshot.Case.ImagesComplete,
        snapshot.Case.InstructionConfirmedByStaff,
        snapshot.Case.ImagesConfirmedByStaff);

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
                request.Overview,
                request.Inspection,
                request.Vehicle,
                request.Damage,
                request.Valuation,
                request.Estimate,
                request.Settlement,
                request.Report,
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
}
