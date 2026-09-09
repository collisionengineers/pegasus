using System.Diagnostics;
using System.Security.Cryptography;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake.Unidentified;
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
    bool IsReevaluation = false,
    bool HasPendingEvaluation = false);

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
    int RecoveredWorkItems,
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

/// <param name="RetryDueAtUtc">
/// When a transient failure has already been given its next attempt, the time
/// that attempt is due. Set only while the work item is retry-scheduled, so a
/// surface can say how long the work genuinely cannot progress for instead of
/// polling as if it were about to move.
/// </param>
public sealed record QueuedIntakeStatus(
    Guid StagedReceiptId,
    string SourceFileName,
    DateTimeOffset ReceivedAtUtc,
    QueuedIntakeStatusKind Status,
    Guid? ProcessedReceiptId,
    string? FailureCode,
    DateTimeOffset? RetryDueAtUtc = null);

public static class QueuedIntakeStatusKinds
{
    /// <summary>
    /// The staff-facing state of a work item. Everything before the work has
    /// been picked up reads as Received: staff are told the file is safe and
    /// waiting, not which internal queue step it is on. Once it has been
    /// picked up it reads as Processing, and a transient failure that has
    /// already been given its next attempt stays Processing — the work is in
    /// hand and still moving, so reporting it as freshly Received would be
    /// untrue.
    /// </summary>
    public static QueuedIntakeStatusKind FromWorkState(IntakeWorkState state) => state switch
    {
        IntakeWorkState.Pending
            or IntakeWorkState.Dispatching
            or IntakeWorkState.Dispatched => QueuedIntakeStatusKind.Received,
        IntakeWorkState.RetryScheduled
            or IntakeWorkState.Processing => QueuedIntakeStatusKind.Processing,
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
    /// Claims one known committed receipt for publication. This is the
    /// post-commit fast path; it must not scan or publish another receipt.
    /// </summary>
    Task<IntakeWorkItem?> ClaimDispatchAsync(
        Guid stagedReceiptId,
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

    Task<IntakeEvaluationRevision> RecordEvaluationAsync(
        Guid workItemId,
        string leaseToken,
        Guid processedReceiptId,
        DateTimeOffset completedAtUtc,
        bool isReevaluation,
        CancellationToken cancellationToken);

    Task CompleteProcessingAsync(
        Guid workItemId,
        string leaseToken,
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

    Task<int> RecoverInterruptedWorkAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset staleDispatchedBeforeUtc,
        int maximumItems,
        CancellationToken cancellationToken);

    Task ScheduleReevaluationAsync(
        Guid stagedReceiptId,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// The staged receipt id for a persisted intake receipt's latest
    /// evaluation, or null when none is retained. Read-only: the
    /// reconciliation sweep only has the receipt on hand and needs the
    /// staged receipt id to re-drive <see cref="IProcessQueuedIntake"/>, but
    /// must never move a completed work item back to a claimable state (that
    /// would force a re-claim through the artifact-reading path, whose
    /// staged copy is already deleted once a receipt has completed once).
    /// Mirrors the join <c>EfIntakeMutationStore.ScheduleReevaluationAsync</c>
    /// performs inline for the staff-facing reevaluation command.
    /// </summary>
    Task<Guid?> FindStagedReceiptIdForReceiptAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken);
}

public interface IIntakeWorkEnqueuer
{
    Task EnqueueAsync(Guid stagedReceiptId, CancellationToken cancellationToken);
}

/// <summary>
/// The post-commit intake publication boundary. Composition must provide it;
/// callers may not silently acknowledge committed work without attempting its
/// queue publication.
/// </summary>
public interface ICommittedIntakeWorkPublisher
{
    Task PublishAsync(Guid stagedReceiptId, CancellationToken cancellationToken);
}

public sealed class ReceiveIntake(
    IIntakeArtifactStore artifactStore,
    IIntakeWorkStore workStore,
    TimeProvider timeProvider,
    ICommittedIntakeWorkPublisher committedWorkPublisher) : IIntakeSubmission
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
        // the form takes one file, a mailbox message carries the whole job, and
        // a Provider API submission is bounded by the request body that carries
        // it inline. One switch, one constant per channel, all of them owned by
        // IntakeEnvelopeLimits (C07 item 5, residual INTK-052).
        var maximumContentLength = source.SourceIdentity.Channel switch
        {
            IntakeSourceChannel.ManualUpload => IntakeEnvelopeLimits.MaximumContentLength,
            IntakeSourceChannel.Mailbox => IntakeEnvelopeLimits.MaximumMailboxContentLength,
            IntakeSourceChannel.Automation => IntakeEnvelopeLimits.MaximumContentLength,
            IntakeSourceChannel.ProviderApi => IntakeEnvelopeLimits.MaximumProviderApiRequestLength,
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

            var received = await workStore.ReceiveAsync(existing, operationKey, cancellationToken);
            await PublishCommittedAsync(received, cancellationToken);
            return received;
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
        var receivedIntake = await workStore.ReceiveAsync(stagedReceipt, operationKey, cancellationToken);
        await PublishCommittedAsync(receivedIntake, cancellationToken);
        return receivedIntake;
    }

    private Task PublishCommittedAsync(
        ReceivedIntake received,
        CancellationToken cancellationToken) =>
        committedWorkPublisher.PublishAsync(received.StagedReceiptId, cancellationToken);

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
    TimeProvider timeProvider) : ICommittedIntakeWorkPublisher
{
    private static readonly TimeSpan DispatchLeaseDuration = TimeSpan.FromMinutes(1);
    private static readonly ActivitySource Telemetry = new("Pegasus.Core.Intake");

    /// <summary>
    /// Attempts publication for one already-committed receipt. A transport
    /// failure leaves the durable row due for the recovery sweep, while the
    /// caller keeps its truthful committed acknowledgement.
    /// </summary>
    public async Task ExecuteCommittedAsync(
        Guid stagedReceiptId,
        CancellationToken cancellationToken = default)
    {
        if (stagedReceiptId == Guid.Empty)
        {
            throw new ArgumentException("A staged receipt identifier is required.", nameof(stagedReceiptId));
        }

        using var activity = Telemetry.StartActivity("publish_committed_intake_work");
        activity?.SetTag("intake.staged_receipt_id", stagedReceiptId);
        activity?.SetTag("intake.publication.path", "immediate");

        var workItem = await workStore.ClaimDispatchAsync(
            stagedReceiptId,
            timeProvider.GetUtcNow(),
            DispatchLeaseDuration,
            cancellationToken);
        if (workItem is null)
        {
            activity?.SetTag("intake.publication.outcome", "already_claimed_or_complete");
            return;
        }

        if (workItem.LeaseToken is null)
        {
            throw new InvalidOperationException("A claimed intake work item must have a lease token.");
        }

        try
        {
            await workEnqueuer.EnqueueAsync(workItem.StagedReceiptId, cancellationToken);
            await workStore.MarkDispatchedAsync(
                workItem.Id,
                workItem.LeaseToken,
                timeProvider.GetUtcNow(),
                cancellationToken);
            activity?.SetTag("intake.publication.outcome", "published");
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            activity?.SetTag("intake.publication.enqueue_error", exception.GetType().Name);
            try
            {
                await workStore.ReleaseDispatchAsync(
                    workItem.Id,
                    workItem.LeaseToken,
                    timeProvider.GetUtcNow(),
                    CancellationToken.None);
                activity?.SetTag("intake.publication.outcome", "enqueue_failed_released");
            }
            catch (Exception releaseException) when (IntakeExceptionPolicy.IsRecoverable(releaseException))
            {
                // The lease expires even if this best-effort release cannot be
                // persisted. The committed receipt remains acknowledged and
                // the recovery sweep reclaims it after the lease.
                activity?.SetTag("intake.publication.release_error", releaseException.GetType().Name);
                activity?.SetTag("intake.publication.outcome", "enqueue_failed_lease_expiry_recovery");
            }

            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
        }
    }

    public Task PublishAsync(Guid stagedReceiptId, CancellationToken cancellationToken) =>
        ExecuteCommittedAsync(stagedReceiptId, cancellationToken);

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

/// <summary>
/// One staged receipt's durable processing entry point. The interface exists
/// so <see cref="ReconcileGroupedImageIntake"/> can re-drive an
/// already-completed receipt (the safe replay branch of
/// <see cref="ProcessQueuedIntake.ExecuteAsync"/>) without depending on every
/// concrete adapter <see cref="ProcessQueuedIntake"/> itself requires.
/// </summary>
public interface IProcessQueuedIntake
{
    Task<QueuedIntakeProcessingOutcome> ExecuteAsync(
        Guid stagedReceiptId,
        CancellationToken cancellationToken = default);
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
    IReadLogicalDocumentVersion retainedContentReader,
    IIntakeOcrOperationStore ocrOperations,
    Pegasus.Core.ImageIntake.IImageIntakeAutomation? imageIntakeAutomation = null,
    IRegisterUnidentified? registerUnidentified = null,
    ReconcileUnidentifiedDestinations? unidentifiedDestinations = null,
    AssociateRetainedMailWithCase? automaticMailCaseAssociation = null,
    SubmitMailboxImageIntake? mailboxImageIntake = null) : IProcessQueuedIntake
{
    private const string SystemActor = "system-worker:intake-processing";

    /// <summary>
    /// The same intake system worker as <see cref="SystemActor"/>, typed, for the
    /// commands that carry an <see cref="ActionActor"/>. Triage records the actor
    /// kind, so the subject is the bare worker identity and the kind is carried
    /// rather than spelled into a prefix.
    /// </summary>
    private static readonly ActionActor SystemWorkerActor =
        ActionActor.SystemWorker("intake-processing");

    private static readonly ActivitySource Telemetry = new("Pegasus.Core.Intake");
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
        (IntakeWorkItem WorkItem, IntakeStagedReceipt Receipt)? claimed;
        using (StartStage("queue_claim"))
        {
            claimed = await workStore.ClaimProcessingAsync(
                stagedReceiptId,
                timeProvider.GetUtcNow(),
                ProcessingLeaseDuration,
                cancellationToken);
        }
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
            if (await AssociateRetainedMailAsync(completedReceipt, cancellationToken))
            {
                completedReceipt = await receiptQueries.GetAsync(
                    completedEvaluation.ProcessedReceiptId,
                    cancellationToken) ?? completedReceipt;
            }

            // Completed redelivery replays destination operation identities.
            var replayAllocation = await allocateIntake.AttemptAutomaticAsync(
                completedReceipt.Id,
                completedEvaluation.Id,
                cancellationToken);
            var replayAllocated =
                replayAllocation?.State.Status == IntakeAllocationProjectionStatus.Succeeded;
            var replayTriage = await CreateTriageIfQualifyingAsync(
                completedReceipt,
                completedEvaluation,
                cancellationToken);
            if (replayAllocated)
            {
                completedReceipt = await receiptQueries.GetAsync(
                    completedEvaluation.ProcessedReceiptId,
                    cancellationToken) ?? completedReceipt;
            }

            var replayImageOutcome = await ApplyImageIntakeAutomationAsync(
                completedReceipt,
                cancellationToken);
            completedReceipt = replayImageOutcome.Receipt;
            if (replayImageOutcome.GroupPending)
            {
                return QueuedIntakeProcessingOutcome.RetryScheduled;
            }

            var replayMailboxImagesHandled = mailboxImageIntake is not null
                && await mailboxImageIntake.HasSubmissionAsync(completedReceipt, cancellationToken);
            await SynchronizeUnidentifiedAsync(
                completedReceipt,
                replayTriage,
                replayMailboxImagesHandled,
                replayImageOutcome.UnidentifiedGroup,
                cancellationToken);
            return QueuedIntakeProcessingOutcome.NoOp;
        }

        var (workItem, stagedReceipt) = claimed.Value;
        if (workItem.LeaseToken is null)
        {
            throw new InvalidOperationException("A claimed intake work item must have a lease token.");
        }

        IntakeReceipt processed;
        IntakeEvaluationRevision evaluation;
        var mailboxImagesHandled = false;
        var groupPending = false;
        try
        {
            if (workItem.HasPendingEvaluation)
            {
                processed = await receiptQueries.GetAsync(workItem.ProcessedReceiptId!.Value, cancellationToken)
                    ?? throw new InvalidDataException("The pending evaluation receipt is missing.");
            }
            else
            {
                ReadOnlyMemory<byte> content;
                string durableStorageKey;
                using (StartStage("artifact_read_and_retain"))
                {
                    if (workItem.IsReevaluation)
                    {
                        (content, durableStorageKey) = await ReadRetainedSourceAsync(
                            workItem,
                            stagedReceipt,
                            cancellationToken);
                    }
                    else
                    {
                        content = await artifactStore.ReadAsync(stagedReceipt.StorageKey, cancellationToken)
                            ?? throw new IntakeArtifactIntegrityException();
                        var actualHash = Convert.ToHexString(SHA256.HashData(content.Span));
                        if (!string.Equals(actualHash, stagedReceipt.SourceHash, StringComparison.Ordinal))
                        {
                            throw new IntakeArtifactIntegrityException();
                        }

                        durableStorageKey = await artifactStore.StoreAsync(
                            stagedReceipt.SourceHash,
                            content,
                            cancellationToken);
                    }
                }
                // Mirrors the terminal check below: once this attempt is the last
                // one the retry schedule allows, a transient reader fault must be
                // recorded as a terminal technical-failure receipt (and registered
                // Unidentified) here rather than deferred to a retry that will
                // never happen.
                var isFinalAttempt = workItem.AttemptCount >= RetryDelays.Length;
                using (StartStage("source_read_and_process"))
                {
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
                        isFinalAttempt,
                        cancellationToken);
                }
            }
            await BeginOcrOperationsAsync(processed, cancellationToken);
            if (mailboxImageIntake is not null)
            {
                mailboxImagesHandled = await mailboxImageIntake.ExecuteAsync(
                    processed,
                    workItem.AttemptCount >= RetryDelays.Length,
                    cancellationToken);
            }
            evaluation = await workStore.RecordEvaluationAsync(
                workItem.Id,
                workItem.LeaseToken,
                processed.Id,
                timeProvider.GetUtcNow(),
                workItem.IsReevaluation,
                cancellationToken);

            TriageCreationOutcome triage;
            using (StartStage("association_and_allocation"))
            {
                var associated = await AssociateCaseIfUnambiguousAsync(processed, evaluation, cancellationToken);
                if (associated)
                {
                    processed = await receiptQueries.GetAsync(processed.Id, cancellationToken) ?? processed;
                }
                if (await AssociateRetainedMailAsync(processed, cancellationToken))
                {
                    processed = await receiptQueries.GetAsync(processed.Id, cancellationToken) ?? processed;
                }

                var allocation = await allocateIntake.AttemptAutomaticAsync(
                    processed.Id,
                    evaluation.Id,
                    cancellationToken);
                var allocated = allocation?.State.Status == IntakeAllocationProjectionStatus.Succeeded;
                triage = await CreateTriageIfQualifyingAsync(processed, evaluation, cancellationToken);
                if (allocated)
                {
                    // Allocation wrote CurrentCaseId durably; image automation must
                    // see the associated state rather than attempt a conflicting link.
                    processed = await receiptQueries.GetAsync(processed.Id, cancellationToken) ?? processed;
                }
            }

            var imageOutcome = await ApplyImageIntakeAutomationAsync(processed, cancellationToken);
            groupPending = imageOutcome.GroupPending;
            processed = imageOutcome.Receipt;
            if (!imageOutcome.GroupPending)
            {
                await SynchronizeUnidentifiedAsync(
                    processed,
                    triage,
                    mailboxImagesHandled,
                    imageOutcome.UnidentifiedGroup,
                    cancellationToken);
            }

            // A pending image group has its own durable group reconciliation owner.
            // All writes for this evaluation must succeed before acknowledging work.
            await workStore.CompleteProcessingAsync(
                workItem.Id,
                workItem.LeaseToken,
                timeProvider.GetUtcNow(),
                cancellationToken);
        }
        catch (Exception exception) when (TerminalInputFailureCode(exception) is { } failureCode)
        {
            await FailProcessingAsync(workItem, terminal: true, failureCode, cancellationToken);
            return QueuedIntakeProcessingOutcome.Failed;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsTransientFailure(exception))
        {
            var terminal = workItem.AttemptCount >= RetryDelays.Length;
            await FailProcessingAsync(workItem, terminal, TransientFailureCode(exception), cancellationToken);
            return terminal ? QueuedIntakeProcessingOutcome.Failed : QueuedIntakeProcessingOutcome.RetryScheduled;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            await FailProcessingAsync(workItem, terminal: true, "unexpected_intake_processing_failure", cancellationToken);
            throw;
        }

