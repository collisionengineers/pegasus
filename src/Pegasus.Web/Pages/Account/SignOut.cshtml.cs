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
        return RedirectToPage("/Account/SignIn");
    }
}
