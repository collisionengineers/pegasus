using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// The versions the Worker's estimate recognition reads: confirmed, current
/// versions with no recorded answer, newest first. An answer is written once
/// and never changes, because a version's bytes never do.
/// </summary>
/// <remarks>
/// A version whose read failed for now is deferred: this process does not
/// list it again for <see cref="DeferInterval"/> (<see cref="DeferredVersionSet"/>).
/// </remarks>
internal sealed class EfEstimateRecognitionCandidates(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    TimeProvider timeProvider) : IEstimateRecognitionCandidates
{
    private static readonly TimeSpan DeferInterval = TimeSpan.FromMinutes(10);

    private readonly DeferredVersionSet deferred = new(timeProvider, DeferInterval);

    public async Task<IReadOnlyList<EstimateRecognitionCandidate>> ListAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var now = timeProvider.GetUtcNow();
        var skipped = deferred.Current(now);

        // Deferred versions are skipped here rather than sent to SQL: the
        // query asks for enough rows to fill the run past every one of them.
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await (
                from version in db.Set<DocumentVersionEntity>().AsNoTracking()
                join document in db.Set<CaseDocumentEntity>().AsNoTracking()
                    on version.DocumentId equals document.Id
                where version.CustodyStatus == DocumentCustodyStatus.Confirmed
                    && version.IsCurrent
                    && !version.IsLogicallyRemoved
                    && version.IsRecognisedEstimate == null
                orderby version.CreatedAtUtc descending, version.Id
                select new EstimateRecognitionCandidate(
                    document.CaseId,
                    version.DocumentId,
                    version.Id,
                    version.FileName,
                    version.MediaType,
                    version.ContentLength,
                    version.Sha256,
                    db.Set<DocumentOccurrenceEntity>().Any(occurrence =>
                        occurrence.VersionId == version.Id
                        && occurrence.Source == DocumentSource.Generated)))
            .Take(maximumItems + skipped.Count)
            .ToArrayAsync(cancellationToken);
        return [.. rows.Where(row => !skipped.Contains(row.VersionId)).Take(maximumItems)];
    }

    public async Task RecordAsync(Guid versionId, bool isEstimate, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Set<DocumentVersionEntity>()
            .Where(version => version.Id == versionId && version.IsRecognisedEstimate == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(version => version.IsRecognisedEstimate, isEstimate),
                cancellationToken);
    }

    public void Defer(Guid versionId) => deferred.Defer(versionId);
}
