using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web.Pages.Account;

public sealed class SignOutModel(SignInManager<PegasusIdentityUser> signInManager) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        await signInManager.SignOutAsync();

        // The signed-out confirmation is a one-time state of the sign-in page,
        // not a page of its own: a bookmarked confirmation URL would assert
        // that a session had just ended when nothing had happened.
        return RedirectToPage("/Account/SignIn", new { signedOut = true });
    }
}
