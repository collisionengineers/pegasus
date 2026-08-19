using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Intake;

public enum IntakeWorkState
{
    Pending = 0,
    Dispatched = 1,
    Processing = 2,
    RetryScheduled = 3,
    Completed = 4,
    Failed = 5,
    Dispatching = 6
}

public sealed record IntakeStagedReceipt(
    Guid Id,
    string SourceFileName,
    string MediaType,
    long SourceLength,
    string SourceHash,
    IntakeSourceIdentity SourceIdentity,
    DateTimeOffset ReceivedAtUtc,
    string Actor,
    string StorageKey,
    DateTimeOffset StagedAtUtc);

public sealed record IntakeWorkItem(
    Guid Id,
    Guid StagedReceiptId,
    string OperationKey,
    IntakeWorkState State,
    int AttemptCount,
    DateTimeOffset DueAtUtc,
    string? LeaseToken,
    DateTimeOffset? LeaseExpiresAtUtc,
    Guid? ProcessedReceiptId,
    string? FailureCode,
    bool IsReevaluation = false);

public enum StagedArtifactAuthorityState
{
    Pending = 0,
    Failed = 1,
    Completed = 2,
    Unmatched = 3
}

public sealed record StagedArtifactAuthority(
    string StorageKey,
    string ExpectedContentHash,
    long ExpectedContentLength,
    StagedArtifactAuthorityState State);

public interface IStagedArtifactAuthority
{
    Task<StagedArtifactAuthority?> FindAsync(
        string storageKey,
        CancellationToken cancellationToken);
}

public sealed record ReconcileStagedArtifactsResult(
    int RecoveredLeases,
    int Completed,
    int Retained,
    int Orphans,
    int Unmatched,
    int Failures);

public sealed record ReceivedIntake(Guid StagedReceiptId, bool IsDuplicate);

public enum QueuedIntakeStatusKind
{
    Received = 0,
    Processing = 1,
    Complete = 2,
    Failed = 3
}

public sealed record QueuedIntakeStatus(
    Guid StagedReceiptId,
    string SourceFileName,
    DateTimeOffset ReceivedAtUtc,
    QueuedIntakeStatusKind Status,
    Guid? ProcessedReceiptId,
    Guid? CaseId,
    string? FailureCode);

public static class QueuedIntakeStatusKinds
{
    /// <summary>
    /// The staff-facing state of a work item. Everything before a lease is
    /// held reads as Received: staff are told the file is safe and waiting,
    /// not which internal queue step it is on.
    /// </summary>
    public static QueuedIntakeStatusKind FromWorkState(IntakeWorkState state) => state switch
    {
        IntakeWorkState.Pending
            or IntakeWorkState.Dispatching
            or IntakeWorkState.Dispatched
            or IntakeWorkState.RetryScheduled => QueuedIntakeStatusKind.Received,
        IntakeWorkState.Processing => QueuedIntakeStatusKind.Processing,
        IntakeWorkState.Completed => QueuedIntakeStatusKind.Complete,
        IntakeWorkState.Failed => QueuedIntakeStatusKind.Failed,
        _ => throw new InvalidOperationException($"Unknown IntakeWorkState value '{(int)state}'.")
    };
}

public interface IQueuedIntakeStatusQueries
{
    Task<QueuedIntakeStatus?> GetAsync(
        Guid stagedReceiptId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// What one queued delivery did. An unexpected fault is not an outcome: it is
/// persisted as a terminal failure and then rethrown to the host.
/// </summary>
public enum QueuedIntakeProcessingOutcome
{
    NoOp = 0,
    Completed = 1,
    RetryScheduled = 2,
    Failed = 3
}

public sealed record IntakeEvaluationRevision(
    Guid Id,
    Guid StagedReceiptId,
    Guid ProcessedReceiptId,
    int Revision,
    DateTimeOffset EvaluatedAtUtc);

public interface IIntakeSubmission
{
    Task<ReceivedIntake> ExecuteAsync(
        IntakeSource source,
        string operationKey,
        CancellationToken cancellationToken = default);
}

public interface IIntakeWorkStore
{
    Task<IntakeStagedReceipt?> FindBySourceIdentityAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken);

    Task<ReceivedIntake> ReceiveAsync(
        IntakeStagedReceipt receipt,
        string operationKey,
        CancellationToken cancellationToken);

    Task<IntakeWorkItem?> ClaimDispatchAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    /// <summary>
    /// The work item for a staged receipt, whoever holds it. Read-only: this
    /// asks whether the work is still in hand, it does not claim it.
    /// </summary>
    Task<IntakeWorkItem?> FindWorkItemAsync(
        Guid stagedReceiptId,
        CancellationToken cancellationToken);

    Task MarkDispatchedAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task ReleaseDispatchAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken);

