using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Intake;

/// <summary>
/// Serves one retained intake asset. Only the admitted raster image types
/// render inline for the evidence viewer; every other type is a forced
/// download, so retained SVG, HTML or scripts can never execute from this
/// origin.
/// </summary>
/// <remarks>
/// An inline image is kept by the browser only when the address names its
/// current content (<see cref="IntakeImageAddress"/>); every other answer,
/// including every refusal, is <c>private, no-store</c>.
/// </remarks>
public sealed partial class AssetModel(
    IDownloadIntakeAsset downloadAsset,
    IReadPreCaseImageThumbnail readPreCaseThumbnail,
    ILogger<AssetModel> logger) : StaffPageModel
{
    /// <summary>
    /// <c>size=thumb</c> asks for a pre-Case image's tile: the recorded crop and
    /// rotation, or the whole frame when none is recorded, drawn as the Case
    /// gallery draws them. An image that cannot be rendered is served whole as
    /// before.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        Guid assetId,
        [FromQuery] string? size = null,
        [FromQuery] string? v = null,
        [FromQuery] string? prep = null,
        [FromQuery] string? renderer = null,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = IntakeImageAddress.UncachedCacheControl;
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var wantsTile = string.Equals(
                size, Pegasus.Core.Documents.CaseDocumentThumbnails.ThumbSizeToken, StringComparison.Ordinal);
            if (wantsTile
                && await readPreCaseThumbnail.OpenAsync(new PreCaseImageThumbnailQuery(id, assetId, actor), cancellationToken) is { } tile)
            {
                var rendering = tile.Rendering;
                if (IntakeImageAddress.NamesContent(v, rendering.SourceSha256)
                    && IntakeImageAddress.NamesPreparation(prep, renderer, tile.PreparationVersion))
                {
                    Response.Headers.CacheControl = IntakeImageAddress.KeptCacheControl;
                    Response.Headers.ETag = IntakeImageAddress.TileETag(
                        rendering.SourceSha256, tile.PreparationVersion);
                }
                Response.Headers.XContentTypeOptions = "nosniff";
                Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline").ToString();
                return File(rendering.Content, rendering.MediaType);
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

            Response.Headers.XContentTypeOptions = "nosniff";
            // Keep the intake route at the same safe raster boundary used by
            // report rendering. SVG is an image media type but can execute
            // active content when navigated from this origin.
            if (!mediaType.MediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
                && !mediaType.MediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
                && !mediaType.MediaType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
            {
                return File(
                    ContentStream(asset.Content),
                    "application/octet-stream",
                    SafeFileName(asset.FileName));
            }
            // The whole image on a tile's address is the answer for this one
            // request only: the tile's rendering may work on the next one.
            if (!wantsTile && IntakeImageAddress.NamesContent(v, asset.Sha256))
            {
                Response.Headers.CacheControl = IntakeImageAddress.KeptCacheControl;
                Response.Headers.ETag = IntakeImageAddress.ContentETag(asset.Sha256);
            }
            Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
            {
                FileName = asset.FileName
            }.ToString();
            return File(ContentStream(asset.Content), asset.ContentType);
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

    /// <summary>
    /// The verified bytes as a read-only stream over the array they already
    /// sit in, so the response is not a second copy of the file. Only bytes
    /// that are not held in an array are copied.
    /// </summary>
    private static MemoryStream ContentStream(ReadOnlyMemory<byte> content) =>
        MemoryMarshal.TryGetArray(content, out var segment)
            ? new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(content.ToArray(), writable: false);

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
