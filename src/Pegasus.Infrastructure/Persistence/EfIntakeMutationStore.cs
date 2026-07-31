using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfIntakeMutationStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IIntakeMutationStore
{
    public Task<IntakeReceipt> ResolveAsync(
        ResolveIntakeRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            request.ReceiptId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            "intake_resolved",
            RequestHash("intake_resolved", request),
            expectedCaseId: null,
            expectedCaseVersion: null,
            editLeaseToken: null,
            (context, receipt, _, _) =>
            {
                if (request.Kind == IntakeResolutionKind.Block)
                {
                    receipt.Decision = EfIntakeReceiptStore.ToCode(IntakeDecision.BlockedIntake);
                    receipt.DecisionReason = request.Reason.Trim();
                    receipt.FailureCode = "blocked_intake";
                    receipt.FailureReason = request.Reason.Trim();
                    return Task.CompletedTask;
                }

                var correctedDraft = request.CorrectedDraft
                    ?? throw new ArgumentException(
                        "A corrected draft is required for a draft correction.",
                        nameof(request));
                ApplyResolvedDraft(receipt, correctedDraft);
                ApplyDraftToReviewFields(receipt, correctedDraft);
                var isComplete = HasRequiredInstructionFields(correctedDraft);
                receipt.Decision = EfIntakeReceiptStore.ToCode(
                    isComplete ? IntakeDecision.DraftReady : IntakeDecision.BlockedIntake);
                receipt.DecisionReason = isComplete
                    ? "The intake correction produced a reviewable instruction draft."
                    : "The intake correction was retained but required instruction fields remain unresolved.";
                receipt.FailureCode = isComplete ? null : "blocked_intake";
                receipt.FailureReason = isComplete
                    ? null
                    : "Required instruction fields remain unresolved.";
                return Task.CompletedTask;
            },
            occurredAtUtc,
            cancellationToken);

    public Task<IntakeReceipt> ScheduleReevaluationAsync(
        ReevaluateIntakeRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            request.ReceiptId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            "intake_reevaluation_queued",
            RequestHash("intake_reevaluation_queued", request),
            expectedCaseId: null,
            expectedCaseVersion: null,
            editLeaseToken: null,
            async (context, receipt, _, token) =>
            {
                var stagedReceiptId = await context.IntakeEvaluations
                    .Where(item => item.ProcessedReceiptId == receipt.Id)
                    .OrderByDescending(item => item.Revision)
                    .Select(item => (Guid?)item.StagedReceiptId)
                    .FirstOrDefaultAsync(token)
                    ?? throw new InvalidDataException(
                        "The intake receipt does not have a retained evaluation source.");
                var workItem = await context.IntakeWorkItems.SingleOrDefaultAsync(
                    item => item.StagedReceiptId == stagedReceiptId,
                    token)
                    ?? throw new InvalidDataException(
                        "The intake receipt does not have durable evaluation work.");
                if (workItem.State == "processing"
                    && workItem.LeaseExpiresAtUtc is { } leaseExpiresAtUtc
                    && leaseExpiresAtUtc > occurredAtUtc)
                {
                    throw new InvalidOperationException(
                        "The intake receipt is already being evaluated.");
                }

                workItem.State = "pending";
                workItem.DueAtUtc = occurredAtUtc;
                workItem.LeaseToken = null;
                workItem.LeaseExpiresAtUtc = null;
                workItem.FailureCode = null;
                receipt.Decision = EfIntakeReceiptStore.ToCode(IntakeDecision.BlockedIntake);
                receipt.DecisionReason = "A policy re-evaluation of the retained source is queued.";
                receipt.FailureCode = "reevaluation_pending";
                receipt.FailureReason = null;
            },
            occurredAtUtc,
            cancellationToken);

    public async Task LinkAsync(
        LinkIntakeRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        _ = await ExecuteAsync(
            request.ReceiptId,
            request.ExpectedIntakeVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            "intake_case_linked",
            RequestHash("intake_case_linked", request),
            request.CaseId,
            request.ExpectedCaseVersion,
            request.EditLeaseToken,
            (context, receipt, @case, _) =>
            {
                if (@case is null)
                {
                    throw new InvalidOperationException("A case is required for an intake association.");
                }

                if (receipt.ManualAssociation is { IsActive: true })
                {
                    throw new IntakeAssociationConflictException(
                        "The intake receipt already has an active manual case association.");
                }

                if (receipt.ManualAssociation is null)
                {
                    receipt.ManualAssociation = new IntakeManualAssociationEntity
                    {
                        IntakeReceiptId = receipt.Id,
                        IntakeReceipt = receipt,
                        CaseId = @case.Id,
                        Case = @case,
                        IsActive = true,
                        Version = 0,
                        LinkedAtUtc = occurredAtUtc,
                        ActorKind = request.Actor.Kind.ToString(),
                        ActorSubjectId = request.Actor.SubjectId,
                        ActorRolesJson = RolesJson(request.Actor),
                        Reason = request.Reason.Trim(),
                        LastOperationKey = request.OperationKey.Trim()
                    };
                }
                else
                {
                    var association = receipt.ManualAssociation;
                    association.CaseId = @case.Id;
                    association.Case = @case;
                    association.IsActive = true;
                    association.Version++;
                    association.LinkedAtUtc = occurredAtUtc;
                    association.UnlinkedAtUtc = null;
                    association.ActorKind = request.Actor.Kind.ToString();
                    association.ActorSubjectId = request.Actor.SubjectId;
                    association.ActorRolesJson = RolesJson(request.Actor);
                    association.Reason = request.Reason.Trim();
                    association.LastOperationKey = request.OperationKey.Trim();
                }

                return Task.CompletedTask;
            },
            occurredAtUtc,
            cancellationToken);
    }

    public async Task ReverseLinkAsync(
        ReverseIntakeLinkRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        _ = await ExecuteAsync(
            request.ReceiptId,
            request.ExpectedIntakeVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            "intake_case_link_reversed",
            RequestHash("intake_case_link_reversed", request),
            request.CaseId,
            request.ExpectedCaseVersion,
            request.EditLeaseToken,
            (_, receipt, _, _) =>
            {
                var association = receipt.ManualAssociation;
                if (association is null
                    || !association.IsActive
                    || association.CaseId != request.CaseId)
                {
                    throw new IntakeAssociationConflictException(
                        "The requested active intake-to-case association does not exist.");
                }

                association.IsActive = false;
                association.Version++;
                association.UnlinkedAtUtc = occurredAtUtc;
                association.ActorKind = request.Actor.Kind.ToString();
                association.ActorSubjectId = request.Actor.SubjectId;
                association.ActorRolesJson = RolesJson(request.Actor);
                association.Reason = request.Reason.Trim();
                association.LastOperationKey = request.OperationKey.Trim();
                return Task.CompletedTask;
            },
            occurredAtUtc,
            cancellationToken);
    }

    private async Task<IntakeReceipt> ExecuteAsync(
        Guid receiptId,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        string reason,
        string eventType,
        string requestHash,
        Guid? expectedCaseId,
        long? expectedCaseVersion,
        string? editLeaseToken,
        Func<PegasusDbContext, IntakeReceiptEntity, CaseEntity?, CancellationToken, Task> mutate,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        operationKey = operationKey.Trim();
        reason = reason.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.IntakeMutationHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == operationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.IntakeReceiptId != receiptId
                || !string.Equals(replay.EventType, eventType, StringComparison.Ordinal)
                || !FixedTimeHashEquals(replay.RequestFingerprint, requestHash))
            {
                throw new IntakeOperationConflictException();
            }

            var replayed = await LoadReceiptAsync(context, receiptId, cancellationToken)
                ?? throw new InvalidDataException("The replayed intake receipt no longer exists.");
            var replayedAcceptedCaseId = await AcceptedCaseIdAsync(context, receiptId, cancellationToken);
            return EfIntakeReceiptStore.Map(replayed, false, replayedAcceptedCaseId);
        }

        var receipt = await LoadReceiptAsync(context, receiptId, cancellationToken)
            ?? throw new KeyNotFoundException("The intake receipt does not exist.");
        if (receipt.Version != expectedVersion)
        {
            throw new IntakeVersionConflictException();
        }

        var acceptedCaseId = await AcceptedCaseIdAsync(context, receiptId, cancellationToken);
        var isAssociationMutation = eventType is
            "intake_case_linked" or "intake_case_link_reversed";
        if (acceptedCaseId is not null && !isAssociationMutation)
        {
            throw new InvalidOperationException(
                "An accepted intake receipt cannot be changed through the pre-case intake workflow.");
        }
        if (eventType == "intake_case_linked"
            && acceptedCaseId is not null
            && receipt.ManualAssociation is null)
        {
            throw new IntakeAssociationConflictException(
                "The accepted intake origin association must be reversed before relinking.");
        }

        CaseEntity? @case = null;
        CaseWorkflowEntity? caseWorkflow = null;
        long? beforeCaseVersion = null;
        if (expectedCaseId is { } caseId)
        {
            caseWorkflow = await context.CaseWorkflows
                .Include(item => item.Case)
                .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
                ?? throw new KeyNotFoundException("The case does not exist.");
            CaseMutationGuard.Require(
                caseWorkflow,
                actor,
                expectedCaseVersion
                    ?? throw new InvalidOperationException("An expected case version is required."),
                editLeaseToken
                    ?? throw new InvalidOperationException("A case edit lease token is required."),
                occurredAtUtc);
            @case = caseWorkflow.Case;
            beforeCaseVersion = caseWorkflow.Version;
        }


        var beforeVersion = receipt.Version;
        var beforeJson = Snapshot(receipt);
        await mutate(context, receipt, @case, cancellationToken);
        receipt.Version++;
        if (caseWorkflow is not null)
        {
            CaseMutationGuard.Complete(caseWorkflow);
        }
        if (caseWorkflow is not null && beforeCaseVersion is not null)
        {
            context.CaseWorkflowEvents.Add(new()
            {
                Id = Guid.NewGuid(),
                CaseId = caseWorkflow.CaseId,
                Workflow = caseWorkflow,
                EventType = eventType,
                OperationKey = operationKey,
                RequestHash = requestHash,
                ActorKind = actor.Kind.ToString(),
                ActorSubjectId = actor.SubjectId,
                ActorRolesJson = RolesJson(actor),
                Reason = reason,
                OccurredAtUtc = occurredAtUtc,
                BeforeVersion = beforeCaseVersion.Value,
                AfterVersion = caseWorkflow.Version,
                ResultJson = Snapshot(receipt)
            });
        }

        context.IntakeMutationHistory.Add(new IntakeMutationHistoryEntity
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            CaseId = @case?.Id,
            Case = @case,
            EventType = eventType,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = RolesJson(actor),
            Reason = reason,
            OperationKey = operationKey,
            RequestFingerprint = requestHash,
            OccurredAtUtc = occurredAtUtc,
            ExpectedIntakeVersion = expectedVersion,
            BeforeIntakeVersion = beforeVersion,
            AfterIntakeVersion = receipt.Version,
            ExpectedCaseVersion = expectedCaseVersion,
            BeforeCaseVersion = beforeCaseVersion,
            AfterCaseVersion = caseWorkflow?.Version,
            BeforeJson = beforeJson,
            AfterJson = Snapshot(receipt)
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new IntakeVersionConflictException();
        }

        return EfIntakeReceiptStore.Map(receipt, false, acceptedCaseId);
    }

    private static Task<IntakeReceiptEntity?> LoadReceiptAsync(
        PegasusDbContext context,
        Guid receiptId,
        CancellationToken cancellationToken) =>
        context.IntakeReceipts
            .Include(item => item.Assets)
            .Include(item => item.InstructionDraft)
            .Include(item => item.MailRouteDecision)
            .Include(item => item.ManualAssociation)
            .SingleOrDefaultAsync(item => item.Id == receiptId, cancellationToken);

    private static Task<Guid?> AcceptedCaseIdAsync(
        PegasusDbContext context,
        Guid receiptId,
        CancellationToken cancellationToken) =>
        context.CaseIntakeLinks
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == receiptId)
            .Select(item => (Guid?)item.CaseId)
            .SingleOrDefaultAsync(cancellationToken);

    private static void ApplyResolvedDraft(
        IntakeReceiptEntity receipt,
        InstructionDraft draft)
    {
        var entity = receipt.InstructionDraft ?? new InstructionDraftEntity
        {
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt
        };
        entity.SuggestedPrincipalCode = draft.SuggestedPrincipalCode;
        entity.ClaimantName = draft.ClaimantName;
        entity.ClaimNumber = draft.ClaimNumber;
        entity.VehicleRegistration = draft.VehicleRegistration;
        entity.VehicleMake = draft.VehicleMake;
        entity.VehicleModel = draft.VehicleModel;
        entity.VehicleMileage = draft.VehicleMileage;
        entity.AccidentCircumstances = draft.AccidentCircumstances;
        entity.DateOfIncident = draft.DateOfIncident;
        entity.InstructionDate = draft.InstructionDate;
        entity.InspectionDate = draft.InspectionDate;
        entity.InspectionAddress = draft.InspectionAddress;
        receipt.InstructionDraft = entity;
    }
    private static void ApplyDraftToReviewFields(
        IntakeReceiptEntity receipt,
        InstructionDraft draft)
    {
        var fields = EfIntakeReceiptStore.DeserializeFields(receipt.FieldsJson)
            .Select(field => field with
            {
                SuggestedValue = field.Name switch
                {
                    "Claimant name" => draft.ClaimantName,
                    "Claim number" => draft.ClaimNumber,
                    "Vehicle registration" => draft.VehicleRegistration,
                    "Vehicle make" => draft.VehicleMake,
                    "Vehicle model" => draft.VehicleModel,
                    "Vehicle mileage" => draft.VehicleMileage?.ToString(CultureInfo.InvariantCulture),
                    "Accident circumstances" => draft.AccidentCircumstances,
                    "Date of incident" => draft.DateOfIncident?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    "Instruction date" => draft.InstructionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    "Inspection address" => draft.InspectionAddress,
                    "Inspection date" => draft.InspectionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    _ => field.SuggestedValue
                }
            })
            .ToArray();
        receipt.FieldsJson = EfIntakeReceiptStore.SerializeFields(fields);
    }

    private static bool HasRequiredInstructionFields(InstructionDraft draft) =>
        !string.IsNullOrWhiteSpace(draft.ClaimantName)
        && !string.IsNullOrWhiteSpace(draft.ClaimNumber)
        && !string.IsNullOrWhiteSpace(draft.VehicleRegistration)
        && !string.IsNullOrWhiteSpace(draft.VehicleMake)
        && !string.IsNullOrWhiteSpace(draft.VehicleModel)
        && draft.VehicleMileage is not null
        && !string.IsNullOrWhiteSpace(draft.AccidentCircumstances)
        && draft.DateOfIncident is not null
        && draft.InstructionDate is not null
        && !string.IsNullOrWhiteSpace(draft.InspectionAddress);

    private static string Snapshot(IntakeReceiptEntity receipt) => JsonSerializer.Serialize(new
    {
        receipt.Id,
        receipt.Decision,
        receipt.DecisionReason,
        receipt.Version,
        Fields = EfIntakeReceiptStore.DeserializeFields(receipt.FieldsJson),
        InstructionDraft = receipt.InstructionDraft is null
            ? null
            : new
            {
                receipt.InstructionDraft.SuggestedPrincipalCode,
                receipt.InstructionDraft.ClaimantName,
                receipt.InstructionDraft.ClaimNumber,
                receipt.InstructionDraft.VehicleRegistration,
                receipt.InstructionDraft.VehicleMake,
                receipt.InstructionDraft.VehicleModel,
                receipt.InstructionDraft.VehicleMileage,
                receipt.InstructionDraft.AccidentCircumstances,
                receipt.InstructionDraft.DateOfIncident,
                receipt.InstructionDraft.InstructionDate,
                receipt.InstructionDraft.InspectionAddress,
                receipt.InstructionDraft.InspectionDate
            },
        Association = receipt.ManualAssociation is null
            ? null
            : new
            {
                receipt.ManualAssociation.CaseId,
                receipt.ManualAssociation.IsActive,
                receipt.ManualAssociation.Version,
                receipt.ManualAssociation.LinkedAtUtc,
                receipt.ManualAssociation.UnlinkedAtUtc
            }
    });

    private static string RolesJson(ActionActor actor) =>
        JsonSerializer.Serialize(actor.Roles.OrderBy(role => role));

    private static string RequestHash(string eventType, ResolveIntakeRequest request) =>
        Hash(JsonSerializer.Serialize(new
        {
            EventType = eventType,
            request.ReceiptId,
            request.ExpectedVersion,
            request.Kind,
            request.CorrectedDraft,
            Actor = ActorMaterial(request.Actor),
            request.OperationKey,
            request.Reason
        }));

    private static string RequestHash(string eventType, ReevaluateIntakeRequest request) =>
        Hash(JsonSerializer.Serialize(new
        {
            EventType = eventType,
            request.ReceiptId,
            request.ExpectedVersion,
            Actor = ActorMaterial(request.Actor),
            request.OperationKey,
            request.Reason
        }));

    private static string RequestHash(string eventType, LinkIntakeRequest request) =>
        Hash(JsonSerializer.Serialize(new
        {
            EventType = eventType,
            request.ReceiptId,
            request.CaseId,
            request.ExpectedIntakeVersion,
            request.ExpectedCaseVersion,
            request.EditLeaseToken,
            Actor = ActorMaterial(request.Actor),
            request.OperationKey,
            request.Reason
        }));

    private static string RequestHash(string eventType, ReverseIntakeLinkRequest request) =>
        Hash(JsonSerializer.Serialize(new
        {
            EventType = eventType,
            request.ReceiptId,
            request.CaseId,
            request.ExpectedIntakeVersion,
            request.ExpectedCaseVersion,
            request.EditLeaseToken,
            Actor = ActorMaterial(request.Actor),
            request.OperationKey,
            request.Reason
        }));

    private static object ActorMaterial(ActionActor actor) => new
    {
        Kind = actor.Kind.ToString(),
        actor.SubjectId,
        Roles = actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray()
    };

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool FixedTimeHashEquals(string left, string right) =>
        left.Length == 64
        && right.Length == 64
        && CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left),
            Convert.FromHexString(right));
}
