using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

internal sealed partial class EfIntakeAllocationStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    ILogger<EfIntakeAllocationStore> logger) : IIntakeAllocationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IntakeAllocationAttempt?> GetCurrentAsync(
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntakeAllocationAttempts
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == receiptId)
            .OrderByDescending(item => item.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<BeginIntakeAllocationResult> BeginAsync(
        BeginIntakeAllocationAttempt request,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        // Two parallel Begins for one receipt each take Serializable
        // key-range shared locks on the reads below and then insert — the
        // textbook check-then-insert deadlock (CASE-005). An exclusive
        // transaction-scoped application lock per receipt makes them queue
        // instead: the second transaction then sees the first's committed
        // attempt and resolves through the existing replay/suppression
        // branches, which is the designed convergence.
        var lockResource = $"intake-allocation:{request.Command.ReceiptId:N}";
        await context.Database.ExecuteSqlAsync(
            $"""
             DECLARE @lockResult int;
             EXEC @lockResult = sp_getapplock
                 @Resource = {lockResource},
                 @LockMode = 'Exclusive',
                 @LockOwner = 'Transaction',
                 @LockTimeout = 15000;
             IF @lockResult < 0 THROW 51205, 'The intake allocation lock was not granted.', 1;
             """,
            cancellationToken);
        var existing = await context.IntakeAllocationAttempts
            .SingleOrDefaultAsync(
                item => item.OperationKey == request.OperationKey,
                cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.CommandHash, request.CommandHash, StringComparison.Ordinal))
            {
                throw new IntakeAllocationOperationConflictException();
            }

            return new(Map(existing), IsReplay: true, IsSuppressed: false);
        }

        var current = await context.IntakeAllocationAttempts
            .Where(item => item.IntakeReceiptId == request.Command.ReceiptId)
            .OrderByDescending(item => item.AttemptNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (request.Kind == IntakeAllocationAttemptKind.Automatic && current is not null)
        {
            return new(Map(current), IsReplay: true, IsSuppressed: true);
        }
        if (request.Kind == IntakeAllocationAttemptKind.StaffCreate && current is not null)
        {
            return new(Map(current), IsReplay: true, IsSuppressed: true);
        }
        if (request.Kind == IntakeAllocationAttemptKind.StaffRetry
            && current is not null
            && current.Status is "pending" or "succeeded")
        {
            return new(Map(current), IsReplay: true, IsSuppressed: true);
        }
        if (request.Kind == IntakeAllocationAttemptKind.StaffRetry
            && (current is null
                || current.Id != request.ExpectedCurrentAttemptId
                || current.Status != ToCode(IntakeAllocationAttemptStatus.Failed)
                || current.RecoveryDisposition is not ("retry_after_correction" or "reload_then_retry")))
        {
            throw new IntakeAllocationConcurrencyException();
        }

        var receiptVersion = await context.IntakeReceipts
            .Where(item => item.Id == request.Command.ReceiptId)
            .Select(item => (long?)item.Version)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("The intake receipt was not found.");
        if (receiptVersion != request.Command.ExpectedReceiptVersion)
        {
            throw new IntakeAllocationConcurrencyException();
        }

        var entity = Map(request, (current?.AttemptNumber ?? 0) + 1);
        context.IntakeAllocationAttempts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(Map(entity), IsReplay: false, IsSuppressed: false);
    }

    internal static async Task<IntakeAllocationAttempt> CompleteSuccessInTransactionAsync(
        PegasusDbContext context,
        Guid attemptId,
        Guid receiptId,
        string operationKey,
        long expectedReceiptVersion,
        string caseType,
        string principalCode,
        Guid? standaloneAuditEvidenceId,
        CaseAcceptanceOutcome outcome,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        var entity = await context.IntakeAllocationAttempts.SingleAsync(
            item => item.Id == attemptId,
            cancellationToken);
        if (entity.Status == ToCode(IntakeAllocationAttemptStatus.Succeeded))
        {
            if (entity.IntakeReceiptId != receiptId
                || !string.Equals(entity.OperationKey, operationKey, StringComparison.Ordinal)
                || entity.CaseId != outcome.Identity.CaseId)
            {
                throw new IntakeAllocationConcurrencyException();
            }
            return Map(entity);
        }
        if (entity.Status != ToCode(IntakeAllocationAttemptStatus.Pending)
            || entity.IntakeReceiptId != receiptId
            || !string.Equals(entity.OperationKey, operationKey, StringComparison.Ordinal)
            || entity.ExpectedReceiptVersion != expectedReceiptVersion
            || !string.Equals(entity.CaseType, caseType, StringComparison.Ordinal)
            || !string.Equals(entity.PrincipalCode, principalCode, StringComparison.Ordinal)
            || entity.StandaloneAuditEvidenceId != standaloneAuditEvidenceId)
        {
            throw new IntakeAllocationConcurrencyException();
        }

        entity.Status = ToCode(IntakeAllocationAttemptStatus.Succeeded);
        entity.CompletedAtUtc = completedAtUtc;
        entity.CaseId = outcome.Identity.CaseId;
        entity.CaseReference = outcome.Identity.Reference;
        entity.AuditReference = outcome.Identity.AuditReference;
        AddOutcomeEvent(context, entity, "intake_allocation_succeeded", completedAtUtc);
        return Map(entity);
    }

    public async Task<IntakeAllocationAttempt> CompleteFailureAsync(
        Guid attemptId,
        IntakeAllocationFailureKind failureKind,
        IntakeAllocationRecoveryDisposition recoveryDisposition,
        string safeReason,
        DateTimeOffset completedAtUtc,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntakeAllocationAttempts.SingleAsync(
            item => item.Id == attemptId,
            cancellationToken);
        if (entity.Status != ToCode(IntakeAllocationAttemptStatus.Pending))
        {
            return Map(entity);
        }

        entity.Status = ToCode(IntakeAllocationAttemptStatus.Failed);
        entity.CompletedAtUtc = completedAtUtc;
        entity.FailureKind = ToCode(failureKind);
        entity.RecoveryDisposition = ToCode(recoveryDisposition);
        entity.SafeReason = safeReason;
        AddOutcomeEvent(context, entity, "intake_allocation_failed", completedAtUtc);
        await context.SaveChangesAsync(cancellationToken);
        LogAllocationFailure(
            logger,
            entity.IntakeReceiptId,
            entity.CaseType,
            entity.FailureKind,
            exception);
        return Map(entity);
    }

    public async Task CancelPendingAsync(Guid attemptId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntakeAllocationAttempts.SingleOrDefaultAsync(
            item => item.Id == attemptId,
            cancellationToken);
        if (entity is null || entity.Status != ToCode(IntakeAllocationAttemptStatus.Pending))
        {
            return;
        }

        context.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static IntakeAllocationAttemptEntity Map(
        BeginIntakeAllocationAttempt request,
        long attemptNumber) => new()
    {
        Id = Guid.NewGuid(),
        IntakeReceiptId = request.Command.ReceiptId,
        AttemptNumber = attemptNumber,
        Kind = ToCode(request.Kind),
        Status = ToCode(IntakeAllocationAttemptStatus.Pending),
        ExpectedReceiptVersion = request.Command.ExpectedReceiptVersion,
        CaseType = request.Command.CaseType is null ? null : ToCode(request.Command.CaseType.Value),
        PrincipalCode = request.Command.PrincipalCode,
        InstructionComplete = request.Command.Completeness.InstructionComplete,
        ImagesComplete = request.Command.Completeness.ImagesComplete,
        InstructionConfirmedByStaff = request.Command.Completeness.InstructionConfirmedByStaff,
        ImagesConfirmedByStaff = request.Command.Completeness.ImagesConfirmedByStaff,
        StandaloneAuditEvidenceId = request.Command.StandaloneAuditEvidenceId,
        AcceptedInspectionDeadline = request.Command.AcceptedInspectionDeadline,
        ActorKind = request.Actor.Kind.ToString(),
        ActorSubjectId = request.Actor.SubjectId,
        ActorRolesJson = JsonSerializer.Serialize(request.Actor.Roles.OrderBy(role => role), JsonOptions),
        OperationKey = request.OperationKey,
        CommandHash = request.CommandHash,
        Reason = request.Reason,
        StartedAtUtc = request.StartedAtUtc
    };

    private static void AddOutcomeEvent(
        PegasusDbContext context,
        IntakeAllocationAttemptEntity attempt,
        string eventType,
        DateTimeOffset occurredAtUtc)
    {
        context.IntakeReceiptEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = attempt.IntakeReceiptId,
            EventType = eventType,
            Actor = $"{attempt.ActorKind}:{attempt.ActorSubjectId}",
            OccurredAtUtc = occurredAtUtc,
            DetailsJson = JsonSerializer.Serialize(new
            {
                Version = 1,
                AttemptId = attempt.Id,
                attempt.OperationKey,
                attempt.CommandHash,
                attempt.CaseType,
                attempt.Status,
                attempt.FailureKind,
                attempt.RecoveryDisposition,
                attempt.SafeReason,
                attempt.CaseId,
                attempt.CaseReference,
                attempt.AuditReference
            }, JsonOptions)
        });
    }

    internal static IntakeAllocationAttempt Map(IntakeAllocationAttemptEntity entity) => new(
        entity.Id,
        entity.IntakeReceiptId,
        ParseAttemptKind(entity.Kind),
        ParseAttemptStatus(entity.Status),
        new(
            entity.IntakeReceiptId,
            entity.ExpectedReceiptVersion,
            entity.CaseType is null ? null : ParseCaseType(entity.CaseType),
            entity.PrincipalCode,
            new(
                entity.InstructionComplete,
                entity.ImagesComplete,
                entity.InstructionConfirmedByStaff,
                entity.ImagesConfirmedByStaff),
            entity.StandaloneAuditEvidenceId,
            entity.AcceptedInspectionDeadline),
        MapActor(entity),
        entity.OperationKey,
        entity.CommandHash,
        entity.Reason,
        entity.StartedAtUtc,
        entity.CompletedAtUtc,
        entity.FailureKind is null ? null : ParseFailureKind(entity.FailureKind),
        entity.RecoveryDisposition is null
            ? null
            : ParseRecoveryDisposition(entity.RecoveryDisposition),
        entity.SafeReason,
        entity.CaseId,
        entity.CaseReference,
        entity.AuditReference);

    private static ActionActor MapActor(IntakeAllocationAttemptEntity entity) => entity.ActorKind switch
    {
        nameof(ActorKind.Staff) => ActionActor.Staff(
            Guid.Parse(entity.ActorSubjectId),
            JsonSerializer.Deserialize<StaffRole[]>(entity.ActorRolesJson, JsonOptions) ?? []),
        nameof(ActorKind.SystemWorker) => ActionActor.SystemWorker(entity.ActorSubjectId),
        nameof(ActorKind.Automation) => ActionActor.Automation(entity.ActorSubjectId),
        _ => throw new InvalidDataException($"Unknown persisted allocation actor kind '{entity.ActorKind}'.")
    };

    private static string ToCode(IntakeAllocationAttemptKind value) => value switch
    {
        IntakeAllocationAttemptKind.Automatic => "automatic",
        IntakeAllocationAttemptKind.StaffCreate => "staff_create",
        IntakeAllocationAttemptKind.StaffRetry => "staff_retry",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static IntakeAllocationAttemptKind ParseAttemptKind(string value) => value switch
    {
        "automatic" => IntakeAllocationAttemptKind.Automatic,
        "staff_create" => IntakeAllocationAttemptKind.StaffCreate,
        "staff_retry" => IntakeAllocationAttemptKind.StaffRetry,
        _ => throw new InvalidDataException($"Unknown allocation-attempt kind '{value}'.")
    };

    private static string ToCode(IntakeAllocationAttemptStatus value) => value switch
    {
        IntakeAllocationAttemptStatus.Pending => "pending",
        IntakeAllocationAttemptStatus.Succeeded => "succeeded",
        IntakeAllocationAttemptStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static IntakeAllocationAttemptStatus ParseAttemptStatus(string value) => value switch
    {
        "pending" => IntakeAllocationAttemptStatus.Pending,
        "succeeded" => IntakeAllocationAttemptStatus.Succeeded,
        "failed" => IntakeAllocationAttemptStatus.Failed,
        _ => throw new InvalidDataException($"Unknown allocation-attempt status '{value}'.")
    };

    private static string ToCode(CaseType value) => value switch
    {
        CaseType.Inspection => "inspection",
        CaseType.Audit => "audit",
        CaseType.InspectionAndAudit => "inspection_and_audit",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static CaseType ParseCaseType(string value) => value switch
    {
        "inspection" => CaseType.Inspection,
        "audit" => CaseType.Audit,
        "inspection_and_audit" => CaseType.InspectionAndAudit,
        _ => throw new InvalidDataException($"Unknown allocation case type '{value}'.")
    };

    private static string ToCode(IntakeAllocationFailureKind value) => value switch
    {
        IntakeAllocationFailureKind.PrincipalUnavailable => "principal_unavailable",
        IntakeAllocationFailureKind.ConcurrencyConflict => "concurrency_conflict",
        IntakeAllocationFailureKind.SequenceExhausted => "sequence_exhausted",
        IntakeAllocationFailureKind.CaseTypeUnavailable => "case_type_unavailable",
        IntakeAllocationFailureKind.Unexpected => "unexpected",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static IntakeAllocationFailureKind ParseFailureKind(string value) => value switch
    {
        "principal_unavailable" => IntakeAllocationFailureKind.PrincipalUnavailable,
        "concurrency_conflict" => IntakeAllocationFailureKind.ConcurrencyConflict,
        "sequence_exhausted" => IntakeAllocationFailureKind.SequenceExhausted,
        "case_type_unavailable" => IntakeAllocationFailureKind.CaseTypeUnavailable,
        "unexpected" => IntakeAllocationFailureKind.Unexpected,
        _ => throw new InvalidDataException($"Unknown allocation failure kind '{value}'.")
    };

    private static string ToCode(IntakeAllocationRecoveryDisposition value) => value switch
    {
        IntakeAllocationRecoveryDisposition.RetryAfterCorrection => "retry_after_correction",
        IntakeAllocationRecoveryDisposition.ReloadThenRetry => "reload_then_retry",
        IntakeAllocationRecoveryDisposition.Blocked => "blocked",
        IntakeAllocationRecoveryDisposition.ManualReview => "manual_review",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static IntakeAllocationRecoveryDisposition ParseRecoveryDisposition(string value) => value switch
    {
        "retry_after_correction" => IntakeAllocationRecoveryDisposition.RetryAfterCorrection,
        "reload_then_retry" => IntakeAllocationRecoveryDisposition.ReloadThenRetry,
        "blocked" => IntakeAllocationRecoveryDisposition.Blocked,
        "manual_review" => IntakeAllocationRecoveryDisposition.ManualReview,
        _ => throw new InvalidDataException($"Unknown allocation recovery disposition '{value}'.")
    };

    [LoggerMessage(
        EventId = 4721,
        Level = LogLevel.Error,
        Message = "Case allocation failed for receipt {ReceiptId}, case type {CaseType}, failure {FailureKind}.")]
    private static partial void LogAllocationFailure(
        ILogger logger,
        Guid receiptId,
        string? caseType,
        string? failureKind,
        Exception exception);
}
