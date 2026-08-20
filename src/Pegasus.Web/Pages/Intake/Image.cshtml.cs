using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Pages.Intake;

/// <summary>
/// Serves one retained intake image inline for viewing, beside
/// <see cref="SourceModel"/> which serves any retained source as a download.
/// Only a true <c>image/*</c> media type is ever rendered inline — everything
/// else stays on the forced-download route, so retained HTML or scripts can
/// never execute from this origin.
/// </summary>
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class ImageModel(
    IDownloadIntakeSource downloadSource,
    ILogger<ImageModel> logger) : StaffPageModel
{
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var source = await downloadSource.ExecuteAsync(
                new DownloadIntakeSourceQuery(id, actor),
                cancellationToken);
            if (source is null)
            {
                return NotFound();
            }
            // Defence-in-depth restatement of the Core image rule
            // (ImageIntakeLifecycleRules.IsImageOnlyMaterial): this endpoint
            // accepts any receipt id, so it gates on the parsed type itself.
            if (!MediaTypeHeaderValue.TryParse(source.ContentType, out var mediaType)
                || !mediaType.Type.Equals("image", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
            {
                FileName = source.FileName
            }.ToString();
            return File(source.Content.ToArray(), source.ContentType);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (IntakeArtifactIntegrityException exception)
        {
            LogIntakeImageIntegrityFailure(logger, id, exception);
            return new ContentResult
            {
                StatusCode = StatusCodes.Status409Conflict,
                ContentType = "text/plain; charset=utf-8",
                Content = "The retained image could not be displayed safely."
            };
        }
    }

    [LoggerMessage(
        EventId = 1206,
        Level = LogLevel.Warning,
        Message = "Retained intake image integrity validation failed for receipt {ReceiptId}.")]
    private static partial void LogIntakeImageIntegrityFailure(
        ILogger logger,
        Guid receiptId,
        Exception exception);
}