    Task<(IntakeWorkItem WorkItem, IntakeStagedReceipt Receipt)?> ClaimProcessingAsync(
        Guid stagedReceiptId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<IntakeEvaluationRevision> CompleteProcessingAsync(
        Guid workItemId,
        string leaseToken,
        Guid processedReceiptId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken);

    Task<IntakeEvaluationRevision?> GetCompletedEvaluationAsync(
        Guid stagedReceiptId,
        CancellationToken cancellationToken);

    Task RetryProcessingAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset dueAtUtc,
        string failureCode,
        bool terminal,
        CancellationToken cancellationToken);

    Task MarkPoisonedAsync(
        Guid stagedReceiptId,
        DateTimeOffset failedAtUtc,
        CancellationToken cancellationToken);

    Task<int> RecoverExpiredLeasesAsync(
        DateTimeOffset nowUtc,
        int maximumItems,
        CancellationToken cancellationToken);

    Task ScheduleReevaluationAsync(
        Guid stagedReceiptId,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken);
}

public interface IIntakeWorkEnqueuer
{
    Task EnqueueAsync(Guid stagedReceiptId, CancellationToken cancellationToken);
}

public sealed class ReceiveIntake(
    IIntakeArtifactStore artifactStore,
    IIntakeWorkStore workStore,
    TimeProvider timeProvider) : IIntakeSubmission
{
    private const int MaximumFileNameLength = 260;
    private const int MaximumMediaTypeLength = 200;
    private const int MaximumActorLength = 200;
    private const int MaximumExternalReceiptTokenLength = 200;
    private const int MaximumOperationKeyLength = 100;

    public async Task<ReceivedIntake> ExecuteAsync(
        IntakeSource source,
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.SourceIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.MediaType);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceIdentity.ExternalReceiptToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);

        var safeFileName = Path.GetFileName(source.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeFileName);
        ValidateLength(safeFileName, MaximumFileNameLength, nameof(source.FileName));
        ValidateLength(source.MediaType, MaximumMediaTypeLength, nameof(source.MediaType));
        ValidateLength(source.Actor, MaximumActorLength, nameof(source.Actor));
        ValidateLength(
            source.SourceIdentity.ExternalReceiptToken,
            MaximumExternalReceiptTokenLength,
            nameof(source.SourceIdentity.ExternalReceiptToken));
        ValidateLength(operationKey, MaximumOperationKeyLength, nameof(operationKey));
        if (source.Content.IsEmpty)
        {
            throw new InvalidDataException("The intake source is empty.");
        }

        // A received message and an uploaded file do not share a size bound:
        // the form takes one file, a mailbox message carries the whole job.
        var maximumContentLength = source.SourceIdentity.Channel switch
        {
            IntakeSourceChannel.ManualUpload => IntakeEnvelopeLimits.MaximumContentLength,
            IntakeSourceChannel.Mailbox => IntakeEnvelopeLimits.MaximumMailboxContentLength,
            IntakeSourceChannel.Automation => IntakeEnvelopeLimits.MaximumContentLength,
            _ => throw new ArgumentOutOfRangeException(
                nameof(source),
                source.SourceIdentity.Channel,
                "The intake source channel is not supported.")
        };
        if (source.Content.Length > maximumContentLength)
        {
            throw new InvalidDataException("The intake source exceeds its channel's size limit.");
        }

        var sourceHash = Convert.ToHexString(SHA256.HashData(source.Content.Span));
        var existing = await workStore.FindBySourceIdentityAsync(
            source.SourceIdentity,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.SourceHash, sourceHash, StringComparison.Ordinal))
            {
                throw new IntakeSourceIdentityConflictException(existing.SourceHash, sourceHash);
            }

            return await workStore.ReceiveAsync(existing, operationKey, cancellationToken);
        }

        var stagedReceiptId = Guid.NewGuid();
        var nowUtc = timeProvider.GetUtcNow();
        StagedArtifactInventoryItem stagedArtifact;
        try
        {
            stagedArtifact = await artifactStore.StageAsync(
                stagedReceiptId,
                sourceHash,
                source.Content,
                nowUtc,
                cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            throw new IntakeArtifactRetentionException(exception);
        }

        var stagedReceipt = new IntakeStagedReceipt(
            stagedReceiptId,
            safeFileName,
            source.MediaType,
            source.Content.Length,
            sourceHash,
            source.SourceIdentity,
            source.ReceivedAtUtc,
            source.Actor,
            stagedArtifact.StorageKey,
            nowUtc);
        return await workStore.ReceiveAsync(stagedReceipt, operationKey, cancellationToken);
    }

    private static void ValidateLength(string value, int maximumLength, string parameterName)
    {
        if (value.Length > maximumLength)
        {
            throw new ArgumentException(
                $"The value must be {maximumLength} characters or fewer.",
                parameterName);
        }
    }
}

