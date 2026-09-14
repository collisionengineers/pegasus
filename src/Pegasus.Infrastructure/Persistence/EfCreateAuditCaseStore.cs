using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Creates the linked Audit Case (decided 13 September): a new Case row sharing
/// the original's Principal, year and sequence, with the <c>a.</c>/<c>ap.</c>
/// value as its own reference, linked back through <c>AuditOfCaseId</c>. The
/// original's current data, assessment fields, current estimate and files are
/// copied so the Audit is a Case in its own right; the files share the same
/// Box content by reference (no copy). Its Box folder is the <c>a.</c>/<c>ap.</c>
/// subfolder under the original's, created by the ordinary custody work with
/// the parent named. One history line lands on each Case. Replays return the
/// committed result.
/// </summary>
public sealed class EfCreateAuditCaseStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : ICreateAuditCaseStore, ICaseAuditLinkQueries, ICaseReportGeneratedQueries
{
    private const string EventKind = "audit_case_created";

    public async Task<bool> HasGeneratedReportAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await (
                from generation in context.Set<CaseReportGenerationEntity>().AsNoTracking()
                join artifact in context.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                    on generation.Id equals artifact.GenerationId
                where generation.CaseId == caseId && artifact.State == "Confirmed"
                select artifact.Id)
            .AnyAsync(cancellationToken);
    }

    public async Task<CaseAuditLink?> GetAuditCaseAsync(Guid sourceCaseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Cases.AsNoTracking()
            .Where(item => item.AuditOfCaseId == sourceCaseId)
            .Select(item => new CaseAuditLink(item.Id, item.Reference))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CaseAuditLink?> GetOriginalCaseAsync(Guid auditCaseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await (
                from audit in context.Cases.AsNoTracking()
                join source in context.Cases.AsNoTracking() on audit.AuditOfCaseId equals source.Id
                where audit.Id == auditCaseId
                select new CaseAuditLink(source.Id, source.Reference))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CreateAuditCaseResult> CreateAsync(
        CreateAuditCaseCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var request = command.Request;
        var fingerprint = Fingerprint(command);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.CaseWorkflowEvents.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CaseId == request.CaseId && item.OperationKey == request.OperationKey,
                cancellationToken);
        if (replay is not null)
        {
            if (replay.EventType != EventKind || !string.Equals(replay.RequestHash, fingerprint, StringComparison.Ordinal))
            {
                throw new CaseOperationConflictException(request.CaseId, request.OperationKey);
            }

            var linked = await context.Cases.AsNoTracking()
                .SingleAsync(item => item.AuditOfCaseId == request.CaseId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(command.Source, Identity(linked, command.Source), command.Assessment, IsReplay: true);
        }

        var workflow = await context.CaseWorkflows
            .Include(item => item.Case).ThenInclude(item => item.Principal)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        var now = timeProvider.GetUtcNow();
        ArchivedCaseGuard.RequireMutable(workflow);
        CaseMutationGuard.RequireVersion(workflow, request.ExpectedVersion);
        CaseMutationGuard.RequireLease(workflow, request.Actor, request.EditLeaseToken, now);
        if (await context.Cases.AnyAsync(item => item.AuditOfCaseId == request.CaseId, cancellationToken))
        {
            throw new AuditCaseCreationException(AuditCaseRefusal.AuditCaseAlreadyExists);
        }

        var source = workflow.Case;
        var auditCaseId = Guid.NewGuid();
        var auditCase = new CaseEntity
        {
            Id = auditCaseId,
            PrincipalId = source.PrincipalId,
            Principal = source.Principal,
            SequenceLineageId = source.SequenceLineageId,
            Year = source.Year,
            Sequence = source.Sequence,
            Reference = command.AuditReference,
            AuditReference = command.AuditReference,
            Type = "audit",
            InitialState = "review",
            CustodyState = "pending",
            AuditOfCaseId = source.Id,
            StandaloneAuditAssessment = command.Assessment.ToString(),
            AcceptedInspectionDeadline = source.AcceptedInspectionDeadline,
            InstructionComplete = true,
            ImagesComplete = true,
            CreatedAtUtc = now,
            Version = 0
        };
        context.Cases.Add(auditCase);

        await CopyDataAsync(context, source.Id, auditCase, now, cancellationToken);
        await CopyAssessmentFieldsAsync(context, source.Id, auditCase, cancellationToken);
        await CopyCurrentEstimateAsync(context, source.Id, auditCase, request, now, cancellationToken);
        await CopyDocumentsAsync(context, source.Id, auditCase, request, now, cancellationToken);

        var auditWorkflow = new CaseWorkflowEntity
        {
            CaseId = auditCaseId,
            Case = auditCase,
            State = nameof(CaseLifecycleState.Review),
            StateEnteredAtUtc = now,
            AssignedEngineerId = command.EngineerId,
            SignOffEngineerId = workflow.SignOffEngineerId,
            Version = 0
        };
        context.CaseWorkflows.Add(auditWorkflow);
        AutomaticEvaReviewSubmissionScheduling.AddForReviewTransition(context, auditWorkflow, 0, now);

        // The same custody work every receiptless Case queues; the processor sees
        // the parent on the row and roots the folder under the original's.
        context.ExternalWorkItems.Add(new()
        {
            Id = Guid.NewGuid(),
            Case = auditCase,
            CaseId = auditCaseId,
            Kind = ExternalWorkKinds.CreateCaseCustody,
            OperationKey = $"audit-custody:{auditCaseId:N}",
            State = "pending",
            AttemptCount = 0,
            DueAtUtc = now,
            CaseRootCreationToken = CustodyCreationOwner.Create()
        });

        var actorName = await ActorNameAsync(context, request.Actor, cancellationToken);
        var sourceLine = AuditCasePolicy.SourceHistoryLine(command.AuditReference, actorName, source.Reference, command.Assessment);
        var beforeVersion = workflow.Version;
        CaseMutationGuard.Complete(workflow);
        var resultJson = JsonSerializer.Serialize(new
        {
            AuditCaseId = auditCaseId,
            command.AuditReference,
            Assessment = command.Assessment.ToString()
        });
        CaseMutationHistory.Add(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            sourceLine,
            EventKind,
            fingerprint,
            beforeVersion,
            workflow.Version,
            "null",
            resultJson,
            "audit-case-v1",
            now);
        CaseMutationHistory.Add(
            context,
            auditWorkflow,
            request.Actor,
            $"{request.OperationKey}:audit",
            AuditCasePolicy.AuditHistoryLine(command.AuditReference, actorName, source.Reference, command.Assessment),
            EventKind,
            fingerprint,
            0,
            0,
            "null",
            resultJson,
            "audit-case-v1",
            now);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(command.Source, Identity(auditCase, command.Source), command.Assessment, IsReplay: false);
    }

    private static async Task CopyDataAsync(
        PegasusDbContext context,
        Guid sourceCaseId,
        CaseEntity auditCase,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var source = await context.CaseDataSnapshots.AsNoTracking()
            .Include(item => item.Fields)
            .SingleOrDefaultAsync(item => item.CaseId == sourceCaseId, cancellationToken);
        var snapshot = new CaseDataSnapshotEntity
        {
            CaseId = auditCase.Id,
            Case = auditCase,
            CompletenessPolicyKey = source?.CompletenessPolicyKey ?? "case-completeness",
            CompletenessPolicyVersion = source?.CompletenessPolicyVersion ?? 1,
            CompletenessPolicySatisfied = true,
            AcceptedAtUtc = now
        };
        foreach (var field in source?.Fields ?? [])
        {
            snapshot.Fields.Add(new CaseDataFieldEntity
            {
                CaseId = auditCase.Id,
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

    private static async Task CopyAssessmentFieldsAsync(
        PegasusDbContext context,
        Guid sourceCaseId,
        CaseEntity auditCase,
        CancellationToken cancellationToken)
    {
        var fields = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.CaseId == sourceCaseId)
            .ToListAsync(cancellationToken);
        foreach (var field in fields)
        {
            context.CaseAssessmentFields.Add(new CaseAssessmentFieldEntity
            {
                CaseId = auditCase.Id,
                Case = auditCase,
                FieldPath = field.FieldPath,
                Value = field.Value,
                RecordedByKind = field.RecordedByKind,
                RecordedBy = field.RecordedBy,
                RecordedAtUtc = field.RecordedAtUtc,
                ConfirmedBy = field.ConfirmedBy,
                ConfirmedAtUtc = field.ConfirmedAtUtc
            });
        }
    }

    /// <summary>The original's current estimate (or its latest draft) becomes the Audit Case's fresh Current estimate, version 1.</summary>
    private static async Task CopyCurrentEstimateAsync(
        PegasusDbContext context,
        Guid sourceCaseId,
        CaseEntity auditCase,
        CreateAuditCaseRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var source = await EfRepairSpecificationStore.AcceptedQuery(context, sourceCaseId)
            .AsNoTracking()
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await EfRepairSpecificationStore.DraftQuery(context, sourceCaseId)
                .AsNoTracking()
                .Include(item => item.Lines)
                .FirstOrDefaultAsync(cancellationToken);
        if (source is null)
        {
            return;
        }

        var copy = new CaseRepairSpecificationEntity
        {
            Id = Guid.NewGuid(),
            CaseId = auditCase.Id,
            Case = auditCase,
            Version = 1,
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
            CreatedBy = request.Actor.SubjectId,
            CreationOperationKey = $"{request.OperationKey}:estimate",
            CreatedAtUtc = now,
            AcceptedBy = source.AcceptedBy,
            AcceptedAtUtc = source.AcceptedAtUtc,
            Name = source.Name,
            RepairDays = source.RepairDays,
            LabourRate = source.LabourRate,
            PaintMaterials = source.PaintMaterials,
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
            Notes = source.Notes,
            IsCurrent = source.IsCurrent,
            LastOperationKey = $"{request.OperationKey}:estimate"
        };
        foreach (var line in source.Lines.OrderBy(line => line.Position))
        {
            copy.Lines.Add(new CaseEstimateLineEntity
            {
                Id = Guid.NewGuid(),
                CaseId = auditCase.Id,
                Case = auditCase,
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
                ConfirmedBy = line.ConfirmedBy,
                ConfirmedAtUtc = line.ConfirmedAtUtc,
                Operation = line.Operation
            });
        }

        context.CaseRepairSpecifications.Add(copy);
    }

    /// <summary>
    /// The original's files become the Audit Case's files: a new document, version
    /// and occurrence row per current file, every version naming the same Box
    /// file and revision as the original's. Nothing is copied in Box.
    /// </summary>
    private static async Task CopyDocumentsAsync(
        PegasusDbContext context,
        Guid sourceCaseId,
        CaseEntity auditCase,
        CreateAuditCaseRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var documents = await context.Set<CaseDocumentEntity>().AsNoTracking()
            .Where(item => item.CaseId == sourceCaseId)
            .OrderBy(item => item.Ordinal)
            .ToListAsync(cancellationToken);
        if (documents.Count == 0)
        {
            return;
        }

        var documentIds = documents.Select(item => item.Id).ToArray();
        var versions = await context.Set<DocumentVersionEntity>().AsNoTracking()
            .Where(item => documentIds.Contains(item.DocumentId) && item.IsCurrent && !item.IsLogicallyRemoved)
            .ToListAsync(cancellationToken);
        var occurrences = await context.Set<DocumentOccurrenceEntity>().AsNoTracking()
            .Where(item => item.CaseId == sourceCaseId)
            .ToListAsync(cancellationToken);
        var ordinal = 0;
        foreach (var document in documents)
        {
            var version = versions.FirstOrDefault(item => item.DocumentId == document.Id);
            if (version is null)
            {
                continue;
            }

            ordinal++;
            var newDocument = new CaseDocumentEntity
            {
                Id = Guid.NewGuid(),
                CaseId = auditCase.Id,
                Ordinal = ordinal,
                SourceOccurrenceIdentity = document.SourceOccurrenceIdentity
            };
            var newVersion = new DocumentVersionEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = newDocument.Id,
                Version = 1,
                FileName = version.FileName,
                MediaType = version.MediaType,
                ContentLength = version.ContentLength,
                Sha256 = version.Sha256,
                BoxFileId = version.BoxFileId,
                BoxVersionId = version.BoxVersionId,
                PendingContentStorageKey = version.PendingContentStorageKey,
                CustodyStatus = version.CustodyStatus,
                CreatedAtUtc = now,
                CreatedBy = request.Actor.SubjectId,
                IsCurrent = true,
                IsLogicallyRemoved = false
            };
            context.Set<CaseDocumentEntity>().Add(newDocument);
            context.Set<DocumentVersionEntity>().Add(newVersion);
            foreach (var occurrence in occurrences.Where(item => item.DocumentId == document.Id && item.VersionId == version.Id))
            {
                context.Set<DocumentOccurrenceEntity>().Add(new DocumentOccurrenceEntity
                {
                    Id = Guid.NewGuid(),
                    CaseId = auditCase.Id,
                    DocumentId = newDocument.Id,
                    VersionId = newVersion.Id,
                    Ordinal = occurrence.Ordinal,
                    SemanticRole = occurrence.SemanticRole,
                    Source = occurrence.Source,
                    SourceOccurrenceIdentity = occurrence.SourceOccurrenceIdentity,
                    RecordedAtUtc = now,
                    OperationKey = $"{request.OperationKey}:file:{occurrence.Id:N}",
                    PreparationRole = occurrence.PreparationRole,
                    SupportingOrder = occurrence.SupportingOrder,
                    RotationDegrees = occurrence.RotationDegrees,
                    CropLeft = occurrence.CropLeft,
                    CropTop = occurrence.CropTop,
                    CropWidth = occurrence.CropWidth,
                    CropHeight = occurrence.CropHeight,
                    PreparationVersion = 0,
                    PreparedBy = occurrence.PreparedBy,
                    PreparedAtUtc = occurrence.PreparedAtUtc
                });
            }
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

    private static CaseIdentity Identity(CaseEntity auditCase, CaseIdentity source) => new(
        auditCase.Id,
        source.PrincipalCode,
        auditCase.Year,
        auditCase.Sequence,
        auditCase.Reference,
        auditCase.AuditReference);

    private static string Fingerprint(CreateAuditCaseCommand command) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            '|',
            "create_audit_case",
            command.Request.CaseId,
            command.Request.ExpectedVersion,
            command.Request.Actor.Kind,
            command.Request.Actor.SubjectId,
            command.AuditReference,
            command.Assessment))));
}
