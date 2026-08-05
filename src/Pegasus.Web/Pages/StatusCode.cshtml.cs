using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Pegasus.Web.Pages;

/// <summary>
/// The designed page for a status code that reaches the browser without an
/// exception behind it: an unknown record URL, a dead external upload link, an
/// oversized upload, a rate-limited sign-in.
/// </summary>
/// <remarks>
/// Anonymous by design. A visitor who has hit a dead public upload link has no
/// account, and a signed-out operator following a stale bookmark should read
/// the sentence rather than a sign-in challenge for a page that does not exist.
/// The page states only what the status code already told the browser, so it
/// discloses nothing.
/// </remarks>
[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public sealed class StatusCodeModel : PageModel
{
    public string Heading { get; private set; } = "We could not complete that request";

    public string Explanation { get; private set; } =
        "Try again, and if it keeps failing, tell your administrator.";

    public bool IsFault { get; private set; }

    /// <summary>
    /// The external upload link is the one screen whose audience is not staff.
    /// Offering it a link into the internal dashboard would both fail (the
    /// visitor has no account) and disclose that the dashboard exists.
    /// </summary>
    public bool ShowReturnToDashboard { get; private set; } = true;

    public void OnGet(int code)
    {
        var isExternalSurface = IsExternalSurface();
        ShowReturnToDashboard = !isExternalSurface;

        switch (code)
        {
            case StatusCodes.Status404NotFound when isExternalSurface:
                Heading = "This link is no longer active";
                Explanation =
                    "The link may have expired, already been used, or been withdrawn. "
                    + "Ask the person who sent it for a new one.";
                break;

            case StatusCodes.Status404NotFound:
                Heading = "We could not find that page";
                Explanation = "The link may be out of date, or the address may have been mistyped.";
                break;

            case StatusCodes.Status413PayloadTooLarge:
            case StatusCodes.Status400BadRequest:
                Heading = "That file is too large";
                Explanation = "Files must be 10 MB or smaller. Choose a smaller file and try again.";
                break;

            case StatusCodes.Status429TooManyRequests:
                Heading = "Too many sign-in attempts";
                Explanation = "Wait a minute, then try again.";
                break;

            case StatusCodes.Status403Forbidden:
                Heading = "Access denied";
                Explanation = "Your account does not have access to this page.";
                break;

            default:
                IsFault = code >= StatusCodes.Status500InternalServerError;
                break;
        }
    }

    private bool IsExternalSurface()
    {
        var originalPath = HttpContext
            .Features
            .Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>()
            ?.OriginalPath;

        return originalPath is not null
            && originalPath.StartsWith("/uploads", StringComparison.OrdinalIgnoreCase);
    }
}
