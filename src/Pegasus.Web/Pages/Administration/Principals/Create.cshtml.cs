using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Principals;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class CreateModel(
    IListOrganizations listOrganizations,
    IGetOrganization getOrganization,
    ICreatePrincipal createPrincipal)
    : AdministrationPageModel
{
    public OrganizationListPage Organizations { get; private set; } =
        new([], 1, ListOrganizations.MaximumPageSize, false, false);

    [BindProperty(SupportsGet = true)]
    public Guid OrganizationId { get; set; }

    [BindProperty]
    [Required, StringLength(OrganizationAdministrationPolicy.MaximumPrincipalCodeLength)]
    public string Code { get; set; } = string.Empty;

    [BindProperty]
    public CaseInspectionMode InspectionMode { get; set; } = CaseInspectionMode.PhysicalAddress;

    /// <summary>
    /// EXT-04/EXT-18 item 7: the one optional, explicit EVA setting a
    /// principal may have. Automatic submission is retired from this
    /// administration surface — the page offers no control for it and a new
    /// principal is always created with it false.
    /// </summary>
    [BindProperty]
    public bool EvaManualSubmission { get; set; }

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

        if (OrganizationId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(OrganizationId), "Select a Work Provider organization.");
        }
        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }
        if (!Enum.IsDefined(InspectionMode))
        {
            ModelState.AddModelError(nameof(InspectionMode), "Select an inspection mode.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                await createPrincipal.ExecuteAsync(
                    new(
                        OrganizationId,
                        Code,
                        actor,
                        OperationKey,
                        InspectionMode,
                        EvaManualSubmission),
                    cancellationToken);
                TempData["AdministrationStatus"] = "The principal was created.";
                return RedirectToPage("Index");
            }
            catch (OrganizationAdministrationException exception)
            {
                ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error));
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "The principal details were not accepted.");
            }
            catch (StaffAuthorizationException)
            {
                return Forbid();
            }
        }

        OperationKey = NewOperationKey();
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        Organizations = await listOrganizations.ExecuteAsync(
            new(actor, 1, ListOrganizations.MaximumPageSize),
            cancellationToken);
        if (OrganizationId == Guid.Empty
            || Organizations.Organizations.Any(item => item.Id == OrganizationId))
        {
            return;
        }

        var selected = await getOrganization.ExecuteAsync(
            new(actor, OrganizationId),
            cancellationToken);
        if (selected is not null)
        {
            Organizations = Organizations with
            {
                Organizations =
                [
                    new(
                        selected.Id,
                        selected.Name,
                        selected.Roles,
                        selected.Version,
                        selected.Principals,
                        selected.HasMorePrincipals),
                    .. Organizations.Organizations
                ]
            };
        }
    }

    private static string MutationErrorMessage(OrganizationAdministrationError error) => error switch
    {
        OrganizationAdministrationError.OrganizationNotFound =>
            "The selected organization no longer exists.",
        OrganizationAdministrationError.OrganizationCannotOwnPrincipals =>
            "The selected organization must have the Work Provider role.",
        OrganizationAdministrationError.DuplicatePrincipalCode =>
            "That normalized principal code already exists.",
        OrganizationAdministrationError.OperationConflict =>
            "The form was already used for a different operation. Retry from the current page.",
        _ => "The principal could not be created."
    };
}
