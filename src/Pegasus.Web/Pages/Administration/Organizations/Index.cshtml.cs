using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Organizations;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IListOrganizations listOrganizations,
    ICreateOrganization createOrganization)
    : AdministrationPageModel
{
    public OrganizationListPage Organizations { get; private set; } =
        new([], 1, 25, false, false);

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty]
    [Required, StringLength(OrganizationAdministrationPolicy.MaximumOrganizationNameLength)]
    public string OrganizationName { get; set; } = string.Empty;

    [BindProperty]
    public bool WorkProvider { get; set; }

    [BindProperty]
    public bool InstructionIntermediary { get; set; }

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        PageNumber = Math.Max(1, PageNumber);
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
        var roles = SelectedRoles();
        if (roles.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Select at least one organization role.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                await createOrganization.ExecuteAsync(
                    new(OrganizationName, roles, actor, OperationKey),
                    cancellationToken);
                TempData["AdministrationStatus"] = "The organization was created.";
                return RedirectToPage();
            }
            catch (OrganizationAdministrationException exception)
            {
                ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error));
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "The organization details were not accepted.");
            }
            catch (StaffAuthorizationException)
            {
                return Forbid();
            }
        }

        OperationKey = NewOperationKey();
        PageNumber = Math.Max(1, PageNumber);
        await LoadAsync(actor, cancellationToken);
        return Page();
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

    private async Task LoadAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        Organizations = await listOrganizations.ExecuteAsync(
            new(actor, PageNumber, 25),
            cancellationToken);
    }

    private static string MutationErrorMessage(OrganizationAdministrationError error) => error switch
    {
        OrganizationAdministrationError.DuplicateOrganizationName =>
            "An organization with that normalized name already exists.",
        OrganizationAdministrationError.EmptyOrganizationRoles =>
            "Select at least one organization role.",
        OrganizationAdministrationError.OperationConflict =>
            "The form was already used for a different operation. Retry from the current page.",
        _ => "The organization could not be created."
    };
}
