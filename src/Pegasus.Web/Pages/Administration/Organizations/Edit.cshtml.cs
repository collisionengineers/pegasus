using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Organizations;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class EditModel(
    IGetOrganization getOrganization,
    IUpdateOrganizationRoles updateOrganizationRoles)
    : AdministrationPageModel
{
    public OrganizationDetails? Organization { get; private set; }

    [BindProperty]
    public long ExpectedVersion { get; set; }

    [BindProperty]
    public bool WorkProvider { get; set; }

    [BindProperty]
    public bool InstructionIntermediary { get; set; }

    [BindProperty]
    [Required, StringLength(OrganizationAdministrationPolicy.MaximumReasonLength, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        return await LoadAsync(actor, id, initializeRoles: true, cancellationToken)
            ? Page()
            : NotFound();
    }

    public async Task<IActionResult> OnPostUpdateAsync(
        Guid id,
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
        var roles = SelectedRoles();
        if (roles.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Select at least one organization role.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                await updateOrganizationRoles.ExecuteAsync(
                    new(id, ExpectedVersion, roles, actor, OperationKey, Reason),
                    cancellationToken);
                TempData["AdministrationStatus"] = "The organization roles were updated.";
                return RedirectToPage(new { id });
            }
            catch (OrganizationAdministrationException exception)
            {
                ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error));
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "The role change was not accepted.");
            }
            catch (StaffAuthorizationException)
            {
                return Forbid();
            }
        }

        OperationKey = NewOperationKey();
        return await LoadAsync(actor, id, initializeRoles: false, cancellationToken)
            ? Page()
            : NotFound();
    }

    private List<OrganizationRole> SelectedRoles()
    {
        var roles = new List<OrganizationRole>(2);
        if (WorkProvider)
        {
            roles.Add(OrganizationRole.WorkProvider);
        }
        if (InstructionIntermediary)
        {
            roles.Add(OrganizationRole.InstructionIntermediary);
        }
        return roles;
    }

    private async Task<bool> LoadAsync(
        ActionActor actor,
        Guid id,
        bool initializeRoles,
        CancellationToken cancellationToken)
    {
        Organization = await getOrganization.ExecuteAsync(
            new(actor, id),
            cancellationToken);
        if (Organization is null)
        {
            return false;
        }

        ExpectedVersion = Organization.Version;
        if (initializeRoles)
        {
            WorkProvider = Organization.Roles.Contains(OrganizationRole.WorkProvider);
            InstructionIntermediary = Organization.Roles.Contains(
                OrganizationRole.InstructionIntermediary);
        }
        return true;
    }

    private static string MutationErrorMessage(OrganizationAdministrationError error) => error switch
    {
        OrganizationAdministrationError.OrganizationNotFound =>
            "The organization no longer exists.",
        OrganizationAdministrationError.ActivePrincipalsRequireWorkProvider =>
            "Work Provider cannot be removed while the organization has an active principal.",
        OrganizationAdministrationError.EmptyOrganizationRoles =>
            "Select at least one organization role.",
        OrganizationAdministrationError.StaleVersion =>
            "The organization changed after this page was loaded. Review the current version and retry.",
        OrganizationAdministrationError.OperationConflict =>
            "The form was already used for a different operation. Retry from the current page.",
        _ => "The role change was not accepted."
    };
}
