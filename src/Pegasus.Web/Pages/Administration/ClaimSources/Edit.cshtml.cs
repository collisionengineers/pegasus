using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.ClaimSources;

/// <summary>
/// EXT-19/S13 item 8: edit or disable a Claim Source. Disable is this same
/// form with the active flag cleared, not a separate destructive action —
/// disabling preserves every Case that already referenced it.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class EditModel(
    IClaimSourceQueries claimSourceQueries,
    IClaimSourceAdministration claimSourceAdministration)
    : AdministrationPageModel
{
    public ClaimSourceRecord? ClaimSource { get; private set; }

    [BindProperty]
    public long ExpectedVersion { get; set; }

    [BindProperty]
    [Required, StringLength(ClaimSourceAdministrationPolicy.MaximumNameLength)]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(ClaimSourceAdministrationPolicy.MaximumContactNameLength)]
    public string? ContactName { get; set; }

    [BindProperty]
    [StringLength(ClaimSourceAdministrationPolicy.MaximumTelephoneLength)]
    public string? Telephone { get; set; }

    [BindProperty]
    [StringLength(ClaimSourceAdministrationPolicy.MaximumEmailLength)]
    public string? Email { get; set; }

    [BindProperty]
    [StringLength(ClaimSourceAdministrationPolicy.MaximumNotesLength)]
    public string? Notes { get; set; }

    [BindProperty]
    public bool Active { get; set; }

    [BindProperty]
    [Required, StringLength(ClaimSourceAdministrationPolicy.MaximumReasonLength, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        ClaimSource = await claimSourceQueries.GetAsync(actor, id, cancellationToken);
        if (ClaimSource is null)
        {
            return NotFound();
        }

        ExpectedVersion = ClaimSource.Version;
        Name = ClaimSource.Name;
        ContactName = ClaimSource.ContactName;
        Telephone = ClaimSource.Telephone;
        Email = ClaimSource.Email;
        Notes = ClaimSource.Notes;
        Active = ClaimSource.Active;
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        // Existence only: the posted ExpectedVersion below is exactly what
        // the caller supplied, never overwritten by this lookup, so a stale
        // form is refused by the store rather than silently refreshed here.
        ClaimSource = await claimSourceQueries.GetAsync(actor, id, cancellationToken);
        if (ClaimSource is null)
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
                await claimSourceAdministration.SaveAsync(
                    new(
                        actor,
                        id,
                        ExpectedVersion,
                        Name,
                        ContactName,
                        Telephone,
                        Email,
                        Notes,
                        Active,
                        Reason,
                        OperationKey),
                    cancellationToken);
                TempData["AdministrationStatus"] = "The claim source was updated.";
                return RedirectToPage("Index");
            }
            catch (ClaimSourceAdministrationException exception)
            {
                ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error));
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "The claim source details were not accepted.");
            }
            catch (StaffAuthorizationException)
            {
                return Forbid();
            }
        }

        OperationKey = NewOperationKey();
        ExpectedVersion = ClaimSource.Version;
        return Page();
    }

    private static string MutationErrorMessage(ClaimSourceAdministrationError error) => error switch
    {
        ClaimSourceAdministrationError.ClaimSourceNotFound =>
            "The claim source no longer exists.",
        ClaimSourceAdministrationError.StaleVersion =>
            "The claim source changed after this page was loaded. Review the current version and retry.",
        ClaimSourceAdministrationError.OperationConflict =>
            "The form was already used for a different operation. Retry from the current page.",
        _ => "The claim source change was not accepted."
    };
}
