using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Tasks;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Creates a formal Case entered by staff or the Automation Actor without
/// manufacturing an intake receipt, source hash, extraction result, or source
/// document.
/// </summary>
public sealed class EfManualCaseCreationStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider? timeProvider = null,
    IEnumerable<IPrincipalCaseMatchPolicy>? caseMatchPolicies = null,
    VehicleLookupAvailability? vehicleLookupAvailability = null)
    : IManualCaseCreationStore
{
    private static readonly JsonSerializerOptions RolesJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The history reason, naming who entered the Case.</summary>
    private static string CreationReason(ActionActor actor) =>
        actor.Kind == ActorKind.Automation
            ? "Case created directly by the Automation actor."
            : "Case created directly by staff.";

    public async Task<ManualCaseCreationOutcome> CreateAsync(
        CreateManualCaseRequest request,
        CancellationToken cancellationToken)
    {
        var fingerprint = Fingerprint(request);
        return await StoreConcurrencyRetry.ExecuteAsync(
            token => CreateOnceAsync(request, fingerprint, token),
            cancellationToken,
            findCommitted: async token => await FindReplayAsync(request.OperationKey, fingerprint, token) is { } replay
                ? new ManualCaseCreationOutcome(replay, null)
                : null);
    }

    private async Task<ManualCaseCreationOutcome> CreateOnceAsync(
        CreateManualCaseRequest request,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);

        // The Case number is allocated first, so the sequence row is this
        // transaction's first lock; a replay or refusal below releases it.
        var principal = await context.Principals
            .Include(item => item.Organization)
            .SingleOrDefaultAsync(
                item => item.Code == request.PrincipalCode && item.IsActive,
                cancellationToken);
        var now = UtcNow();
        var allocated = principal is null
            ? null
            : await CaseIdentityAllocator.AllocateAsync(
                context, principal, request.CaseType, now, cancellationToken);

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
            // A replay publishes nothing: the first pass already published the
            // lookup it enqueued, and the sweep covers a lost publication.
            return new(replay, null);
        }

        if (principal is null || allocated is null)
        {
            throw new PrincipalUnavailableException(request.PrincipalCode);
        }
        if (request.CaseType == CaseType.Triage)
        {
            return await CreateTriageAsync(
                context, transaction, request, principal, allocated, fingerprint, now, cancellationToken);
        }
        // An image-based Principal's Case takes its inspection mode from the
        // Principal setting, as an accepted intake does (ADR-0018), so its
        // address counts toward the instruction like the autofilled value.
        var imageBased = principal.InspectionMode == PrincipalInspectionModePolicy.ImageBasedAssessmentCode;
        var completeness = new CaseCompleteness(
            InstructionComplete: IsInstructionComplete(imageBased
                ? request.Data with { InspectionAddress = Ext18InspectionAddressPolicy.ImageBasedAssessment }
                : request.Data),
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
            Type = CaseTypeCodes.ToCode(request.CaseType),
            InitialState = ToCode(initialState),
            CustodyState = ToCode(CaseCustodyState.Pending),
            InstructionComplete = completeness.InstructionComplete,
            ImagesComplete = completeness.ImagesComplete,
            CreatedAtUtc = now,
            Version = 0
        };
        context.Cases.Add(caseEntity);
        PrincipalDefaultFeeWriter.Apply(context, caseId, principal, now);

        var snapshot = CaseDataSnapshotFactory.CreateManual(
            caseEntity, evaluation, now);
        CaseDataFieldWriter.ApplyEditableData(context, snapshot, request.Data, request.Actor, now);
        // The accepted Principal is the creator's confirmation, as on a
        // receipt-backed staff acceptance (FRD-02): without it the match index
        // projected no row and the Case export named no Work Provider.
        CaseDataSnapshotFactory.AddStaffAllocatedPrincipal(
            snapshot,
            request.Actor,
            principal.Code,
            evaluation.PolicyKey,
            evaluation.PolicyVersion,
            now);
        if (imageBased)
        {
            CaseDataSnapshotFactory.AddPrincipalInspectionMode(
                snapshot,
                principal.Code,
                request.Actor.SubjectId,
                request.Actor.SubjectId,
                now);
            context.CaseHistory.Add(new()
            {
                Id = Guid.NewGuid(),
                Case = caseEntity,
                CaseId = caseId,
                EventType = "principal_inspection_mode_applied",
                Actor = request.Actor.SubjectId,
                Reason = "Principal setting: inspection address autofilled as Image Based Assessment",
                OccurredAtUtc = now,
                OperationKey = $"principal-mode:{caseId:N}",
                BeforeVersion = null,
                AfterVersion = 0
            });
        }
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
            StateEnteredAtUtc = now,
            Version = 0
        };
        context.CaseWorkflows.Add(workflow);
        if (initialState != CaseInitialState.Review)
        {
            context.CaseDueWork.Add(new()
            {
                CaseId = caseId,
                Workflow = workflow,
                MissingMaterialReason = "Details are incomplete",
                DueBy = request.Data.InspectionDeadline,
                State = CaseDueWorkState.Scheduled.ToString(),
                NextChaseAtUtc = CaseChaseSchedule.NextChaseAt(now, configuration.ChaseIntervalDays),
                Version = 0
            });
        }

        await ClaimSourceChoice.RequireActiveAsync(context, request.Data.ClaimSourceId, cancellationToken);

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

        // The staff-keyed registration is confirmed above, so the DVLA/MOT
        // lookup is due now rather than on the Worker's next
        // reconciliation sweep (FRD-06 D34). Enqueued in this transaction so
        // it cannot outlive a rolled-back creation; published by the use case
        // after the commit. Looked-up values remain suggestions.
        Guid? vehicleLookupWorkId = null;
        if (vehicleLookupAvailability?.RequestsEnabled == true)
        {
            var registration = EfVehicleWorkflowStore.CurrentRegistration(
                snapshot.Fields
                    .Where(field => field.FieldName == CaseDataFieldNames.VehicleRegistration)
                    .Select(field => (field.ValueKind, field.Value)));
            if (registration is not null)
            {
                vehicleLookupWorkId = EfVehicleWorkflowStore.EnqueueForCase(
                    context,
                    caseId,
                    caseEntity,
                    registration,
                    workflow.Version,
                    now);
            }
        }

        // The Case's one automatic Glass's valuation is requested here too,
        // beside its vehicle lookup, while the Administrator's switch is on
        // (R545, R546). The Web host runs it once the registration and mileage
        // are accepted.
        await EfAutomaticGuideValuationStore.EnqueueForCaseAsync(
            context, caseId, caseEntity, now, cancellationToken);

        CaseMutationHistory.Add(
            context,
            workflow,
            request.Actor,
            request.OperationKey,
            CreationReason(request.Actor),
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
        return new(Identity(caseEntity), vehicleLookupWorkId);
    }

    /// <summary>
    /// A Triage Case entered by staff: its Principal and registration only. It
    /// has no Case workflow, data snapshot, match index entry, due work or
    /// vehicle lookup; it gets standard Case custody. With no workflow to write
    /// the Case history triple, the replay row is written directly.
    /// </summary>
    private static async Task<ManualCaseCreationOutcome> CreateTriageAsync(
        PegasusDbContext context,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CreateManualCaseRequest request,
        PrincipalEntity principal,
        AllocatedCaseIdentity allocated,
        string fingerprint,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var reason = CreationReason(request.Actor);
        var registration = request.Data.VehicleRegistration
            ?? throw new InvalidOperationException("A manual case needs Vehicle registration.");
        var triage = TriageCaseRows.Add(
            context,
            principal,
            allocated,
            null,
            registration,
            request.Actor,
            request.OperationKey,
            reason,
            fingerprint,
            now);
        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = "case",
            AggregateId = triage.CaseId.ToString("D"),
            EventKind = "manual_case_created",
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                request.Actor.Roles.OrderBy(role => role),
                RolesJsonOptions),
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = request.OperationKey,
            Reason = reason,
            BeforeJson = "null",
            AfterJson = JsonSerializer.Serialize(new
            {
                CommandFingerprint = fingerprint,
                Identity = Identity(triage.Case)
            }),
            PolicyVersion = $"{CaseDataPolicy.EditPolicyKey}/v{CaseDataPolicy.EditPolicyVersion}"
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(Identity(triage.Case), null);
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
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private DateTimeOffset UtcNow()
    {
        var now = timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow();
        return now.Offset == TimeSpan.Zero ? now : now.ToUniversalTime();
    }

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

    private sealed record ManualCreationReplay(string CommandFingerprint, CaseIdentity Identity);
}
