using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages;

/// <summary>
/// An uploaded image, served inline to the upload review from the bytes intake
/// holds, so its tile shows at once while the file is processed and before
/// custody confirms it (operator, 28 October 2026, R463). The id is the staged
/// receipt the review already names. The browser keeps the image only when
/// <c>v=</c> names its content hash, as for every intake image
/// (<see cref="IntakeImageAddress"/>).
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class UploadImageModel(ReadUploadedImage readUploadedImage) : StaffPageModel, IShellFreeGet
{
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        [FromQuery] string? v = null,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = IntakeImageAddress.UncachedCacheControl;
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var image = await readUploadedImage.ExecuteAsync(id, actor, cancellationToken);
            if (image is null)
            {
                return NotFound();
            }

            if (IntakeImageAddress.NamesContent(v, image.Sha256))
            {
                Response.Headers.CacheControl = IntakeImageAddress.KeptCacheControl;
                Response.Headers.ETag = IntakeImageAddress.ContentETag(image.Sha256);
            }
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
            {
                FileName = image.FileName
            }.ToString();
            return File(DocumentContentCachePublisherExtensions.StreamOf(image.Content), image.ContentType);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (IntakeArtifactIntegrityException)
        {
            return StatusCode(StatusCodes.Status409Conflict);
        }
    }
}
