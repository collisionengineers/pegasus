using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfRepairSpecificationStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IRepairSpecificationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RepairSpecificationVersion> StartDraftAsync(
        StartRepairSpecificationDraftRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireEngineer(request.Actor);
        var source = request.Source.Route == RepairSpecificationSourceRoute.LegacyUnresolved
            ? request.Source
            : RepairSpecificationPolicy.ValidateSource(request.Source);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = Hash(request);
        if (await FindReplayAsync(context, request.CaseId, request.OperationKey, requestHash, cancellationToken))
        {
            var replayed = await context.CaseRepairSpecifications.AsNoTracking().Include(item => item.Lines)
                .SingleAsync(item => item.CaseId == request.CaseId
                    && item.CreationOperationKey == request.OperationKey, cancellationToken);
            return Map(replayed);
        }

        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        Guard(workflow, request.ExpectedCaseVersion, request.Actor, request.EditLeaseToken, Now());
        if (await context.CaseRepairSpecifications.AnyAsync(
                item => item.CaseId == request.CaseId
                    && item.State == RepairSpecificationState.Draft.ToString(),
                cancellationToken))
        {
            throw new InvalidOperationException("A current repair-specification draft already exists for this case.");
        }

        CaseRepairSpecificationEntity? predecessor = null;
        if (request.SupersedesSpecificationId is { } predecessorId)
        {
            predecessor = await context.CaseRepairSpecifications
                .Include(item => item.Lines)
                .SingleOrDefaultAsync(
                    item => item.Id == predecessorId && item.CaseId == request.CaseId,
                    cancellationToken)
                ?? throw new InvalidOperationException("The repair specification being corrected was not found.");
            if (predecessor.State != RepairSpecificationState.Accepted.ToString())
            {
                throw new InvalidOperationException("A correction must supersede the accepted repair specification.");
            }
        }
        else if (await context.CaseRepairSpecifications.AnyAsync(
            item => item.CaseId == request.CaseId
                && item.State == RepairSpecificationState.Accepted.ToString(),
            cancellationToken))
        {
            throw new InvalidOperationException(
                "The accepted repair specification is immutable; start a reasoned correction that identifies it.");
        }

        var nextVersion = (await context.CaseRepairSpecifications
            .Where(item => item.CaseId == request.CaseId)
            .MaxAsync(item => (int?)item.Version, cancellationToken) ?? 0) + 1;
        var now = Now();
        var entity = new CaseRepairSpecificationEntity
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            Case = workflow.Case,
            Version = nextVersion,
            State = RepairSpecificationState.Draft.ToString(),
            SourceRoute = source.Route.ToString(),
            SourceArtifactReference = source.ArtifactReference,
            SourceVersion = source.SourceVersion,
            SourceSha256 = source.Sha256,
            CreatedBy = request.Actor.SubjectId,
            CreationOperationKey = request.OperationKey,
            CreatedAtUtc = now,
            SupersedesSpecificationId = predecessor?.Id,
            SupersessionReason = predecessor is null ? null : RequiredReason(request.Reason),
        };
        context.CaseRepairSpecifications.Add(entity);
        if (predecessor is not null)
        {
            foreach (var line in predecessor.Lines.OrderBy(item => item.Position))
            {
                context.CaseEstimateLines.Add(CloneLine(line, entity, request.Actor, now));
            }
        }
        else if (request.Lines is { } suppliedLines)
        {
            var position = 0;
            foreach (var line in AssessmentPolicy.NormalizeRepairSpecificationLines(suppliedLines))
            {
                position++;
                context.CaseEstimateLines.Add(NewLine(line, position, entity, request.Actor, now));
            }
        }
        AddHistory(context, workflow, request.Actor, request.OperationKey, request.Reason,
            "repair_specification_draft_started", requestHash,
            new { entity.Id, entity.Version }, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<RepairSpecificationVersion> AcceptAsync(
        AcceptRepairSpecificationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireEngineer(request.Actor);
        var source = RepairSpecificationPolicy.ValidateSource(request.Source);
        var basis = RepairSpecificationPolicy.ValidateCalculationBasis(request.CalculationBasis);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var requestHash = Hash(request);
        if (await FindReplayAsync(context, request.CaseId, request.OperationKey, requestHash, cancellationToken))
        {
            return await GetRequiredVersionAsync(context, request.CaseId, request.SpecificationId, cancellationToken);
        }
        var workflow = await RequiredWorkflowAsync(context, request.CaseId, cancellationToken);
        var now = Now();
        Guard(workflow, request.ExpectedCaseVersion, request.Actor, request.EditLeaseToken, now);
        var entity = await context.CaseRepairSpecifications.Include(item => item.Lines)
            .SingleOrDefaultAsync(
                item => item.Id == request.SpecificationId && item.CaseId == request.CaseId,
                cancellationToken)
            ?? throw new InvalidOperationException("The repair-specification draft was not found.");
        if (entity.Version != request.ExpectedSpecificationVersion)
        {
            throw new InvalidOperationException("The repair-specification version is stale.");
        }
        var candidate = Map(entity) with { Source = source, CalculationBasis = basis };
        RepairSpecificationPolicy.ValidateAcceptance(candidate, request.Actor);
        if (await context.CaseRepairSpecifications.AnyAsync(
                item => item.CaseId == request.CaseId && item.Id != entity.Id
                    && item.State == RepairSpecificationState.Accepted.ToString()
                    && item.Id != entity.SupersedesSpecificationId,
                cancellationToken))
        {
            throw new InvalidOperationException("A current accepted repair specification already exists; start a reasoned correction.");
        }
        if (entity.SupersedesSpecificationId is { } predecessorId)
        {
            var predecessor = await context.CaseRepairSpecifications.SingleAsync(
                item => item.Id == predecessorId,
                cancellationToken);
            predecessor.State = RepairSpecificationState.Superseded.ToString();
            await context.SaveChangesAsync(cancellationToken);
        }
        entity.SourceRoute = source.Route.ToString();
        entity.SourceArtifactReference = source.ArtifactReference;
        entity.SourceVersion = source.SourceVersion;
        entity.SourceSha256 = source.Sha256;
        entity.CalculationLabour = basis.Labour;
        entity.CalculationParts = basis.Parts;
        entity.CalculationPaintMaterials = basis.PaintMaterials;
        entity.CalculationSpecialistOther = basis.SpecialistOther;
        entity.RepairerVatRegistered = basis.RepairerVatRegistered;
        entity.CalculationVat = basis.Vat;
        entity.CalculationTotal = basis.Total;
        entity.CalculationPolicyVersion = basis.PolicyVersion;
        entity.State = RepairSpecificationState.Accepted.ToString();
        entity.AcceptedBy = request.Actor.SubjectId;
        entity.AcceptedAtUtc = now;
        AddHistory(context, workflow, request.Actor, request.OperationKey, request.Reason,
            "repair_specification_accepted", requestHash,
            new { entity.Id, entity.Version }, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<RepairSpecificationVersion?> GetVersionAsync(
        Guid caseId, Guid specificationId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CaseRepairSpecifications.AsNoTracking().Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.CaseId == caseId && item.Id == specificationId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<RepairSpecificationVersion?> GetCurrentAcceptedAsync(
        Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CaseRepairSpecifications.AsNoTracking().Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.CaseId == caseId
                && item.State == RepairSpecificationState.Accepted.ToString(), cancellationToken);
        return entity is null ? null : Map(entity);
    }

    private static async Task<RepairSpecificationVersion> GetRequiredVersionAsync(
        PegasusDbContext context, Guid caseId, Guid id, CancellationToken cancellationToken) =>
        Map(await context.CaseRepairSpecifications.AsNoTracking().Include(item => item.Lines)
            .SingleAsync(item => item.CaseId == caseId && item.Id == id, cancellationToken));

    private static async Task<CaseWorkflowEntity> RequiredWorkflowAsync(
        PegasusDbContext context, Guid caseId, CancellationToken cancellationToken) =>
        await context.CaseWorkflows.Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
        ?? throw new KeyNotFoundException($"Case '{caseId}' was not found.");

    private static void Guard(
        CaseWorkflowEntity workflow, long expectedVersion, ActionActor actor, string lease, DateTimeOffset now)
    {
        CaseMutationGuard.RequireVersion(workflow, expectedVersion);
        CaseMutationGuard.RequireLease(workflow, actor, lease, now);
        ArchivedCaseGuard.RequireMutable(workflow);
        workflow.Version++;
        CaseMutationGuard.ClearLease(workflow);
    }

    private static void RequireEngineer(ActionActor actor)
    {
        if (actor.Kind != ActorKind.Staff || !actor.IsInRole(StaffRole.Engineer))
        {
            throw new InvalidOperationException("Only an authenticated staff Engineer can change repair-specification acceptance.");
        }
    }

    private static string RequiredReason(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A reason is required.") : value.Trim();

    private DateTimeOffset Now()
    {
        var now = timeProvider.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }

    private static async Task<bool> FindReplayAsync(
        PegasusDbContext context, Guid caseId, string operationKey, string requestHash,
        CancellationToken cancellationToken)
    {
        var replay = await context.CaseWorkflowEvents.AsNoTracking().SingleOrDefaultAsync(
            item => item.CaseId == caseId && item.OperationKey == operationKey,
            cancellationToken);
        if (replay is null)
        {
            return false;
        }
        if (!string.Equals(replay.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new CaseOperationConflictException(caseId, operationKey);
        }
        return true;
    }

    private static string Hash<T>(T request) => Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, JsonOptions))));

    private static CaseEstimateLineEntity CloneLine(
        CaseEstimateLineEntity line, CaseRepairSpecificationEntity target, ActionActor actor, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), CaseId = target.CaseId, Case = target.Case,
        RepairSpecificationId = target.Id, RepairSpecification = target, Position = line.Position,
        LineType = line.LineType, GuideCode = line.GuideCode, Description = line.Description,
        WorkUnits = line.WorkUnits, Price = line.Price, Unpriced = line.Unpriced,
        PartNumber = line.PartNumber, Betterment = line.Betterment, Status = line.Status,
        EvidenceLabel = line.EvidenceLabel, Justification = line.Justification,
        RecordedByKind = actor.Kind.ToString(), RecordedBy = actor.SubjectId, RecordedAtUtc = now,
        ConfirmedBy = actor.SubjectId, ConfirmedAtUtc = now,
    };

    private static CaseEstimateLineEntity NewLine(
        EstimateLineInput line, int position, CaseRepairSpecificationEntity target,
        ActionActor actor, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), CaseId = target.CaseId, Case = target.Case,
        RepairSpecificationId = target.Id, RepairSpecification = target, Position = position,
        LineType = line.Type, GuideCode = line.GuideCode, Description = line.Description,
        WorkUnits = line.WorkUnits, Price = line.Price, Unpriced = line.Unpriced,
        PartNumber = line.PartNumber, Betterment = line.Betterment, Status = line.Status,
        EvidenceLabel = line.EvidenceLabel, Justification = line.Justification,
        RecordedByKind = actor.Kind.ToString(), RecordedBy = actor.SubjectId, RecordedAtUtc = now,
        ConfirmedBy = actor.SubjectId, ConfirmedAtUtc = now,
    };

    private static RepairSpecificationVersion Map(CaseRepairSpecificationEntity entity) => new(
        entity.Id, entity.CaseId, entity.Version,
        Enum.Parse<RepairSpecificationState>(entity.State),
        new(Enum.Parse<RepairSpecificationSourceRoute>(entity.SourceRoute),
            entity.SourceArtifactReference, entity.SourceVersion, entity.SourceSha256),
        entity.Lines.OrderBy(line => line.Position).Select(line => new CaseEstimateLineRecord(
            line.Id, line.Position, line.LineType, line.GuideCode, line.Description,
            line.WorkUnits, line.Price, line.Unpriced, line.PartNumber, line.Betterment,
            line.Status, line.EvidenceLabel, line.Justification,
            Enum.Parse<ActorKind>(line.RecordedByKind), line.RecordedBy, line.RecordedAtUtc,
            line.ConfirmedBy, line.ConfirmedAtUtc)).ToArray(),
        entity.CalculationLabour is { } labour ? new(
            labour, entity.CalculationParts!.Value, entity.CalculationPaintMaterials!.Value,
            entity.CalculationSpecialistOther!.Value, entity.RepairerVatRegistered!.Value,
            entity.CalculationVat!.Value, entity.CalculationTotal!.Value,
            entity.CalculationPolicyVersion!) : null,
        entity.CreatedBy, entity.CreatedAtUtc, entity.AcceptedBy, entity.AcceptedAtUtc,
        entity.SupersedesSpecificationId, entity.SupersessionReason);

    private static void AddHistory(
        PegasusDbContext context, CaseWorkflowEntity workflow, ActionActor actor,
        string operationKey, string reason, string eventType, string requestHash, object after,
        DateTimeOffset now)
    {
        var beforeVersion = workflow.Version - 1;
        var roles = JsonSerializer.Serialize(actor.Roles.OrderBy(role => role), JsonOptions);
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(), CaseId = workflow.CaseId, Workflow = workflow,
            EventType = eventType, OperationKey = operationKey, RequestHash = requestHash,
            ActorKind = actor.Kind.ToString(), ActorSubjectId = actor.SubjectId,
            ActorRolesJson = roles, Reason = RequiredReason(reason), OccurredAtUtc = now,
            BeforeVersion = beforeVersion, AfterVersion = workflow.Version,
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(), AggregateType = "case", AggregateId = workflow.CaseId.ToString("D"),
            EventKind = eventType, ActorKind = actor.Kind.ToString(), ActorSubjectId = actor.SubjectId,
            ActorRolesJson = roles, OccurredAtUtc = now, Outcome = "Succeeded",
            CorrelationId = operationKey, Reason = reason, BeforeJson = "{}",
            AfterJson = JsonSerializer.Serialize(after, JsonOptions),
            PolicyVersion = $"{RepairSpecificationPolicy.PolicyKey}/v{RepairSpecificationPolicy.PolicyVersion}",
        });
        context.CaseHistory.Add(new()
        {
            Id = Guid.NewGuid(), CaseId = workflow.CaseId, Case = workflow.Case,
            EventType = eventType, Actor = actor.SubjectId, Reason = reason, OccurredAtUtc = now,
            OperationKey = operationKey, BeforeVersion = beforeVersion, AfterVersion = workflow.Version,
        });
    }
}