        await TryDeleteCompletedStagingAsync(
            stagedReceipt.StorageKey,
            cancellationToken);
        return groupPending ? QueuedIntakeProcessingOutcome.RetryScheduled : QueuedIntakeProcessingOutcome.Completed;
    }

    private async Task BeginOcrOperationsAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        if (!IntakeOcrOperations.IsEligibleIncomingInstruction(receipt))
        {
            return;
        }

        var qualifiedSources = receipt.ScannedPdfPages
            .GroupBy(candidate => candidate.SourceLabel, StringComparer.Ordinal)
            .Select(candidates => new
            {
                Asset = IntakeOcrOperations.ResolveQualifiedAsset(receipt, candidates.Key),
                Pages = candidates.Select(candidate => candidate.PageNumber).ToArray()
            })
            .ToArray();
        if (qualifiedSources.Any(source => source.Asset is null))
        {
            throw new InvalidDataException(
                "An OCR-qualified source does not identify its retained asset.");
        }

        // One instruction analysis can incorporate OCR from one immutable
        // retained source. Never queue side effects for a multi-source scan
        // that the analysis boundary will correctly refuse for staff review.
        if (qualifiedSources.Select(source => source.Asset!.Id).Distinct().Skip(1).Any())
        {
            return;
        }

        foreach (var source in qualifiedSources)
        {
            await IntakeOcrOperations.BeginAsync(
                ocrOperations,
                receipt.Id,
                source.Asset!,
                source.Pages,
                cancellationToken);
        }
    }

    private async Task<(ReadOnlyMemory<byte> Content, string StorageKey)> ReadRetainedSourceAsync(
        IntakeWorkItem workItem,
        IntakeStagedReceipt stagedReceipt,
        CancellationToken cancellationToken)
    {
        if (workItem.ProcessedReceiptId is not { } receiptId)
        {
            throw new IntakeArtifactIntegrityException();
        }

        var receipt = await receiptQueries.GetAsync(receiptId, cancellationToken)
            ?? throw new IntakeArtifactIntegrityException();
        var sources = receipt.AssetRecords
            .Where(asset => asset.Kind == IntakeAssetKind.Source
                && asset.Disposition == IntakeAssetDisposition.Source)
            .Take(2)
            .ToArray();
        if (sources.Length != 1)
        {
            throw new IntakeArtifactIntegrityException();
        }

        var source = sources[0];
        if (source.ContentLength != receipt.SourceLength
            || source.ContentLength != stagedReceipt.SourceLength
            || !string.Equals(source.ContentHash, receipt.SourceHash, StringComparison.Ordinal)
            || !string.Equals(source.ContentHash, stagedReceipt.SourceHash, StringComparison.Ordinal)
            || source.ContentLength > int.MaxValue)
        {
            throw new IntakeArtifactIntegrityException();
        }

        try
        {
            await using var logical = await retainedContentReader.OpenAsync(
                new(
                    SystemWorkerActor,
                    DocumentId: null,
                    VersionId: null,
                    IntakeAssetId: source.Id,
                    CaseId: receipt.CurrentCaseId,
                    IntakeReceiptId: receipt.Id,
                    ExpectedSha256: source.ContentHash,
                    ExpectedContentLength: source.ContentLength),
                cancellationToken);
            var bytes = GC.AllocateUninitializedArray<byte>(checked((int)source.ContentLength));
            await logical.Content.ReadExactlyAsync(bytes, cancellationToken);
            return (bytes, source.StorageKey);
        }
        catch (Exception exception) when (exception is FileNotFoundException
            or InvalidDataException
            or UnauthorizedAccessException)
        {
            throw new IntakeArtifactIntegrityException();
        }
    }

    private static Activity? StartStage(string stage)
    {
        var activity = Telemetry.StartActivity($"intake.{stage}", ActivityKind.Internal);
        activity?.SetTag("intake.stage", stage);
        return activity;
    }

    /// <summary>
    /// Runs destination automation after its evaluation is recorded. Write failures
    /// remain owned by the intake work item; a pending group is owned by the
    /// bounded grouped-image reconciliation sweep.
    /// </summary>
    private async Task<Pegasus.Core.ImageIntake.ImageIntakeAutomationOutcome> ApplyImageIntakeAutomationAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        if (imageIntakeAutomation is null)
        {
            return new(receipt);
        }

        return await imageIntakeAutomation.ApplyAsync(receipt, cancellationToken);
    }

    /// <summary>
    /// Persists the one holding outcome after destination automation, or resolves
    /// an existing receipt holding item to its now-established destination.
    /// </summary>
    private async Task SynchronizeUnidentifiedAsync(
        IntakeReceipt receipt,
        TriageCreationOutcome triage,
        bool mailboxImagesHandled,
        RegisterUnidentifiedRequest? unidentifiedGroup,
        CancellationToken cancellationToken)
    {
        if (unidentifiedGroup is not null && registerUnidentified is not null)
        {
            await registerUnidentified.ExecuteAsync(unidentifiedGroup, cancellationToken);
            return;
        }

        if (registerUnidentified is not null
            && ProcessIntake.IsDeferredForAutomation(receipt)
            && !mailboxImagesHandled
            && !(ProcessIntake.IsTriageRequest(receipt)
                && triage is not TriageCreationOutcome.NotQualifying))
        {
            await registerUnidentified.ExecuteAsync(
                ProcessIntake.BuildUnidentifiedRegistrationRequest(receipt),
                cancellationToken);

            return;
        }

        if (unidentifiedDestinations is null)
        {
            return;
        }

        await unidentifiedDestinations.SynchronizeForReceiptAsync(receipt, cancellationToken);
    }

    /// <summary>
    /// Applies the recorded unique match. Persistence failures propagate to the
    /// durable processing retry owner, never to the new-case allocation branch.
    /// </summary>
    private async Task<bool> AssociateCaseIfUnambiguousAsync(
        IntakeReceipt receipt,
        IntakeEvaluationRevision evaluation,
        CancellationToken cancellationToken)
    {
        // A file placed into Pegasus by a member of staff is deliberately not
        // consent to a recorded match. It stays available for the upload
        // confirmation surface, even where matching found exactly one Case.
        // Mailbox and provider deliveries retain the existing automatic path.
        if (receipt.SourceIdentity.Channel == IntakeSourceChannel.ManualUpload)
        {
            return false;
        }

        if (receipt.CaseMatchDecision is not
            { Outcome: CaseMatchOutcome.UniqueMatch, MatchedCaseId: { } matchedCaseId } decision)
        {
            return false;
        }

        if (receipt.CurrentCaseId is not null)
        {
            return false;
        }

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
        return outcome is AutomaticCaseAssociationOutcome.Associated or AutomaticCaseAssociationOutcome.AlreadyAssociated;
    }

    private async Task<bool> AssociateRetainedMailAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        if (automaticMailCaseAssociation is null || receipt.CurrentCaseId is not null)
        {
            return false;
        }

        var outcome = await automaticMailCaseAssociation.ExecuteAsync(
            receipt.Id,
            cancellationToken);
        return outcome is AutomaticCaseAssociationOutcome.Associated or AutomaticCaseAssociationOutcome.AlreadyAssociated;
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
        // API-01's existing-Case rejection is a property of the submitted
        // facts, not a fault: a redelivery would reach the same conclusion, so
        // it fails on the first attempt under its own code with no backoff.
        ProviderExistingCaseMatchException => ProviderExistingCaseMatchException.FailureCode,
        _ => null
    };

    private static string TransientFailureCode(Exception exception) =>
        exception is IntakeArtifactRetentionException
            ? "artifact_retention_failure"
            : "intake_processing_failure";

    /// <summary>
    /// Opens qualifying Triage work under its evaluation identity. A missing
    /// registration is Unidentified; a failed Triage write is a processing failure.
    /// </summary>
    private async Task<TriageCreationOutcome> CreateTriageIfQualifyingAsync(
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
            return TriageCreationOutcome.NotQualifying;
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
                SystemWorkerActor,
                $"triage-from-intake-evaluation:{evaluation.Id:N}"),
            cancellationToken);
        return TriageCreationOutcome.Created;
    }
}

/// <summary>
/// Whether the receipt qualified for Triage. Write failures propagate.
/// </summary>
internal enum TriageCreationOutcome
{
    NotQualifying,
    Created
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

        var nowUtc = timeProvider.GetUtcNow();
        var recoveredWorkItems = await workStore.RecoverInterruptedWorkAsync(
            nowUtc,
            nowUtc - TimeSpan.FromMinutes(1),
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
            recoveredWorkItems,
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

        // The reasoned link committed. The same observable owner completes
        // untouched group members and merge, or leaves durable timer recovery.
        var pairing = await casePairing.PairRegisteredReceiptAsync(request.ReceiptId, cancellationToken);
        Activity.Current?.SetTag("image_intake.pairing_failures", pairing.Failures);
        Activity.Current?.SetTag("image_intake.failure_type", pairing.FirstFailure);
        if (pairing.Failures > 0)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, "image_pairing_failed");
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
