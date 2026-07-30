using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web.Pages.Account;

public sealed class PasswordChangeModel(UserManager<StaffAccount> userManager, SignInManager<StaffAccount> signInManager) : PageModel
{
    [BindProperty]
    public PasswordInput Input { get; set; } = new();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var result = await userManager.ChangePasswordAsync(user, Input.CurrentPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }
        user.ForcePasswordChange = false;
        await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);
        return RedirectToPage("/Index");
    }

    public sealed class PasswordInput
    {
        [Required, Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;
        [Required, DataType(DataType.Password), Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;
        [Required, Compare(nameof(NewPassword)), Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
