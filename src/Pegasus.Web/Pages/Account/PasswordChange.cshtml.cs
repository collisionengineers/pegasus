using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web.Pages.Account;

public sealed class PasswordChangeModel(
    IChangeStaffPassword changeStaffPassword,
    SignInManager<PegasusIdentityUser> signInManager)
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

    [BindProperty]
    public string OperationKey { get; set; } = CreateOperationKey();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var userName = User.Identity?.Name;
        if (!TryGetActor(out var actor, out var staffId)
            || string.IsNullOrWhiteSpace(userName))
        {
            return Forbid();
        }
        if (!Guid.TryParseExact(OperationKey, "N", out _))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the password change.");
        }
        if (!ModelState.IsValid)
        {
            ResetSensitiveInput();
            return Page();
        }

        try
        {
            await changeStaffPassword.ExecuteAsync(
                new(actor, staffId, CurrentPassword, NewPassword, OperationKey),
                cancellationToken);
        }
        catch (StaffPasswordChangeException exception)
        {
            if (exception.Error == StaffPasswordChangeError.StaffAccountNotFound)
            {
                return Forbid();
            }

            ModelState.AddModelError(
                string.Empty,
                exception.Error == StaffPasswordChangeError.OperationConflict
                    ? "This password-change form was already used. Retry from the current page."
                    : "The password could not be changed. Check the current password and the new password requirements.");
            ResetSensitiveInput();
            return Page();
        }
        catch (ArgumentException)
        {
            ModelState.AddModelError(
                string.Empty,
                "The password could not be changed. Check the current password and the new password requirements.");
            ResetSensitiveInput();
            return Page();
        }

        await signInManager.SignOutAsync();
        var refreshedSession = await signInManager.PasswordSignInAsync(
            userName,
            NewPassword,
            isPersistent: false,
            lockoutOnFailure: false);
        return refreshedSession.Succeeded
            ? RedirectToPage("/Index")
            : RedirectToPage("/Account/SignIn");
    }

    private void ResetSensitiveInput()
    {
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        OperationKey = CreateOperationKey();
        ModelState.Remove(nameof(CurrentPassword));
        ModelState.Remove(nameof(NewPassword));
        ModelState.Remove(nameof(ConfirmPassword));
        ModelState.Remove(nameof(OperationKey));
    }

    private bool TryGetActor(out ActionActor actor, out Guid staffId)
    {
        if (StaffActorFactory.TryCreate(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var resolved)
            && Guid.TryParse(resolved.SubjectId, out var resolvedStaffId))
        {
            staffId = resolvedStaffId;
            actor = resolved;
            return true;
        }

        actor = null!;
        staffId = Guid.Empty;
        return false;
    }

    private static string CreateOperationKey() => Guid.NewGuid().ToString("N");
}
