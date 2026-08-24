using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

public enum IntakeAllocationAttemptKind
{
    Automatic,
    StaffCreate,
    StaffRetry
}

public enum IntakeAllocationAttemptStatus
{
    Pending,
    Succeeded,
    Failed
}

public enum IntakeAllocationFailureKind
{
    PrincipalUnavailable,
    ConcurrencyConflict,
    SequenceExhausted,
    CaseTypeUnavailable,
    Unexpected
}

public enum IntakeAllocationRecoveryDisposition
{
    RetryAfterCorrection,
    ReloadThenRetry,
    Blocked,
    ManualReview
}

public enum IntakeAllocationProjectionStatus
{
    NotApplicable,
    AwaitingStaffEvidence,
    Pending,
    Succeeded,
    FailedRecoverable,
    FailedBlocked
}

/// <summary>
/// The immutable business command that an allocation attempt executes. A staff
/// retry reuses these values exactly and adds only its own actor, reason and
/// operation identity.
/// </summary>
public sealed record IntakeAllocationCommand(
    Guid ReceiptId,
    long ExpectedReceiptVersion,
    CaseType? CaseType,
    string PrincipalCode,
    CaseCompleteness Completeness,
    Guid? StandaloneAuditEvidenceId,
    DateOnly? AcceptedInspectionDeadline);

public sealed record IntakeAllocationAttempt(
    Guid Id,
    Guid ReceiptId,
    IntakeAllocationAttemptKind Kind,
    IntakeAllocationAttemptStatus Status,
    IntakeAllocationCommand Command,
    ActionActor Actor,
    string OperationKey,
    string CommandHash,
    string Reason,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IntakeAllocationFailureKind? FailureKind,
    IntakeAllocationRecoveryDisposition? RecoveryDisposition,
    string? SafeReason,
    Guid? CaseId,
    string? CaseReference,
    string? AuditReference);

public sealed record IntakeAllocationState(
    Guid AttemptId,
    IntakeAllocationProjectionStatus Status,
    CaseType? AttemptedCaseType,
    DateTimeOffset OccurredAtUtc,
    IntakeAllocationFailureKind? FailureKind = null,
    IntakeAllocationRecoveryDisposition? RecoveryDisposition = null,
    string? SafeReason = null,
    Guid? CaseId = null,
    string? CaseReference = null,
    string? AuditReference = null)
{
    public bool CanRetry =>
        Status == IntakeAllocationProjectionStatus.FailedRecoverable
        && RecoveryDisposition is IntakeAllocationRecoveryDisposition.RetryAfterCorrection
            or IntakeAllocationRecoveryDisposition.ReloadThenRetry;

    public static IntakeAllocationState FromAttempt(IntakeAllocationAttempt attempt) => new(
        attempt.Id,
        attempt.Status switch
        {
            IntakeAllocationAttemptStatus.Pending => IntakeAllocationProjectionStatus.Pending,
            IntakeAllocationAttemptStatus.Succeeded => IntakeAllocationProjectionStatus.Succeeded,
            IntakeAllocationAttemptStatus.Failed
                when attempt.RecoveryDisposition is IntakeAllocationRecoveryDisposition.RetryAfterCorrection
                    or IntakeAllocationRecoveryDisposition.ReloadThenRetry =>
                IntakeAllocationProjectionStatus.FailedRecoverable,
            IntakeAllocationAttemptStatus.Failed => IntakeAllocationProjectionStatus.FailedBlocked,
            _ => throw new InvalidOperationException(
                $"Unknown allocation-attempt status '{(int)attempt.Status}'.")
        },
        attempt.Command.CaseType,
        attempt.CompletedAtUtc ?? attempt.StartedAtUtc,
        attempt.FailureKind,
        attempt.RecoveryDisposition,
        attempt.SafeReason,
        attempt.CaseId,
        attempt.CaseReference,
        attempt.AuditReference);
}

public sealed record BeginIntakeAllocationAttempt(
    IntakeAllocationAttemptKind Kind,
    IntakeAllocationCommand Command,
    ActionActor Actor,
    string OperationKey,
    string CommandHash,
    string Reason,
    Guid? ExpectedCurrentAttemptId,
    DateTimeOffset StartedAtUtc);

