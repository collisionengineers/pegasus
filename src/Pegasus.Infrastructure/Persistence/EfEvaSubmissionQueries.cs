using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Eva;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The case surface's view of what EVA said (EXT-04).
/// </summary>
public sealed class EfEvaSubmissionQueries(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IEvaSubmissionQueries
{
    public async Task<EvaSubmissionRecord?> GetLatestAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        if (caseId == Guid.Empty)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // The case surface reports the latest attempt, regardless of whether
        // an earlier one reached EVA. Staff need the outcome of the action
        // they most recently took.
        // Projected to a tuple first: the outcome is stored as text and
        // Enum.Parse has no SQL translation, so the parse belongs after the
        // row has been read rather than inside the query.
        var row = await context.EvaSubmissions
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderByDescending(item => item.SubmittedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new
            {
                item.Outcome,
                item.EvaId,
                item.FileReference,
                item.FailureCode,
                item.SubmittedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new(
                Enum.Parse<EvaSubmissionOutcome>(row.Outcome!),
                row.EvaId,
                row.FileReference,
                row.FailureCode,
                row.SubmittedAtUtc);
    }

    public async Task<IReadOnlyList<EvaSubmissionFailure>> GetRecentFailuresAsync(
        DateTimeOffset sinceUtc,
        int maximumResults,
        CancellationToken cancellationToken = default)
    {
        if (maximumResults is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResults),
                "An EVA failure query must return between one and 100 items.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.EvaSubmissions
            .AsNoTracking()
            .Where(item => !item.IsDelivered && item.SubmittedAtUtc >= sinceUtc)
            .OrderByDescending(item => item.SubmittedAtUtc)
            .ThenBy(item => item.Id)
            .Take(maximumResults)
            .Select(item => new { item.CaseId, item.Outcome, item.FailureCode, item.SubmittedAtUtc })
            .ToListAsync(cancellationToken);
        return rows
            .Select(row => new EvaSubmissionFailure(
                row.CaseId,
                Enum.Parse<EvaSubmissionOutcome>(row.Outcome!),
                row.FailureCode,
                row.SubmittedAtUtc))
            .ToList();
    }

    public async Task<EvaSubmissionActivity> GetActivityAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var latest = await context.EvaSubmissions
            .AsNoTracking()
            .MaxAsync(item => (DateTimeOffset?)item.SubmittedAtUtc, cancellationToken);
        return new(latest);
    }

    public async Task<bool> CanRetryAutomaticFailureAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        if (caseId == Guid.Empty)
        {
            return false;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (await context.EvaSubmissions.AnyAsync(item => item.CaseId == caseId
            && !item.IsDelivered, cancellationToken))
        {
            return true;
        }

        return await context.Set<AutomaticEvaReviewSubmissionEntity>()
            .AnyAsync(item => item.CaseId == caseId
                && item.State == nameof(AutomaticEvaReviewSubmissionState.ReconciliationRequired),
                cancellationToken);
    }
}
