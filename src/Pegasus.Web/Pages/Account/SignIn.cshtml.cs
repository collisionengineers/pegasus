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
    [BindProperty]
    [Required, StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [BindProperty]
    [Required, DataType(DataType.Password), StringLength(256)]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

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
            ModelState.AddModelError(string.Empty, "The username or password is incorrect.");
            return Page();
        }

        await WriteSecurityEventAsync(
            user!.Id.ToString("D"),
            SecurityEventOutcome.Succeeded,
            null,
            cancellationToken);
        await signInManager.SignInAsync(user, isPersistent: false);
        return user.MustChangePassword
            ? RedirectToPage("/Account/ChangePassword")
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
