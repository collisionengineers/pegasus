using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Pages.Administration.Principals;

/// <summary>
/// One customer's settings, through the existing Core administration commands.
/// Credential secrets stay only in the immediate no-store issue/reset response.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SettingsModel(
    IGetPrincipal getPrincipal,
    IGetPrincipalCredential getCredential,
    IIssuePrincipalCredential issueCredential,
    IPausePrincipalCredential pauseCredential,
    IResumePrincipalCredential resumeCredential,
    IRevokePrincipalCredential revokeCredential,
    IUpdatePrincipalEvaSubmission updatePrincipalEvaSubmission,
    IUpdatePrincipalDefaultInspectionLocation updatePrincipalDefaultInspectionLocation)
    : AdministrationPageModel
{
    public PrincipalAdministrationDetails? Customer { get; private set; }
    public PrincipalCredentialRecord? Credential { get; private set; }
    public string? IssuedSecret { get; private set; }
    public IReadOnlyList<string> AcceptedIdentities => Principal?.Code is { } code
        && PrincipalMailRoutePolicy.AcceptedIdentities.TryGetValue(code, out var identities)
        ? identities
        : [];

    [BindProperty]
    public long CredentialVersion { get; set; }

    [BindProperty]
    public string? CredentialOperationKey { get; set; } = NewOperationKey();

    [BindProperty, StringLength(OrganizationAdministrationPolicy.MaximumReasonLength)]
    public string? CredentialReason { get; set; }
    public PrincipalAdministrationSummary? Principal { get; private set; }

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

    // Nullable, not string: each form posts only its own operation key, and a
    // non-nullable string here would be implicitly Required even on the
    // *other* form's POST (C06-R-16) — the same reasoning as EvaReason above.
    // Each handler still requires and validates its own key explicitly, below.
    [BindProperty]
    public string? EvaOperationKey { get; set; } = NewOperationKey();

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

    // Nullable for the same reason as EvaOperationKey above (C06-R-16).
    [BindProperty]
    public string? LocationOperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!await LoadAsync(actor, principalId, cancellationToken))
        {
            return NotFound();
        }

        InitializeFromPrincipal();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateEvaAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!await LoadAsync(actor, principalId, cancellationToken))
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
                        EvaOperationKey!,
                        EvaReason!,
                        EvaManualSubmission),
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    "The principal's manual EVA submission setting was updated.";
                return RedirectToPage(new { principalId });
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
        ModelState.Remove(nameof(EvaOperationKey));
        ModelState.Remove(nameof(ExpectedVersion));
        InitializeLocationFromPrincipal();
        ExpectedVersion = Principal!.Version;
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateLocationAsync(
        Guid principalId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!await LoadAsync(actor, principalId, cancellationToken))
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
                await updatePrincipalDefaultInspectionLocation.ExecuteAsync(
                    new(
                        actor,
                        principalId,
                        ExpectedVersion,
                        LocationOperationKey!,
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
                return RedirectToPage(new { principalId });
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
        ModelState.Remove(nameof(LocationOperationKey));
        ModelState.Remove(nameof(ExpectedVersion));
        EvaManualSubmission = Principal!.EvaManualSubmission;
        ExpectedVersion = Principal!.Version;
        return Page();
    }


    public Task<IActionResult> OnPostIssueCredentialAsync(Guid principalId, CancellationToken cancellationToken) =>
        ChangeCredentialAsync(principalId, "issue", cancellationToken);

    public Task<IActionResult> OnPostPauseCredentialAsync(Guid principalId, CancellationToken cancellationToken) =>
        ChangeCredentialAsync(principalId, "pause", cancellationToken);

    public Task<IActionResult> OnPostResumeCredentialAsync(Guid principalId, CancellationToken cancellationToken) =>
        ChangeCredentialAsync(principalId, "resume", cancellationToken);

    public Task<IActionResult> OnPostRevokeCredentialAsync(Guid principalId, CancellationToken cancellationToken) =>
        ChangeCredentialAsync(principalId, "revoke", cancellationToken);

    private async Task<IActionResult> ChangeCredentialAsync(
        Guid principalId, string action, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        var submittedVersion = CredentialVersion;
        if (!await LoadAsync(actor, principalId, cancellationToken))
        {
            return NotFound();
        }
        if (!IsOperationKeyValid(CredentialOperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }
        if (string.IsNullOrWhiteSpace(CredentialReason))
        {
            ModelState.AddModelError(nameof(CredentialReason), "A reason is required.");
        }
        if (ModelState.IsValid)
        {
            try
            {
                var request = new PrincipalCredentialCommandRequest(
                    principalId, submittedVersion, actor, CredentialOperationKey!, CredentialReason!);
                if (action == "issue")
                {
                    var result = await issueCredential.ExecuteAsync(request, cancellationToken);
                    Credential = result.Credential;
                    IssuedSecret = result.Secret;
                    CredentialVersion = Credential.Version;
                    CredentialOperationKey = NewOperationKey();
                    ModelState.Clear();
                    InitializeFromPrincipal();
                    return Page();
                }
                Credential = action switch
                {
                    "pause" => await pauseCredential.ExecuteAsync(request, cancellationToken),
                    "resume" => await resumeCredential.ExecuteAsync(request, cancellationToken),
                    "revoke" => await revokeCredential.ExecuteAsync(request, cancellationToken),
                    _ => throw new InvalidOperationException("Unknown credential action.")
                };
                TempData["AdministrationStatus"] = "The provider credential was updated.";
                return RedirectToPage(new { principalId });
            }
            catch (PrincipalCredentialException exception)
            {
                ModelState.AddModelError(string.Empty, exception.Error switch
                {
                    PrincipalCredentialError.StaleVersion => "The API key changed. Retry from the current settings.",
                    PrincipalCredentialError.OperationConflict => "The form was already used for a different operation.",
                    PrincipalCredentialError.PrincipalInactive => "The principal is disabled.",
                    _ => "The API key change was not accepted."
                });
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "The API key change was not accepted.");
            }
            catch (StaffAuthorizationException)
            {
                return Forbid();
            }
        }
        CredentialOperationKey = NewOperationKey();
        ModelState.Remove(nameof(CredentialOperationKey));
        ModelState.Remove(nameof(CredentialVersion));
        InitializeFromPrincipal();
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
        Guid principalId,
        CancellationToken cancellationToken)
    {
        Customer = await getPrincipal.ExecuteAsync(actor, principalId, cancellationToken);
        Principal = Customer?.Principal;
        if (Principal is null)
        {
            return false;
        }
        Credential = await getCredential.ExecuteAsync(actor, principalId, cancellationToken);
        CredentialVersion = Credential?.Version ?? 0;
        return true;
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
