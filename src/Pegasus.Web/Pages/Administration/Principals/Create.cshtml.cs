using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Principals;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class CreateModel(ICreatePrincipal createPrincipal) : AdministrationPageModel
{
    [BindProperty]
    [Required, StringLength(OrganizationAdministrationPolicy.MaximumOrganizationNameLength)]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    [Required, StringLength(OrganizationAdministrationPolicy.MaximumPrincipalCodeLength)]
    public string Code { get; set; } = string.Empty;

    [BindProperty]
    public CaseInspectionMode InspectionMode { get; set; } = CaseInspectionMode.PhysicalAddress;

    [BindProperty]
    public bool EvaManualSubmission { get; set; }

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    public IActionResult OnGet() => TryGetActor(out _) ? Page() : Forbid();

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
        if (!Enum.IsDefined(InspectionMode))
        {
            ModelState.AddModelError(nameof(InspectionMode), "Select an inspection mode.");
        }
        if (ModelState.IsValid)
        {
            try
            {
                await createPrincipal.ExecuteAsync(
                    new(Name, Code, actor, OperationKey, InspectionMode, EvaManualSubmission),
                    cancellationToken);
                TempData["AdministrationStatus"] = "The principal was created.";
                return RedirectToPage("Index");
            }
            catch (OrganizationAdministrationException exception)
            {
                ModelState.AddModelError(string.Empty, exception.Error switch
                {
                    OrganizationAdministrationError.DuplicateOrganizationName => "That customer name already exists.",
                    OrganizationAdministrationError.DuplicatePrincipalCode => "That principal code already exists.",
                    OrganizationAdministrationError.OperationConflict => "The form was already used for a different operation.",
                    _ => "The principal could not be created."
                });
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
        ModelState.Remove(nameof(OperationKey));
        return Page();
    }
}