public sealed class DispatchPendingIntakeWork(
    IIntakeWorkStore workStore,
    IIntakeWorkEnqueuer workEnqueuer,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan DispatchLeaseDuration = TimeSpan.FromMinutes(1);

    public async Task<int> ExecuteAsync(int maximumItems, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var dispatched = 0;
        for (; dispatched < maximumItems; dispatched++)
        {
            var nowUtc = timeProvider.GetUtcNow();
            var workItem = await workStore.ClaimDispatchAsync(nowUtc, DispatchLeaseDuration, cancellationToken);
            if (workItem is null)
            {
                break;
            }

            if (workItem.LeaseToken is null)
            {
                throw new InvalidOperationException("A claimed intake work item must have a lease token.");
            }

            try
            {
                await workEnqueuer.EnqueueAsync(workItem.StagedReceiptId, cancellationToken);
                await workStore.MarkDispatchedAsync(workItem.Id, workItem.LeaseToken, timeProvider.GetUtcNow(), cancellationToken);
            }
            catch
            {
                await workStore.ReleaseDispatchAsync(
                    workItem.Id,
                    workItem.LeaseToken,
                    timeProvider.GetUtcNow().AddSeconds(30),
                    cancellationToken);
                throw;
            }
        }

        return dispatched;
    }
}

