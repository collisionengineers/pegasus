using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web.Pages.Account;

public sealed class PasswordChangeModel(
    IChangeStaffPassword changeStaffPassword,
    SignInManager<PegasusIdentityUser> signInManager)
    : StaffPageModel
{
    // Explicit messages throughout: the framework defaults print bind-property
    // names and CLR type talk at the operator — "'ConfirmPassword' and
    // 'NewPassword' do not match.", "must be a string or array type with a
    // minimum length of '8'".
    [BindProperty]
    [Display(Name = "Current password")]
    [Required(ErrorMessage = "Enter your current password.")]
    [DataType(DataType.Password), StringLength(256)]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    [Display(Name = "New password")]
    [Required(ErrorMessage = "Enter a new password.")]
    [MinLength(8, ErrorMessage = "The new password must be at least 8 characters.")]
    [DataType(DataType.Password), StringLength(256)]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    [Display(Name = "Confirm new password")]
    [Required(ErrorMessage = "Confirm the new password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    /// <summary>
    /// True when the operator was sent here by the must-change-password gate
    /// rather than choosing to change their password.
    /// </summary>
    /// <remarks>
    /// The two are not the same screen. Under the gate every other destination
    /// is already locked, so the page renders without navigation and states the
    /// consequence; a voluntary change keeps the application around it.
    /// </remarks>
    public bool Forced { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Forced = await IsPasswordChangeRequiredAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Forced = await IsPasswordChangeRequiredAsync(cancellationToken);
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

            // The persisted error already distinguishes these outcomes; saying
            // "check the current password and the new password requirements"
            // for all of them made the operator guess which one failed.
            switch (exception.Error)
            {
                case StaffPasswordChangeError.CurrentPasswordInvalid:
                    ModelState.AddModelError(
                        nameof(CurrentPassword),
                        "The current password is incorrect.");
                    break;
                case StaffPasswordChangeError.PasswordUnchanged:
                    ModelState.AddModelError(
                        nameof(NewPassword),
                        "The new password must be different from the current one.");
                    break;
                case StaffPasswordChangeError.PasswordRejected:
                    ModelState.AddModelError(
                        nameof(NewPassword),
                        "The new password must be at least 8 characters.");
                    break;
                case StaffPasswordChangeError.OperationConflict:
                    ModelState.AddModelError(
                        string.Empty,
                        "This password-change form was already used. Retry from the current page.");
                    break;
                default:
                    ModelState.AddModelError(
                        string.Empty,
                        "The password could not be changed.");
                    break;
            }

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
        if (!refreshedSession.Succeeded)
        {
            return RedirectToPage("/Account/SignIn");
        }

        // A silent redirect left the operator unsure whether anything happened.
        TempData["Confirmation"] = "Your password has been changed.";
        return RedirectToPage("/Index");
    }

    /// <summary>
    /// Whether the must-change-password gate is what put the operator here.
    /// The middleware already knows; the page has to ask again because the
    /// redirect carries no state.
    /// </summary>
    private async Task<bool> IsPasswordChangeRequiredAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var user = await signInManager.UserManager.GetUserAsync(User);
        return user?.MustChangePassword == true;
    }

    private void ResetSensitiveInput()
    {
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        OperationKey = NewOperationKey();
        ModelState.Remove(nameof(CurrentPassword));
        ModelState.Remove(nameof(NewPassword));
        ModelState.Remove(nameof(ConfirmPassword));
        ModelState.Remove(nameof(OperationKey));
    }

    private bool TryGetActor(out ActionActor actor, out Guid staffId)
    {
        if (TryGetActor(out var resolved)
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
}
