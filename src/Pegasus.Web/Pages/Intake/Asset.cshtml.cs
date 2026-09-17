using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Pages.Intake;

/// <summary>
/// Serves one retained intake asset. Only the admitted raster image types
/// render inline for the evidence viewer; every other type is a forced
/// download, so retained SVG, HTML or scripts can never execute from this
/// origin.
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
            if (!MediaTypeHeaderValue.TryParse(asset.ContentType, out var mediaType))
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            // Keep the intake route at the same safe raster boundary used by
            // report rendering. SVG is an image media type but can execute
            // active content when navigated from this origin.
            if (!mediaType.MediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
                && !mediaType.MediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
                && !mediaType.MediaType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
            {
                return File(
                    asset.Content.ToArray(),
                    "application/octet-stream",
                    SafeFileName(asset.FileName));
            }
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
        catch (IntakeCustodyUnavailableException)
        {
            return CustodyUnavailable();
        }
        catch (FileNotFoundException)
        {
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

    private static ContentResult CustodyUnavailable() => new()
    {
        StatusCode = StatusCodes.Status409Conflict,
        ContentType = "text/plain; charset=utf-8",
        Content = "The retained file is not available until durable storage is confirmed. Refresh this record and try again."
    };

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "intake-asset.bin";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Concat(name.Where(character =>
            !char.IsControl(character)
            && character != '"'
            && character != '\''
            && character != ';'
            && !invalid.Contains(character)));
        return string.IsNullOrWhiteSpace(safe) ? "intake-asset.bin" : safe;
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
