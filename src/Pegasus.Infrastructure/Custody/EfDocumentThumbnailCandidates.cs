using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// The versions the Worker's thumbnail sweep makes thumbnails for:
/// confirmed, current image versions filed within the plain thumbnail's idle
/// lifetime that have no live plain thumbnail, newest first; then the saved
/// rotation and crop of an image occurrence, under its own variant, that has
/// no live thumbnail of that variant.
/// </summary>
/// <remarks>
/// A version whose thumbnail could not be made (a format the renderer does
/// not decode, or a read that failed for now) is deferred: this process does
/// not list it again for <see cref="DeferInterval"/>, so it cannot take a
/// place in every run ahead of newer photographs (<see cref="DeferredVersionSet"/>).
/// A made thumbnail leaves the candidate set by itself and is not remembered.
/// A version older than the lifetime is left
/// to its first view, so an expired thumbnail is not made again for nobody.
/// A saved rotation and crop is listed for the prepared thumbnail's own idle
/// lifetime after it was saved, which is as long as the thumbnail made for
/// it lives unviewed, so it is made once ahead of its first view and not
/// again for nobody. The plain thumbnails are listed first and fill the run
/// before any saved variant does.
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
        List<DocumentThumbnailCandidate> listed =
            [.. rows.Where(row => !skipped.Contains(row.VersionId)).Take(maximumItems)];
        if (listed.Count < maximumItems)
        {
            await AddSavedVariantsAsync(
                db, listed, maximumItems, skipped, now, filedAfter, cancellationToken);
        }
        return listed;
    }

    /// <summary>
    /// Fills the run with the saved rotation and crop of image occurrences
    /// whose thumbnail of that variant is not cached. The cached variants of
    /// a version come back with its occurrences in the one read, and the
    /// variant of each occurrence is told from them in memory, because the
    /// token is formatted by Core and not by SQL.
    /// </summary>
    private static async Task AddSavedVariantsAsync(
        PegasusDbContext db,
        List<DocumentThumbnailCandidate> listed,
        int maximumItems,
        HashSet<Guid> skipped,
        DateTimeOffset now,
        DateTimeOffset filedAfter,
        CancellationToken cancellationToken)
    {
        var plainVariant = DocumentThumbnailCache.Variant;
        var savedAfter = now - DocumentThumbnailCache.PreparedIdleLifetime;
        const long maximumLength = ImageThumbnailRendering.MaximumSourceBytes;
        var saved = await (
                from occurrence in db.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in db.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                join document in db.Set<CaseDocumentEntity>().AsNoTracking()
                    on version.DocumentId equals document.Id
                where occurrence.SemanticRole == DocumentSemanticRole.Image
                    && (occurrence.RotationDegrees != 0 || occurrence.CropLeft != null)
                    && (occurrence.PreparedAtUtc ?? occurrence.RecordedAtUtc) > savedAfter
                    && version.CustodyStatus == DocumentCustodyStatus.Confirmed
                    && version.IsCurrent
                    && !version.IsLogicallyRemoved
                    && version.CreatedAtUtc > filedAfter
                    && version.ContentLength > 0
                    && version.ContentLength <= maximumLength
                    && EF.Functions.Like(version.MediaType, "image/%")
                    && !EF.Functions.Like(version.MediaType, "image/svg%")
                orderby (occurrence.PreparedAtUtc ?? occurrence.RecordedAtUtc) descending, occurrence.Id
                select new
                {
                    document.CaseId,
                    version.DocumentId,
                    VersionId = version.Id,
                    version.Sha256,
                    version.ContentLength,
                    version.MediaType,
                    occurrence.RotationDegrees,
                    occurrence.CropLeft,
                    occurrence.CropTop,
                    occurrence.CropWidth,
                    occurrence.CropHeight,
                    Cached = db.Set<DocumentContentCacheEntryEntity>()
                        .Where(entry => entry.DocumentVersionId == version.Id
                            && entry.Variant != plainVariant
                            && entry.ExpiresAtUtc > now)
                        .Select(entry => entry.Variant)
                        .ToList()
                })
            .ToArrayAsync(cancellationToken);

        var seen = new HashSet<(Guid VersionId, string Variant)>();
        foreach (var row in saved)
        {
            if (listed.Count >= maximumItems)
            {
                return;
            }
            if (skipped.Contains(row.VersionId))
            {
                continue;
            }
            var rotation = (CaseAssetRotation)row.RotationDegrees;
            CaseAssetCrop? crop = row.CropLeft is { } left
                && row.CropTop is { } top
                && row.CropWidth is { } width
                && row.CropHeight is { } height
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
