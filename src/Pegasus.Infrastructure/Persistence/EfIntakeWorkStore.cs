using System.Data;
using Pegasus.Core.Intake;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfIntakeWorkStore(IDbContextFactory<PegasusDbContext> contextFactory) : IIntakeWorkStore
{
    public async Task<ReceivedIntake> ReceiveAsync(
        IntakeStagedReceipt receipt,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var channel = ToCode(receipt.SourceIdentity.Channel);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existing = await context.IntakeStagedReceipts.SingleOrDefaultAsync(
            item => item.SourceChannel == channel
                && item.ExternalReceiptToken == receipt.SourceIdentity.ExternalReceiptToken,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.SourceHash, receipt.SourceHash, StringComparison.Ordinal))
            {
                throw new IntakeSourceIdentityConflictException();
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
            State = ToCode(IntakeWorkState.Pending),
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
        var item = await context.IntakeWorkItems
            .Where(item => (item.State == "pending" || item.State == "retry_scheduled")
                && item.DueAtUtc <= nowUtc)
            .OrderBy(item => item.DueAtUtc)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            return null;
        }

        item.LeaseToken = Guid.NewGuid().ToString("N");
        item.LeaseExpiresAtUtc = nowUtc.Add(leaseDuration);
        item.State = ToCode(IntakeWorkState.Processing);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(item);
    }

    public async Task MarkDispatchedAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await UpdateClaimedAsync(workItemId, leaseToken, item =>
        {
            item.State = ToCode(IntakeWorkState.Dispatched);
            item.DueAtUtc = nowUtc;
            item.LeaseToken = null;
            item.LeaseExpiresAtUtc = null;
            item.FailureCode = null;
        }, cancellationToken);
    }

    public async Task ReleaseDispatchAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken)
    {
        await UpdateClaimedAsync(workItemId, leaseToken, item =>
        {
            item.State = ToCode(IntakeWorkState.Pending);
            item.DueAtUtc = dueAtUtc;
            item.LeaseToken = null;
            item.LeaseExpiresAtUtc = null;
        }, cancellationToken);
    }

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

        if (item.State is not ("dispatched" or "processing"))
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

    public async Task CompleteProcessingAsync(
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
        context.IntakeEvaluations.Add(new()
        {
            Id = Guid.NewGuid(),
            StagedReceiptId = item.StagedReceiptId,
            ProcessedReceiptId = processedReceiptId,
            Revision = revision,
            EvaluatedAtUtc = completedAtUtc
        });
        item.State = ToCode(IntakeWorkState.Completed);
        item.ProcessedReceiptId = processedReceiptId;
        item.CompletedAtUtc = completedAtUtc;
        item.LeaseToken = null;
        item.LeaseExpiresAtUtc = null;
        item.FailureCode = null;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var items = await context.IntakeWorkItems
            .Where(item => item.State == "processing"
                && item.LeaseExpiresAtUtc != null
                && item.LeaseExpiresAtUtc <= nowUtc)
            .OrderBy(item => item.LeaseExpiresAtUtc)
            .Take(maximumItems)
            .ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.State = ToCode(item.AttemptCount >= 5
                ? IntakeWorkState.Failed
                : IntakeWorkState.RetryScheduled);
            item.DueAtUtc = nowUtc;
            item.FailureCode = item.AttemptCount >= 5 ? "processing_lease_expired" : null;
            item.LeaseToken = null;
            item.LeaseExpiresAtUtc = null;
        }

        await context.SaveChangesAsync(cancellationToken);
        return items.Count;
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
        entity.FailureCode);

    private static string ToCode(IntakeSourceChannel value) => value switch
    {
        IntakeSourceChannel.ManualUpload => "manual_upload",
        _ => throw new InvalidOperationException($"Unknown IntakeSourceChannel value '{(int)value}'.")
    };

    private static IntakeSourceChannel ParseSourceChannel(string value) => value switch
    {
        "manual_upload" => IntakeSourceChannel.ManualUpload,
        _ => throw new InvalidDataException($"Unknown persisted intake source channel '{value}'.")
    };

    private static string ToCode(IntakeWorkState value) => value switch
    {
        IntakeWorkState.Pending => "pending",
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
        "dispatched" => IntakeWorkState.Dispatched,
        "processing" => IntakeWorkState.Processing,
        "retry_scheduled" => IntakeWorkState.RetryScheduled,
        "completed" => IntakeWorkState.Completed,
        "failed" => IntakeWorkState.Failed,
        _ => throw new InvalidDataException($"Unknown persisted intake work state '{value}'.")
    };
}
