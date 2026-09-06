using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Principals;

/// <summary>
/// EXT-04/EXT-18 item 6-7: a principal's two in-place settings — the optional
/// manual EVA API submission flag, and its one default inspection-location
/// choice. Automatic EVA submission is retired from this administration
/// surface (item 7): the page offers no control for it and never sends a
/// value that could turn it on.
///
/// ADR-0018 gave the inspection mode no post-creation edit and left a
/// production change as a runbook action. These settings get their own page
/// because a delivery route or a default location that could only be chosen
/// while creating a principal could never be switched for the principals that
/// already exist — and every principal in production already exists.
///
/// Both are settings changes, not a replacement: the code, the organization,
/// the lineage and the allocation history are untouched, and neither ever
/// changes B's separate CE assessment method.
/// </summary>
/// <remarks>
/// EXT-18/S05 correction round 2 (ASSUMPTION 8): <paramref name="updatePrincipalDefaultInspectionLocation"/>
/// is an optional constructor dependency because this branch does not yet
/// carry Stream A's registration for <see cref="IUpdatePrincipalDefaultInspectionLocation"/>
/// — this page is a shared, already-linked route (reachable from
/// Principals/Index), so a required dependency here would 500 every visit
/// until A's registration lands. When unregistered, the page renders without
/// the default inspection location form (the manual-EVA form still works)
/// and the location handler answers 404 rather than faking a save. Reverts
/// to required once Stream A registers the interface.
/// </remarks>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class EvaSubmissionModel(
    IGetOrganization getOrganization,
    IUpdatePrincipalEvaSubmission updatePrincipalEvaSubmission,
    IUpdatePrincipalDefaultInspectionLocation? updatePrincipalDefaultInspectionLocation = null)
    : AdministrationPageModel
{
    public OrganizationDetails? Organization { get; private set; }
    public PrincipalAdministrationSummary? Principal { get; private set; }

    public bool DefaultLocationAvailable => updatePrincipalDefaultInspectionLocation is not null;

    [BindProperty]
    public long ExpectedVersion { get; set; }

    [BindProperty]
    public bool EvaManualSubmission { get; set; }

    // Nullable, not string.Empty: this page has two independent forms/handlers
    // sharing one PageModel, and a non-nullable string here would be
    // implicitly Required (nullable reference types + ASP.NET Core's model
    // validation) even when the *other* form's POST never submits it. Each
    // handler still requires its own reason explicitly, below.
    [BindProperty]
    [StringLength(OrganizationAdministrationPolicy.MaximumReasonLength)]
    public string? EvaReason { get; set; }

    [BindProperty]
    public string EvaOperationKey { get; set; } = NewOperationKey();

    [BindProperty]
    public bool LocationIsImageBasedAssessment { get; set; }

    [BindProperty]
    [StringLength(200)]
    public string? LocationLabel { get; set; }

    [BindProperty]
    [StringLength(500)]
    public string? LocationAddress { get; set; }

    [BindProperty]
    [StringLength(20)]
    public string? LocationPostcode { get; set; }

    // Nullable for the same reason as EvaReason above.
    [BindProperty]
    [StringLength(OrganizationAdministrationPolicy.MaximumReasonLength)]
    public string? LocationReason { get; set; }

    [BindProperty]
    public string LocationOperationKey { get; set; } = NewOperationKey();

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

        InitializeFromPrincipal();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateEvaAsync(
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
        if (!IsOperationKeyValid(EvaOperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }
        if (string.IsNullOrWhiteSpace(EvaReason))
        {
            ModelState.AddModelError(nameof(EvaReason), "A reason is required.");
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
                        EvaOperationKey,
                        EvaReason!,
                        EvaManualSubmission,
                        EvaAutomaticSubmission: false),
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    "The principal's manual EVA submission setting was updated.";
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

        EvaOperationKey = NewOperationKey();
        InitializeLocationFromPrincipal();
        ExpectedVersion = Principal!.Version;
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateLocationAsync(
        Guid organizationId,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        if (updatePrincipalDefaultInspectionLocation is not { } updateLocation)
        {
            // ASSUMPTION 8 (see the class remarks): unregistered on this
            // branch until Stream A lands IUpdatePrincipalDefaultInspectionLocation.
            // The page never renders this form when the dependency is
            // absent, so a POST here has no legitimate origin — 404, never a
            // faked save.
            return NotFound();
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!await LoadAsync(actor, organizationId, principalId, cancellationToken))
        {
            return NotFound();
        }
        if (!IsOperationKeyValid(LocationOperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }
        if (string.IsNullOrWhiteSpace(LocationReason))
        {
            ModelState.AddModelError(nameof(LocationReason), "A reason is required.");
        }
        if (!LocationIsImageBasedAssessment && string.IsNullOrWhiteSpace(LocationAddress))
        {
            ModelState.AddModelError(nameof(LocationAddress), "An address is required.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                await updateLocation.ExecuteAsync(
                    new(
                        actor,
                        principalId,
                        ExpectedVersion,
                        LocationOperationKey,
                        LocationReason!,
                        LocationIsImageBasedAssessment
                            ? InspectionAddressEvidenceKind.ImageBasedAssessment
                            : InspectionAddressEvidenceKind.PhysicalAddress,
                        LocationLabel,
                        LocationAddress,
                        LocationPostcode,
                        SourceKind: "manual",
                        SourceRecordId: null,
                        SourceVersion: null),
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    "The principal's default inspection location was updated.";
                return RedirectToPage("Index");
            }
            catch (OrganizationAdministrationException exception)
            {
                ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error));
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "The default location was not accepted.");
            }
            catch (StaffAuthorizationException)
            {
                return Forbid();
            }
        }

        LocationOperationKey = NewOperationKey();
        EvaManualSubmission = Principal!.EvaManualSubmission;
        ExpectedVersion = Principal!.Version;
        return Page();
    }

    private void InitializeFromPrincipal()
    {
        ExpectedVersion = Principal!.Version;
        EvaManualSubmission = Principal.EvaManualSubmission;
        InitializeLocationFromPrincipal();
    }

    private void InitializeLocationFromPrincipal()
    {
        LocationIsImageBasedAssessment = Principal!.DefaultInspectionAddress is null;
        LocationLabel = Principal.DefaultInspectionLocationLabel;
        LocationAddress = Principal.DefaultInspectionAddress;
        LocationPostcode = Principal.DefaultInspectionPostcode;
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
