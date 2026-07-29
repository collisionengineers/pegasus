using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Access;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(IStaffAccountAdministration administration)
    : AdministrationPageModel
{
    public IReadOnlyList<StaffAccountSummary> Accounts { get; private set; } = [];

    [BindProperty]
    public Guid StaffId { get; set; }

    [BindProperty]
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ReviewStaffAccess);
        Accounts = await administration.ListAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostReviewAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (StaffId == Guid.Empty || !IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                await administration.ReviewAccessAsync(
                    actor,
                    StaffId,
                    Reason,
                    OperationKey,
                    cancellationToken);
                TempData["AdministrationStatus"] = "The access review was recorded.";
                return RedirectToPage();
            }
            catch (StaffAccountAdministrationException exception)
            {
                var message = exception.Error switch
                {
                    StaffAccountAdministrationError.StaffAccountNotFound =>
                        "The staff account no longer exists.",
                    StaffAccountAdministrationError.OperationConflict =>
                        "The form was already used for a different operation. Retry from the current page.",
                    _ => "The access review was not accepted."
                };
                ModelState.AddModelError(string.Empty, message);
            }
        }

        OperationKey = NewOperationKey();
        Accounts = await administration.ListAsync(actor, cancellationToken);
        return Page();
    }
}