public sealed class ProcessQueuedIntake(
    IIntakeWorkStore workStore,
    IIntakeArtifactStore artifactStore,
    ProcessIntake processIntake,
    IIntakeReceiptQueries receiptQueries,
    ICreateTriageFromIntake createTriage,
    IAutomaticCaseAssociationStore caseAssociationStore,
    IAllocateIntake allocateIntake,
    TimeProvider timeProvider,
    Pegasus.Core.ImageIntake.IImageIntakeAutomation? imageIntakeAutomation = null)
{
    private const string SystemActor = "system-worker:intake-processing";
    private static readonly TimeSpan ProcessingLeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2)
    ];

    public async Task<QueuedIntakeProcessingOutcome> ExecuteAsync(
        Guid stagedReceiptId,
        CancellationToken cancellationToken = default)
    {
        var claimed = await workStore.ClaimProcessingAsync(
            stagedReceiptId,
            timeProvider.GetUtcNow(),
            ProcessingLeaseDuration,
            cancellationToken);
        if (claimed is null)
        {
            var completedEvaluation = await workStore.GetCompletedEvaluationAsync(
                stagedReceiptId,
                cancellationToken);
            if (completedEvaluation is null)
            {
                return QueuedIntakeProcessingOutcome.NoOp;
            }

            var completedReceipt = await receiptQueries.GetAsync(
                completedEvaluation.ProcessedReceiptId,
                cancellationToken)
                ?? throw new InvalidDataException(
                    "The completed intake evaluation does not identify a persisted receipt.");
            var replayAssociated = await AssociateCaseIfUnambiguousAsync(
                completedReceipt,
                completedEvaluation,
                cancellationToken);
            if (replayAssociated)
            {
                completedReceipt = await receiptQueries.GetAsync(
                    completedEvaluation.ProcessedReceiptId,
                    cancellationToken) ?? completedReceipt;
            }

            // Re-drive automatic allocation on replay. The live path allocates
            // after CompleteProcessingAsync and outside its try/catch, so a
            // recoverable throw (e.g. a serializable begin failing transiently)
            // after completion would otherwise strand a definitive receipt with
            // no case. AttemptAutomaticAsync is idempotent: it no-ops once a case
            // exists and suppresses a duplicate automatic attempt, so replaying
            // it either mints the missing case or does nothing.
            var replayAllocation = await allocateIntake.AttemptAutomaticAsync(
                completedReceipt.Id,
                completedEvaluation.Id,
                cancellationToken);
            var replayAllocated =
                replayAllocation?.State.Status == IntakeAllocationProjectionStatus.Succeeded;
            await CreateTriageIfQualifyingAsync(
                completedReceipt,
                completedEvaluation,
                cancellationToken);
            if (replayAllocated)
            {
                completedReceipt = await receiptQueries.GetAsync(
                    completedEvaluation.ProcessedReceiptId,
                    cancellationToken) ?? completedReceipt;
            }

            await ApplyImageIntakeAutomationAsync(completedReceipt, cancellationToken);
            return QueuedIntakeProcessingOutcome.NoOp;
        }

        var (workItem, stagedReceipt) = claimed.Value;
        if (workItem.LeaseToken is null)
        {
            throw new InvalidOperationException("A claimed intake work item must have a lease token.");
        }

        IntakeReceipt processed;
        IntakeEvaluationRevision evaluation;
        try
        {
            var content = await artifactStore.ReadAsync(stagedReceipt.StorageKey, cancellationToken)
                ?? throw new IntakeArtifactIntegrityException();
            var actualHash = Convert.ToHexString(SHA256.HashData(content.Span));
            if (!string.Equals(actualHash, stagedReceipt.SourceHash, StringComparison.Ordinal))
            {
                throw new IntakeArtifactIntegrityException();
            }

            var durableStorageKey = await artifactStore.StoreAsync(
                stagedReceipt.SourceHash,
                content,
                cancellationToken);
            processed = await processIntake.ExecuteRetainedAsync(
                new(
                    stagedReceipt.SourceFileName,
                    stagedReceipt.MediaType,
                    content,
                    stagedReceipt.ReceivedAtUtc,
                    stagedReceipt.Actor,
                    stagedReceipt.SourceIdentity),
                durableStorageKey,
                workItem.IsReevaluation,
                cancellationToken);
            evaluation = await workStore.CompleteProcessingAsync(
                workItem.Id,
                workItem.LeaseToken,
                processed.Id,
                timeProvider.GetUtcNow(),
                cancellationToken);
        }
        catch (Exception exception) when (TerminalInputFailureCode(exception) is { } failureCode)
        {
            await FailProcessingAsync(workItem, terminal: true, failureCode, cancellationToken);
            return QueuedIntakeProcessingOutcome.Failed;
        }
        catch (Exception exception) when (IsTransientProcessingFailure(exception))
        {
            var terminal = workItem.AttemptCount >= RetryDelays.Length;
            await FailProcessingAsync(
                workItem,
                terminal,
                TransientFailureCode(exception),
                cancellationToken);
            return terminal
                ? QueuedIntakeProcessingOutcome.Failed
                : QueuedIntakeProcessingOutcome.RetryScheduled;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            // Anything else is a defect, not a condition to retry. The item is
            // failed durably so staff see it, then the fault goes to the host
            // unchanged: it is logged there in full, and the redelivery that
            // follows finds the work failed and does nothing.
            await FailProcessingAsync(
                workItem,
                terminal: true,
                "unexpected_intake_processing_failure",
                cancellationToken);
            throw;
        }

        await TryDeleteCompletedStagingAsync(
            stagedReceipt.StorageKey,
            cancellationToken);

        var associated = await AssociateCaseIfUnambiguousAsync(processed, evaluation, cancellationToken);
        if (associated)
        {
            processed = await receiptQueries.GetAsync(processed.Id, cancellationToken) ?? processed;
        }

        var allocation = await allocateIntake.AttemptAutomaticAsync(
            processed.Id,
            evaluation.Id,
            cancellationToken);
        var allocated = allocation?.State.Status == IntakeAllocationProjectionStatus.Succeeded;
        await CreateTriageIfQualifyingAsync(processed, evaluation, cancellationToken);
        if (allocated)
        {
            // Allocation wrote CurrentCaseId durably; image automation must
            // see the associated state rather than attempt a conflicting link.
            processed = await receiptQueries.GetAsync(processed.Id, cancellationToken) ?? processed;
        }

        await ApplyImageIntakeAutomationAsync(processed, cancellationToken);
        return QueuedIntakeProcessingOutcome.Completed;
    }

    /// <summary>
    /// Image-intake automation runs after the evaluation revision is durably
    /// recorded (registration binds to that revision) and is advisory and
    /// non-blocking: the persisted receipt stands regardless of any
    /// automation failure, and every operation key is receipt-scoped so a
    /// reprocessed receipt replays instead of duplicating.
    /// </summary>
    private async Task ApplyImageIntakeAutomationAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        if (imageIntakeAutomation is null)
        {
            return;
        }

        try
        {
            _ = await imageIntakeAutomation.ApplyAsync(receipt, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            // Non-blocking by design; suggestions and receipt state carry the
            // visible outcome.
        }
    }

    /// <summary>
    /// Advisory and non-blocking, like image automation: the evaluation and
    /// its case-match decision are already durable, staff can always link
    /// manually from the recorded decision, and a redelivered receipt replays
    /// through the operation key — so a failed association write is never
    /// allowed to fail the completed receipt.
    /// </summary>
    private async Task<bool> AssociateCaseIfUnambiguousAsync(
        IntakeReceipt receipt,
        IntakeEvaluationRevision evaluation,
        CancellationToken cancellationToken)
    {
        if (receipt.CaseMatchDecision is not
            { Outcome: CaseMatchOutcome.UniqueMatch, MatchedCaseId: { } matchedCaseId } decision)
        {
            return false;
        }

        if (receipt.CurrentCaseId is not null)
        {
            return false;
        }

        try
        {
            var outcome = await caseAssociationStore.AssociateFromMatchAsync(
                new(
                    receipt.Id,
                    matchedCaseId,
                    decision.PolicyKey,
                    decision.PolicyVersion,
                    SystemActor,
                    $"case-match-association:{evaluation.Id:N}",
                    $"Automatic association from the recorded case-match decision ({decision.PolicyKey} v{decision.PolicyVersion})."),
                timeProvider.GetUtcNow(),
                cancellationToken);
            return outcome == AutomaticCaseAssociationOutcome.Associated;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // A vanished case, an archived case, or a live staff edit lease
            // yields; the recorded decision stays visible for a staff link.
            return false;
        }
    }

    private async Task TryDeleteCompletedStagingAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {

        try
        {
            var staged = await artifactStore.GetStagedAsync(
                storageKey,
                cancellationToken);
            if (staged is null)
            {
                return;
            }

            if (staged.Disposition != StagedArtifactDisposition.Completed)
            {
                staged = await artifactStore.TrySetStagedDispositionAsync(
                    staged.StorageKey,
                    staged.ConcurrencyToken,
                    StagedArtifactDisposition.Completed,
                    cancellationToken);
            }

            if (staged is not null)
            {
                await artifactStore.DeleteCompletedStagedAsync(
                    staged.StorageKey,
                    staged.ConcurrencyToken,
                    cancellationToken);
            }
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            // ReconcileStagedArtifacts repairs a completion/tag/delete interruption.
        }
    }

    private async Task FailProcessingAsync(
        IntakeWorkItem workItem,
        bool terminal,
        string failureCode,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        var dueAtUtc = terminal
            ? nowUtc
            : nowUtc.Add(RetryDelays[workItem.AttemptCount - 1]);
        await workStore.RetryProcessingAsync(
            workItem.Id,
            workItem.LeaseToken
                ?? throw new InvalidOperationException("A claimed intake work item must have a lease token."),
            dueAtUtc,
            failureCode,
            terminal,
            cancellationToken);
    }

    /// <summary>
    /// The failure code for a fault that says the input itself is wrong, or
    /// null when the fault is not one of those. Retrying cannot change these,
    /// so they fail on the first attempt under their own code.
    /// </summary>
    private static string? TerminalInputFailureCode(Exception exception) => exception switch
    {
        IntakeArtifactIntegrityException => "staged_artifact_integrity_failure",
        InvalidDataException => "invalid_intake_data",
        IntakeSourceIdentityConflictException => "source_identity_conflict",
        _ => null
    };

    /// <summary>
    /// Faults worth the bounded retry schedule: the named intake conflicts, the
    /// dependency-unavailable fault adapters translate to, and raw I/O, timeout
    /// and database faults, including any of those wrapped by another
    /// exception, which is how EF surfaces a deadlock or dropped connection.
    /// </summary>
    private static bool IsTransientProcessingFailure(Exception exception) =>
        exception is IntakeArtifactRetentionException
            or IntakeOperationConflictException
            or IntakeVersionConflictException
            or IntakeDependencyUnavailableException
            or IOException
            or TimeoutException
            or DbException
        || (exception.InnerException is { } inner && IsTransientProcessingFailure(inner));

    private static string TransientFailureCode(Exception exception) =>
        exception is IntakeArtifactRetentionException
            ? "artifact_retention_failure"
            : "intake_processing_failure";

    private async Task CreateTriageIfQualifyingAsync(
        IntakeReceipt receipt,
        IntakeEvaluationRevision evaluation,
        CancellationToken cancellationToken)
    {
        var registration = receipt.InstructionDraft?.VehicleRegistration;
        var acceptedMatches = receipt.Evidence
            .Where(evidence => evidence.Finding == IntakeEvidenceFinding.AcceptedTriageMatch)
            .Take(2)
            .ToArray();
        if (string.IsNullOrWhiteSpace(registration)
            || acceptedMatches.Length != 1
            || acceptedMatches[0].Strength != IntakeEvidenceStrength.Strong
            || string.IsNullOrWhiteSpace(acceptedMatches[0].MatcherKey)
            || acceptedMatches[0].MatcherVersion is null or <= 0)
        {
            return;
        }

        await createTriage.ExecuteAsync(
            new(
                new(
                    receipt.Id,
                    receipt.SourceIdentity,
                    receipt.SourceHash,
                    evaluation.Id),
                registration,
                acceptedMatches[0],
                SystemActor,
                $"triage-from-intake-evaluation:{evaluation.Id:N}"),
            cancellationToken);
    }
}

