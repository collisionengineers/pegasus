using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Files a photo's full-size original on the Case its compressed picture was
/// filed on. No existing path adds a version to an intake-filed image: a
/// regenerated report is a new Case document whose Box file takes the next
/// version (R664), and a staff add needs the Case's edit lease. So the
/// original is filed through Case custody as a document of its own, an image
/// named after the photo with "(full size)", out of the report so the report
/// keeps the compressed picture. Nothing is filed until the compressed
/// picture is on a Case; the phone sends the original again later.
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

        var operationKey = $"photo-capture-original:{item.Id:N}";
        Guid caseId;
        bool alreadyRecorded;
        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            var filedOn = await FiledCaseAsync(context, item, assetId, cancellationToken);
            if (filedOn is null)
            {
                return false;
            }

            caseId = filedOn.Value;
            alreadyRecorded = await context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                .AnyAsync(value => value.CaseId == caseId && value.OperationKey == operationKey, cancellationToken);
        }

        CaseArtifactCustodyResult result;
        await using (var content = await openContentAsync(cancellationToken))
        {
            result = await custody.RetainAsync(
                new CaseArtifactCustodyRequest(
                    ActionActor.SystemWorker(ActorId),
                    caseId,
                    IntakeReceiptId: null,
                    OccurrenceIdentity: operationKey,
                    OperationKey: operationKey,
                    FullSizeName(fileName, mediaType),
                    mediaType,
                    contentLength,
                    sha256,
                    content,
                    SemanticRole: DocumentSemanticRole.Image,
                    Source: DocumentSource.Intake),
                cancellationToken);
        }

        if (result.Disposition is not (CaseArtifactCustodyDisposition.Confirmed or CaseArtifactCustodyDisposition.Pending))
        {
            return false;
        }

        if (!alreadyRecorded && result.OccurrenceId is { } occurrenceId)
        {
            // A new image starts In report; the original stays out of it, so
            // the report keeps the picture the sender took. Only on the first
            // filing: a replay never undoes what staff chose since.
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await context.Set<DocumentOccurrenceEntity>()
                .Where(value => value.Id == occurrenceId && value.CaseId == caseId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.InReport, false), cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// The Case the item's compressed picture is a current document of: the
    /// capture's own Case, or the Case its receipt was filed on.
    /// </summary>
    private static async Task<Guid?> FiledCaseAsync(
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
            join version in context.Set<DocumentVersionEntity>().AsNoTracking()
                on document.Id equals version.DocumentId
            where EF.Parameter(caseIds).Contains(document.CaseId)
                && document.SourceOccurrenceIdentity == identity
                && version.IsCurrent
                && !version.IsLogicallyRemoved
                && version.CustodyStatus != DocumentCustodyStatus.Failed
            select (Guid?)document.CaseId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>The photo's own name with "(full size)", and the extension of the original's own type.</summary>
    internal static string FullSizeName(string fileName, string mediaType)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = IntakeUploadFilePolicy.ExtensionFor(mediaType);
        if (extension.Length == 0)
        {
            extension = Path.GetExtension(fileName);
        }

        return $"{stem} (full size){extension}";
    }
}
