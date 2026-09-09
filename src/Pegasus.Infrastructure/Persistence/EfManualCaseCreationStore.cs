using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Creates a formal Case entered by staff without manufacturing an intake
/// receipt, source hash, extraction result, or source document.
/// </summary>
public sealed class EfManualCaseCreationStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider? timeProvider = null,
    IEnumerable<IProviderCaseMatchPolicy>? caseMatchPolicies = null)
    : IManualCaseCreationStore
{
    public async Task<CaseIdentity> CreateAsync(
        CreateManualCaseRequest request,
        CancellationToken cancellationToken)
    {
        var fingerprint = Fingerprint(request);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await CreateOnceAsync(request, fingerprint, cancellationToken);
            }
            catch (Exception exception) when (IsRetryable(exception) && attempt < 3)
            {
                var replay = await FindReplayAsync(request.OperationKey, fingerprint, cancellationToken);
                if (replay is not null)
                {
                    return replay;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        return await CreateOnceAsync(request, fingerprint, cancellationToken);
    }

    private async Task<CaseIdentity> CreateOnceAsync(
        CreateManualCaseRequest request,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existing = await context.ActionHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.AggregateType == "case"
                    && item.EventKind == "manual_case_created"
                    && item.CorrelationId == request.OperationKey,
                cancellationToken);
        if (existing is not null)
        {
            var replay = ReadReplay(existing, fingerprint);
            if (replay is null)
            {
                throw new InvalidOperationException(
                    "This manual case request was already used with different details.");
            }
            return replay;
        }

        var principal = await context.Principals
            .Include(item => item.Organization)
            .SingleOrDefaultAsync(
                item => item.Code == request.PrincipalCode && item.IsActive,
                cancellationToken)
            ?? throw new PrincipalUnavailableException(request.PrincipalCode);
        var now = UtcNow();
        var allocated = await CaseIdentityAllocator.AllocateAsync(
            context, principal, now, cancellationToken);
        var completeness = new CaseCompleteness(
            InstructionComplete: IsInstructionComplete(request.Data),
            ImagesComplete: false);
        var configuration = await EfWorkflowConfigurationStore.ReadAsync(context, cancellationToken);
        var evaluation = CaseCompletenessPolicy.Evaluate(completeness, configuration);
        var initialState = evaluation.SatisfiesPolicy
            ? CaseInitialState.Review
            : CaseInitialState.NotReady;
        var caseId = Guid.NewGuid();
        var caseEntity = new CaseEntity
        {
            Id = caseId,
            PrincipalId = principal.Id,
            Principal = principal,
            SequenceLineageId = principal.SequenceLineageId,
            Year = allocated.Year,
            Sequence = allocated.Sequence,
            Reference = allocated.Reference,
            Type = ToCode(request.CaseType),
            InitialState = ToCode(initialState),
            CustodyState = ToCode(CaseCustodyState.Pending),
            InstructionComplete = completeness.InstructionComplete,
            ImagesComplete = completeness.ImagesComplete,
            CreatedAtUtc = now,
            Version = 0
        };
        context.Cases.Add(caseEntity);

        var snapshot = CaseDataSnapshotFactory.CreateManual(
            caseEntity, evaluation, now);
        CaseDataFieldWriter.ApplyEditableData(context, snapshot, request.Data, request.Actor, now);
        context.CaseDataSnapshots.Add(snapshot);
        CaseMatchIndexProjector.Apply(
            context,
            existing: null,
            CaseMatchIndexProjector.Project(
                caseEntity, snapshot.Fields, caseMatchPolicies ?? [], now));

        var workflow = new CaseWorkflowEntity
        {
            CaseId = caseId,
            Case = caseEntity,
            State = CaseInitialWorkflowState.From(initialState).ToString(),
            Version = 0
        };
        context.CaseWorkflows.Add(workflow);
        if (initialState == CaseInitialState.Review)
        {
            AutomaticEvaReviewSubmissionScheduling.AddForReviewTransition(
                context, workflow, workflow.Version, now);
        }
        else
        {
            context.CaseDueWork.Add(new()
            {
                CaseId = caseId,
                Workflow = workflow,
                MissingMaterialReason = "Details are incomplete",
                DueBy = request.Data.InspectionDeadline,
                State = CaseDueWorkState.Scheduled.ToString(),
                NextChaseAtUtc = CaseChaseSchedule.FirstChaseAt(now, configuration.ChaseIntervalDays),
                Version = 0
            });
        }

        await CaseGuidance.ApplyCreationAsync(
            context,
            workflow,
            request.Data.ClaimSourceId,
            now,
            fingerprint,
            cancellationToken);

        var custodyWorkId = Guid.NewGuid();
        context.ExternalWorkItems.Add(new()
        {
            Id = custodyWorkId,
            Case = caseEntity,
            CaseId = caseId,
            Kind = ExternalWorkKinds.CreateCaseCustody,
            OperationKey = $"manual-custody:{caseId:N}",
            State = "pending",
            AttemptCount = 0,
            DueAtUtc = now,
            CaseRootCreationToken = CustodyCreationOwner.Create()
        });
        CaseMutationHistory.Add(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            "Case created directly by staff.",
            "manual_case_created",
            fingerprint,
            0,
            0,
            "null",
            JsonSerializer.Serialize(new
            {
                CommandFingerprint = fingerprint,
                Identity = Identity(caseEntity),
                Completeness = completeness
            }),
            $"{CaseDataPolicy.EditPolicyKey}/v{CaseDataPolicy.EditPolicyVersion}",
            now);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Identity(caseEntity);
    }

    private async Task<CaseIdentity?> FindReplayAsync(
        string operationKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.ActionHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.AggregateType == "case"
                    && item.EventKind == "manual_case_created"
                    && item.CorrelationId == operationKey,
                cancellationToken);
        if (existing is null)
        {
            return null;
        }
        return ReadReplay(existing, fingerprint) ?? throw new InvalidOperationException(
            "This manual case request was already used with different details.");
    }

    private static CaseIdentity? ReadReplay(ActionHistoryEntity existing, string fingerprint)
    {
        try
        {
            var replay = JsonSerializer.Deserialize<ManualCreationReplay>(existing.AfterJson ?? "");
            return replay is not null
                && string.Equals(replay.CommandFingerprint, fingerprint, StringComparison.Ordinal)
                ? replay.Identity
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsInstructionComplete(CaseEditableData data) =>
        InstructionDraftCompleteness.IsComplete(new(
            null,
            data.ClaimantName,
            data.ClaimNumber,
            data.VehicleRegistration,
            data.VehicleMake,
            data.VehicleModel,
            data.VehicleMileage,
            data.AccidentCircumstances,
            data.IncidentDate,
            data.InstructionDate,
            data.InspectionAddress,
            data.InspectionDate));

    private static CaseIdentity Identity(CaseEntity entity) => new(
        entity.Id,
        entity.Principal.Code,
        entity.Year,
        entity.Sequence,
        entity.Reference,
        entity.AuditReference);

    private static string Fingerprint(CreateManualCaseRequest request)
    {
        var material = JsonSerializer.Serialize(new
        {
            request.OperationKey,
            ActorKind = request.Actor.Kind.ToString(),
            request.Actor.SubjectId,
            Roles = request.Actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray(),
            request.PrincipalCode,
            CaseType = request.CaseType.ToString(),
            request.Data
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    private DateTimeOffset UtcNow()
    {
        var now = timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }

    private static string ToCode(CaseType value) => value switch
    {
        CaseType.Inspection => "inspection",
        CaseType.InspectionAndAudit => "inspection_and_audit",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string ToCode(CaseInitialState value) => value switch
    {
        CaseInitialState.NotReady => "not_ready",
        CaseInitialState.Review => "review",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string ToCode(CaseCustodyState value) => value switch
    {
        CaseCustodyState.Pending => "pending",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static bool IsRetryable(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        SqlException { Number: 1205 or 2601 or 2627 } => true,
        _ when exception.InnerException is not null => IsRetryable(exception.InnerException),
        _ => false
    };

    private sealed record ManualCreationReplay(string CommandFingerprint, CaseIdentity Identity);
}
