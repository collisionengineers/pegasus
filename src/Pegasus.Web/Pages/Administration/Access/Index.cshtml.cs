using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Access;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IGetAccessReview getAccessReview,
    IReviewStaffAccess reviewStaffAccess,
    IRevokeStaffMcpAuthorizations revokeStaffMcpAuthorizations)
    : AdministrationPageModel
{
    public IReadOnlyList<StaffAccessReviewProjection> Accounts { get; private set; } = [];

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

        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostReviewAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        ValidateSubmittedOperation();
        if (ModelState.IsValid)
        {
            try
            {
                await reviewStaffAccess.ExecuteAsync(
                    new(actor, StaffId, Reason, OperationKey),
                    cancellationToken);
                TempData["AdministrationStatus"] = "The access review was recorded.";
                return RedirectToPage();
            }
            catch (StaffAccountAdministrationException exception)
            {
                ModelState.AddModelError(string.Empty, exception.Error switch
                {
                    StaffAccountAdministrationError.StaffAccountNotFound =>
                        "The staff account no longer exists.",
                    StaffAccountAdministrationError.OperationConflict =>
                        "The form was already used for a different operation. Retry from the current page.",
                    _ => "The access review was not accepted."
                });
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "The access review was not accepted.");
            }
        }

        return await ReloadForFailureAsync(actor, cancellationToken);
    }

    public async Task<IActionResult> OnPostRevokeMcpAsync(
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        ValidateSubmittedOperation();
        if (ModelState.IsValid)
        {
            try
            {
                await revokeStaffMcpAuthorizations.ExecuteAsync(
                    new(actor, StaffId, Reason, OperationKey),
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    "The staff member's MCP authorizations and tokens were revoked.";
                return RedirectToPage();
            }
            catch (AuthenticationClientAdministrationException exception)
            {
                ModelState.AddModelError(string.Empty, exception.Error switch
                {
                    AuthenticationClientAdministrationError.StaffAccountNotFound =>
                        "The staff account no longer exists.",
                    AuthenticationClientAdministrationError.OperationConflict =>
                        "The form was already used for a different operation. Retry from the current page.",
                    _ => "The MCP authorization revocation was not accepted."
                });
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The MCP authorization revocation was not accepted.");
            }
        }

        return await ReloadForFailureAsync(actor, cancellationToken);
    }

    private void ValidateSubmittedOperation()
    {
        if (StaffId == Guid.Empty || !IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }
    }

    private async Task<IActionResult> ReloadForFailureAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        OperationKey = NewOperationKey();
        ModelState.Remove(nameof(OperationKey));
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    private async Task LoadAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var result = await getAccessReview.ExecuteAsync(
            new(actor),
            cancellationToken);
        Accounts = result.Accounts;
    }
}
