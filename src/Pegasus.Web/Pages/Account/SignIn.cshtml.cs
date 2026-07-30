using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web.Pages.Account;

[AllowAnonymous]
public sealed class SignInModel(SignInManager<StaffAccount> signInManager) : PageModel
{
    [BindProperty]
    public SignInInput Input { get; set; } = new();

    public void OnGet(string? returnUrl = null) => Input.ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var result = await signInManager.PasswordSignInAsync(Input.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            var user = await signInManager.UserManager.FindByNameAsync(Input.UserName);
            if (user?.ForcePasswordChange == true) return RedirectToPage("/Account/PasswordChange");
            if (Input.ReturnUrl is not null && Url.IsLocalUrl(Input.ReturnUrl)) return LocalRedirect(Input.ReturnUrl);
            return RedirectToPage("/Index");
        }
        ModelState.AddModelError(string.Empty, result.IsLockedOut ? "This account is temporarily locked." : "The sign-in details were not accepted.");
        return Page();
    }
    public sealed class SignInInput
    {
        [Required, Display(Name = "User name")]
        public string UserName { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }
}
