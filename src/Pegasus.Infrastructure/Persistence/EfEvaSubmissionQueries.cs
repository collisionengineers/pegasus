using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Custody;
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

        // A succeeded attempt wins over a later failure, because the once-per-
        // case rule means a success is final: a case that reached EVA has
        // reached it, and showing a subsequent refused retry as the current
        // state would say otherwise. Failures order by recency among
        // themselves.
        // Projected to a tuple first: the outcome is stored as text and
        // Enum.Parse has no SQL translation, so the parse belongs after the
        // row has been read rather than inside the query.
        var row = await context.EvaSubmissions
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderByDescending(item => item.IsDelivered)
            .ThenByDescending(item => item.SubmittedAtUtc)
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
                Enum.Parse<EvaSubmissionOutcome>(row.Outcome),
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
                Enum.Parse<EvaSubmissionOutcome>(row.Outcome),
                row.FailureCode,
                row.SubmittedAtUtc))
            .ToList();
    }

    public async Task<EvaSubmissionActivity> GetActivityAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // The same rows and state words EfEvaSubmissionWorkStore claims from:
        // anything not yet completed or failed is still pending work.
        var pending = await context.ExternalWorkItems
            .AsNoTracking()
            .CountAsync(
                item => item.Kind == ExternalWorkKinds.SubmitCaseToEva
                    && item.State != ExternalWorkStatePersistence.Completed
                    && item.State != ExternalWorkStatePersistence.Failed,
                cancellationToken);
        var latest = await context.EvaSubmissions
            .AsNoTracking()
            .MaxAsync(item => (DateTimeOffset?)item.SubmittedAtUtc, cancellationToken);
        return new(pending, latest);
    }
}
