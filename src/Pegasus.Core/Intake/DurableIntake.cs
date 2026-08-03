using System.Security.Cryptography;
using Pegasus.Core.Identity;
using Pegasus.Core.Triage;

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

public sealed record IntakeEvaluationRevision(
    Guid Id,
    Guid StagedReceiptId,
    Guid ProcessedReceiptId,
    int Revision,
    DateTimeOffset EvaluatedAtUtc);

public enum IntakeSubmissionDisposition
{
    Processed = 1,
    Queued = 2
}

public sealed record IntakeSubmissionResult(
    Guid ReceiptId,
    bool IsDuplicate,
    IntakeSubmissionDisposition Disposition);

public interface IIntakeSubmission
{
    Task<IntakeSubmissionResult> ExecuteAsync(
        IntakeSource source,
        string operationKey,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessIntakeSubmission(
    ReceiveIntake receiveIntake,
    ProcessQueuedIntake processQueuedIntake,
    IIntakeWorkStore workStore) : IIntakeSubmission
{
    public async Task<IntakeSubmissionResult> ExecuteAsync(
        IntakeSource source,
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);

        var received = await receiveIntake.ExecuteInlineAsync(
            source,
            operationKey,
            cancellationToken);
        await processQueuedIntake.ExecuteAsync(received.StagedReceiptId, cancellationToken);
        var evaluation = await workStore.GetCompletedEvaluationAsync(
            received.StagedReceiptId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Inline intake processing did not persist a completed evaluation revision.");
        return new(
            evaluation.ProcessedReceiptId,
            received.IsDuplicate,
            IntakeSubmissionDisposition.Processed);
    }
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

    Task<ReceivedIntake> ReceiveForProcessingAsync(
        IntakeStagedReceipt receipt,
        string operationKey,
        CancellationToken cancellationToken);

    Task<IntakeWorkItem?> ClaimDispatchAsync(
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

    public Task<ReceivedIntake> ExecuteAsync(
        IntakeSource source,
        string operationKey,
        CancellationToken cancellationToken = default) =>
        ReceiveCoreAsync(source, operationKey, processInline: false, cancellationToken);

    public Task<ReceivedIntake> ExecuteInlineAsync(
        IntakeSource source,
        string operationKey,
        CancellationToken cancellationToken = default) =>
        ReceiveCoreAsync(source, operationKey, processInline: true, cancellationToken);

    private async Task<ReceivedIntake> ReceiveCoreAsync(
        IntakeSource source,
        string operationKey,
        bool processInline,
        CancellationToken cancellationToken)
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

        if (source.Content.Length > IntakeEnvelopeLimits.MaximumContentLength)
        {
            throw new InvalidDataException("The intake source exceeds the 10 MB limit.");
        }

        _ = source.SourceIdentity.Channel switch
        {
            IntakeSourceChannel.ManualUpload => true,
            IntakeSourceChannel.Mailbox => true,
            IntakeSourceChannel.Automation => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(source),
                source.SourceIdentity.Channel,
                "The intake source channel is not supported.")
        };
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

            return processInline
                ? await workStore.ReceiveForProcessingAsync(
                    existing,
                    operationKey,
                    cancellationToken)
                : await workStore.ReceiveAsync(
                    existing,
                    operationKey,
                    cancellationToken);
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
        return processInline
            ? await workStore.ReceiveForProcessingAsync(
                stagedReceipt,
                operationKey,
                cancellationToken)
            : await workStore.ReceiveAsync(
                stagedReceipt,
                operationKey,
                cancellationToken);
    }

    async Task<IntakeSubmissionResult> IIntakeSubmission.ExecuteAsync(
        IntakeSource source,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var receipt = await ExecuteAsync(source, operationKey, cancellationToken);
        return new(
            receipt.StagedReceiptId,
            receipt.IsDuplicate,
            IntakeSubmissionDisposition.Queued);
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

    public async Task ExecuteAsync(Guid stagedReceiptId, CancellationToken cancellationToken = default)
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
                return;
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
            await CreateTriageIfQualifyingAsync(
                completedReceipt,
                completedEvaluation,
                cancellationToken);
            if (replayAssociated)
            {
                completedReceipt = await receiptQueries.GetAsync(
                    completedEvaluation.ProcessedReceiptId,
                    cancellationToken) ?? completedReceipt;
            }

            await ApplyImageIntakeAutomationAsync(completedReceipt, cancellationToken);
            return;
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
        catch (IntakeArtifactIntegrityException exception)
        {
            await FailProcessingAsync(workItem, exception, terminal: true, cancellationToken);
            return;
        }
        catch (InvalidDataException exception)
        {
            await FailProcessingAsync(workItem, exception, terminal: true, cancellationToken);
            return;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            await FailProcessingAsync(
                workItem,
                exception,
                terminal: workItem.AttemptCount >= RetryDelays.Length,
                cancellationToken);
            return;
        }

        await TryDeleteCompletedStagingAsync(
            stagedReceipt.StorageKey,
            cancellationToken);

        var associated = await AssociateCaseIfUnambiguousAsync(processed, evaluation, cancellationToken);
        await CreateTriageIfQualifyingAsync(processed, evaluation, cancellationToken);
        if (associated)
        {
            // The association wrote CurrentCaseId durably; the in-memory
            // receipt is stale, and image automation must see the associated
            // state or it would attempt a conflicting auto-link.
            processed = await receiptQueries.GetAsync(processed.Id, cancellationToken) ?? processed;
        }

        await ApplyImageIntakeAutomationAsync(processed, cancellationToken);
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
        Exception exception,
        bool terminal,
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
            FailureCode(exception),
            terminal,
            cancellationToken);
    }

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
        if (receipt.Decision != IntakeDecision.DraftReady
            || string.IsNullOrWhiteSpace(registration)
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

    private static string FailureCode(Exception exception) => exception switch
    {
        IntakeArtifactIntegrityException => "staged_artifact_integrity_failure",
        InvalidDataException => "invalid_intake_data",
        IntakeArtifactRetentionException => "artifact_retention_failure",
        IntakeSourceIdentityConflictException => "source_identity_conflict",
        _ => "intake_processing_failure"
    };
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
    TimeProvider timeProvider) : ILinkIntake
{
    public Task ExecuteAsync(
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
        return store.LinkAsync(request, timeProvider.GetUtcNow(), cancellationToken);
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
        if (editLeaseToken.Length > 64)
        {
            throw new ArgumentException(
                "The case edit lease token must be 64 characters or fewer.",
                nameof(editLeaseToken));
        }
    }
}
