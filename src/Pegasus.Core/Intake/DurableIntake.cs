using System.Diagnostics;
using System.Security.Cryptography;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Triage;
using Pegasus.Core.Notifications;
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

/// <param name="DeclaredCaseId">
/// The Case the uploading member of staff chose before the upload (Add
/// evidence on a Case page). Carried from the source to the processed receipt
/// so the Worker links the file there and runs no identification.
/// </param>
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
    DateTimeOffset StagedAtUtc,
    Guid? DeclaredCaseId = null);

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
    Unmatched = 3,

    /// <summary>
    /// The work item completed, but no source asset is retained under another
    /// key, so the staging blob may be the only copy of the source. It reads
    /// as Unmatched and is kept at any age (operator, 16 October 2026).
    /// </summary>
    CompletedSourceNotRetained = 4
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

/// <param name="Expired">
/// Orphan and Unmatched blobs deleted once past
/// <see cref="ReconcileStagedArtifacts.ExpiryGracePeriod"/>.
/// </param>
/// <param name="UnmatchedKept">
/// The part of <paramref name="Unmatched"/> kept at any age because the
/// blob may be the only copy of a completed item's source.
/// </param>
public sealed record ReconcileStagedArtifactsResult(
    int RecoveredWorkItems,
    int Completed,
    int Expired,
    int Retained,
    int Orphans,
    int Unmatched,
    int UnmatchedKept,
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

    /// <summary>The status of the source retained under this identity, or null when none was.</summary>
    Task<QueuedIntakeStatus?> FindBySourceIdentityAsync(
        IntakeSourceIdentity sourceIdentity,
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

    Task<ReceivedIntake> ExecuteStreamedAsync(
        StreamedIntakeSource source,
        string operationKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <see cref="ExecuteStreamedAsync"/> up to the commit: the receipt is
    /// durable but its work is not yet published, so a caller receiving several
    /// files can publish them together with <see cref="PublishCommittedAsync"/>.
    /// </summary>
    Task<ReceivedIntake> ReceiveStreamedAsync(
        StreamedIntakeSource source,
        string operationKey,
        CancellationToken cancellationToken = default);

    Task PublishCommittedAsync(
        ReceivedIntake received,
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
        // a Principal API submission is bounded by the request body that carries
        // it inline. One switch, one constant per channel, all of them owned by
        // IntakeEnvelopeLimits (C07 item 5).
        var maximumContentLength = source.SourceIdentity.Channel switch
        {
            IntakeSourceChannel.ManualUpload => IntakeEnvelopeLimits.MaximumContentLength,
            IntakeSourceChannel.Mailbox => IntakeEnvelopeLimits.MaximumMailboxContentLength,
            IntakeSourceChannel.Automation => IntakeEnvelopeLimits.MaximumContentLength,
            IntakeSourceChannel.PrincipalApi => IntakeEnvelopeLimits.MaximumPrincipalApiRequestLength,
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
            RequireSameDeclaredDestination(existing, source.DeclaredCaseId);

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
            nowUtc,
            source.DeclaredCaseId);
        var receivedIntake = await workStore.ReceiveAsync(stagedReceipt, operationKey, cancellationToken);
        await PublishCommittedAsync(receivedIntake, cancellationToken);
        return receivedIntake;
    }

    public async Task<ReceivedIntake> ExecuteStreamedAsync(StreamedIntakeSource source, string operationKey,
        CancellationToken cancellationToken = default)
    {
        var received = await ReceiveStreamedAsync(source, operationKey, cancellationToken);
        await PublishCommittedAsync(received, cancellationToken);
        return received;
    }

    public async Task<ReceivedIntake> ReceiveStreamedAsync(StreamedIntakeSource source, string operationKey,
        CancellationToken cancellationToken = default)
    {
        var safeFileName = ValidateStreamedSource(source, operationKey);
        var hash = await StreamHashAsync(source, cancellationToken);
        var existing = await workStore.FindBySourceIdentityAsync(
            source.SourceIdentity,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.SourceHash, hash, StringComparison.Ordinal))
            {
                throw new IntakeSourceIdentityConflictException(existing.SourceHash, hash);
            }
            RequireSameDeclaredDestination(existing, source.DeclaredCaseId);

            return await workStore.ReceiveAsync(existing, operationKey, cancellationToken);
        }

        var id = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        StagedArtifactInventoryItem staged;
        try
        {
            await using var content = await OpenReadableContentAsync(source, cancellationToken);
            staged = await artifactStore.StageAsync(
                id,
                hash,
                content,
                source.ContentLength,
                now,
                cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            throw new IntakeArtifactRetentionException(exception);
        }

        var receipt = new IntakeStagedReceipt(
            id,
            safeFileName,
            source.MediaType,
            source.ContentLength,
            hash,
            source.SourceIdentity,
            source.ReceivedAtUtc,
            source.Actor,
            staged.StorageKey,
            now,
            source.DeclaredCaseId);
        return await workStore.ReceiveAsync(receipt, operationKey, cancellationToken);
    }

    /// <summary>
    /// A replay of the same upload receipt must carry the same decision: the
    /// same token presented for a different Case is a second, different
    /// submission, and is refused like different bytes under one identity.
    /// </summary>
    private static void RequireSameDeclaredDestination(IntakeStagedReceipt existing, Guid? declaredCaseId)
    {
        if (existing.DeclaredCaseId != declaredCaseId)
        {
            throw new IntakeSourceIdentityConflictException();
        }
    }

    internal static async Task<string> StreamHashAsync(StreamedIntakeSource source, CancellationToken cancellationToken)
    {
        await using var stream = await OpenReadableContentAsync(source, cancellationToken);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total = checked(total + read);
            if (total > source.ContentLength)
            {
                throw new InvalidDataException("The uploaded length is invalid.");
            }

            hash.AppendData(buffer, 0, read);
        }

        if (total != source.ContentLength)
        {
            throw new InvalidDataException("The uploaded length is invalid.");
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    internal static string ValidateStreamedSource(
        StreamedIntakeSource source,
        string operationKey)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.OpenContentAsync);
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
        if (source.SourceIdentity.Channel != IntakeSourceChannel.ManualUpload)
        {
            throw new ArgumentOutOfRangeException(
                nameof(source),
                "Streamed intake is only supported for manual upload.");
        }

        if (source.ContentLength is <= 0 or > IntakeEnvelopeLimits.MaximumContentLength)
        {
            throw new InvalidDataException("The intake source exceeds its channel's size limit.");
        }

        return safeFileName;
    }

    private static async ValueTask<Stream> OpenReadableContentAsync(
        StreamedIntakeSource source,
        CancellationToken cancellationToken)
    {
        var stream = await source.OpenContentAsync(cancellationToken);
        if (stream is null || !stream.CanRead)
        {
            throw new InvalidDataException("The uploaded content stream is unavailable.");
        }

        return stream;
    }


    public Task PublishCommittedAsync(
        ReceivedIntake received,
        CancellationToken cancellationToken = default) =>
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
    ITriagePrincipalGate triagePrincipalGate,
    IAutomaticCaseAssociationStore caseAssociationStore,
    IAllocateIntake allocateIntake,
    TimeProvider timeProvider,
    IIntakeOcrOperationStore ocrOperations,
    Pegasus.Core.ImageIntake.IImageIntakeAutomation? imageIntakeAutomation = null,
    IRegisterUnidentified? registerUnidentified = null,
    ReconcileUnidentifiedDestinations? unidentifiedDestinations = null,
    AssociateRetainedMailWithCase? automaticMailCaseAssociation = null,
    IIntakeSubmissionGroupStore? submissionGroups = null,
    ICaseStaffNotifier? caseNotifier = null,
    PromoteAssociatedIntakeCaseEvidence? promoteAssociatedCaseEvidence = null,
    IIntakeMutationStore? mutationStore = null) : IProcessQueuedIntake
{
    private const string SystemActor = "system-worker:intake-processing";

    /// <summary>The reason every declared-destination link records (FRD-18 Add evidence).</summary>
    internal const string DeclaredDestinationReason = "Destination declared by staff on upload.";

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
            if (await IsDiscardedSubmissionMemberAsync(completedReceipt, cancellationToken))
            {
                // A manual group discard is a durable terminal submission
                // decision. A completed queue redelivery must retain its
                // source and receipt, but must not replay allocation,
                // association, image automation, or Unidentified work.
                return QueuedIntakeProcessingOutcome.NoOp;
            }
            if (await AssociateCaseIfUnambiguousAsync(
                    completedReceipt,
                    completedEvaluation,
                    cancellationToken))
            {
                completedReceipt = await ReloadAsync(completedReceipt, cancellationToken);
            }
            if (await AssociateRetainedMailAsync(completedReceipt, cancellationToken))
            {
                completedReceipt = await ReloadAsync(completedReceipt, cancellationToken);
            }
            var replayPromotion = await PromoteAssociatedCaseEvidenceAsync(
                completedReceipt, cancellationToken);

            // Completed redelivery replays destination operation identities.
            var replayAllocation = await allocateIntake.AttemptAutomaticAsync(
                completedReceipt,
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
                completedReceipt = await ReloadAsync(completedReceipt, cancellationToken);
            }

            var replayImageOutcome = await ApplyImageIntakeAutomationAsync(
                completedReceipt,
                cancellationToken);
            if (replayImageOutcome.AssociatedCase)
            {
                // Images filed on a matched Case: no record, so file them there.
                replayPromotion = await PromoteAssociatedCaseEvidenceAsync(
                    replayImageOutcome.Receipt, cancellationToken);
            }
            if (replayImageOutcome.AwaitingStaffDecision)
            {
                // Waiting for staff: nothing is held or made Unidentified, and
                // the sweep (not GroupPending) re-drives it at the window.
                return QueuedIntakeProcessingOutcome.NoOp;
            }
            completedReceipt = await RetainHoldingIfRequiredAsync(
                replayImageOutcome.Receipt, replayPromotion, cancellationToken);
            if (replayImageOutcome.GroupPending)
            {
                return QueuedIntakeProcessingOutcome.RetryScheduled;
            }

            await SynchronizeUnidentifiedAsync(
                completedReceipt,
                replayTriage,
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
                        content = await ReadVerifiedAsync(
                            stagedReceipt.StorageKey,
                            stagedReceipt.SourceHash,
                            cancellationToken);
                        durableStorageKey = await artifactStore.PromoteStagedAsync(
                            stagedReceipt.StorageKey,
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
                            stagedReceipt.SourceIdentity,
                            stagedReceipt.DeclaredCaseId),
                        durableStorageKey,
                        workItem.IsReevaluation,
                        isFinalAttempt,
                        cancellationToken);
                }
            }
            await BeginOcrOperationsAsync(processed, cancellationToken);
            evaluation = await workStore.RecordEvaluationAsync(
                workItem.Id,
                workItem.LeaseToken,
                processed.Id,
                timeProvider.GetUtcNow(),
                workItem.IsReevaluation,
                cancellationToken);

            TriageCreationOutcome triage;
            AutomaticCaseEvidencePromotionOutcome promotion;
            using (StartStage("association_and_allocation"))
            {
                // A destination the uploading member of staff declared is
                // their decision, made before the upload: it is recorded
                // first, so nothing below identifies, registers or holds
                // material that already has its Case.
                if (await AssociateDeclaredDestinationAsync(processed, stagedReceipt, cancellationToken))
                {
                    processed = await ReloadAsync(processed, cancellationToken);
                }
                if (await AssociateCaseIfUnambiguousAsync(processed, evaluation, cancellationToken))
                {
                    processed = await ReloadAsync(processed, cancellationToken);
                }
                if (await AssociateRetainedMailAsync(processed, cancellationToken))
                {
                    processed = await ReloadAsync(processed, cancellationToken);
                }
                promotion = await PromoteAssociatedCaseEvidenceAsync(processed, cancellationToken);

                var allocation = await allocateIntake.AttemptAutomaticAsync(
                    processed,
                    evaluation.Id,
                    cancellationToken);
                var allocated = allocation?.State.Status == IntakeAllocationProjectionStatus.Succeeded;
                triage = await CreateTriageIfQualifyingAsync(processed, evaluation, cancellationToken);
                if (allocated)
                {
                    // Allocation wrote CurrentCaseId durably; image automation must
                    // see the associated state rather than attempt a conflicting link.
                    processed = await ReloadAsync(processed, cancellationToken);
                }
            }

            var imageOutcome = await ApplyImageIntakeAutomationAsync(processed, cancellationToken);
            groupPending = imageOutcome.GroupPending;
            if (imageOutcome.AssociatedCase)
            {
                promotion = await PromoteAssociatedCaseEvidenceAsync(imageOutcome.Receipt, cancellationToken);
            }
            processed = imageOutcome.AwaitingStaffDecision
                ? imageOutcome.Receipt
                : await RetainHoldingIfRequiredAsync(imageOutcome.Receipt, promotion, cancellationToken);
            if (!imageOutcome.GroupPending && !imageOutcome.AwaitingStaffDecision)
            {
                await SynchronizeUnidentifiedAsync(
                    processed,
                    triage,
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
        if (!IntakeOcrOperations.IsEligibleSource(receipt))
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

        // One operation per scanned document. The instruction reader still
        // refuses to combine two scanned sources into one decision; the report
        // reader reads each document on its own, so every one is read.
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

        // Re-evaluation reads the retained source bytes the same way first
        // processing reads staging, integrity-checked. It needs no Box copy, so
        // it never makes one: the holding decision follows destination
        // automation here as on every other path.
        var content = await ReadVerifiedAsync(source.StorageKey, source.ContentHash, cancellationToken);
        return (content, source.StorageKey);
    }

    /// <summary>Staged or retained intake bytes whose SHA-256 is the recorded one; anything else fails closed.</summary>
    private async Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(
        string storageKey,
        string sha256,
        CancellationToken cancellationToken)
    {
        var content = await artifactStore.ReadAsync(storageKey, cancellationToken)
            ?? throw new IntakeArtifactIntegrityException();
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(content.Span)), sha256, StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }
        return content;
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
    /// The receipt as it is persisted now, after a step wrote to it. A link or
    /// an allocation moves the receipt's version, association and Case
    /// references, and no step returns them, so the row is read again rather
    /// than any of them being assumed.
    /// </summary>
    private async Task<IntakeReceipt> ReloadAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken) =>
        await receiptQueries.GetAsync(receipt.Id, cancellationToken) ?? receipt;

    private async Task<bool> IsDiscardedSubmissionMemberAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        if (submissionGroups is null || receipt.SourceIdentity.Channel != IntakeSourceChannel.ManualUpload)
        {
            return false;
        }

        var group = await submissionGroups.FindForMemberSourceAsync(
            receipt.SourceIdentity,
            cancellationToken);
        return group?.Discard is not null;
    }

    /// <summary>
    /// Persists the one holding outcome after destination automation, or resolves
    /// an existing receipt holding item to its now-established destination.
    /// </summary>
    private async Task SynchronizeUnidentifiedAsync(
        IntakeReceipt receipt,
        TriageCreationOutcome triage,
        RegisterUnidentifiedRequest? unidentifiedGroup,
        CancellationToken cancellationToken)
    {
        if (unidentifiedGroup is not null && registerUnidentified is not null)
        {
            await registerUnidentified.ExecuteAsync(unidentifiedGroup, cancellationToken);
            return;
        }

        // A receipt that already has its Case — a declared destination the
        // link above recorded — is never registered: it was deferred to this
        // caller precisely so the link could be tried first. Only images
        // become Unidentified; anything else waits where it arrived.
        if (registerUnidentified is not null
            && ProcessIntake.IsDeferredForAutomation(receipt)
            && ProcessIntake.BecomesUnidentified(receipt)
            && receipt.CurrentCaseId is null
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
    /// Records the Case the uploading member of staff declared before the
    /// upload (Add evidence on a Case page, FRD-18) as their association, in
    /// their name, so the evidence files onto that Case like any staff link.
    /// Only a receipt with no association of its own is linked; a later staff
    /// decision, a missing Case or an archived one leaves the receipt
    /// unlinked, and the ordinary path then holds it and registers it
    /// Unidentified for staff to place by hand. Returns whether the receipt's
    /// association changed.
    /// </summary>
    private async Task<bool> AssociateDeclaredDestinationAsync(
        IntakeReceipt receipt,
        IntakeStagedReceipt stagedReceipt,
        CancellationToken cancellationToken)
    {
        if (mutationStore is null
            || receipt.DeclaredCaseId is not { } declaredCaseId
            || receipt.ManualAssociationVersion is not null)
        {
            return false;
        }

        if (!IntakeActorIdentity.TryParseStaff(stagedReceipt.Actor, out var staffSubjectId))
        {
            Activity.Current?.SetTag("intake.declared_destination", "actor_not_staff");
            return false;
        }

        var outcome = await mutationStore.LinkDeclaredDestinationAsync(
            new(
                receipt.Id,
                declaredCaseId,
                staffSubjectId,
                $"declared-destination:{receipt.Id:N}",
                DeclaredDestinationReason),
            timeProvider.GetUtcNow(),
            cancellationToken);
        Activity.Current?.SetTag("intake.declared_destination", outcome.ToString());
        return outcome is DeclaredDestinationLinkOutcome.Linked or DeclaredDestinationLinkOutcome.AlreadyLinked;
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
        // Mailbox and Principal deliveries retain the existing automatic path.
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
        if (outcome == AutomaticCaseAssociationOutcome.Associated && caseNotifier is not null)
        {
            // Work Centre D10 cause 3: the Case's engineer learns an e-mail arrived.
            await caseNotifier.NotifyMailArrivalAsync(matchedCaseId, receipt.Id, null, cancellationToken);
        }

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

    private async Task<AutomaticCaseEvidencePromotionOutcome> PromoteAssociatedCaseEvidenceAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        if (promoteAssociatedCaseEvidence is null)
        {
            return AutomaticCaseEvidencePromotionOutcome.NotApplicable;
        }

        var outcome = await promoteAssociatedCaseEvidence.ExecuteAsync(receipt, cancellationToken);
        if (outcome == AutomaticCaseEvidencePromotionOutcome.Deferred)
        {
            // An active staff edit is a temporary Case-target guard. Keep the
            // existing intake work owner so the same receipt is retried after
            // its ordinary bounded delay instead of filing against a moving Case.
            throw new IntakeDependencyUnavailableException(
                "Automatic Case evidence promotion is waiting for the Case editor.");
        }

        return outcome;
    }

    /// <summary>
    /// Holds the receipt's files once its destination is known, and only when
    /// no automatic destination files them (<see cref="ProcessIntake.RequiresHolding"/>).
    /// An unconfirmed hand-over throws, so the work item keeps its bounded retry.
    /// </summary>
    private async Task<IntakeReceipt> RetainHoldingIfRequiredAsync(
        IntakeReceipt receipt,
        AutomaticCaseEvidencePromotionOutcome promotion,
        CancellationToken cancellationToken) =>
        ProcessIntake.RequiresHolding(receipt, promotion)
            ? await processIntake.RetainHoldingAssetsAsync(receipt, cancellationToken)
            : receipt;

    private async Task TryDeleteCompletedStagingAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await artifactStore.TryCompleteStagedAsync(storageKey, cancellationToken);
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
        PrincipalExistingCaseMatchException => PrincipalExistingCaseMatchException.FailureCode,
        _ => null
    };

    private static string TransientFailureCode(Exception exception) =>
        exception is IntakeArtifactRetentionException
            ? "artifact_retention_failure"
            : "intake_processing_failure";

    /// <summary>
    /// Opens qualifying Triage work under its evaluation identity. A missing
    /// registration, or a receipt whose Principal is not established, is
    /// Unidentified; a failed Triage write is a processing failure.
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

        // Principal identification comes before classification: a request is
        // opened as a Triage Case only once its Principal is established.
        if (await triagePrincipalGate.GetEstablishedPrincipalIdAsync(receipt.Id, cancellationToken) is null)
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

/// <summary>
/// Where the staged-artifact sweep resumes its listing, so blobs that are
/// never deleted cannot hold the first page for good. A singleton, because
/// the Worker builds the timer's function afresh for every tick; a restart
/// begins the listing again.
/// </summary>
public sealed class StagedArtifactSweepCursor
{
    public string? Continuation { get; set; }
}

public sealed class ReconcileStagedArtifacts(
    IIntakeWorkStore workStore,
    IStagedArtifactAuthority authority,
    IIntakeArtifactStore artifactStore,
    StagedArtifactSweepCursor cursor,
    TimeProvider timeProvider)
{
    /// <summary>
    /// How long after it was first seen an Orphan or Unmatched staging blob is
    /// kept before the sweep deletes it (operator, 16 October 2026). It covers
    /// the window between staging a blob and committing its receipt.
    /// </summary>
    public static readonly TimeSpan ExpiryGracePeriod = TimeSpan.FromHours(24);

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
        var page = await artifactStore.ListStagedAsync(
            maximumItems,
            cursor.Continuation,
            cancellationToken);
        cursor.Continuation = page.NextContinuation;
        var completed = 0;
        var expired = 0;
        var retained = 0;
        var orphans = 0;
        var unmatched = 0;
        var unmatchedKept = 0;
        var failures = 0;

        foreach (var item in page.Items)
        {
            try
            {
                var durable = await authority.FindAsync(item.StorageKey, cancellationToken);
                var (target, kept) = Classify(item, durable);
                var expires = target is StagedArtifactDisposition.Orphan or StagedArtifactDisposition.Unmatched
                    && !kept
                    && nowUtc - item.FirstSeenAtUtc >= ExpiryGracePeriod;
                var current = item;
                // An expiring blob is set even when it lists with its target: a
                // listed Unmatched may carry another tag, which the delete's tag
                // condition would refuse on every run.
                if (current.Disposition != target || expires)
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
                    case StagedArtifactDisposition.Orphan or StagedArtifactDisposition.Unmatched when expires:
                        if (!await artifactStore.DeleteStagedAsync(
                                current.StorageKey,
                                current.ConcurrencyToken,
                                target,
                                cancellationToken))
                        {
                            failures++;
                        }
                        else if (expires)
                        {
                            expired++;
                        }
                        else
                        {
                            completed++;
                        }
                        break;
                    case StagedArtifactDisposition.Orphan:
                        orphans++;
                        break;
                    case StagedArtifactDisposition.Unmatched:
                        unmatched++;
                        if (kept)
                        {
                            unmatchedKept++;
                        }
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
            expired,
            retained,
            orphans,
            unmatched,
            unmatchedKept,
            failures);
    }

    /// <summary>
    /// The blob's target disposition, and whether it is kept at any age
    /// because it may be the only copy of a completed item's source.
    /// </summary>
    private static (StagedArtifactDisposition Target, bool Kept) Classify(
        StagedArtifactInventoryItem item,
        StagedArtifactAuthority? durable)
    {
        if (durable is null)
        {
            return (StagedArtifactDisposition.Orphan, false);
        }

        if (!string.Equals(
                item.ContentHash,
                durable.ExpectedContentHash,
                StringComparison.Ordinal)
            || item.ContentLength != durable.ExpectedContentLength)
        {
            return (StagedArtifactDisposition.Unmatched, false);
        }

        return durable.State switch
        {
            StagedArtifactAuthorityState.Pending => (StagedArtifactDisposition.Pending, false),
            StagedArtifactAuthorityState.Failed => (StagedArtifactDisposition.Failed, false),
            StagedArtifactAuthorityState.Completed => (StagedArtifactDisposition.Completed, false),
            StagedArtifactAuthorityState.Unmatched => (StagedArtifactDisposition.Unmatched, false),
            StagedArtifactAuthorityState.CompletedSourceNotRetained => (StagedArtifactDisposition.Unmatched, true),
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

/// <summary>
/// The one owner of a staff link (Add to an existing case on Upload, Link to
/// Case on an Unidentified item, the Inbox's link): the association, which
/// commits here, and the receipt's Unidentified item, which the link resolves
/// through <see cref="ReconcileUnidentifiedDestinations"/>. Filing the linked
/// material on the Case continues in the Worker: the link enqueues the
/// receipt's staged work, whose replay files through
/// <see cref="PromoteAssociatedIntakeCaseEvidence"/> (FRD-22, FRD-02). The
/// request never touches Box or the intake assets.
/// </summary>
public sealed class LinkIntake(
    IIntakeMutationStore store,
    IImageIntakeCasePairing casePairing,
    TimeProvider timeProvider,
    ICaseStaffNotifier? caseNotifier = null,
    IIntakeWorkStore? workStore = null,
    IIntakeWorkEnqueuer? workEnqueuer = null,
    IIntakeReceiptQueries? receiptQueries = null,
    ReconcileUnidentifiedDestinations? unidentifiedDestinations = null) : ILinkIntake
{
    public async Task ExecuteAsync(
        LinkIntakeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntakeCommandValidation.RequireStaffLinkMutation(
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
        if (caseNotifier is not null)
        {
            // Work Centre D10 cause 3: the Case's engineer learns an e-mail (or a
            // query) arrived, unless they linked it themself.
            await caseNotifier.NotifyMailArrivalAsync(request.CaseId, request.ReceiptId, request.Actor, cancellationToken);
        }

        // The reasoned link committed. The same observable owner completes
        // untouched group members and merge, or leaves durable timer recovery.
        var pairing = await casePairing.PairRegisteredReceiptAsync(request.ReceiptId, cancellationToken);
        Activity.Current?.SetTag("image_intake.pairing_failures", pairing.Failures);
        Activity.Current?.SetTag("image_intake.failure_type", pairing.FirstFailure);
        if (pairing.Failures > 0)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, "image_pairing_failed");
        }

        if (request.SettleUnidentified)
        {
            await ResolveUnidentifiedAsync(request.ReceiptId, cancellationToken);
        }

        await HandFilingToWorkerAsync(request.ReceiptId, cancellationToken);
    }

    /// <summary>
    /// Resolves the receipt's Unidentified item to the Case it is now linked
    /// to, reading the receipt afresh so the committed link is in hand. The
    /// link stands whatever happens here, including a conflicting operation
    /// key; the Worker's replay and the sweep remain the backstop.
    /// </summary>
    private async Task ResolveUnidentifiedAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        if (receiptQueries is null || unidentifiedDestinations is null)
        {
            return;
        }

        try
        {
            if (await receiptQueries.GetAsync(receiptId, cancellationToken) is { } receipt)
            {
                await unidentifiedDestinations.SynchronizeForReceiptAsync(receipt, cancellationToken);
                Activity.Current?.SetTag("intake.link_unidentified", "synchronized");
            }
        }
        catch (Exception exception) when (exception is UnidentifiedOperationConflictException
            || IntakeExceptionPolicy.IsRecoverable(exception))
        {
            Activity.Current?.SetTag("intake.link_unidentified", "failed");
        }
    }

    /// <summary>
    /// The link stands whatever happens here; the sweeps remain the backstop
    /// when the hand-over cannot be enqueued.
    /// </summary>
    private async Task HandFilingToWorkerAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        if (workStore is null || workEnqueuer is null)
        {
            return;
        }

        try
        {
            var stagedReceiptId = await workStore.FindStagedReceiptIdForReceiptAsync(receiptId, cancellationToken);
            if (stagedReceiptId is { } id)
            {
                await workEnqueuer.EnqueueAsync(id, cancellationToken);
                Activity.Current?.SetTag("intake.link_filing_retry", "enqueued");
            }
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            Activity.Current?.SetTag("intake.link_filing_retry", "failed");
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

    /// <summary>
    /// An initial staff association is an explicit destination decision, but
    /// its reason is optional. The persistence owner distinguishes a first
    /// association from a relink and retains the established relink reason
    /// requirement where an association already exists.
    /// </summary>
    public static void RequireStaffLinkMutation(
        Guid receiptId,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        string? reason)
    {
        if (receiptId == Guid.Empty)
        {
            throw new ArgumentException("An intake receipt identifier is required.", nameof(receiptId));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        if (operationKey.Length > 100)
        {
            throw new ArgumentException(
                "The operation key must be 100 characters or fewer.",
                nameof(operationKey));
        }
        if (reason?.Trim().Length > 500)
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
