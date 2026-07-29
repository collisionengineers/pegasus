using System.Data;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Custody;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfExternalWorkStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IExternalWorkStore
{
    private const int CandidateBatchSize = 256;

    public async Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            leaseDuration,
            TimeSpan.Zero);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        while (true)
        {
            var candidate = await FindNextCandidateAsync(context, nowUtc, cancellationToken);
            if (candidate is null)
            {
                return null;
            }

            var selected = candidate.Value;
            var leaseToken = Guid.NewGuid().ToString("N");
            var leaseExpiresAtUtc = nowUtc.Add(leaseDuration);
            var claimed = await context.ExternalWorkItems
                .Where(item => item.Id == selected.Id
                    && item.State == selected.State
                    && item.DueAtUtc == selected.DueAtUtc
                    && item.LeaseToken == selected.LeaseToken
                    && item.LeaseExpiresAtUtc == selected.LeaseExpiresAtUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.State, "dispatching")
                    .SetProperty(item => item.LeaseToken, leaseToken)
                    .SetProperty(item => item.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                    .SetProperty(item => item.FailureCode, (string?)null)
                    .SetProperty(item => item.FailureReason, (string?)null),
                    cancellationToken);
            if (claimed == 1)
            {
                return new(selected.Id, leaseToken);
            }
        }
    }

    public async Task MarkDispatchedAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset dispatchedAtUtc,
        CancellationToken cancellationToken)
    {
        ValidateLease(workItemId, leaseToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var updated = await context.ExternalWorkItems
            .Where(item => item.Id == workItemId
                && item.State == "dispatching"
                && item.LeaseToken == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, "queued")
                .SetProperty(item => item.DueAtUtc, dispatchedAtUtc)
                .SetProperty(item => item.LeaseToken, (string?)null)
                .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.FailureCode, (string?)null)
                .SetProperty(item => item.FailureReason, (string?)null),
                cancellationToken);
        if (updated == 0)
        {
            await EnsureWorkExistsAsync(context, workItemId, cancellationToken);
        }
    }

    public async Task ReleaseDispatchAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken)
    {
        ValidateLease(workItemId, leaseToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var updated = await context.ExternalWorkItems
            .Where(item => item.Id == workItemId
                && item.State == "dispatching"
                && item.LeaseToken == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, "pending")
                .SetProperty(item => item.DueAtUtc, dueAtUtc)
                .SetProperty(item => item.LeaseToken, (string?)null)
                .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.FailureCode, "queue_dispatch_failure")
                .SetProperty(
                    item => item.FailureReason,
                    "The external work identifier could not be confirmed in the queue."),
                cancellationToken);
        if (updated == 0)
        {
            await EnsureWorkExistsAsync(context, workItemId, cancellationToken);
        }
    }

    public async Task MarkPoisonedAsync(
        Guid workItemId,
        DateTimeOffset failedAtUtc,
        CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "An external work item identifier is required.",
                nameof(workItemId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var work = await context.ExternalWorkItems
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.Id == workItemId, cancellationToken)
            ?? throw new InvalidOperationException("The external work item is unavailable.");
        if (work.State is "completed" or "failed")
        {
            return;
        }

        if ((work.State is "dispatching" or "processing")
            && work.LeaseExpiresAtUtc is { } leaseExpiresAtUtc
            && leaseExpiresAtUtc > failedAtUtc)
        {
            work.State = "pending";
            work.DueAtUtc = leaseExpiresAtUtc;
            work.FailureCode = "queue_poisoned_during_active_lease";
            work.FailureReason =
                "The poison delivery overlapped an active lease and was scheduled for safe replay.";
            work.LeaseToken = null;
            work.LeaseExpiresAtUtc = null;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (string.Equals(work.Case.CustodyState, "confirmed", StringComparison.Ordinal))
        {
            work.State = "completed";
            work.CompletedAtUtc ??= failedAtUtc;
            work.LeaseToken = null;
            work.LeaseExpiresAtUtc = null;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        work.State = "failed";
        work.DueAtUtc = failedAtUtc;
        work.FailureCode = "queue_poisoned";
        work.FailureReason = "External custody work exhausted the queue retry policy.";
        work.LeaseToken = null;
        work.LeaseExpiresAtUtc = null;
        if (!string.Equals(work.Case.CustodyState, "failed", StringComparison.Ordinal))
        {
            var beforeVersion = work.Case.Version;
            work.Case.CustodyState = "failed";
            work.Case.Version = checked(work.Case.Version + 1);
            context.CaseHistory.Add(new()
            {
                Id = Guid.NewGuid(),
                CaseId = work.CaseId,
                EventType = "custody_failed",
                Actor = "system",
                Reason = "Accepted source custody exhausted the queue retry policy.",
                OccurredAtUtc = failedAtUtc,
                OperationKey = $"{work.OperationKey}:poisoned",
                BeforeVersion = beforeVersion,
                AfterVersion = work.Case.Version
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<DispatchCandidate?> FindNextCandidateAsync(
        PegasusDbContext context,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        DispatchCandidate? next = null;
        for (var offset = 0; ; offset += CandidateBatchSize)
        {
            var batch = await context.ExternalWorkItems
                .AsNoTracking()
                .Where(item => item.State == "pending" || item.State == "dispatching")
                .OrderBy(item => item.Id)
                .Skip(offset)
                .Take(CandidateBatchSize)
                .Select(item => new DispatchCandidate(
                    item.Id,
                    item.State,
                    item.DueAtUtc,
                    item.LeaseToken,
                    item.LeaseExpiresAtUtc))
                .ToListAsync(cancellationToken);
            foreach (var candidate in batch)
            {
                var availableAtUtc = candidate.State == "pending"
                    ? candidate.DueAtUtc
                    : candidate.LeaseExpiresAtUtc ?? DateTimeOffset.MinValue;
                if (availableAtUtc <= nowUtc
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

    private static int Compare(DispatchCandidate left, DispatchCandidate right)
    {
        var leftAvailableAtUtc = left.State == "pending"
            ? left.DueAtUtc
            : left.LeaseExpiresAtUtc ?? DateTimeOffset.MinValue;
        var rightAvailableAtUtc = right.State == "pending"
            ? right.DueAtUtc
            : right.LeaseExpiresAtUtc ?? DateTimeOffset.MinValue;
        var comparison = leftAvailableAtUtc.CompareTo(rightAvailableAtUtc);
        return comparison != 0 ? comparison : left.Id.CompareTo(right.Id);
    }

    private static async Task EnsureWorkExistsAsync(
        PegasusDbContext context,
        Guid workItemId,
        CancellationToken cancellationToken)
    {
        if (!await context.ExternalWorkItems
                .AsNoTracking()
                .AnyAsync(item => item.Id == workItemId, cancellationToken))
        {
            throw new InvalidOperationException("The external work item is unavailable.");
        }
    }

    private static void ValidateLease(Guid workItemId, string leaseToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "An external work item identifier is required.",
                nameof(workItemId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(leaseToken);
    }

    private readonly record struct DispatchCandidate(
        Guid Id,
        string State,
        DateTimeOffset DueAtUtc,
        string? LeaseToken,
        DateTimeOffset? LeaseExpiresAtUtc);
}
