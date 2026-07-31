using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Principals;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ReplaceModel(
    IGetOrganization getOrganization,
    IListOrganizations listOrganizations,
    IReplacePrincipal replacePrincipal)
    : AdministrationPageModel
{
    public OrganizationDetails? Organization { get; private set; }
    public PrincipalAdministrationSummary? Predecessor { get; private set; }
    public OrganizationListPage Organizations { get; private set; } =
        new([], 1, ListOrganizations.MaximumPageSize, false, false);

    [BindProperty]
    public long ExpectedVersion { get; set; }

    [BindProperty]
    public Guid SuccessorOrganizationId { get; set; }

    [BindProperty]
    [Required, StringLength(OrganizationAdministrationPolicy.MaximumPrincipalCodeLength)]
    public string SuccessorCode { get; set; } = string.Empty;

    [BindProperty]
    [Required, StringLength(OrganizationAdministrationPolicy.MaximumReasonLength, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(
        Guid organizationId,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        return await LoadAsync(
            actor,
            organizationId,
            principalId,
            initializeForm: true,
            cancellationToken)
            ? Page()
            : NotFound();
    }

    public async Task<IActionResult> OnPostReplaceAsync(
        Guid organizationId,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!await LoadAsync(
                actor,
                organizationId,
                principalId,
                initializeForm: false,
                cancellationToken))
        {
            return NotFound();
        }

        if (SuccessorOrganizationId == Guid.Empty)
        {
            ModelState.AddModelError(
                nameof(SuccessorOrganizationId),
                "Select a Work Provider organization for the successor.");
        }
        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                await replacePrincipal.ExecuteAsync(
                    new(
                        principalId,
                        ExpectedVersion,
                        SuccessorOrganizationId,
                        SuccessorCode,
                        actor,
                        OperationKey,
                        Reason),
                    cancellationToken);
                TempData["AdministrationStatus"] = "The predecessor was disabled and its linked successor was created.";
                return RedirectToPage("Index");
            }
            catch (OrganizationAdministrationException exception)
            {
                ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error));
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "The replacement details were not accepted.");
            }
            catch (StaffAuthorizationException)
            {
                return Forbid();
            }
        }

        OperationKey = NewOperationKey();
        ExpectedVersion = Predecessor!.Version;
        return Page();
    }

    private async Task<bool> LoadAsync(
        ActionActor actor,
        Guid organizationId,
        Guid principalId,
        bool initializeForm,
        CancellationToken cancellationToken)
    {
        Organization = await getOrganization.ExecuteAsync(
            new(actor, organizationId, principalId),
            cancellationToken);
        Predecessor = Organization?.Principals.SingleOrDefault(
            principal => principal.Id == principalId);
        if (Organization is null || Predecessor is null)
        {
            return false;
        }

        Organizations = await listOrganizations.ExecuteAsync(
            new(actor, 1, ListOrganizations.MaximumPageSize),
            cancellationToken);
        if (initializeForm)
        {
            ExpectedVersion = Predecessor.Version;
            SuccessorOrganizationId = Organization.Id;
        }
        if (SuccessorOrganizationId != Guid.Empty
            && !Organizations.Organizations.Any(
                item => item.Id == SuccessorOrganizationId))
        {
            var selected = SuccessorOrganizationId == Organization.Id
                ? Organization
                : await getOrganization.ExecuteAsync(
                    new(actor, SuccessorOrganizationId),
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
        return true;
    }

    private static string MutationErrorMessage(OrganizationAdministrationError error) => error switch
    {
        OrganizationAdministrationError.PrincipalNotFound =>
            "The predecessor no longer exists.",
        OrganizationAdministrationError.PrincipalInactive =>
            "The predecessor is already disabled and cannot be replaced again.",
        OrganizationAdministrationError.PrincipalAlreadyReplaced =>
            "The predecessor already has a linked successor.",
        OrganizationAdministrationError.OrganizationNotFound =>
            "The successor organization no longer exists.",
        OrganizationAdministrationError.OrganizationCannotOwnPrincipals =>
            "The successor organization must have the Work Provider role.",
        OrganizationAdministrationError.DuplicatePrincipalCode =>
            "That normalized successor code already exists.",
        OrganizationAdministrationError.StaleVersion =>
            "The principal changed after this page was loaded. Review the current version and retry.",
        OrganizationAdministrationError.OperationConflict =>
            "The form was already used for a different operation. Retry from the current page.",
        _ => "The principal replacement was not accepted."
    };
}
