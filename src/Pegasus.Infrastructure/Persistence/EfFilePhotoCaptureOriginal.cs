using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Files a photo's full-size original as the next version of the Case image
/// its compressed picture was filed as (FRD-28 Routing and filing): the image
/// keeps its tile, tags, report place and crop, Box shows the compressed
/// picture as the earlier version of the one file (R664), and the report
/// prints the original once it is filed. Nothing is filed until the
/// compressed picture is a confirmed Case image; the phone sends the original
/// again later.
/// </summary>
public sealed class EfFilePhotoCaptureOriginal(
    IDbContextFactory<PegasusDbContext> contextFactory,
    ICaseArtifactCustody? custody = null) : IFilePhotoCaptureOriginal
{
    public const string ActorId = "photo-capture-original";

    public async Task<bool> FileAsync(
        PhotoCaptureItem item,
        string fileName,
        string mediaType,
        long contentLength,
        string sha256,
        Func<CancellationToken, ValueTask<Stream>> openContentAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentNullException.ThrowIfNull(openContentAsync);
        if (custody is null || item.IntakeAssetId is not { } assetId)
        {
            return false;
        }

        FiledImage? image;
        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            image = await FiledImageAsync(context, item, assetId, cancellationToken);
        }

        if (image is null)
        {
            return false;
        }

        CaseArtifactCustodyResult result;
        await using (var content = await openContentAsync(cancellationToken))
        {
            result = await custody.RetainNextVersionAsync(
                new CaseArtifactVersionRequest(
                    ActionActor.SystemWorker(ActorId),
                    image.CaseId,
                    image.OccurrenceId,
                    PhotoCaptureStory.OriginalFileName(image.FileName, mediaType),
                    mediaType,
                    contentLength,
                    sha256,
                    content),
                cancellationToken);
        }

        return result.Disposition is CaseArtifactCustodyDisposition.Confirmed or CaseArtifactCustodyDisposition.Pending;
    }

    /// <summary>The Case image the item's compressed picture stands as: on the capture's own Case, or the Case its receipt was filed on.</summary>
    private sealed record FiledImage(Guid CaseId, Guid OccurrenceId, string FileName);

    /// <summary>
    /// The confirmed, current Case image filed from the item's intake asset,
    /// with the name its current version carries. Null while the picture is
    /// still filing or when it is on no Case.
    /// </summary>
    private static async Task<FiledImage?> FiledImageAsync(
        PegasusDbContext context,
        PhotoCaptureItem item,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var receiptId = item.IntakeReceiptId;
        var candidates = await context.PhotoCaptures.AsNoTracking()
            .Where(capture => capture.Id == item.CaptureId && capture.CaseId != null)
            .Select(capture => capture.CaseId!.Value)
            .Concat(context.IntakeManualAssociations.AsNoTracking()
                .Where(association => association.IntakeReceiptId == receiptId && association.IsActive)
                .Select(association => association.CaseId))
            .Concat(context.CaseIntakeLinks.AsNoTracking()
                .Where(link => link.IntakeReceiptId == receiptId)
                .Select(link => link.CaseId))
            .ToListAsync(cancellationToken);
        var caseIds = candidates.Distinct().ToArray();
        if (caseIds.Length == 0)
        {
            return null;
        }

        var identity = assetId.ToString("N");
        return await (
            from document in context.Set<CaseDocumentEntity>().AsNoTracking()
            join occurrence in context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                on document.Id equals occurrence.DocumentId
            join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                on occurrence.VersionId equals version.Id
            where EF.Parameter(caseIds).Contains(document.CaseId)
                && document.SourceOccurrenceIdentity == identity
                && occurrence.CaseId == document.CaseId
                && occurrence.SemanticRole == DocumentSemanticRole.Image
                && version.IsCurrent
                && !version.IsLogicallyRemoved
                && version.CustodyStatus == DocumentCustodyStatus.Confirmed
            select new FiledImage(document.CaseId, occurrence.Id, version.FileName))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
