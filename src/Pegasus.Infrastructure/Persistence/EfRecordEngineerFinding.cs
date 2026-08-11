using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfRecordEngineerFinding(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IRecordEngineerFinding
{
    public async Task<CaseIdentity> ExecuteAsync(
        RecordEngineerFindingRequest request,
        CancellationToken cancellationToken)
    {
        var actingEngineerId = EngineerFindingPolicy.ValidateRequest(request);
        var operationKey = request.OperationKey.Trim();
        var reason = request.Reason.Trim();
        var requestHash = RequestHash(request, operationKey, reason);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingFinding = await context.Set<CaseEngineerFindingEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken);
        if (existingFinding is not null)
        {
            if (string.Equals(existingFinding.OperationKey, operationKey, StringComparison.Ordinal)
                && FixedTimeHashEquals(existingFinding.RequestHash, requestHash))
            {
                return await LoadIdentityAsync(context, request.CaseId, cancellationToken);
            }
            if (string.Equals(existingFinding.OperationKey, operationKey, StringComparison.Ordinal))
            {
                throw new CaseOperationConflictException(request.CaseId, request.OperationKey);
            }

            throw new InvalidOperationException(
                "The assigned Engineer finding and Audit identity are already immutable for this case.");
        }

        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        ArchivedCaseGuard.RequireMutable(workflow);
        RequireVersion(workflow, request.ExpectedVersion);
        var recordedAtUtc = timeProvider.GetUtcNow();
        if (recordedAtUtc.Offset != TimeSpan.Zero)
        {
            recordedAtUtc = recordedAtUtc.ToUniversalTime();
        }
        RequireLease(workflow, request.Actor, request.EditLeaseToken, recordedAtUtc);

        var caseType = ParseCaseType(workflow.Case.Type);
        // The principal was settled when the case was allocated and is
        // immutable after it. Re-asserting which principal it is here refused
        // engineer findings on perfectly valid non-QDOS cases.
        var state = ParseLifecycleState(workflow.State);
        EngineerFindingPolicy.RequireAssignedInspectionAndAudit(
            caseType,
            state,
            workflow.AssignedEngineerId,
            actingEngineerId);
        if (!string.IsNullOrWhiteSpace(workflow.Case.AuditReference))
        {
            throw new InvalidDataException(
                "The case already has an Audit identity without its immutable Engineer finding evidence.");
        }

        var beforeVersion = workflow.Version;
        var auditReference = AuditIdentity.Create(workflow.Case.Reference, request.Assessment);
        var custodyWork = new ExternalWorkItemEntity
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Case = workflow.Case,
            Kind = "create_audit_reference_custody",
            OperationKey = $"audit-custody:{workflow.CaseId:N}",
            State = "pending",
            AttemptCount = 0,
            DueAtUtc = recordedAtUtc,
            AuditFolderCreationToken = CustodyCreationOwner.Create()
        };
        workflow.Case.AuditReference = auditReference;
        workflow.Version = checked(workflow.Version + 1);
        ClearLease(workflow);
        context.ExternalWorkItems.Add(custodyWork);
        context.Set<CaseEngineerFindingEntity>().Add(new()
        {
            CaseId = workflow.CaseId,
            Case = workflow.Case,
            Assessment = ToCode(request.Assessment),
            RecordedByKind = request.Actor.Kind.ToString(),
            RecordedBySubjectId = request.Actor.SubjectId,
            RecordedByRolesJson = RolesJson(request.Actor),
            Reason = reason,
            RecordedAtUtc = recordedAtUtc,
            OperationKey = operationKey,
            RequestHash = requestHash,
            CustodyWorkId = custodyWork.Id,
            CustodyWork = custodyWork
        });
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = workflow.CaseId,
            Workflow = workflow,
            EventType = "engineer_finding_recorded",
            OperationKey = operationKey,
            RequestHash = requestHash,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = RolesJson(request.Actor),
            Reason = reason,
            OccurredAtUtc = recordedAtUtc,
            BeforeVersion = beforeVersion,
            AfterVersion = workflow.Version
        });
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = workflow.CaseId.ToString("D"),
            EventKind = "engineer_finding_recorded",
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = RolesJson(request.Actor),
            OccurredAtUtc = recordedAtUtc,
            Outcome = "Succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = JsonSerializer.Serialize(new
            {
                workflow.CaseId,
                AuditReference = (string?)null,
                WorkflowVersion = beforeVersion
            }),
            AfterJson = JsonSerializer.Serialize(new
            {
                workflow.CaseId,
                AuditReference = auditReference,
                Assessment = ToCode(request.Assessment),
                WorkflowVersion = workflow.Version
            }),
            PolicyVersion = "engineer-finding-v1"
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return MapIdentity(workflow.Case);
        }
        catch (Exception exception) when (IsConcurrencyConflict(exception))
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                await using var verification = await contextFactory.CreateDbContextAsync(cancellationToken);
                var winner = await verification.Set<CaseEngineerFindingEntity>()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken);
                if (winner is not null)
                {
                    if (string.Equals(winner.OperationKey, operationKey, StringComparison.Ordinal)
                        && FixedTimeHashEquals(winner.RequestHash, requestHash))
                    {
                        return await LoadIdentityAsync(verification, request.CaseId, cancellationToken);
                    }
                    throw new CaseOperationConflictException(request.CaseId, request.OperationKey);
                }
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }
            throw new CaseOperationConflictException(request.CaseId, request.OperationKey);
        }
    }

    private static bool IsConcurrencyConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException
                || current is SqlException { Number: 1205 or 2601 or 2627 })
            {
                return true;
            }
        }
        return false;
    }

    private static async Task<CaseIdentity> LoadIdentityAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var entity = await context.Cases
            .AsNoTracking()
            .Include(item => item.Principal)
            .SingleAsync(item => item.Id == caseId, cancellationToken);
        if (string.IsNullOrWhiteSpace(entity.AuditReference))
        {
            throw new InvalidDataException(
                "The retained Engineer finding has no corresponding immutable Audit identity.");
        }

        return MapIdentity(entity);
    }

    private static CaseIdentity MapIdentity(CaseEntity entity) => new(
        entity.Id,
        entity.Principal.Code,
        entity.Year,
        entity.Sequence,
        entity.Reference,
        entity.AuditReference);

    private static void RequireVersion(CaseWorkflowEntity workflow, long expectedVersion) =>
        CaseMutationGuard.RequireVersion(workflow, expectedVersion);

    private static void RequireLease(
        CaseWorkflowEntity workflow,
        ActionActor actor,
        string leaseToken,
        DateTimeOffset nowUtc) =>
        CaseMutationGuard.RequireLease(workflow, actor, leaseToken, nowUtc);

    private static void ClearLease(CaseWorkflowEntity workflow) =>
        CaseMutationGuard.ClearLease(workflow);

    private static string RequestHash(
        RecordEngineerFindingRequest request,
        string operationKey,
        string reason)
    {
        var material = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            request.CaseId,
            request.ExpectedVersion,
            assessment = ToCode(request.Assessment),
            actorKind = request.Actor.Kind.ToString(),
            actorSubjectId = request.Actor.SubjectId,
            actorRoles = request.Actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray(),
            operationKey,
            reason,
            request.EditLeaseToken
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    private static bool FixedTimeHashEquals(string left, string right) =>
        left.Length == 64
        && right.Length == 64
        && left.All(char.IsAsciiHexDigit)
        && right.All(char.IsAsciiHexDigit)
        && CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left),
            Convert.FromHexString(right));

    private static string RolesJson(ActionActor actor) => JsonSerializer.Serialize(
        actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray());

    private static string ToCode(AuditAssessment assessment) => assessment switch
    {
        AuditAssessment.Repairable => "repairable",
        AuditAssessment.TotalLoss => "total_loss",
        _ => throw new ArgumentOutOfRangeException(nameof(assessment))
    };

    private static CaseType ParseCaseType(string value) => value switch
    {
        "inspection" => CaseType.Inspection,
        "audit" => CaseType.Audit,
        "inspection_and_audit" => CaseType.InspectionAndAudit,
        _ => throw new InvalidDataException($"Unknown persisted case type '{value}'.")
    };

    private static CaseLifecycleState ParseLifecycleState(string value) =>
        Enum.TryParse<CaseLifecycleState>(value, ignoreCase: false, out var state)
        && Enum.IsDefined(state)
            ? state
            : throw new InvalidDataException($"Unknown persisted case lifecycle state '{value}'.");
}