public sealed class ReconcilePoisonedIntakeWork(
    IIntakeWorkStore workStore,
    TimeProvider timeProvider)
{
    public Task ExecuteAsync(Guid stagedReceiptId, CancellationToken cancellationToken = default) =>
        workStore.MarkPoisonedAsync(stagedReceiptId, timeProvider.GetUtcNow(), cancellationToken);
}

public sealed class ReconcileStagedArtifacts(
    IIntakeWorkStore workStore,
    IStagedArtifactAuthority authority,
    IIntakeArtifactStore artifactStore,
    TimeProvider timeProvider)
{
    public async Task<ReconcileStagedArtifactsResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);

        var recoveredLeases = await workStore.RecoverExpiredLeasesAsync(
            timeProvider.GetUtcNow(),
            maximumItems,
            cancellationToken);
        var items = await artifactStore.ListStagedAsync(maximumItems, cancellationToken);
        var completed = 0;
        var retained = 0;
        var orphans = 0;
        var unmatched = 0;
        var failures = 0;

        foreach (var item in items)
        {
            try
            {
                var durable = await authority.FindAsync(item.StorageKey, cancellationToken);
                var target = Classify(item, durable);
                var current = item;
                if (current.Disposition != target)
                {
                    current = await artifactStore.TrySetStagedDispositionAsync(
                        item.StorageKey,
                        item.ConcurrencyToken,
                        target,
                        cancellationToken);
                    if (current is null)
                    {
                        failures++;
                        continue;
                    }
                }

                switch (target)
                {
                    case StagedArtifactDisposition.Completed:
                        if (await artifactStore.DeleteCompletedStagedAsync(
                                current.StorageKey,
                                current.ConcurrencyToken,
                                cancellationToken))
                        {
                            completed++;
                        }
                        else
                        {
                            failures++;
                        }
                        break;
                    case StagedArtifactDisposition.Orphan:
                        orphans++;
                        break;
                    case StagedArtifactDisposition.Unmatched:
                        unmatched++;
                        break;
                    case StagedArtifactDisposition.Pending:
                    case StagedArtifactDisposition.Failed:
                        retained++;
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown staged artifact disposition '{(int)target}'.");
                }
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
            }
        }

        return new(
            recoveredLeases,
            completed,
            retained,
            orphans,
            unmatched,
            failures);
    }

    private static StagedArtifactDisposition Classify(
        StagedArtifactInventoryItem item,
        StagedArtifactAuthority? durable)
    {
        if (durable is null)
        {
            return StagedArtifactDisposition.Orphan;
        }

        if (!string.Equals(
                item.ContentHash,
                durable.ExpectedContentHash,
                StringComparison.Ordinal)
            || item.ContentLength != durable.ExpectedContentLength)
        {
            return StagedArtifactDisposition.Unmatched;
        }

        return durable.State switch
        {
            StagedArtifactAuthorityState.Pending => StagedArtifactDisposition.Pending,
            StagedArtifactAuthorityState.Failed => StagedArtifactDisposition.Failed,
            StagedArtifactAuthorityState.Completed => StagedArtifactDisposition.Completed,
            StagedArtifactAuthorityState.Unmatched => StagedArtifactDisposition.Unmatched,
            _ => throw new InvalidOperationException(
                $"Unknown staged artifact authority state '{(int)durable.State}'.")
        };
    }
}

