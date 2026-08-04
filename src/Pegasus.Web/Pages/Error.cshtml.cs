using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Pegasus.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    /// <summary>
    /// Where "Try again" goes: the path the operator was reading when the
    /// request failed, re-requested as a GET.
    /// </summary>
    /// <remarks>
    /// Never a replay of the failed POST. The page tells the operator their
    /// submission may not have been saved; re-submitting it silently on their
    /// behalf would be the one thing that sentence promises not to do.
    /// </remarks>
    public string? ReturnPath { get; set; }

    public bool ShowReturnPath => !string.IsNullOrEmpty(ReturnPath);

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        var originalPath = HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Path;
        if (!string.IsNullOrWhiteSpace(originalPath)
            && Url.IsLocalUrl(originalPath)
            && !originalPath.StartsWith("/Error", StringComparison.OrdinalIgnoreCase))
        {
            ReturnPath = originalPath;
        }
    }
}
