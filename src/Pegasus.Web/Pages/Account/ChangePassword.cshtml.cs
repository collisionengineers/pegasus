using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web.Pages.Account;

public sealed class ChangePasswordModel(
    PegasusDbContext context,
    UserManager<PegasusIdentityUser> userManager,
    SignInManager<PegasusIdentityUser> signInManager,
    ISecurityEventWriter securityEvents,
    TimeProvider timeProvider)
    : PageModel
{
    [BindProperty]
    [Required, DataType(DataType.Password), StringLength(256)]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required, DataType(DataType.Password), MinLength(8), StringLength(256)]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = string.Empty;

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsEnabled)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var result = await userManager.ChangePasswordAsync(user, CurrentPassword, NewPassword);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            await WriteEventAsync(user.Id, SecurityEventOutcome.Denied, "password_change_rejected", cancellationToken);
            ModelState.AddModelError(string.Empty, "The password could not be changed. Check the current password and the new password requirements.");
            return Page();
        }

        user.MustChangePassword = false;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            await signInManager.SignOutAsync();
            throw new InvalidOperationException("The password-change state could not be persisted.");
        }

        await transaction.CommitAsync(cancellationToken);
        await WriteEventAsync(user.Id, SecurityEventOutcome.Succeeded, null, cancellationToken);
        await signInManager.SignOutAsync();
        await signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToPage("/Index");
    }

    private Task WriteEventAsync(
        Guid staffId,
        SecurityEventOutcome outcome,
        string? reasonCode,
        CancellationToken cancellationToken) =>
        securityEvents.AppendAsync(
            new(
                Guid.NewGuid(),
                SecurityEventType.PasswordChanged,
                outcome,
                staffId.ToString("D"),
                timeProvider.GetUtcNow(),
                HttpContext.TraceIdentifier,
                reasonCode),
            cancellationToken);
}
