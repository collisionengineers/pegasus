using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Eva;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfAutomaticEvaReviewSubmissionStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IAutomaticEvaReviewSubmissionStore
{
    public async Task<AutomaticEvaReviewSubmissionClaim?> ClaimAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // A worker may have submitted the request before it stopped, so an
        // expired dispatch lease cannot safely be sent again. Leave it for
        // operator reconciliation instead of treating the expiry as a retry.
        await context.Set<AutomaticEvaReviewSubmissionEntity>()
            .Where(item => item.State == nameof(AutomaticEvaReviewSubmissionState.Dispatching)
                && item.LeaseExpiresAtUtc <= nowUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State,
                    nameof(AutomaticEvaReviewSubmissionState.ReconciliationRequired))
                .SetProperty(item => item.CompletedAtUtc, nowUtc)
                .SetProperty(item => item.LeaseToken, (string?)null)
                .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null), cancellationToken);

        var candidate = await (
                from item in context.Set<AutomaticEvaReviewSubmissionEntity>()
                join @case in context.Cases on item.CaseId equals @case.Id
                where item.State == nameof(AutomaticEvaReviewSubmissionState.Pending)
                    && item.DueAtUtc <= nowUtc
                    && @case.CustodyState == "Confirmed"
                select item)
            .OrderBy(item => item.DueAtUtc).ThenBy(item => item.Id)
            .Select(item => new { item.Id, item.CaseId, item.WorkflowVersion, item.OperationKey })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        var leaseToken = Guid.NewGuid().ToString("N");
        var claimed = await context.Set<AutomaticEvaReviewSubmissionEntity>()
            .Where(item => item.Id == candidate.Id
                && item.State == nameof(AutomaticEvaReviewSubmissionState.Pending)
                && item.DueAtUtc <= nowUtc
                && context.Cases.Any(@case => @case.Id == item.CaseId
                    && @case.CustodyState == "Confirmed"))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, nameof(AutomaticEvaReviewSubmissionState.Dispatching))
                .SetProperty(item => item.LeaseToken, leaseToken)
                .SetProperty(item => item.LeaseExpiresAtUtc, nowUtc.Add(leaseDuration)), cancellationToken);
        return claimed != 1
            ? null
            : new(new(candidate.Id, candidate.CaseId, candidate.WorkflowVersion,
                candidate.OperationKey, AutomaticEvaReviewSubmissionState.Dispatching), leaseToken);
    }

    public async Task CompleteAsync(
        Guid intentId,
        string leaseToken,
        EvaSubmissionOutcome outcome,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseToken);
        var state = outcome == EvaSubmissionOutcome.Unknown
            ? AutomaticEvaReviewSubmissionState.ReconciliationRequired
            : AutomaticEvaReviewSubmissionState.Completed;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var completed = await context.Set<AutomaticEvaReviewSubmissionEntity>()
            .Where(item => item.Id == intentId
                && item.State == nameof(AutomaticEvaReviewSubmissionState.Dispatching)
                && item.LeaseToken == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, state.ToString())
                .SetProperty(item => item.CompletedAtUtc, timeProvider.GetUtcNow())
                .SetProperty(item => item.LeaseToken, (string?)null)
                .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null), cancellationToken);
        if (completed != 1)
        {
            throw new InvalidOperationException("The automatic EVA submission intent is no longer claimed by this worker.");
        }
    }
}
