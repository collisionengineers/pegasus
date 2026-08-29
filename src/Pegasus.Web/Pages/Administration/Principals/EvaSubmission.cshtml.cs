using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Principals;

/// <summary>
/// EXT-04: switch a principal's EVA API submission settings.
///
/// ADR-0018 gave the inspection mode no post-creation edit and left a
/// production change as a runbook action. These settings get their own page
/// because a delivery route that could only be chosen while creating a
/// principal could never be switched on for the principals that already
/// exist — and every principal in production already exists.
///
/// It is a settings change, not a replacement: the code, the organization, the
/// lineage and the allocation history are untouched.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class EvaSubmissionModel(
    IGetOrganization getOrganization,
    IUpdatePrincipalEvaSubmission updatePrincipalEvaSubmission)
    : AdministrationPageModel
{
    public OrganizationDetails? Organization { get; private set; }
    public PrincipalAdministrationSummary? Principal { get; private set; }

    [BindProperty]
    public long ExpectedVersion { get; set; }

    [BindProperty]
    public bool EvaManualSubmission { get; set; }

    [BindProperty]
    public bool EvaAutomaticSubmission { get; set; }

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
        if (!await LoadAsync(actor, organizationId, principalId, cancellationToken))
        {
            return NotFound();
        }

        ExpectedVersion = Principal!.Version;
        EvaManualSubmission = Principal.EvaManualSubmission;
        EvaAutomaticSubmission = Principal.EvaAutomaticSubmission;
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(
        Guid organizationId,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!await LoadAsync(actor, organizationId, principalId, cancellationToken))
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
                await updatePrincipalEvaSubmission.ExecuteAsync(
                    new(
                        principalId,
                        ExpectedVersion,
                        actor,
                        OperationKey,
                        Reason,
                        EvaManualSubmission,
                        EvaAutomaticSubmission),
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    "The principal's EVA API submission settings were updated.";
                return RedirectToPage("Index");
            }
            catch (OrganizationAdministrationException exception)
            {
                ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error));
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "The settings were not accepted.");
            }
            catch (StaffAuthorizationException)
            {
                return Forbid();
            }
        }

        OperationKey = NewOperationKey();
        ExpectedVersion = Principal!.Version;
        return Page();
    }

    private async Task<bool> LoadAsync(
        ActionActor actor,
        Guid organizationId,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        Organization = await getOrganization.ExecuteAsync(
            new(actor, organizationId, principalId),
            cancellationToken);
        Principal = Organization?.Principals.SingleOrDefault(
            principal => principal.Id == principalId);
        return Organization is not null && Principal is not null;
    }

    private static string MutationErrorMessage(OrganizationAdministrationError error) => error switch
    {
        OrganizationAdministrationError.PrincipalNotFound =>
            "The principal no longer exists.",
        OrganizationAdministrationError.PrincipalInactive =>
            "The principal is disabled. Change the settings on its successor instead.",
        OrganizationAdministrationError.StaleVersion =>
            "The principal changed after this page was loaded. Review the current settings and retry.",
        OrganizationAdministrationError.OperationConflict =>
            "The form was already used for a different operation. Retry from the current page.",
        _ => "The settings change was not accepted."
    };
}
