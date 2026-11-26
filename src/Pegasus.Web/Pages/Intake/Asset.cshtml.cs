using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Intake;

/// <summary>
/// Serves one retained intake asset. Only the admitted raster image types
/// render inline for the evidence viewer; every other type is a forced
/// download, so retained SVG, HTML or scripts can never execute from this
/// origin. Asked with <c>inline=true</c>, as an e-mail's View asks (R1275),
/// a PDF is inline too; anything else, another image type included, is
/// still the download.
/// </summary>
/// <remarks>
/// An inline answer is kept by the browser only when the address names its
/// current content (<see cref="IntakeImageAddress"/>); every other answer,
/// including every refusal, is <c>private, no-store</c>. A Word file is
/// viewed as its PDF rendering, from the <c>Pdf</c> handler, which answers
/// as the Case's rendering does.
/// </remarks>
public sealed partial class AssetModel(
    IDownloadIntakeAsset downloadAsset,
    ReadPreCaseImageThumbnail readPreCaseThumbnail,
    IRenderDocumentAsPdf renderDocumentAsPdf,
    ILogger<AssetModel> logger) : StaffPageModel, Pegasus.Web.Presentation.IShellFreeGet
{
    /// <summary>How long a caller waits before asking again for a rendering not made yet.</summary>
    private const int TransientRetrySeconds = 5;

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
        [FromQuery] bool inline = false,
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
            IntakeSourceDownload? asset;
            if (wantsTile)
            {
                var read = await readPreCaseThumbnail.OpenAsync(new PreCaseImageThumbnailQuery(id, assetId, actor), cancellationToken);
                if (read.Tile is { } tile)
                {
                    return TileResult(tile, v, prep, renderer);
                }
                // The tile read already holds the verified original.
                asset = read.Original;
            }
            else
            {
                asset = await downloadAsset.ExecuteAsync(
                    new DownloadIntakeAssetQuery(id, assetId, actor),
                    cancellationToken);
            }

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
            if (!CaseDocumentPreviews.IsInlineIntakeImage(mediaType.MediaType))
            {
                // The viewer's own request (R1275): a PDF, an octet-stream
                // one known by its name included, is shown as
                // application/pdf. The admitted images are served above; any
                // other image type, SVG, video, Word and anything unknown
                // stay the download, because the type is the sender's.
                var kind = CaseDocumentPreviews.KindOf(asset.ContentType, asset.FileName);
                if (inline && !wantsTile && kind is CaseDocumentPreviewKind.Pdf)
                {
                    if (IntakeImageAddress.NamesContent(v, asset.Sha256))
                    {
                        Response.Headers.CacheControl = IntakeImageAddress.KeptCacheControl;
                        Response.Headers.ETag = IntakeImageAddress.ContentETag(asset.Sha256);
                    }
                    Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
                    {
                        FileName = IntakeDownloadFileName.Safe(asset.FileName, "intake-asset.bin")
                    }.ToString();
                    return File(
                        DocumentContentCachePublisherExtensions.StreamOf(asset.Content),
                        CaseDocumentPreviews.ShownMediaType(asset.ContentType, asset.FileName));
                }
                return Saved(asset);
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
            return File(DocumentContentCachePublisherExtensions.StreamOf(asset.Content), asset.ContentType);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (IntakeCustodyUnavailableException)
        {
            return IntakeCustodyUnavailableResult.Create();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (IntakeArtifactIntegrityException exception)
        {
            LogIntakeAssetIntegrityFailure(logger, id, assetId, exception);
            return IntegrityConflict();
        }
    }

    /// <summary>
    /// The viewer's PDF rendering of a retained Word file (R1275), answered as
    /// the Case's rendering is (<see cref="Pegasus.Web.Pages.Cases.Documents.DownloadModel"/>):
    /// the PDF inline, kept by the browser only when the address names the
    /// current rendering; 422 problem+json when none can be made and the
    /// viewer's script asked, else the file itself, as it was before there was
    /// a rendering; 503 with <c>Retry-After</c> while one is still being made,
    /// custody is not yet confirmed or Box is busy; 404 when the file is not
    /// there or not this receipt's. Anything but a Word file is the download.
    /// </summary>
    public async Task<IActionResult> OnGetPdfAsync(
        Guid id,
        Guid assetId,
        [FromQuery] string? renderer = null,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = IntakeImageAddress.UncachedCacheControl;
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        IntakeSourceDownload? asset;
        try
        {
            asset = await downloadAsset.ExecuteAsync(
                new DownloadIntakeAssetQuery(id, assetId, actor),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (IntakeCustodyUnavailableException)
        {
            return TransientlyUnavailable();
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (IntakeArtifactIntegrityException exception)
        {
            LogIntakeAssetIntegrityFailure(logger, id, assetId, exception);
            return IntegrityConflict();
        }
        if (asset is null)
        {
            return NotFound();
        }
        Response.Headers.XContentTypeOptions = "nosniff";
        if (CaseDocumentPreviews.KindOf(asset.ContentType, asset.FileName) != CaseDocumentPreviewKind.WordAsPdf)
        {
            return Saved(asset);
        }

        var etag = $"\"{asset.Sha256.ToLowerInvariant()}-{CaseDocumentPreviews.RenditionVariant}\"";
        var addressIsCurrent = string.Equals(
            renderer, CaseDocumentPreviews.RenditionIdentity, StringComparison.Ordinal);
        if (addressIsCurrent && Pegasus.Web.Pages.Cases.Documents.DownloadModel.HoldsRepresentation(Request, etag))
        {
            SetRenditionCaching(etag, cacheable: true);
            return StatusCode(StatusCodes.Status304NotModified);
        }

        byte[]? rendering;
        try
        {
            rendering = await renderDocumentAsPdf.RenderIntakeAssetAsync(
                actor,
                new IntakeAssetPreview(id, assetId, asset.FileName, asset.ContentType, asset.ContentLength, asset.Sha256),
                cancellationToken);
        }
        catch (IntakeCustodyUnavailableException)
        {
            return TransientlyUnavailable();
        }
        catch (Exception exception) when (Pegasus.Web.Pages.Cases.Documents.DownloadModel.IsMissing(exception))
        {
            LogIntakeAssetRenditionUnavailable(logger, id, assetId, exception);
            return NotFound();
        }
        catch (Exception exception) when (Pegasus.Web.Pages.Cases.Documents.DownloadModel.IsTransient(exception))
        {
            LogIntakeAssetRenditionUnavailable(logger, id, assetId, exception);
            return TransientlyUnavailable();
        }
        if (rendering is null)
        {
            if (!Request.Headers.ContainsKey(Pegasus.Web.Pages.Cases.Documents.DownloadModel.DocumentPreviewHeader))
            {
                // A plain link, with no script to show a refusal: it gives the file.
                return Saved(asset);
            }
            var refusal = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
            })
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity,
            };
            refusal.ContentTypes.Add("application/problem+json");
            return refusal;
        }

        SetRenditionCaching(etag, cacheable: addressIsCurrent);
        Response.ContentLength = rendering.LongLength;
        // Named as what these bytes are; the Word file is the saved download.
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
        {
            FileName = IntakeDownloadFileName.Safe(Path.ChangeExtension(asset.FileName, ".pdf"), "intake-asset.pdf")
        }.ToString();
        return File(rendering, CaseDocumentPreviews.RenditionMediaType);
    }

    /// <summary>The forced download every asset the viewer does not draw is.</summary>
    private FileStreamResult Saved(IntakeSourceDownload asset) =>
        File(
            DocumentContentCachePublisherExtensions.StreamOf(asset.Content),
            "application/octet-stream",
            IntakeDownloadFileName.Safe(asset.FileName, "intake-asset.bin"));

    private void SetRenditionCaching(string etag, bool cacheable)
    {
        Response.Headers.CacheControl = cacheable
            ? IntakeImageAddress.KeptCacheControl
            : IntakeImageAddress.UncachedCacheControl;
        if (cacheable)
        {
            Response.Headers.ETag = etag;
        }
    }

    private StatusCodeResult TransientlyUnavailable()
    {
        Response.Headers.CacheControl = IntakeImageAddress.UncachedCacheControl;
        Response.Headers.RetryAfter = TransientRetrySeconds.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        return StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    private static ContentResult IntegrityConflict() => new()
    {
        StatusCode = StatusCodes.Status409Conflict,
        ContentType = "text/plain; charset=utf-8",
        Content = "The retained image could not be displayed safely."
    };

    private FileStreamResult TileResult(PreCaseImageThumbnail tile, string? v, string? prep, string? renderer)
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


    [LoggerMessage(
        EventId = 1207,
        Level = LogLevel.Warning,
        Message = "Retained intake asset integrity validation failed for receipt {ReceiptId}, asset {AssetId}.")]
    private static partial void LogIntakeAssetIntegrityFailure(
        ILogger logger,
        Guid receiptId,
        Guid assetId,
        Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The PDF rendering of retained intake receipt {ReceiptId}, asset {AssetId} was unavailable.")]
    private static partial void LogIntakeAssetRenditionUnavailable(
        ILogger logger,
        Guid receiptId,
        Guid assetId,
        Exception exception);
}
