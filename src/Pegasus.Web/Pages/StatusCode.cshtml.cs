using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Pegasus.Web.Pages;

/// <summary>
/// The designed page for a status code that reaches the browser without an
/// exception behind it: an unknown record URL, an oversized staff upload, or a
/// rate-limited sign-in.
/// </summary>
/// <remarks>
/// Anonymous by design so signed-out operators can read the response for a
/// stale bookmark or failed request. The page states only what the status code
/// already told the browser, so it discloses nothing.
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

    public void OnGet(int code)
    {
        var isSignInSurface = IsStaffSignInSurface();

        switch (code)
        {
            case StatusCodes.Status404NotFound:
                Heading = "We could not find that page";
                Explanation = "The link may be out of date, or the address may have been mistyped.";
                break;

            case StatusCodes.Status413PayloadTooLarge:
                Heading = "The upload is too large";
                Explanation = "This upload exceeds the allowed size limit. Choose a smaller file and try again.";
                break;

            case StatusCodes.Status429TooManyRequests when isSignInSurface:
                Heading = "Too many sign-in attempts";
                Explanation = "Wait a minute, then try again.";
                break;

            case StatusCodes.Status429TooManyRequests:
                Heading = "Too many requests";
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

    private bool IsStaffSignInSurface() =>
        string.Equals(OriginalPath(), "/Account/SignIn", StringComparison.OrdinalIgnoreCase);

    private string? OriginalPath() => HttpContext
        .Features
        .Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>()
        ?.OriginalPath;
}
