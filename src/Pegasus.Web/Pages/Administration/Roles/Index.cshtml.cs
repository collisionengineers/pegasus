using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Roles;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IGetRoleAssignments getRoleAssignments,
    IAssignStaffRoles assignStaffRoles) : AdministrationPageModel
{
    public IReadOnlyList<StaffRoleAssignmentProjection> Accounts { get; private set; } = [];

    [BindProperty]
    public Guid StaffId { get; set; }

    [BindProperty]
    public string[] SelectedRoles { get; set; } = [];

    [BindProperty]
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    public bool IsRoleSelected(
        StaffRoleAssignmentProjection account,
        string roleName)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.StaffId == StaffId)
        {
            return SelectedRoles.Contains(roleName, StringComparer.Ordinal);
        }

        return Enum.TryParse<StaffRole>(roleName, ignoreCase: false, out var role)
            && account.CurrentRoles.Contains(role);
    }

    public string ReasonFor(StaffRoleAssignmentProjection account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return account.StaffId == StaffId ? Reason : string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAssignAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        var roles = new HashSet<StaffRole>();
        foreach (var roleName in SelectedRoles)
        {
            if (!Enum.TryParse<StaffRole>(roleName, ignoreCase: false, out var role)
                || !Enum.IsDefined(role))
            {
                ModelState.AddModelError(
                    nameof(SelectedRoles),
                    "Select only supported staff roles.");
                break;
            }

            roles.Add(role);
        }

        if (StaffId == Guid.Empty || !IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }
        if (roles.Count == 0)
        {
            ModelState.AddModelError(nameof(SelectedRoles), "Select at least one role.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                await assignStaffRoles.ExecuteAsync(
                    new(actor, StaffId, roles, Reason, OperationKey),
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    "Roles updated. Existing browser and MCP sessions were revoked.";
                return RedirectToPage();
            }
            catch (StaffAccountAdministrationException exception)
            {
                ModelState.AddModelError(string.Empty, exception.Error switch
                {
                    StaffAccountAdministrationError.StaffAccountNotFound =>
                        "The staff account no longer exists.",
                    StaffAccountAdministrationError.LastAdministrator =>
                        "The change was denied because at least one enabled Administrator must remain.",
                    StaffAccountAdministrationError.OperationConflict =>
                        "The form was already used for a different operation. Retry from the current page.",
                    _ => "The role assignment was not accepted."
                });
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "The role assignment was not accepted.");
            }
        }

        OperationKey = NewOperationKey();
        ModelState.Remove(nameof(OperationKey));
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    private async Task LoadAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        var result = await getRoleAssignments.ExecuteAsync(
            new(actor),
            cancellationToken);
        Accounts = result.Accounts;
    }
}
