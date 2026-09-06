using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.ClaimSources;

/// <summary>
/// EXT-19/S13 item 8: list and create Claim Sources. A Claim Source is a
/// linked but distinct record from principal, sender, insurer and
/// third-party engineer — no password or mailbox control belongs here.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IClaimSourceQueries claimSourceQueries,
    IClaimSourceAdministration claimSourceAdministration)
    : AdministrationPageModel
{
    public IReadOnlyList<ClaimSourceRecord> ClaimSources { get; private set; } = [];

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
    [Required, StringLength(ClaimSourceAdministrationPolicy.MaximumReasonLength, MinimumLength = 1)]
    public string Reason { get; set; } = "Created";

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    // C06 review R-14: minted alongside OperationKey and bound as a hidden
    // field so a replayed create POST (same OperationKey, e.g. a retried
    // submission) carries the same id too — the store's idempotent-receipt
    // replay is keyed on OperationKey but its request hash includes Id, so a
    // freshly-minted Guid.NewGuid() per request would make every retry an
    // OperationConflict instead of the intended replay of the original create.
    [BindProperty]
    public Guid NewClaimSourceId { get; set; } = Guid.NewGuid();

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
                        NewClaimSourceId,
                        ExpectedVersion: 0,
                        Name,
                        ContactName,
                        Telephone,
                        Email,
                        Notes,
                        Active: true,
                        Reason,
                        OperationKey),
                    cancellationToken);
                TempData["AdministrationStatus"] = "The claim source was created.";
                return RedirectToPage();
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
        NewClaimSourceId = Guid.NewGuid();
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        ClaimSources = await claimSourceQueries.SearchAsync(
            actor,
            string.Empty,
            ClaimSourceAdministrationPolicy.MaximumSearchLimit,
            cancellationToken);
    }

    private static string MutationErrorMessage(ClaimSourceAdministrationError error) => error switch
    {
        ClaimSourceAdministrationError.ClaimSourceNotFound =>
            "The claim source no longer exists.",
        ClaimSourceAdministrationError.StaleVersion =>
            "The claim source changed after this page was loaded. Review the current version and retry.",
        ClaimSourceAdministrationError.OperationConflict =>
            "The form was already used for a different operation. Retry from the current page.",
        _ => "The claim source could not be created."
    };
}
