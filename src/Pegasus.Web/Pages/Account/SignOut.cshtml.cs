using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web.Pages.Account;

public sealed class SignOutModel(SignInManager<StaffAccount> signInManager) : PageModel
{
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await signInManager.SignOutAsync();
        return RedirectToPage("/Account/SignIn");
    }

    public void OnGet() { }
}
