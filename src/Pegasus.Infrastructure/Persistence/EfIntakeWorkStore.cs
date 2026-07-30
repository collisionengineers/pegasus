using System.Data;
using Pegasus.Core.Intake;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfIntakeWorkStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IIntakeWorkStore, IStagedArtifactAuthority
{
    private const int CandidateBatchSize = 256;

    private static readonly IComparer<LeaseRecoveryCandidate> LatestLeaseFirst =
        Comparer<LeaseRecoveryCandidate>.Create(static (left, right) =>
        {
            var comparison = right.LeaseExpiresAtUtc.CompareTo(left.LeaseExpiresAtUtc);
            return comparison != 0 ? comparison : right.Id.CompareTo(left.Id);
        });

    public async Task<IntakeStagedReceipt?> FindBySourceIdentityAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceIdentity);
        var channel = ToCode(sourceIdentity.Channel);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntakeStagedReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.SourceChannel == channel
                    && item.ExternalReceiptToken == sourceIdentity.ExternalReceiptToken,
                cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<StagedArtifactAuthority?> FindAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var staged = await context.IntakeStagedReceipts
            .AsNoTracking()
            .Include(item => item.WorkItem)
            .SingleOrDefaultAsync(
                item => item.StorageKey == storageKey,
                cancellationToken);
        if (staged is null)
        {
            return null;
        }

        var state = StagedArtifactAuthorityState.Unmatched;
        if (staged.WorkItem is { } workItem)
        {
            state = workItem.State switch
            {
                "failed" => StagedArtifactAuthorityState.Failed,
                "completed" => await HasDurablyRetainedSourceAsync(
                    context,
                    workItem,
                    staged,
                    cancellationToken)
                    ? StagedArtifactAuthorityState.Completed
                    : StagedArtifactAuthorityState.Unmatched,
                "pending" or "dispatching" or "dispatched" or "processing" or "retry_scheduled"
                    => StagedArtifactAuthorityState.Pending,
                _ => StagedArtifactAuthorityState.Unmatched
            };
        }

        return new(
            staged.StorageKey,
            staged.SourceHash,
            staged.SourceLength,
            state);
    }

    private static async Task<bool> HasDurablyRetainedSourceAsync(
        PegasusDbContext context,
        IntakeWorkItemEntity workItem,
        IntakeStagedReceiptEntity staged,
        CancellationToken cancellationToken)
    {
        if (workItem.ProcessedReceiptId is not { } receiptId)
        {
            return false;
        }

        return await context.IntakeAssets
            .AsNoTracking()
            .AnyAsync(
                asset => asset.IntakeReceiptId == receiptId
                    && asset.Kind == "source"
                    && asset.ContentHash == staged.SourceHash
                    && asset.ContentLength == staged.SourceLength
                    && asset.StorageKey != staged.StorageKey,
                cancellationToken);
    }

    public Task<ReceivedIntake> ReceiveAsync(
        IntakeStagedReceipt receipt,
        string operationKey,
        CancellationToken cancellationToken) =>
        ReceiveWithRetryAsync(receipt, operationKey, IntakeWorkState.Pending, cancellationToken);

    public Task<ReceivedIntake> ReceiveForProcessingAsync(
        IntakeStagedReceipt receipt,
        string operationKey,
        CancellationToken cancellationToken) =>
        ReceiveWithRetryAsync(receipt, operationKey, IntakeWorkState.Dispatched, cancellationToken);

    private async Task<ReceivedIntake> ReceiveWithRetryAsync(
        IntakeStagedReceipt receipt,
        string operationKey,
        IntakeWorkState initialState,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await ReceiveCoreAsync(
                    receipt,
                    operationKey,
                    initialState,
                    cancellationToken);
            }
            catch (Exception exception)
                when (attempt < 3 && IsRetryableConcurrencyFailure(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException(
            "The staged intake receipt could not be stored after the concurrency retry limit.");
    }

    private async Task<ReceivedIntake> ReceiveCoreAsync(
        IntakeStagedReceipt receipt,
        string operationKey,
        IntakeWorkState initialState,
        CancellationToken cancellationToken)
    {
        var channel = ToCode(receipt.SourceIdentity.Channel);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var existing = await context.IntakeStagedReceipts
            .Include(item => item.WorkItem)
            .SingleOrDefaultAsync(
                item => item.SourceChannel == channel
                    && item.ExternalReceiptToken == receipt.SourceIdentity.ExternalReceiptToken,
                cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.SourceHash, receipt.SourceHash, StringComparison.Ordinal))
            {
                throw new IntakeSourceIdentityConflictException(
                    existing.SourceHash,
                    receipt.SourceHash);
            }

            var existingWork = existing.WorkItem
                ?? throw new InvalidDataException(
                    "The staged intake receipt does not have a durable work item.");
            if (initialState == IntakeWorkState.Dispatched
                && existingWork.State is "pending" or "retry_scheduled")
            {
                existingWork.State = ToCode(IntakeWorkState.Dispatched);
                existingWork.DueAtUtc = receipt.StagedAtUtc;
                existingWork.LeaseToken = null;
                existingWork.LeaseExpiresAtUtc = null;
                existingWork.FailureCode = null;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            return new(existing.Id, true);
        }

        var entity = new IntakeStagedReceiptEntity
        {
            Id = receipt.Id,
            SourceFileName = receipt.SourceFileName,
            MediaType = receipt.MediaType,
            SourceLength = receipt.SourceLength,
            SourceHash = receipt.SourceHash,
            SourceChannel = channel,
            ExternalReceiptToken = receipt.SourceIdentity.ExternalReceiptToken,
            ReceivedAtUtc = receipt.ReceivedAtUtc,
            Actor = receipt.Actor,
            StorageKey = receipt.StorageKey,
            StagedAtUtc = receipt.StagedAtUtc
        };
        context.IntakeStagedReceipts.Add(entity);
        context.IntakeWorkItems.Add(new()
        {
            Id = Guid.NewGuid(),
            StagedReceipt = entity,
            StagedReceiptId = entity.Id,
            OperationKey = operationKey,
            State = ToCode(initialState),
            AttemptCount = 0,
            DueAtUtc = receipt.StagedAtUtc
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(entity.Id, false);
    }

    public async Task<IntakeWorkItem?> ClaimDispatchAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var candidate = await FindNextDispatchCandidateAsync(context, nowUtc, cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        var item = await context.IntakeWorkItems.SingleOrDefaultAsync(
            item => item.Id == candidate.Value.Id
                && (item.State == "pending" || item.State == "retry_scheduled"),
            cancellationToken);
        if (item is null || item.DueAtUtc > nowUtc)
        {
            return null;
        }

        item.LeaseToken = Guid.NewGuid().ToString("N");
        item.LeaseExpiresAtUtc = nowUtc.Add(leaseDuration);
        item.State = ToCode(IntakeWorkState.Dispatching);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(item);
    }

    public Task MarkDispatchedAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        UpdateDispatchClaimIfOwnedAsync(
            workItemId,
            leaseToken,
            IntakeWorkState.Dispatched,
            nowUtc,
            clearFailure: true,
            cancellationToken);

    public Task ReleaseDispatchAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken) =>
        UpdateDispatchClaimIfOwnedAsync(
            workItemId,
            leaseToken,
            IntakeWorkState.Pending,
            dueAtUtc,
            clearFailure: false,
            cancellationToken);

    public async Task<(IntakeWorkItem WorkItem, IntakeStagedReceipt Receipt)?> ClaimProcessingAsync(
        Guid stagedReceiptId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var item = await context.IntakeWorkItems
            .Include(item => item.StagedReceipt)
            .SingleOrDefaultAsync(item => item.StagedReceiptId == stagedReceiptId, cancellationToken);
        if (item is null || item.State is "completed" or "failed")
        {
            return null;
        }

        if (item.State == "processing" && item.LeaseExpiresAtUtc > nowUtc)
        {
            return null;
        }

        if (item.State is not ("dispatching" or "dispatched" or "processing"))
        {
            return null;
        }

        item.State = ToCode(IntakeWorkState.Processing);
        item.AttemptCount++;
        item.LeaseToken = Guid.NewGuid().ToString("N");
        item.LeaseExpiresAtUtc = nowUtc.Add(leaseDuration);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (Map(item), Map(item.StagedReceipt));
    }

    public async Task<IntakeEvaluationRevision> CompleteProcessingAsync(
        Guid workItemId,
        string leaseToken,
        Guid processedReceiptId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var item = await context.IntakeWorkItems.SingleOrDefaultAsync(item =>
            item.Id == workItemId && item.LeaseToken == leaseToken,
            cancellationToken)
            ?? throw new InvalidOperationException("The intake work item lease was lost before completion.");
        var stagedReceiptId = item.StagedReceiptId;
        var revision = (await context.IntakeEvaluations
            .Where(evaluation => evaluation.StagedReceiptId == stagedReceiptId)
            .Select(evaluation => (int?)evaluation.Revision)
            .MaxAsync(cancellationToken) ?? 0) + 1;
        var evaluation = new IntakeEvaluationEntity
        {
            Id = Guid.NewGuid(),
            StagedReceiptId = item.StagedReceiptId,
            ProcessedReceiptId = processedReceiptId,
            Revision = revision,
            EvaluatedAtUtc = completedAtUtc
        };
        context.IntakeEvaluations.Add(evaluation);
        item.State = ToCode(IntakeWorkState.Completed);
        item.ProcessedReceiptId = processedReceiptId;
        item.CompletedAtUtc = completedAtUtc;
        item.LeaseToken = null;
        item.LeaseExpiresAtUtc = null;
        item.FailureCode = null;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(evaluation);
    }

    public async Task<IntakeEvaluationRevision?> GetCompletedEvaluationAsync(
        Guid stagedReceiptId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.IntakeWorkItems
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.StagedReceiptId == stagedReceiptId && item.State == "completed",
                cancellationToken);
        if (item is null)
        {
            return null;
        }

        if (item.ProcessedReceiptId is not { } processedReceiptId)
        {
            throw new InvalidDataException(
                "The completed intake work item does not identify its processed receipt.");
        }

        var evaluation = await context.IntakeEvaluations
            .AsNoTracking()
            .Where(candidate => candidate.StagedReceiptId == stagedReceiptId
                && candidate.ProcessedReceiptId == processedReceiptId)
            .OrderByDescending(candidate => candidate.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidDataException(
                "The completed intake work item does not have an evaluation revision.");
        return Map(evaluation);
    }

    public async Task RetryProcessingAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset dueAtUtc,
        string failureCode,
        bool terminal,
        CancellationToken cancellationToken)
    {
        await UpdateClaimedAsync(workItemId, leaseToken, item =>
        {
            item.State = ToCode(terminal ? IntakeWorkState.Failed : IntakeWorkState.RetryScheduled);
            item.DueAtUtc = dueAtUtc;
            item.FailureCode = failureCode;
            item.LeaseToken = null;
            item.LeaseExpiresAtUtc = null;
        }, cancellationToken);
    }

    public async Task MarkPoisonedAsync(
        Guid stagedReceiptId,
        DateTimeOffset failedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.IntakeWorkItems.SingleOrDefaultAsync(
            item => item.StagedReceiptId == stagedReceiptId,
            cancellationToken)
            ?? throw new InvalidDataException("The poisoned intake work message does not identify a staged receipt.");
        if (item.State == "completed")
        {
            return;
        }

        item.State = ToCode(IntakeWorkState.Failed);
        item.DueAtUtc = failedAtUtc;
        item.FailureCode = "queue_poisoned";
        item.LeaseToken = null;
        item.LeaseExpiresAtUtc = null;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> RecoverExpiredLeasesAsync(
        DateTimeOffset nowUtc,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        if (maximumItems <= 0)
        {
            return 0;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await FindExpiredLeaseCandidatesAsync(
            context,
            nowUtc,
            maximumItems,
            cancellationToken);
        var recovered = 0;
        foreach (var candidate in candidates)
        {
            var terminal = candidate.State == "processing" && candidate.AttemptCount >= 5;
            var targetState = candidate.State switch
            {
                "dispatching" => ToCode(IntakeWorkState.Pending),
                "processing" when terminal => ToCode(IntakeWorkState.Failed),
                "processing" => ToCode(IntakeWorkState.RetryScheduled),
                _ => throw new InvalidDataException(
                    $"Unknown leased intake work state '{candidate.State}'.")
            };
            var failureCode = terminal ? "processing_lease_expired" : null;
            recovered += await context.IntakeWorkItems
                .Where(item => item.Id == candidate.Id
                    && item.State == candidate.State
                    && item.AttemptCount == candidate.AttemptCount
                    && item.LeaseToken == candidate.LeaseToken
                    && item.LeaseExpiresAtUtc == candidate.LeaseExpiresAtUtc)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.State, targetState)
                        .SetProperty(item => item.DueAtUtc, nowUtc)
                        .SetProperty(item => item.FailureCode, failureCode)
                        .SetProperty(item => item.LeaseToken, (string?)null)
                        .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null),
                    cancellationToken);
        }

        return recovered;
    }

    public async Task ScheduleReevaluationAsync(
        Guid stagedReceiptId,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.IntakeWorkItems.SingleOrDefaultAsync(
            item => item.StagedReceiptId == stagedReceiptId,
            cancellationToken)
            ?? throw new InvalidDataException("The intake receipt does not exist.");
        if (item.State == "processing"
            && item.LeaseExpiresAtUtc is { } leaseExpiresAtUtc
            && leaseExpiresAtUtc > dueAtUtc)
        {
            throw new InvalidOperationException("The intake receipt is already being processed.");
        }

        item.State = ToCode(IntakeWorkState.Pending);
        item.DueAtUtc = dueAtUtc;
        item.LeaseToken = null;
        item.LeaseExpiresAtUtc = null;
        item.FailureCode = null;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<DispatchCandidate?> FindNextDispatchCandidateAsync(
        PegasusDbContext context,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        DispatchCandidate? next = null;
        for (var offset = 0; ; offset += CandidateBatchSize)
        {
            var batch = await context.IntakeWorkItems
                .AsNoTracking()
                .Where(item => item.State == "pending" || item.State == "retry_scheduled")
                .OrderBy(item => item.Id)
                .Skip(offset)
                .Take(CandidateBatchSize)
                .Select(item => new DispatchCandidate(item.Id, item.DueAtUtc))
                .ToListAsync(cancellationToken);
            foreach (var candidate in batch)
            {
                if (candidate.DueAtUtc <= nowUtc
                    && (next is null || Compare(candidate, next.Value) < 0))
                {
                    next = candidate;
                }
            }

            if (batch.Count < CandidateBatchSize)
            {
                return next;
            }
        }
    }

    private static async Task<LeaseRecoveryCandidate[]> FindExpiredLeaseCandidatesAsync(
        PegasusDbContext context,
        DateTimeOffset nowUtc,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        var earliest = new PriorityQueue<LeaseRecoveryCandidate, LeaseRecoveryCandidate>(
            LatestLeaseFirst);
        for (var offset = 0; ; offset += CandidateBatchSize)
        {
            var batch = await context.IntakeWorkItems
                .AsNoTracking()
                .Where(item => (item.State == "dispatching" || item.State == "processing")
                    && item.LeaseExpiresAtUtc != null)
                .OrderBy(item => item.Id)
                .Skip(offset)
                .Take(CandidateBatchSize)
                .Select(item => new LeaseRecoveryCandidate(
                    item.Id,
                    item.State,
                    item.AttemptCount,
                    item.LeaseToken,
                    item.LeaseExpiresAtUtc!.Value))
                .ToListAsync(cancellationToken);
            foreach (var candidate in batch)
            {
                if (candidate.LeaseExpiresAtUtc > nowUtc)
                {
                    continue;
                }

                if (earliest.Count < maximumItems)
                {
                    earliest.Enqueue(candidate, candidate);
                }
                else if (Compare(candidate, earliest.Peek()) < 0)
                {
                    earliest.Dequeue();
                    earliest.Enqueue(candidate, candidate);
                }
            }

            if (batch.Count < CandidateBatchSize)
            {
                return earliest.UnorderedItems
                    .Select(item => item.Element)
                    .OrderBy(item => item.LeaseExpiresAtUtc)
                    .ThenBy(item => item.Id)
                    .ToArray();
            }
        }
    }

    private static int Compare(DispatchCandidate left, DispatchCandidate right)
    {
        var comparison = left.DueAtUtc.CompareTo(right.DueAtUtc);
        return comparison != 0 ? comparison : left.Id.CompareTo(right.Id);
    }

    private static int Compare(LeaseRecoveryCandidate left, LeaseRecoveryCandidate right)
    {
        var comparison = left.LeaseExpiresAtUtc.CompareTo(right.LeaseExpiresAtUtc);
        return comparison != 0 ? comparison : left.Id.CompareTo(right.Id);
    }

    private readonly record struct DispatchCandidate(Guid Id, DateTimeOffset DueAtUtc);

    private readonly record struct LeaseRecoveryCandidate(
        Guid Id,
        string State,
        int AttemptCount,
        string? LeaseToken,
        DateTimeOffset LeaseExpiresAtUtc);

    private async Task UpdateDispatchClaimIfOwnedAsync(
        Guid workItemId,
        string leaseToken,
        IntakeWorkState targetState,
        DateTimeOffset dueAtUtc,
        bool clearFailure,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var targetStateCode = ToCode(targetState);
        var updated = await context.IntakeWorkItems
            .Where(item => item.Id == workItemId
                && item.LeaseToken == leaseToken
                && item.State == "dispatching")
            .ExecuteUpdateAsync(setters =>
            {
                setters.SetProperty(item => item.State, targetStateCode);
                setters.SetProperty(item => item.DueAtUtc, dueAtUtc);
                setters.SetProperty(item => item.LeaseToken, (string?)null);
                setters.SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null);
                if (clearFailure)
                {
                    setters.SetProperty(item => item.FailureCode, (string?)null);
                }
            }, cancellationToken);
        if (updated == 1)
        {
            return;
        }

        if (updated != 0)
        {
            throw new InvalidDataException(
                "The intake dispatch claim update affected more than one work item.");
        }

        if (!await context.IntakeWorkItems.AnyAsync(
                item => item.Id == workItemId,
                cancellationToken))
        {
            throw new InvalidDataException("The intake dispatch work item does not exist.");
        }
    }

    private async Task UpdateClaimedAsync(
        Guid workItemId,
        string leaseToken,
        Action<IntakeWorkItemEntity> update,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await context.IntakeWorkItems.SingleOrDefaultAsync(item =>
            item.Id == workItemId && item.LeaseToken == leaseToken,
            cancellationToken)
            ?? throw new InvalidOperationException("The intake work item lease was lost before its state could be persisted.");
        update(item);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static IntakeStagedReceipt Map(IntakeStagedReceiptEntity entity) => new(
        entity.Id,
        entity.SourceFileName,
        entity.MediaType,
        entity.SourceLength,
        entity.SourceHash,
        new(ParseSourceChannel(entity.SourceChannel), entity.ExternalReceiptToken),
        entity.ReceivedAtUtc,
        entity.Actor,
        entity.StorageKey,
        entity.StagedAtUtc);

    private static IntakeWorkItem Map(IntakeWorkItemEntity entity) => new(
        entity.Id,
        entity.StagedReceiptId,
        entity.OperationKey,
        ParseState(entity.State),
        entity.AttemptCount,
        entity.DueAtUtc,
        entity.LeaseToken,
        entity.LeaseExpiresAtUtc,
        entity.ProcessedReceiptId,
        entity.FailureCode,
        entity.ProcessedReceiptId is not null);

    private static IntakeEvaluationRevision Map(IntakeEvaluationEntity entity) => new(
        entity.Id,
        entity.StagedReceiptId,
        entity.ProcessedReceiptId,
        entity.Revision,
        entity.EvaluatedAtUtc);

    private static string ToCode(IntakeSourceChannel value) => value switch
    {
        IntakeSourceChannel.ManualUpload => "manual_upload",
        IntakeSourceChannel.Mailbox => "mailbox",
        _ => throw new InvalidOperationException($"Unknown IntakeSourceChannel value '{(int)value}'.")
    };

    private static IntakeSourceChannel ParseSourceChannel(string value) => value switch
    {
        "manual_upload" => IntakeSourceChannel.ManualUpload,
        "mailbox" => IntakeSourceChannel.Mailbox,
        _ => throw new InvalidDataException($"Unknown persisted intake source channel '{value}'.")
    };

    private static string ToCode(IntakeWorkState value) => value switch
    {
        IntakeWorkState.Pending => "pending",
        IntakeWorkState.Dispatching => "dispatching",
        IntakeWorkState.Dispatched => "dispatched",
        IntakeWorkState.Processing => "processing",
        IntakeWorkState.RetryScheduled => "retry_scheduled",
        IntakeWorkState.Completed => "completed",
        IntakeWorkState.Failed => "failed",
        _ => throw new InvalidOperationException($"Unknown IntakeWorkState value '{(int)value}'.")
    };

    private static IntakeWorkState ParseState(string value) => value switch
    {
        "pending" => IntakeWorkState.Pending,
        "dispatching" => IntakeWorkState.Dispatching,
        "dispatched" => IntakeWorkState.Dispatched,
        "processing" => IntakeWorkState.Processing,
        "retry_scheduled" => IntakeWorkState.RetryScheduled,
        "completed" => IntakeWorkState.Completed,
        "failed" => IntakeWorkState.Failed,
        _ => throw new InvalidDataException($"Unknown persisted intake work state '{value}'.")
    };
    private static bool IsRetryableConcurrencyFailure(Exception exception) => exception switch
    {
        SqlException { Number: 1205 or 2601 or 2627 } => true,
        _ when exception.InnerException is not null =>
            IsRetryableConcurrencyFailure(exception.InnerException),
        _ => false
    };

}
