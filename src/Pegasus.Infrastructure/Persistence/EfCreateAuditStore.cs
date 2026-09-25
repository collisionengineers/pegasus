using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Creates the Audit work of an Inspection + Audit Case in one serializable
/// transaction: the Audit work row, the Inspection report's approval and sent
/// evidence moved onto the primary work, the Case back in Report preparation
/// with its Engineers, the <c>a.</c> Audit reference, a full copy of the
/// primary work's data into the Audit work, and the custody work that creates
/// the Audit's <c>a.</c> folder. Replays return the committed result.
/// </summary>
public sealed class EfCreateAuditStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : ICreateAuditStore
{
    private const string EventKind = "audit_created";

    public async Task<CreateAuditOutcome> CreateAsync(
        CreateAuditCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var request = command.Request;
        var fingerprint = Fingerprint(command);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await EfCaseWorkflowStore.AcquireWorkflowMutationLockAsync(context, request.CaseId, cancellationToken);

        if (await context.CaseEditLeaseOperations.AsNoTracking().AnyAsync(
                item => item.CaseId == request.CaseId && item.OperationKey == request.OperationKey,
                cancellationToken))
        {
            throw new CaseOperationConflictException(request.CaseId, request.OperationKey);
        }

        var replay = await context.CaseWorkflowEvents.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CaseId == request.CaseId && item.OperationKey == request.OperationKey,
                cancellationToken);
        if (replay is not null)
        {
            if (!string.Equals(replay.EventType, EventKind, StringComparison.Ordinal)
                || !CaseOperationReplay.FixedTimeEquals(replay.RequestHash, fingerprint))
            {
                throw new CaseOperationConflictException(request.CaseId, request.OperationKey);
            }

            var replayed = await ReplayAsync(context, command, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return replayed;
        }

        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .Include(item => item.DueWork)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        var now = timeProvider.GetUtcNow();
        ArchivedCaseGuard.RequireMutable(workflow);
        CaseMutationGuard.RequireVersion(workflow, request.ExpectedVersion);
        CaseMutationGuard.RequireLease(workflow, request.Actor, request.EditLeaseToken, now);

        // Tracked: the primary row takes the Inspection report's evidence.
        var works = await context.CaseWorks
            .Where(item => item.CaseId == request.CaseId)
            .ToListAsync(cancellationToken);
        if (works.Any(item => item.Kind == CaseWorkKinds.Audit))
        {
            throw new AuditCreationException(request.CaseId, AuditRefusal.AuditAlreadyExists);
        }

        if (workflow.State is not (nameof(CaseLifecycleState.PostReport)
                or nameof(CaseLifecycleState.PostReportComplete)
                or nameof(CaseLifecycleState.Query))
            || workflow.ReportSentEvidenceId is null)
        {
            throw new AuditCreationException(request.CaseId, AuditRefusal.ReportNotSent);
        }

        var primary = works.SingleOrDefault(item => item.Kind == CaseWorkKinds.Primary)
            ?? throw new InvalidDataException($"Case '{request.CaseId}' has no primary work.");
        var caseEntity = workflow.Case;
        var auditReference = CaseReferenceFormat.AuditReport(caseEntity.Reference);
        var beforeVersion = workflow.Version;
        var beforeJson = JsonSerializer.Serialize(new
        {
            workflow.State,
            workflow.ReportApprovalId,
            workflow.ReportSentEvidenceId,
            caseEntity.AuditReference
        });

        var auditWork = new CaseWorkEntity
        {
            Id = Guid.NewGuid(),
            CaseId = caseEntity.Id,
            Case = caseEntity,
            Kind = CaseWorkKinds.Audit,
            CreatedAtUtc = now
        };
        context.CaseWorks.Add(auditWork);

        // The Inspection report's approval and sent evidence stay with the
        // Inspection; the workflow is now the Audit's.
        primary.ReportApprovalId = workflow.ReportApprovalId;
        primary.ReportSentEvidenceId = workflow.ReportSentEvidenceId;
        workflow.ReportApprovalId = null;
        workflow.ReportSentEvidenceId = null;
        workflow.State = nameof(CaseLifecycleState.ReportPreparation);
        workflow.StateEnteredAtUtc = now;
        workflow.ClosureOutcome = null;
        CaseChaseState.Stop(workflow);
        caseEntity.AuditReference = auditReference;

        await CopyDataAsync(context, primary.Id, auditWork.Id, cancellationToken);
        await CopyAssessmentFieldsAsync(context, primary.Id, auditWork.Id, cancellationToken);
        await CopySpecificationsAsync(context, primary.Id, auditWork.Id, cancellationToken);
        var guideValuationIds = await CopyValuationsAsync(context, primary.Id, auditWork.Id, cancellationToken);
        await CopyAppliedValuationsAsync(
            context, caseEntity.Id, primary.Id, auditWork.Id, guideValuationIds, cancellationToken);
        await CopyReportWordingsAsync(context, primary.Id, auditWork.Id, cancellationToken);

        var custodyWork = new ExternalWorkItemEntity
        {
            Id = Guid.NewGuid(),
            CaseId = caseEntity.Id,
            Case = caseEntity,
            Kind = ExternalWorkKinds.CreateAuditReferenceCustody,
            OperationKey = CustodyOperationKey(caseEntity.Id),
            State = ExternalWorkStatePersistence.Pending,
            AttemptCount = 0,
            DueAtUtc = now,
            AuditFolderCreationToken = CustodyCreationOwner.Create()
        };
        context.ExternalWorkItems.Add(custodyWork);

        var actorName = await ActorNameAsync(context, request.Actor, cancellationToken);
        CaseMutationGuard.Complete(workflow);
        CaseMutationHistory.Add(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            AuditPolicy.HistoryLine(auditReference, actorName),
            EventKind,
            fingerprint,
            beforeVersion,
            workflow.Version,
            beforeJson,
            JsonSerializer.Serialize(new
            {
                workflow.State,
                AuditWorkId = auditWork.Id,
                AuditReference = auditReference,
                PrimaryReportApprovalId = primary.ReportApprovalId,
                PrimaryReportSentEvidenceId = primary.ReportSentEvidenceId,
                workflow.AssignedEngineerId,
                workflow.SignOffEngineerId
            }),
            "create-audit-v1",
            now);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            new(command.Case with { AuditReference = auditReference }, auditWork.Id, auditReference, IsReplay: false),
            custodyWork.Id);
    }

    private static async Task<CreateAuditOutcome> ReplayAsync(
        PegasusDbContext context,
        CreateAuditCommand command,
        CancellationToken cancellationToken)
    {
        var caseId = command.Request.CaseId;
        var audit = await context.CaseWorks.AsNoTracking()
            .Where(item => item.CaseId == caseId && item.Kind == CaseWorkKinds.Audit)
            .Select(item => new { item.Id, item.Case.AuditReference })
            .SingleAsync(cancellationToken);
        var auditReference = audit.AuditReference
            ?? throw new InvalidDataException($"Case '{caseId}' has an Audit work without its Audit reference.");
        var operationKey = CustodyOperationKey(caseId);
        var custodyWorkId = await context.ExternalWorkItems.AsNoTracking()
            .Where(item => item.OperationKey == operationKey)
            .Select(item => item.Id)
            .SingleAsync(cancellationToken);
        return new(
            new(command.Case with { AuditReference = auditReference }, audit.Id, auditReference, IsReplay: true),
            custodyWorkId);
    }

    /// <summary>The typed Case data and its fields, every column; confirmed values stay confirmed.</summary>
    private static async Task CopyDataAsync(
        PegasusDbContext context,
        Guid sourceWorkId,
        Guid auditWorkId,
        CancellationToken cancellationToken)
    {
        var source = await context.CaseDataSnapshots.AsNoTracking()
            .Include(item => item.Fields)
            .SingleOrDefaultAsync(item => item.WorkId == sourceWorkId, cancellationToken)
            ?? throw new InvalidDataException("The Inspection has no typed Case data to copy.");
        var snapshot = new CaseDataSnapshotEntity
        {
            WorkId = auditWorkId,
            OriginIntakeReceiptId = source.OriginIntakeReceiptId,
            OriginSourceChannel = source.OriginSourceChannel,
            OriginExternalReceiptToken = source.OriginExternalReceiptToken,
            OriginSourceHash = source.OriginSourceHash,
            OriginReceivedAtUtc = source.OriginReceivedAtUtc,
            SourceReaderKey = source.SourceReaderKey,
            SourceReaderVersion = source.SourceReaderVersion,
            ExtractionPolicyKey = source.ExtractionPolicyKey,
            ExtractionPolicyVersion = source.ExtractionPolicyVersion,
            CompletenessPolicyKey = source.CompletenessPolicyKey,
            CompletenessPolicyVersion = source.CompletenessPolicyVersion,
            CompletenessPolicySatisfied = source.CompletenessPolicySatisfied,
            AcceptedAtUtc = source.AcceptedAtUtc,
            ClaimSourceOverrideContactName = source.ClaimSourceOverrideContactName,
            ClaimSourceOverrideContactTelephone = source.ClaimSourceOverrideContactTelephone,
            ClaimSourceOverrideContactEmailAddress = source.ClaimSourceOverrideContactEmailAddress
        };
        foreach (var field in source.Fields)
        {
            snapshot.Fields.Add(new CaseDataFieldEntity
            {
                WorkId = auditWorkId,
                Snapshot = snapshot,
                FieldName = field.FieldName,
                ValueKind = field.ValueKind,
                ValueType = field.ValueType,
                Value = field.Value,
                SourceKind = field.SourceKind,
                SourceIdentity = field.SourceIdentity,
                SourceLabel = field.SourceLabel,
                PolicyKey = field.PolicyKey,
                PolicyVersion = field.PolicyVersion,
                ConfirmedByActor = field.ConfirmedByActor,
                ConfirmedAtUtc = field.ConfirmedAtUtc
            });
        }

        context.CaseDataSnapshots.Add(snapshot);
    }

    /// <summary>The assessment fields: fee, damage, decisions and report switches included.</summary>
    private static async Task CopyAssessmentFieldsAsync(
        PegasusDbContext context,
        Guid sourceWorkId,
        Guid auditWorkId,
        CancellationToken cancellationToken)
    {
        var fields = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.WorkId == sourceWorkId)
            .ToListAsync(cancellationToken);
        foreach (var field in fields)
        {
            context.CaseAssessmentFields.Add(new CaseAssessmentFieldEntity
            {
                WorkId = auditWorkId,
                FieldPath = field.FieldPath,
                Value = field.Value,
                RecordedByKind = field.RecordedByKind,
                RecordedBy = field.RecordedBy,
                RecordedAtUtc = field.RecordedAtUtc
            });
        }
    }

    /// <summary>
    /// Every live repair specification with its lines, every column: new ids,
    /// with the supersedes and supplementary links re-pointed at the copies.
    /// A link to a specification that is not copied (a discarded one) is
    /// cleared. Specification snapshots are not copied.
    /// </summary>
    private static async Task CopySpecificationsAsync(
        PegasusDbContext context,
        Guid sourceWorkId,
        Guid auditWorkId,
        CancellationToken cancellationToken)
    {
        const string discarded = nameof(RepairSpecificationState.Discarded);
        var specifications = await context.CaseRepairSpecifications.AsNoTracking()
            .Include(item => item.Lines)
            .Where(item => item.WorkId == sourceWorkId && item.State != discarded)
            .OrderBy(item => item.Version)
            .ToListAsync(cancellationToken);
        var ids = specifications.ToDictionary(item => item.Id, _ => Guid.NewGuid());
        Guid? Remap(Guid? id) => id is { } value && ids.TryGetValue(value, out var copied) ? copied : (Guid?)null;

        foreach (var source in specifications)
        {
            var copy = new CaseRepairSpecificationEntity
            {
                Id = ids[source.Id],
                WorkId = auditWorkId,
                Version = source.Version,
                State = source.State,
                SourceRoute = source.SourceRoute,
                SourceArtifactReference = source.SourceArtifactReference,
                SourceVersion = source.SourceVersion,
                SourceSha256 = source.SourceSha256,
                CalculationLabour = source.CalculationLabour,
                CalculationParts = source.CalculationParts,
                CalculationPaintMaterials = source.CalculationPaintMaterials,
                CalculationSpecialistOther = source.CalculationSpecialistOther,
                RepairerVatRegistered = source.RepairerVatRegistered,
                CalculationVat = source.CalculationVat,
                CalculationTotal = source.CalculationTotal,
                CalculationPolicyVersion = source.CalculationPolicyVersion,
                CreatedBy = source.CreatedBy,
                CreationOperationKey = source.CreationOperationKey,
                CreatedAtUtc = source.CreatedAtUtc,
                AcceptedBy = source.AcceptedBy,
                AcceptedAtUtc = source.AcceptedAtUtc,
                SupersedesSpecificationId = Remap(source.SupersedesSpecificationId),
                SupersessionReason = source.SupersessionReason,
                Name = source.Name,
                LabourRate = source.LabourRate,
                RegionalUplift = source.RegionalUplift,
                OtherCosts = source.OtherCosts,
                RateCardId = source.RateCardId,
                RateCardVersion = source.RateCardVersion,
                PartsDiscountPercent = source.PartsDiscountPercent,
                MaterialsDiscountPercent = source.MaterialsDiscountPercent,
                SpecialistDiscountPercent = source.SpecialistDiscountPercent,
                OverallDiscountPercent = source.OverallDiscountPercent,
                LabourVatApplicable = source.LabourVatApplicable,
                PartsVatApplicable = source.PartsVatApplicable,
                MaterialsVatApplicable = source.MaterialsVatApplicable,
                SpecialistVatApplicable = source.SpecialistVatApplicable,
                RepairerVatStatus = source.RepairerVatStatus,
                VatOverrideReason = source.VatOverrideReason,
                CalculationBreakdownJson = source.CalculationBreakdownJson,
                VatPercent = source.VatPercent,
                IsCurrent = source.IsCurrent,
                AiJobId = source.AiJobId,
                DiscardedBy = source.DiscardedBy,
                DiscardedAtUtc = source.DiscardedAtUtc,
                DiscardReason = source.DiscardReason,
                LastOperationKey = source.LastOperationKey,
                SupplementaryOfSpecificationId = Remap(source.SupplementaryOfSpecificationId),
                SupplementaryReason = source.SupplementaryReason,
                SupplementaryExplainOnReport = source.SupplementaryExplainOnReport,
                SupplementaryStatement = source.SupplementaryStatement
            };
            foreach (var line in source.Lines.OrderBy(line => line.Position))
            {
                copy.Lines.Add(new CaseEstimateLineEntity
                {
                    Id = Guid.NewGuid(),
                    WorkId = auditWorkId,
                    RepairSpecificationId = copy.Id,
                    Position = line.Position,
                    LineType = line.LineType,
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
                    RecordedByKind = line.RecordedByKind,
                    RecordedBy = line.RecordedBy,
                    RecordedAtUtc = line.RecordedAtUtc,
                    Operation = line.Operation,
                    Materials = line.Materials,
                    OriginalValuesJson = line.OriginalValuesJson,
                    CurrentValuesJson = line.CurrentValuesJson,
                    SourceDocumentIdentity = line.SourceDocumentIdentity,
                    SourceDocumentVersionId = line.SourceDocumentVersionId,
                    SourceDocumentSha256 = line.SourceDocumentSha256,
                    SourceRowIdentity = line.SourceRowIdentity,
                    AmendedBy = line.AmendedBy,
                    AmendedAtUtc = line.AmendedAtUtc
                });
            }

            context.CaseRepairSpecifications.Add(copy);
        }
    }

    /// <summary>The guide valuation cards, every column, under new ids; returns the id map.</summary>
    private static async Task<IReadOnlyDictionary<Guid, Guid>> CopyValuationsAsync(
        PegasusDbContext context,
        Guid sourceWorkId,
        Guid auditWorkId,
        CancellationToken cancellationToken)
    {
        var valuations = await context.CaseValuations.AsNoTracking()
            .Where(item => item.WorkId == sourceWorkId)
            .ToListAsync(cancellationToken);
        var ids = new Dictionary<Guid, Guid>();
        foreach (var source in valuations)
        {
            var copy = new CaseValuationEntity
            {
                Id = Guid.NewGuid(),
                WorkId = auditWorkId,
                Source = source.Source,
                Date = source.Date,
                Time = source.Time,
                GuideMonth = source.GuideMonth,
                Mileage = source.Mileage,
                RetailValue = source.RetailValue,
                TradeValue = source.TradeValue,
                RecordedBy = source.RecordedBy,
                RecordedAtUtc = source.RecordedAtUtc,
                LastEditedBy = source.LastEditedBy,
                LastEditedAtUtc = source.LastEditedAtUtc
            };
            ids.Add(source.Id, copy.Id);
            context.CaseValuations.Add(copy);
        }

        return ids;
    }

    /// <summary>
    /// The applied valuation snapshots under new ids, each re-pointed at its
    /// copied guide card and rehashed exactly as applying a valuation hashes it.
    /// </summary>
    private static async Task CopyAppliedValuationsAsync(
        PegasusDbContext context,
        Guid caseId,
        Guid sourceWorkId,
        Guid auditWorkId,
        IReadOnlyDictionary<Guid, Guid> guideValuationIds,
        CancellationToken cancellationToken)
    {
        var applied = await context.Set<AppliedValuationSnapshotEntity>().AsNoTracking()
            .Where(item => item.WorkId == sourceWorkId)
            .ToListAsync(cancellationToken);
        foreach (var source in applied)
        {
            var (snapshotJson, snapshotHash) = EfValuationStore.RepointAppliedSnapshot(
                source,
                caseId,
                guideValuationIds);
            context.Set<AppliedValuationSnapshotEntity>().Add(new AppliedValuationSnapshotEntity
            {
                Id = Guid.NewGuid(),
                WorkId = auditWorkId,
                SnapshotJson = snapshotJson,
                CalculationPolicyVersion = source.CalculationPolicyVersion,
                GeneratedByKind = source.GeneratedByKind,
                GeneratedBySubjectId = source.GeneratedBySubjectId,
                SnapshotHash = snapshotHash,
                AcceptedEngineerValue = source.AcceptedEngineerValue,
                AcceptedBy = source.AcceptedBy,
                AcceptedAtUtc = source.AcceptedAtUtc,
                Reason = source.Reason,
                PolicyVersion = source.PolicyVersion
            });
        }
    }

    /// <summary>The report wording blocks, every column, under new ids.</summary>
    private static async Task CopyReportWordingsAsync(
        PegasusDbContext context,
        Guid sourceWorkId,
        Guid auditWorkId,
        CancellationToken cancellationToken)
    {
        var wordings = await context.CaseReportWordings.AsNoTracking()
            .Where(item => item.WorkId == sourceWorkId)
            .ToListAsync(cancellationToken);
        foreach (var source in wordings)
        {
            context.CaseReportWordings.Add(new CaseReportWordingEntity
            {
                Id = Guid.NewGuid(),
                WorkId = auditWorkId,
                BlockKey = source.BlockKey,
                Title = source.Title,
                Text = source.Text,
                Order = source.Order,
                Included = source.Included,
                Manual = source.Manual,
                UpdatedBy = source.UpdatedBy,
                UpdatedAtUtc = source.UpdatedAtUtc
            });
        }
    }

    private static async Task<string> ActorNameAsync(PegasusDbContext context, ActionActor actor, CancellationToken cancellationToken)
    {
        if (actor.Kind == ActorKind.Staff && Guid.TryParse(actor.SubjectId, out var staffId))
        {
            var name = await context.Users.AsNoTracking()
                .Where(user => user.Id == staffId)
                .Select(user => user.UserName)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return actor.SubjectId;
    }

    /// <summary>One Audit per Case, so one <c>a.</c> folder custody operation per Case.</summary>
    private static string CustodyOperationKey(Guid caseId) =>
        string.Create(CultureInfo.InvariantCulture, $"audit-reference-custody:{caseId:N}");

    private static string Fingerprint(CreateAuditCommand command) =>
        CaseOperationReplay.Hash(string.Create(
            CultureInfo.InvariantCulture,
            $"create_audit|{command.Request.CaseId:D}|{command.Request.ExpectedVersion}|{command.Request.Actor.Kind}|{command.Request.Actor.SubjectId}|{command.AuditReference}"));
}
