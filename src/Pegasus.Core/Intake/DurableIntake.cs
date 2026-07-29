using System.Security.Cryptography;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Intake;

public enum IntakeWorkState
{
    Pending,
    Dispatched,
    Processing,
    RetryScheduled,
    Completed,
    Failed
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
    string? FailureCode);

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
    private const int MaximumSourceLength = 10 * 1024 * 1024;
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

        if (source.Content.Length > MaximumSourceLength)
        {
            throw new InvalidDataException("The intake source exceeds the 10 MB limit.");
        }

        _ = source.SourceIdentity.Channel switch
        {
            IntakeSourceChannel.ManualUpload => true,
            IntakeSourceChannel.Mailbox => true,
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
                throw new IntakeSourceIdentityConflictException();
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

        string storageKey;
        try
        {
            storageKey = await artifactStore.StoreAsync(sourceHash, source.Content, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            throw new IntakeArtifactRetentionException(exception);
        }

        var nowUtc = timeProvider.GetUtcNow();
        var stagedReceipt = new IntakeStagedReceipt(
            Guid.NewGuid(),
            safeFileName,
            source.MediaType,
            source.Content.Length,
            sourceHash,
            source.SourceIdentity,
            source.ReceivedAtUtc,
            source.Actor,
            storageKey,
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
    TimeProvider timeProvider)
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
            await CreateTriageIfQualifyingAsync(
                completedReceipt,
                completedEvaluation,
                cancellationToken);
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

            processed = await processIntake.ExecuteRetainedAsync(
                new(
                    stagedReceipt.SourceFileName,
                    stagedReceipt.MediaType,
                    content,
                    stagedReceipt.ReceivedAtUtc,
                    stagedReceipt.Actor,
                    stagedReceipt.SourceIdentity),
                stagedReceipt.StorageKey,
                cancellationToken);
            evaluation = await workStore.CompleteProcessingAsync(
                workItem.Id,
                workItem.LeaseToken,
                processed.Id,
                timeProvider.GetUtcNow(),
                cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            var terminal = workItem.AttemptCount >= RetryDelays.Length;
            var dueAtUtc = terminal
                ? timeProvider.GetUtcNow()
                : timeProvider.GetUtcNow().Add(RetryDelays[workItem.AttemptCount - 1]);
            await workStore.RetryProcessingAsync(
                workItem.Id,
                workItem.LeaseToken,
                dueAtUtc,
                FailureCode(exception),
                terminal,
                cancellationToken);
            return;
        }

        await CreateTriageIfQualifyingAsync(processed, evaluation, cancellationToken);
    }

    private async Task CreateTriageIfQualifyingAsync(
        IntakeReceipt receipt,
        IntakeEvaluationRevision evaluation,
        CancellationToken cancellationToken)
    {
        var registration = receipt.InstructionDraft?.VehicleRegistration;
        if (receipt.Decision != IntakeDecision.DraftReady
            || string.IsNullOrWhiteSpace(receipt.ExtractionPolicyKey)
            || receipt.ExtractionPolicyVersion is null or <= 0
            || string.IsNullOrWhiteSpace(registration))
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
                SystemActor,
                $"triage-from-intake-evaluation:{evaluation.Id:N}"),
            cancellationToken);
    }

    private static string FailureCode(Exception exception) => exception switch
    {
        IntakeArtifactIntegrityException => "staged_artifact_integrity_failure",
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

public sealed class ReconcileStagedIntakeArtifacts(
    IIntakeWorkStore workStore,
    TimeProvider timeProvider)
{
    public Task<int> ExecuteAsync(int maximumItems, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        return workStore.RecoverExpiredLeasesAsync(
            timeProvider.GetUtcNow(),
            maximumItems,
            cancellationToken);
    }
}

public sealed class ResolveIntake(
    IIntakeWorkStore workStore,
    TimeProvider timeProvider)
{
    public Task ExecuteAsync(Guid stagedReceiptId, CancellationToken cancellationToken = default) =>
        workStore.ScheduleReevaluationAsync(stagedReceiptId, timeProvider.GetUtcNow(), cancellationToken);
}

public sealed class ReevaluateIntake(
    IIntakeWorkStore workStore,
    TimeProvider timeProvider)
{
    public Task ExecuteAsync(Guid stagedReceiptId, CancellationToken cancellationToken = default) =>
        workStore.ScheduleReevaluationAsync(stagedReceiptId, timeProvider.GetUtcNow(), cancellationToken);
}
