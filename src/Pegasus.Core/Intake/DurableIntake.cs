using System.Security.Cryptography;

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

public interface IIntakeWorkStore
{
    Task<ReceivedIntake> ReceiveAsync(
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

    Task CompleteProcessingAsync(
        Guid workItemId,
        string leaseToken,
        Guid processedReceiptId,
        DateTimeOffset completedAtUtc,
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
    TimeProvider timeProvider)
{
    public async Task<ReceivedIntake> ExecuteAsync(
        IntakeSource source,
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceIdentity.ExternalReceiptToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);

        var sourceHash = Convert.ToHexString(SHA256.HashData(source.Content.Span));
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
        return await workStore.ReceiveAsync(
            new(
                Guid.NewGuid(),
                Path.GetFileName(source.FileName),
                source.MediaType,
                source.Content.Length,
                sourceHash,
                source.SourceIdentity,
                source.ReceivedAtUtc,
                source.Actor,
                storageKey,
                nowUtc),
            operationKey,
            cancellationToken);
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
    TimeProvider timeProvider)
{
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
            return;
        }

        var (workItem, receipt) = claimed.Value;
        if (workItem.LeaseToken is null)
        {
            throw new InvalidOperationException("A claimed intake work item must have a lease token.");
        }

        try
        {
            var content = await artifactStore.ReadAsync(receipt.StorageKey, cancellationToken)
                ?? throw new IntakeArtifactIntegrityException();
            var actualHash = Convert.ToHexString(SHA256.HashData(content.Span));
            if (!string.Equals(actualHash, receipt.SourceHash, StringComparison.Ordinal))
            {
                throw new IntakeArtifactIntegrityException();
            }

            var processed = await processIntake.ExecuteAsync(
                new(
                    receipt.SourceFileName,
                    receipt.MediaType,
                    content,
                    receipt.ReceivedAtUtc,
                    receipt.Actor,
                    receipt.SourceIdentity),
                cancellationToken);
            await workStore.CompleteProcessingAsync(
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
        }
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
