using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// The versions the Worker's thumbnail sweep makes plain thumbnails for:
/// confirmed, current image versions filed within the plain thumbnail's idle
/// lifetime that have no live plain thumbnail, newest first.
/// </summary>
/// <remarks>
/// A version whose thumbnail could not be made (a format the renderer does
/// not decode, or a read that failed for now) is deferred: this process does
/// not list it again for <see cref="DeferInterval"/>, so it cannot take a
/// place in every run ahead of newer photographs (<see cref="DeferredVersionSet"/>).
/// A made thumbnail leaves the candidate set by itself and is not remembered.
/// A version older than the lifetime is left
/// to its first view, so an expired thumbnail is not made again for nobody.
/// </remarks>
internal sealed class EfDocumentThumbnailCandidates(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    TimeProvider timeProvider) : IListDocumentThumbnailCandidates
{
    private static readonly TimeSpan DeferInterval = TimeSpan.FromDays(1);

    private readonly DeferredVersionSet deferred = new(timeProvider, DeferInterval);

    public async Task<IReadOnlyList<DocumentThumbnailCandidate>> ListAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var now = timeProvider.GetUtcNow();
        var skipped = deferred.Current(now);
        var filedAfter = now - DocumentThumbnailCache.PlainIdleLifetime;
        var variant = DocumentThumbnailCache.Variant;
        const long maximumLength = ImageThumbnailRendering.MaximumSourceBytes;

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
                    && version.CreatedAtUtc > filedAfter
                    && version.ContentLength > 0
                    && version.ContentLength <= maximumLength
                    && EF.Functions.Like(version.MediaType, "image/%")
                    && !EF.Functions.Like(version.MediaType, "image/svg%")
                    && !db.Set<DocumentContentCacheEntryEntity>().Any(entry =>
                        entry.DocumentVersionId == version.Id
                        && entry.Variant == variant
                        && entry.ExpiresAtUtc > now)
                orderby version.CreatedAtUtc descending, version.Id
                select new DocumentThumbnailCandidate(
                    document.CaseId,
                    version.DocumentId,
                    version.Id,
                    version.Sha256,
                    version.ContentLength,
                    version.MediaType))
            .Take(maximumItems + skipped.Count)
            .ToArrayAsync(cancellationToken);
        return [.. rows.Where(row => !skipped.Contains(row.VersionId)).Take(maximumItems)];
    }

    public void Defer(Guid versionId) => deferred.Defer(versionId);
}

/// <summary>
/// The candidate list of a profile with no thumbnail cache: nothing, because
/// a thumbnail made there would not be kept for its first view.
/// </summary>
public sealed class NoDocumentThumbnailCandidates : IListDocumentThumbnailCandidates
{
    public Task<IReadOnlyList<DocumentThumbnailCandidate>> ListAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        return Task.FromResult<IReadOnlyList<DocumentThumbnailCandidate>>([]);
    }

    public void Defer(Guid versionId)
    {
    }
}
