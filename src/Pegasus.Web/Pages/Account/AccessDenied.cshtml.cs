using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Account;

/// <summary>
/// The refusal a signed-in operator reads on a route their role does not hold
/// (a User on a Manage route): the cookie scheme's AccessDeniedPath, so it is
/// only ever reached signed in. It renders inside the shell as the v26 shell
/// draws it — the area as the eyebrow, "Access denied", one sentence — because
/// the rail an operator already sees offers nothing this page has declined.
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
