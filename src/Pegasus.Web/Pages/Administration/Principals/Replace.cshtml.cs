using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Principals;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ReplaceModel(
    IGetPrincipal getPrincipal,
    IReplacePrincipal replacePrincipal)
    : AdministrationPageModel
{
    public PrincipalAdministrationDetails? Customer { get; private set; }
    public PrincipalAdministrationSummary? Predecessor { get; private set; }

    [BindProperty]
    public long ExpectedVersion { get; set; }

    [BindProperty]
    [Required, StringLength(OrganizationAdministrationPolicy.MaximumPrincipalCodeLength)]
    public string SuccessorCode { get; set; } = string.Empty;

    [BindProperty]
    [Required, StringLength(OrganizationAdministrationPolicy.MaximumReasonLength, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        return await LoadAsync(
            actor,
            principalId,
            initializeForm: true,
            cancellationToken)
            ? Page()
            : NotFound();
    }

    public async Task<IActionResult> OnPostReplaceAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!await LoadAsync(
                actor,
                principalId,
                initializeForm: false,
                cancellationToken))
        {
            return NotFound();
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
        ModelState.Remove(nameof(OperationKey));
        ModelState.Remove(nameof(ExpectedVersion));
        ExpectedVersion = Predecessor!.Version;
        return Page();
    }

    private async Task<bool> LoadAsync(
        ActionActor actor,
        Guid principalId,
        bool initializeForm,
        CancellationToken cancellationToken)
    {
        Customer = await getPrincipal.ExecuteAsync(actor, principalId, cancellationToken);
        Predecessor = Customer?.Principal;
        if (Predecessor is null)
        {
            return false;
        }
        if (initializeForm)
        {
            ExpectedVersion = Predecessor.Version;
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
        OrganizationAdministrationError.DuplicatePrincipalCode =>
            "That normalized successor code already exists.",
        OrganizationAdministrationError.StaleVersion =>
            "The principal changed after this page was loaded. Review the current version and retry.",
        OrganizationAdministrationError.OperationConflict =>
            "The form was already used for a different operation. Retry from the current page.",
        _ => "The principal replacement was not accepted."
    };
}
