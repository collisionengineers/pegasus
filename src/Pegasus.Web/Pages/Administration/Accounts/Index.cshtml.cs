using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Accounts;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IListStaffAccounts listStaffAccounts,
    ICreateStaffAccount createStaffAccount) : AdministrationPageModel
{
    public IReadOnlyList<StaffAccountSummary> Accounts { get; private set; } = [];

    [BindProperty]
    [Required, StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [BindProperty]
    [Required, DataType(DataType.Password), MinLength(8), StringLength(256)]
    public string TemporaryPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required, StringLength(
        StaffAccountAdministrationPolicy.MaximumReasonLength,
        MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
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
                await createStaffAccount.ExecuteAsync(
                    new(actor, UserName, TemporaryPassword, Reason, OperationKey),
                    cancellationToken);
                TemporaryPassword = string.Empty;
                TempData["AdministrationStatus"] =
                    "The staff account was created and must change its password at first sign-in.";
                return RedirectToPage();
            }
            catch (StaffAccountAdministrationException exception)
            {
                var message = exception.Error switch
                {
                    StaffAccountAdministrationError.DuplicateUserName =>
                        "That username is already assigned.",
                    StaffAccountAdministrationError.OperationConflict =>
                        "The form was already used for a different operation. Retry from the current page.",
                    _ => "The account details were not accepted."
                };
                ModelState.AddModelError(string.Empty, message);
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "The account details were not accepted.");
            }
        }

        TemporaryPassword = string.Empty;
        ModelState.Remove(nameof(TemporaryPassword));
        OperationKey = NewOperationKey();
        ModelState.Remove(nameof(OperationKey));
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    private async Task LoadAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var result = await listStaffAccounts.ExecuteAsync(
            new(actor, PageSize: ListStaffAccounts.MaximumPageSize),
            cancellationToken);
        Accounts = result.Accounts;
    }
}
