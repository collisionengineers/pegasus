using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Pegasus.Core.PhotoCapture;
using Pegasus.Web.PhotoCapture;

namespace Pegasus.Web.Pages.Capture;

/// <summary>
/// The page a standing link opens (FRD-28): the same app, which starts a
/// capture or hands one to a customer and never takes a photo itself. A
/// paused, replaced or unknown slug gets the one unavailable card and a 404.
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[EnableRateLimiting(PhotoCaptureSurface.CapturePagePolicy)]
public sealed class StandingModel(
    PhotoCaptureAccess access,
    CaptureContextFactory contexts,
    CaptureAssets assets) : PageModel, Pegasus.Web.Presentation.IShellFreeGet
{
    public CaptureDocumentView Document { get; private set; } = CaptureDocumentView.Empty;

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        CaptureResponseHeaders.Apply(Response);
        var resolved = await access.ResolveSlugAsync(slug, cancellationToken);
        if (resolved.StandingLink is not { } link)
        {
            return CaptureCardView.UnavailableResult(this);
        }

        Document = new(
            CaptureContextFactory.Serialize(await contexts.ForStandingAsync(link, cancellationToken)),
            assets.ManifestJson(Request.PathBase));
        return Page();
    }
}
