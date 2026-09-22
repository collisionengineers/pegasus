using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Account;

/// <summary>
/// The refusal a signed-in operator reads on a route their role does not hold
/// (a User on a Manage route): the cookie scheme's AccessDeniedPath, so it is
/// only ever reached signed in. It renders in the navless frame with the rest
/// of the error family (v28 P6-D, 18 September 2026) — the area as the
/// eyebrow, "Access denied", one sentence and Return to Work Centre.
/// </summary>
public sealed class AccessDeniedModel : PageModel
{
    /// <summary>The area the refused route belongs to, when it is a Manage route.</summary>
    public string? Area { get; private set; }

    public string Sentence { get; private set; } = OperatorLabels.Shell.AccessDeniedSentence;

    public void OnGet(string? returnUrl)
    {
        if (returnUrl is not null
            && Url.IsLocalUrl(returnUrl)
            && returnUrl.StartsWith("/Administration", StringComparison.OrdinalIgnoreCase))
        {
            Area = OperatorLabels.Nav.Administration;
            Sentence = OperatorLabels.Shell.AdministrationDenied;
        }
    }
}
