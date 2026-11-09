using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Pegasus.Core.PhotoCapture;
using Pegasus.Web.PhotoCapture;

namespace Pegasus.Web.Pages.Capture;

/// <summary>
/// The page a photo link opens (FRD-28, ADR-0090): the capture app, outside
/// the staff shell, with no cookie. A link that is unknown, expired, revoked
/// or paused gets the one unavailable card and a 404. Opened is not recorded
/// here, because link previews fetch pages; the app's first API call records it.
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[EnableRateLimiting(PhotoCaptureSurface.CapturePagePolicy)]
public sealed class LinkModel(
    PhotoCaptureAccess access,
    CaptureContextFactory contexts,
    CaptureAssets assets) : PageModel, Pegasus.Web.Presentation.IShellFreeGet
{
    public CaptureDocumentView Document { get; private set; } = CaptureDocumentView.Empty;

    public async Task<IActionResult> OnGetAsync(string token, CancellationToken cancellationToken)
    {
        CaptureResponseHeaders.Apply(Response);
        var resolved = await access.ResolveTokenAsync(token, cancellationToken);
        if (resolved.Capture is not { } capture)
        {
            return CaptureCardView.UnavailableResult(this);
        }

        Document = new(
            CaptureContextFactory.Serialize(await contexts.ForCaptureAsync(capture, cancellationToken)),
            assets.ManifestJson(Request.PathBase));
        return Page();
    }
}
