using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfOperationsStore(
    IDbContextFactory<PegasusDbContext> contextFactory) :
    IRequestOperationsProjectionStore
{
    private readonly IDbContextFactory<PegasusDbContext> contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    async Task<RequestOperationsProjection> IRequestOperationsProjectionStore.GetAsync(
        int maximumItems,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var sourceLimit = checked(maximumItems + 1);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Every Case's work, a Triage Case's included: it has no workflow row
        // but keeps standard Case custody, so its failed job is retried here too.
        var workRows = await (
                from item in context.ExternalWorkItems.AsNoTracking()
                where item.CaseId != null
                    && item.State == "failed"
                    && ((item.LeaseToken == null && item.LeaseExpiresAtUtc == null)
                        || (item.LeaseToken != null && item.LeaseExpiresAtUtc <= nowUtc))
                orderby item.DueAtUtc descending, item.Id
                select new ExternalWorkRow(
                    item.Id,
                    item.State,
                    item.CaseId!.Value,
                    item.Case!.Reference,
                    item.Case!.Principal.Code,
                    item.Kind,
                    item.AttemptCount,
                    item.DueAtUtc,
                    item.LeaseExpiresAtUtc,
                    item.LeaseToken != null,
                    item.CompletedAtUtc,
                    item.FailureCode,
                    item.FailureReason))
            .Take(sourceLimit)
            .ToListAsync(cancellationToken);

        var ordered = workRows.Select(item => MapExternalWork(item, nowUtc))
            .OrderByDescending(item => item.LastActivityAtUtc)
            .ThenBy(item => item.Id)
            .ToArray();
        return new(
            ordered.Take(maximumItems).ToImmutableArray(),
            ordered.Length > maximumItems);
    }

    private static DateTimeOffset LatestActivity(
        DateTimeOffset createdAtUtc,
        DateTimeOffset? revokedAtUtc,
        DateTimeOffset? lastReceiptAtUtc)
    {
        var latest = revokedAtUtc is { } revoked && revoked > createdAtUtc
            ? revoked
            : createdAtUtc;
        return lastReceiptAtUtc is { } receipt && receipt > latest
            ? receipt
            : latest;
    }

    private static RequestOperationProjection MapExternalWork(
        ExternalWorkRow item,
        DateTimeOffset nowUtc)
    {
        var leaseIsConsistent = item.HasWorkLease == item.LeaseExpiresAtUtc.HasValue;
        var activelyLeased = item.HasWorkLease && item.LeaseExpiresAtUtc > nowUtc;
        var state = !leaseIsConsistent
            ? RequestOperationState.UnknownExternal
            : item.State switch
            {
                "pending" or "dispatching" or "queued" or "processing" => RequestOperationState.Pending,
                "completed" => RequestOperationState.Completed,
                "failed" => RequestOperationState.Failed,
                _ => RequestOperationState.UnknownExternal
            };
        return new(
            item.Id,
            state,
            item.CaseId,
            item.CaseReference,
            item.PrincipalCode,
            LatestActivity(item.DueAtUtc, item.LeaseExpiresAtUtc, item.CompletedAtUtc),
            item.Kind,
            item.AttemptCount,
            item.FailureCode,
            item.FailureReason,
            CanRetry: state == RequestOperationState.Failed && !activelyLeased);
    }

    private sealed record ExternalWorkRow(
        Guid Id,
        string State,
        Guid CaseId,
        string CaseReference,
        string PrincipalCode,
        string Kind,
        int AttemptCount,
        DateTimeOffset DueAtUtc,
        DateTimeOffset? LeaseExpiresAtUtc,
        bool HasWorkLease,
        DateTimeOffset? CompletedAtUtc,
        string? FailureCode,
        string? FailureReason);
}
