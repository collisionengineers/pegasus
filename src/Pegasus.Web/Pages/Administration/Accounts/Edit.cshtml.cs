using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Accounts;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class EditModel(IStaffAccountAdministration administration)
    : AdministrationPageModel
{
    public StaffAccountSummary? Account { get; private set; }

    [BindProperty]
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;


    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        return await LoadAsync(actor, id, cancellationToken) ? Page() : NotFound();
    }

    public Task<IActionResult> OnPostEnableAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeEnabledAsync(id, enabled: true, cancellationToken);

    public Task<IActionResult> OnPostDisableAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeEnabledAsync(id, enabled: false, cancellationToken);


    private async Task<IActionResult> ChangeEnabledAsync(
        Guid id,
        bool enabled,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                await administration.SetEnabledAsync(actor, id, enabled, Reason, OperationKey, cancellationToken);
                TempData["AdministrationStatus"] = enabled
                    ? "The account was enabled. A fresh sign-in is required."
                    : "The account was disabled and its existing browser sessions were invalidated.";
                return RedirectToPage(new { id });
            }
            catch (StaffAccountAdministrationException exception)
            {
                ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error));
            }
        }

        OperationKey = NewOperationKey();
        return await LoadAsync(actor, id, cancellationToken) ? Page() : NotFound();
    }
    private static string MutationErrorMessage(StaffAccountAdministrationError error) => error switch
    {
        StaffAccountAdministrationError.StaffAccountNotFound =>
            "The staff account no longer exists.",
        StaffAccountAdministrationError.LastAdministrator =>
            "The change was denied because at least one enabled Administrator must remain.",
        StaffAccountAdministrationError.OperationConflict =>
            "The form was already used for a different operation. Retry from the current page.",
        _ => "The access change was not accepted."
    };


    private async Task<bool> LoadAsync(
        ActionActor actor,
        Guid id,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageStaffAccounts);
        Account = (await administration.ListAsync(actor, cancellationToken))
            .SingleOrDefault(account => account.Id == id);
        if (Account is null)
        {
            return false;
        }
        return true;
    }
}
