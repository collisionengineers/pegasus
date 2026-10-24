using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// The versions the Worker's thumbnail sweep makes thumbnails for:
/// confirmed, current image versions filed within the plain thumbnail's idle
/// lifetime that have no live plain thumbnail, newest first; then the saved
/// rotation and crop of an image occurrence, under its own variant, that has
/// no live thumbnail of that variant, for a version filed within the prepared
/// thumbnail's idle lifetime.
/// </summary>
/// <remarks>
/// A version whose thumbnail could not be made (a format the renderer does
/// not decode, or a read that failed for now) is deferred: this process does
/// not list it again for <see cref="DeferInterval"/>, so it cannot take a
/// place in every run ahead of newer photographs (<see cref="DeferredVersionSet"/>).
/// A made thumbnail leaves the candidate set by itself and is not remembered.
/// A version older than the lifetime is left
/// to its first view, so an expired thumbnail is not made again for nobody.
/// A saved rotation and crop is listed only while its version was filed within
/// the prepared thumbnail's own idle lifetime, and for that lifetime after it
/// was filed on the Case, which is as long as the thumbnail made for it lives
/// unviewed, so it is made once ahead of its first view and not again for
/// nobody. The plain thumbnails are listed first and
/// fill the run before any saved variant does.
/// </remarks>
internal sealed class EfDocumentThumbnailCandidates(
    IDbContextFactory<PegasusDbContext> dbContextFactory,
    TimeProvider timeProvider) : IListDocumentThumbnailCandidates
{
    private static readonly TimeSpan DeferInterval = TimeSpan.FromDays(1);

    private readonly DeferredVersionSet deferred = new(timeProvider, DeferInterval);

    /// <summary>
    /// One statement, driven from the versions filed within the plain
    /// thumbnail's idle lifetime: a version is returned when it has no live
    /// plain thumbnail or, filed within the prepared thumbnail's idle
    /// lifetime, has an image occurrence with a saved rotation or crop. An
    /// occurrence counts from the time it was recorded on the Case (filing an
    /// image copies the preparation of Image Intake onto the Case, so the
    /// filing is when its variant is first wanted), and is left out in SQL when
    /// a prepared thumbnail (other than the original content, which filing and
    /// any read publish to the same table) was written since that time; an
    /// edit after the filing is left to the page's own redraw. Versions with no plain
    /// thumbnail come first, newest first, so the plain thumbnails fill the
    /// run before any saved variant does. The cached variants of the rows that
    /// come back are compared in memory, because the token is formatted by
    /// Core and not by SQL; rows that give nothing there (and the deferred
    /// ones) are replaced by the next rows in order, so the run is filled while
    /// candidates remain.
    /// </summary>
    public async Task<IReadOnlyList<DocumentThumbnailCandidate>> ListAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var now = timeProvider.GetUtcNow();
        var skipped = deferred.Current(now);
        var filedAfter = now - DocumentThumbnailCache.PlainIdleLifetime;
        var savedAfter = now - DocumentThumbnailCache.PreparedIdleLifetime;
        var preparedIdleHours = DocumentThumbnailCache.PreparedIdleLifetime.TotalHours;
        var plainVariant = DocumentThumbnailCache.Variant;
        var originalVariant = CachedDocumentContentStore.OriginalVariant;
        const long maximumLength = ImageThumbnailRendering.MaximumSourceBytes;

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var wanting =
            from occurrence in db.Set<DocumentOccurrenceEntity>().AsNoTracking()
            where occurrence.SemanticRole == DocumentSemanticRole.Image
                && (occurrence.RotationDegrees != 0 || occurrence.CropLeft != null)
                && occurrence.RecordedAtUtc > savedAfter
                // A prepared thumbnail written since the filing is the one this
                // occurrence is waiting for (a write gives it the idle lifetime
                // from that moment); a later edit is left to the page's redraw.
                && !db.Set<DocumentContentCacheEntryEntity>().Any(entry =>
                    entry.DocumentVersionId == occurrence.VersionId
                    && entry.Variant != plainVariant
                    && entry.Variant != originalVariant
                    && entry.ExpiresAtUtc > occurrence.RecordedAtUtc.AddHours(preparedIdleHours))
            select new
            {
                occurrence.VersionId,
                occurrence.RotationDegrees,
                occurrence.CropLeft,
                occurrence.CropTop,
                occurrence.CropWidth,
                occurrence.CropHeight
            };
        var query =
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
            let hasPlain = db.Set<DocumentContentCacheEntryEntity>().Any(entry =>
                entry.DocumentVersionId == version.Id
                && entry.Variant == plainVariant
                && entry.ExpiresAtUtc > now)
            where !hasPlain
                || (version.CreatedAtUtc > savedAfter
                    && wanting.Any(saved => saved.VersionId == version.Id))
            orderby hasPlain ? 1 : 0, version.CreatedAtUtc descending, version.Id
            select new
            {
                document.CaseId,
                version.DocumentId,
                VersionId = version.Id,
                version.Sha256,
                version.ContentLength,
                version.MediaType,
                HasPlain = hasPlain,
                Saved = wanting
                    .Where(saved => saved.VersionId == version.Id && version.CreatedAtUtc > savedAfter)
                    .ToList(),
                Cached = db.Set<DocumentContentCacheEntryEntity>()
                    .Where(entry => entry.DocumentVersionId == version.Id
                        && entry.Variant != plainVariant
                        && entry.Variant != originalVariant
                        && entry.ExpiresAtUtc > now)
                    .Select(entry => entry.Variant)
                    .ToList()
            };

        // Deferred versions are skipped here rather than sent to SQL: each
        // page asks for enough rows to fill the run past every one of them.
        List<DocumentThumbnailCandidate> listed = [];
        var seen = new HashSet<(Guid VersionId, string Variant)>();
        var offset = 0;
        while (listed.Count < maximumItems)
        {
            var take = maximumItems - listed.Count + skipped.Count;
            var page = await query.Skip(offset).Take(take).ToArrayAsync(cancellationToken);
            offset += page.Length;
            // A page holds every version without a plain thumbnail before any
            // other, so its plain candidates come ahead of its saved variants.
            foreach (var row in page)
            {
                if (listed.Count >= maximumItems)
                {
                    return listed;
                }
                if (!row.HasPlain && !skipped.Contains(row.VersionId))
                {
                    listed.Add(new DocumentThumbnailCandidate(
                        row.CaseId,
                        row.DocumentId,
                        row.VersionId,
                        row.Sha256,
                        row.ContentLength,
                        row.MediaType));
                }
            }
            foreach (var row in page)
            {
                if (skipped.Contains(row.VersionId))
                {
                    continue;
                }
                foreach (var saved in row.Saved)
                {
                    if (listed.Count >= maximumItems)
                    {
                        return listed;
                    }
                    var rotation = (CaseAssetRotation)saved.RotationDegrees;
                    CaseAssetCrop? crop = saved.CropLeft is { } left
                        && saved.CropTop is { } top
                        && saved.CropWidth is { } width
                        && saved.CropHeight is { } height
                            ? new(left, top, width, height)
                            : null;
                    var variant = CaseDocumentThumbnails.VariantToken(rotation, crop);
                    if (string.Equals(variant, plainVariant, StringComparison.Ordinal)
                        || row.Cached.Contains(variant)
                        || !seen.Add((row.VersionId, variant)))
                    {
                        continue;
                    }
                    listed.Add(new DocumentThumbnailCandidate(
                        row.CaseId,
                        row.DocumentId,
                        row.VersionId,
                        row.Sha256,
                        row.ContentLength,
                        row.MediaType,
                        rotation,
                        crop));
                }
            }
            if (page.Length < take)
            {
                break;
            }
        }
        return listed;
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
