using System.Collections.Concurrent;
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
/// A version offered once is not offered again by this process for
/// <see cref="OfferInterval"/>. One that was rendered has left the candidate
/// set anyway; one that could not be rendered (a format the renderer does
/// not decode) or failed for now waits, rather than taking a place in every
/// run ahead of newer photographs. A version older than the lifetime is left
/// to its first view, so an expired thumbnail is not made again for nobody.
/// </remarks>
internal sealed class EfDocumentThumbnailCandidates(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    TimeProvider timeProvider) : IListDocumentThumbnailCandidates
{
    private static readonly TimeSpan OfferInterval = TimeSpan.FromDays(1);

    private readonly ConcurrentDictionary<Guid, DateTimeOffset> offered = [];

    public async Task<IReadOnlyList<DocumentThumbnailCandidate>> ListAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var now = timeProvider.GetUtcNow();
        foreach (var entry in offered)
        {
            if (entry.Value <= now)
            {
                offered.TryRemove(entry);
            }
        }
        var recentlyOffered = offered.Keys.ToArray();
        var filedAfter = now - DocumentThumbnailCache.PlainIdleLifetime;
        var variant = DocumentThumbnailCache.Variant;
        const long maximumLength = ImageThumbnailRendering.MaximumSourceBytes;

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await (
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
                    && !recentlyOffered.Contains(version.Id)
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
            .Take(maximumItems)
            .ToArrayAsync(cancellationToken);
        var offeredUntil = now + OfferInterval;
        foreach (var candidate in candidates)
        {
            offered[candidate.VersionId] = offeredUntil;
        }
        return candidates;
    }
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
}
