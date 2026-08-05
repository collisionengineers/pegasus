using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("StaffSignIn")]
public sealed class SignInModel(
    UserManager<PegasusIdentityUser> userManager,
    SignInManager<PegasusIdentityUser> signInManager,
    ISecurityEventWriter securityEvents,
    TimeProvider timeProvider)
    : PageModel
{
    // Explicit messages: the framework defaults name the bind property
    // ("The UserName field is required."), which is a C# identifier, not a
    // word the operator has ever seen on this screen.
    [BindProperty]
    [Display(Name = "Username")]
    [Required(ErrorMessage = "Enter your username."), StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [BindProperty]
    [Display(Name = "Password")]
    [Required(ErrorMessage = "Enter your password."), DataType(DataType.Password), StringLength(256)]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// The one-time confirmation that a session has just ended, set only by the
    /// sign-out redirect.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public bool SignedOut { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(SafeReturnUrl());
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.FindByNameAsync(UserName.Trim());
        var result = user is null || !user.IsEnabled
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await signInManager.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            await WriteSecurityEventAsync(
                user?.Id.ToString("D") ?? "unknown",
                SecurityEventOutcome.Denied,
                "invalid_credentials",
                cancellationToken);
            ModelState.AddModelError(
                string.Empty,
                "The username or password is incorrect. If your access has changed, contact an administrator.");
            Password = string.Empty;
            ModelState.Remove(nameof(Password));
            return Page();
        }

        var authenticatedUser = user
            ?? throw new InvalidOperationException("A successful credential check requires a staff user.");
        await signInManager.SignInAsync(authenticatedUser, isPersistent: false);
        return authenticatedUser.MustChangePassword
            ? RedirectToPage("/Account/PasswordChange")
            : LocalRedirect(SafeReturnUrl());
    }

    private string SafeReturnUrl() =>
        Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Page("/Index")!;

    private Task WriteSecurityEventAsync(
        string subjectId,
        SecurityEventOutcome outcome,
        string? reasonCode,
        CancellationToken cancellationToken) =>
        securityEvents.AppendAsync(
            new(
                Guid.NewGuid(),
                SecurityEventType.SignIn,
                outcome,
                subjectId,
                timeProvider.GetUtcNow(),
                HttpContext.TraceIdentifier,
                reasonCode),
            cancellationToken);
}