public sealed class ResolveIntake(
    IIntakeMutationStore store,
    TimeProvider timeProvider) : IResolveIntake
{
    public Task<IntakeReceipt> ExecuteAsync(
        ResolveIntakeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntakeCommandValidation.RequireStaffMutation(
            request.ReceiptId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason);
        if (!Enum.IsDefined(request.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The resolution kind is invalid.");
        }
        if ((request.Kind == IntakeResolutionKind.CorrectDraft) != (request.CorrectedDraft is not null))
        {
            throw new ArgumentException(
                "A corrected draft is required only for a draft correction.",
                nameof(request));
        }

        return store.ResolveAsync(request, timeProvider.GetUtcNow(), cancellationToken);
    }
}

public sealed class ReevaluateIntake(
    IIntakeMutationStore store,
    TimeProvider timeProvider) : IReevaluateIntake
{
    public Task<IntakeReceipt> ExecuteAsync(
        ReevaluateIntakeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntakeCommandValidation.RequireStaffMutation(
            request.ReceiptId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason);
        return store.ScheduleReevaluationAsync(
            request,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}

public sealed class LinkIntake(
    IIntakeMutationStore store,
    IImageIntakeCasePairing casePairing,
    TimeProvider timeProvider) : ILinkIntake
{
    public async Task ExecuteAsync(
        LinkIntakeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntakeCommandValidation.RequireStaffMutation(
            request.ReceiptId,
            request.ExpectedIntakeVersion,
            request.Actor,
            request.OperationKey,
            request.Reason);
        IntakeCommandValidation.RequireCase(
            request.CaseId,
            request.ExpectedCaseVersion,
            request.EditLeaseToken);
        await store.LinkAsync(request, timeProvider.GetUtcNow(), cancellationToken);

        // A manually linked receipt whose image-only material already
        // registered an Image intake must move that Image-initiated Case out
        // of Awaiting instruction too — the one lifecycle transition owner
        // also used by the automatic pairing paths. Advisory: the manual
        // link itself has already committed, so a sync failure here is
        // retried the next time this receipt is linked or a case is accepted.
        try
        {
            await casePairing.SyncMergeAfterLinkAsync(
                request.ReceiptId,
                request.CaseId,
                request.Actor,
                cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
        }
    }
}

public sealed class ReverseIntakeLink(
    IIntakeMutationStore store,
    TimeProvider timeProvider) : IReverseIntakeLink
{
    public Task ExecuteAsync(
        ReverseIntakeLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntakeCommandValidation.RequireStaffMutation(
            request.ReceiptId,
            request.ExpectedIntakeVersion,
            request.Actor,
            request.OperationKey,
            request.Reason);
        IntakeCommandValidation.RequireCase(
            request.CaseId,
            request.ExpectedCaseVersion,
            request.EditLeaseToken);
        return store.ReverseLinkAsync(request, timeProvider.GetUtcNow(), cancellationToken);
    }
}

internal static class IntakeCommandValidation
{
    public static void RequireStaffMutation(
        Guid receiptId,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        string reason)
    {
        if (receiptId == Guid.Empty)
        {
            throw new ArgumentException("An intake receipt identifier is required.", nameof(receiptId));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (operationKey.Length > 100)
        {
            throw new ArgumentException(
                "The operation key must be 100 characters or fewer.",
                nameof(operationKey));
        }
        if (reason.Trim().Length > 500)
        {
            throw new ArgumentException(
                "The reason must be 500 characters or fewer.",
                nameof(reason));
        }
    }

    public static void RequireCase(
        Guid caseId,
        long expectedVersion,
        string editLeaseToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(editLeaseToken);
        if (editLeaseToken.Length > CaseEditAuthority.LeaseTokenLength)
        {
            throw new ArgumentException(
                "The case edit lease token must be "
                + $"{CaseEditAuthority.LeaseTokenLength} characters or fewer.",
                nameof(editLeaseToken));
        }
    }
}
