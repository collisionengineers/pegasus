using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Pages.Intake;

/// <summary>
/// Serves one retained intake asset (an evidence photograph) inline, beside
/// <see cref="ImageModel"/> which serves an image receipt's source. Only a
/// true <c>image/*</c> media type is ever rendered inline — everything else
/// stays off this route, so retained HTML or scripts can never execute from
/// this origin.
/// </summary>
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class AssetModel(
    IDownloadIntakeAsset downloadAsset,
    IReadPreCaseImageThumbnail readPreCaseThumbnail,
    ILogger<AssetModel> logger) : StaffPageModel
{
    /// <summary>
    /// <c>size=thumb</c> asks for a pre-Case image's tile: the recorded crop and
    /// rotation drawn as the Case gallery draws them. An image with nothing
    /// recorded, or one that cannot be rendered, is served whole as before.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        Guid assetId,
        [FromQuery] string? size = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            if (string.Equals(size, Pegasus.Core.Documents.CaseDocumentThumbnails.ThumbSizeToken, StringComparison.Ordinal)
                && await readPreCaseThumbnail.OpenAsync(new PreCaseImageThumbnailQuery(id, assetId, actor), cancellationToken) is { } thumbnail)
            {
                Response.Headers.CacheControl = "private, no-store";
                Response.Headers.XContentTypeOptions = "nosniff";
                Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline").ToString();
                return File(thumbnail.Content, thumbnail.MediaType);
            }

            var asset = await downloadAsset.ExecuteAsync(
                new DownloadIntakeAssetQuery(id, assetId, actor),
                cancellationToken);
            if (asset is null)
            {
                return NotFound();
            }
            // Defence-in-depth restatement of the image-only rule: this
            // endpoint accepts any asset id, so it gates on the parsed type.
            if (!MediaTypeHeaderValue.TryParse(asset.ContentType, out var mediaType)
                || !mediaType.Type.Equals("image", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
            {
                FileName = asset.FileName
            }.ToString();
            return File(asset.Content.ToArray(), asset.ContentType);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (FileNotFoundException)
        {
            // Durable custody has not confirmed the bytes yet: the receipt is
            // still being filed, so there is nothing to serve, not a fault.
            return NotFound();
        }
        catch (IntakeArtifactIntegrityException exception)
        {
            LogIntakeAssetIntegrityFailure(logger, id, assetId, exception);
            return new ContentResult
            {
                StatusCode = StatusCodes.Status409Conflict,
                ContentType = "text/plain; charset=utf-8",
                Content = "The retained image could not be displayed safely."
            };
        }
    }

    [LoggerMessage(
        EventId = 1207,
        Level = LogLevel.Warning,
        Message = "Retained intake asset integrity validation failed for receipt {ReceiptId}, asset {AssetId}.")]
    private static partial void LogIntakeAssetIntegrityFailure(
        ILogger logger,
        Guid receiptId,
        Guid assetId,
        Exception exception);
}