public sealed record BeginIntakeAllocationResult(
    IntakeAllocationAttempt Attempt,
    bool IsReplay,
    bool IsSuppressed);

public interface IIntakeAllocationStore
{
    Task<IntakeAllocationAttempt?> GetCurrentAsync(
        Guid receiptId,
        CancellationToken cancellationToken);

    Task<BeginIntakeAllocationResult> BeginAsync(
        BeginIntakeAllocationAttempt request,
        CancellationToken cancellationToken);

    Task<IntakeAllocationAttempt> CompleteFailureAsync(
        Guid attemptId,
        IntakeAllocationFailureKind failureKind,
        IntakeAllocationRecoveryDisposition recoveryDisposition,
        string safeReason,
        DateTimeOffset completedAtUtc,
        Exception exception,
        CancellationToken cancellationToken);

    Task CancelPendingAsync(Guid attemptId, CancellationToken cancellationToken);
}

public sealed record RetryIntakeAllocationRequest(
    Guid ReceiptId,
    long ExpectedReceiptVersion,
    Guid ExpectedCurrentAttemptId,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public sealed record IntakeAllocationResult(
    IntakeAllocationState State,
    bool IsReplay,
    bool IsSuppressed);

public interface IAllocateIntake
{
    Task<IntakeAllocationResult?> AttemptAutomaticAsync(
        Guid receiptId,
        Guid evaluationId,
        CancellationToken cancellationToken = default);

    Task<IntakeAllocationResult> AttemptStaffCreateAsync(
        AcceptIntakeRequest request,
        CancellationToken cancellationToken = default);

    Task<IntakeAllocationResult> RetryAsync(
        RetryIntakeAllocationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class IntakeAllocationOperationConflictException()
    : Exception("The allocation operation key was already used for different command details.");

public sealed class IntakeAllocationConcurrencyException()
    : Exception("The receipt or allocation state changed after it was loaded.");

public sealed class PrincipalUnavailableException(string principalCode)
    : Exception($"The active principal '{principalCode}' does not exist.");

/// <summary>
/// The one Core owner for initial allocation, durable failure and reasoned
/// staff retry. Completed-work replay never calls this use case.
/// </summary>
public sealed class AllocateIntake(
    IIntakeReceiptQueries receiptQueries,
    IIntakeAllocationStore allocationStore,
    IAcceptIntake acceptIntake,
    TimeProvider timeProvider,
    IStandaloneAuditEvidenceQueries? standaloneAuditEvidenceQueries = null) : IAllocateIntake
{
    private const string SystemActor = "system-worker:intake-processing";

    /// <summary>
    /// What the automatic route actually knows when it allocates. The
    /// instruction half is an observation: this runs only for a receipt whose
    /// decision is already <see cref="IntakeDecision.CaseCreated"/>, a
    /// definitive authorised instruction. Staff have confirmed neither half,
    /// and the policy waives that for an automatically definitive intake
    /// rather than pretending otherwise.
    ///
    /// The images half is now observed too, from the receipt's own retained
    /// assets. It used to be a constant `true`, so every automatic case was
    /// born claiming complete images whatever arrived — and since the Review
    /// gate reduces to this one flag, an audit with an instruction, a report
    /// and no photographs went straight to Review while the EVA export refused
    /// the very same case for having no images (CASE-021).
    /// </summary>
    private static CaseCompleteness AutomaticCompleteness(IntakeReceipt receipt) =>
        new(InstructionComplete: true,
            // The one owner of which assets are photographs, so Review and
            // the EVA export agree by construction rather than by a second rule.
            ImagesComplete: InstructionEvidenceImages.Select(receipt.AssetRecords).Count > 0,
            InstructionConfirmedByStaff: false,
            ImagesConfirmedByStaff: false);

    public async Task<IntakeAllocationResult?> AttemptAutomaticAsync(
        Guid receiptId,
        Guid evaluationId,
        CancellationToken cancellationToken = default)
    {
        var receipt = await receiptQueries.GetAsync(receiptId, cancellationToken)
            ?? throw new KeyNotFoundException("The intake receipt was not found.");
        if (receipt.CurrentCaseId is not null || receipt.Decision != IntakeDecision.CaseCreated)
        {
            return null;
        }

        var caseType = receipt.MailClassificationDecision?.CaseType;
        var principalCode = receipt.MailRouteDecision is
        {
            Disposition: MailRouteDisposition.Accepted,
            SelectedRoute: { } selectedRoute
        }
            ? selectedRoute.WorkProviderCode.Trim().ToUpperInvariant()
            : throw new InvalidOperationException(
                "Automatic mailbox allocation requires an accepted principal route.");
        if (!string.Equals(
                receipt.InstructionDraft?.SuggestedPrincipalCode,
                principalCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The instruction draft principal does not match the accepted principal route.");
        }
        var standaloneAuditEvidenceId = caseType == CaseType.Audit
            ? (standaloneAuditEvidenceQueries is null
                ? null
                : (await standaloneAuditEvidenceQueries.GetForReceiptAsync(receipt.Id, cancellationToken))?.Id)
            : null;
        var command = new IntakeAllocationCommand(
            receipt.Id,
            receipt.Version,
            caseType,
            principalCode,
            AutomaticCompleteness(receipt),
            StandaloneAuditEvidenceId: standaloneAuditEvidenceId,
            receipt.InstructionDraft?.InspectionDate);
        var actor = ActionActor.SystemWorker(SystemActor);
        return await ExecuteAsync(
            IntakeAllocationAttemptKind.Automatic,
            command,
            actor,
            $"intake-allocation:{evaluationId:N}",
            "Created automatically from a definitive authorised instruction.",
            expectedCurrentAttemptId: null,
            cancellationToken);
    }

    public Task<IntakeAllocationResult> AttemptStaffCreateAsync(
        AcceptIntakeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireStaffActor(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        var command = new IntakeAllocationCommand(
            request.ReceiptId,
            request.ExpectedVersion,
            request.CaseType,
            request.PrincipalCode.Trim().ToUpperInvariant(),
            request.Completeness,
            request.StandaloneAuditEvidenceId,
            request.AcceptedInspectionDeadline);
        return ExecuteAsync(
            IntakeAllocationAttemptKind.StaffCreate,
            command,
            request.Actor,
            request.OperationKey,
            request.Reason,
            expectedCurrentAttemptId: null,
            cancellationToken);
    }

    public async Task<IntakeAllocationResult> RetryAsync(
        RetryIntakeAllocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireStaffActor(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        ValidateReasonAndOperation(request.Reason, request.OperationKey);
        var normalizedOperationKey = request.OperationKey.Trim();
        var normalizedReason = request.Reason.Trim();
        var current = await allocationStore.GetCurrentAsync(request.ReceiptId, cancellationToken)
            ?? throw new IntakeAllocationConcurrencyException();
        if (string.Equals(current.OperationKey, normalizedOperationKey, StringComparison.Ordinal))
        {
            var replayHash = CommandHash(
                IntakeAllocationAttemptKind.StaffRetry,
                current.Command,
                request.Actor,
                normalizedOperationKey,
                normalizedReason);
            if (!string.Equals(current.CommandHash, replayHash, StringComparison.Ordinal))
            {
                throw new IntakeAllocationOperationConflictException();
            }

            return new(IntakeAllocationState.FromAttempt(current), IsReplay: true, IsSuppressed: false);
        }
        if (current.Status == IntakeAllocationAttemptStatus.Succeeded)
        {
            return new(IntakeAllocationState.FromAttempt(current), IsReplay: true, IsSuppressed: true);
        }
        if (current.Status == IntakeAllocationAttemptStatus.Pending)
        {
            var recorded = await AwaitRecordedOutcomeAsync(
                new(current, IsReplay: true, IsSuppressed: true),
                cancellationToken);
            return new(
                IntakeAllocationState.FromAttempt(recorded.Attempt),
                IsReplay: true,
                IsSuppressed: true);
        }

        var receipt = await receiptQueries.GetAsync(request.ReceiptId, cancellationToken)
            ?? throw new KeyNotFoundException("The intake receipt was not found.");
        if (receipt.Version != request.ExpectedReceiptVersion)
        {
            throw new IntakeAllocationConcurrencyException();
        }

        var state = IntakeAllocationState.FromAttempt(current);
        if (current.Id != request.ExpectedCurrentAttemptId || !state.CanRetry)
        {
            throw new IntakeAllocationConcurrencyException();
        }

        return await ExecuteAsync(
            IntakeAllocationAttemptKind.StaffRetry,
            current.Command,
            request.Actor,
            request.OperationKey,
            request.Reason,
            current.Id,
            cancellationToken);
    }

    private async Task<IntakeAllocationResult> ExecuteAsync(
        IntakeAllocationAttemptKind kind,
        IntakeAllocationCommand command,
        ActionActor actor,
        string operationKey,
        string reason,
        Guid? expectedCurrentAttemptId,
        CancellationToken cancellationToken)
    {
        ValidateReasonAndOperation(reason, operationKey);
        var normalizedReason = reason.Trim();
        var normalizedOperationKey = operationKey.Trim();
        var hash = CommandHash(kind, command, actor, normalizedOperationKey, normalizedReason);
        var begun = await allocationStore.BeginAsync(
            new(
                kind,
                command,
                actor,
                normalizedOperationKey,
                hash,
                normalizedReason,
                expectedCurrentAttemptId,
                timeProvider.GetUtcNow()),
            cancellationToken);
        if (begun.IsSuppressed
            && begun.Attempt.Status == IntakeAllocationAttemptStatus.Pending)
        {
            begun = await AwaitRecordedOutcomeAsync(begun, cancellationToken);
        }
        if (begun.IsSuppressed || begun.Attempt.Status != IntakeAllocationAttemptStatus.Pending)
        {
            return new(
                IntakeAllocationState.FromAttempt(begun.Attempt),
                begun.IsReplay,
                begun.IsSuppressed);
        }

        if (command.CaseType is null)
        {
            return await CompleteFailureAsync(
                begun,
                IntakeAllocationFailureKind.CaseTypeUnavailable,
                IntakeAllocationRecoveryDisposition.ManualReview,
                "The accepted case type is unavailable. Review this item before creating a case.",
                new InvalidOperationException("The persisted allocation command has no accepted case type."),
                cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(command.PrincipalCode))
        {
            return await CompleteFailureAsync(
                begun,
                IntakeAllocationFailureKind.PrincipalUnavailable,
                IntakeAllocationRecoveryDisposition.RetryAfterCorrection,
                "The selected Principal is not available. Correct it in Principal administration, then retry.",
                new PrincipalUnavailableException(command.PrincipalCode),
                cancellationToken);
        }

        try
        {
            var completedAtUtc = timeProvider.GetUtcNow();
            var outcome = await acceptIntake.ExecuteAsync(
                new(
                    command.ReceiptId,
                    command.ExpectedReceiptVersion,
                    actor,
                    normalizedOperationKey,
                    normalizedReason,
                    command.CaseType.Value,
                    command.PrincipalCode,
                    command.Completeness,
                    command.StandaloneAuditEvidenceId,
                    command.AcceptedInspectionDeadline,
                    begun.Attempt.Id,
                    completedAtUtc),
                cancellationToken);
            var completed = begun.Attempt with
            {
                Status = IntakeAllocationAttemptStatus.Succeeded,
                CompletedAtUtc = completedAtUtc,
                CaseId = outcome.Identity.CaseId,
                CaseReference = outcome.Identity.Reference,
                AuditReference = outcome.Identity.AuditReference
            };
            return new(IntakeAllocationState.FromAttempt(completed), begun.IsReplay, false);
        }
        catch (OperationCanceledException)
        {
            await allocationStore.CancelPendingAsync(begun.Attempt.Id, CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            var failure = Classify(exception);
            return await CompleteFailureAsync(
                begun,
                failure.Kind,
                failure.Disposition,
                failure.SafeReason,
                exception,
                cancellationToken);
        }
    }

    private async Task<BeginIntakeAllocationResult> AwaitRecordedOutcomeAsync(
        BeginIntakeAllocationResult begun,
        CancellationToken cancellationToken)
    {
        // A concurrent request never invokes acceptance. It waits only for the
        // already-owned durable attempt to publish its recorded outcome, so
        // parallel staff retries converge on the same Case identity. The
        // window is bounded but generous (ten seconds): a one-second budget
        // returned Pending to the concurrent caller whenever allocation ran
        // slowly under load (CASE-005), which is the divergence this wait
        // exists to prevent. Still Pending after the window is reported
        // honestly.
        for (var poll = 0; poll < 100; poll++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            var current = await allocationStore.GetCurrentAsync(
                begun.Attempt.ReceiptId,
                cancellationToken);
            if (current is not null
                && current.Status != IntakeAllocationAttemptStatus.Pending)
            {
                return new(current, IsReplay: true, IsSuppressed: true);
            }
        }

        return begun;
    }

    private async Task<IntakeAllocationResult> CompleteFailureAsync(
        BeginIntakeAllocationResult begun,
        IntakeAllocationFailureKind kind,
        IntakeAllocationRecoveryDisposition disposition,
        string safeReason,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var completed = await allocationStore.CompleteFailureAsync(
            begun.Attempt.Id,
            kind,
            disposition,
            safeReason,
            timeProvider.GetUtcNow(),
            exception,
            cancellationToken);
        return new(IntakeAllocationState.FromAttempt(completed), begun.IsReplay, false);
    }

    private static (IntakeAllocationFailureKind Kind,
        IntakeAllocationRecoveryDisposition Disposition,
        string SafeReason) Classify(Exception exception) => exception switch
    {
        PrincipalUnavailableException => (
            IntakeAllocationFailureKind.PrincipalUnavailable,
            IntakeAllocationRecoveryDisposition.RetryAfterCorrection,
            "The selected Principal is not available. Correct it in Principal administration, then retry."),
        IntakeVersionConflictException or IntakeAllocationConcurrencyException => (
            IntakeAllocationFailureKind.ConcurrencyConflict,
            IntakeAllocationRecoveryDisposition.ReloadThenRetry,
            "The receipt or allocation state changed. Reload it before retrying."),
        CaseIdentitySequenceExhaustedException => (
            IntakeAllocationFailureKind.SequenceExhausted,
            IntakeAllocationRecoveryDisposition.Blocked,
            "The Principal's case reference sequence is exhausted. No case was created."),
        _ => (
            IntakeAllocationFailureKind.Unexpected,
            IntakeAllocationRecoveryDisposition.Blocked,
            "The case could not be created. No reference was allocated."),
    };

    private static void ValidateReasonAndOperation(string reason, string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        if (reason.Trim().Length > 500)
        {
            throw new ArgumentException("The allocation reason must be 500 characters or fewer.", nameof(reason));
        }
        if (operationKey.Trim().Length > 100)
        {
            throw new ArgumentException("The allocation operation key must be 100 characters or fewer.", nameof(operationKey));
        }
    }

    private static void RequireStaffActor(ActionActor actor)
    {
        if (actor.Kind != ActorKind.Staff
            || !actor.Roles.Any(role => role is StaffRole.Administrator
                or StaffRole.Engineer
                or StaffRole.User))
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }
    }

    private static string CommandHash(
        IntakeAllocationAttemptKind kind,
        IntakeAllocationCommand command,
        ActionActor actor,
        string operationKey,
        string reason)
    {
        var material = JsonSerializer.Serialize(new
        {
            SchemaVersion = 1,
            Kind = kind.ToString(),
            Command = command,
            ActorKind = actor.Kind.ToString(),
            actor.SubjectId,
            Roles = actor.Roles.OrderBy(role => role).Select(role => role.ToString()),
            OperationKey = operationKey,
            Reason = reason
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)))
            .ToLowerInvariant();
    }
}
