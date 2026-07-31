using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class MailboxesModel(
    ListApprovedMailboxes listApprovedMailboxes,
    UpdateApprovedMailbox updateApprovedMailbox)
    : AdministrationPageModel
{
    public IReadOnlyList<ApprovedMailbox> Mailboxes { get; private set; } = [];

    public Guid NewMailboxId { get; private set; }

    [BindProperty]
    public Guid MailboxId { get; set; }

    [BindProperty]
    [Required, StringLength(320, MinimumLength = 3)]
    public string Address { get; set; } = string.Empty;

    [BindProperty]
    public string[] SelectedRouteScopes { get; set; } = [];

    [BindProperty]
    [Required]
    public string SelectedState { get; set; } = ApprovedMailboxState.Approved.ToString();

    [BindProperty]
    [Range(0, int.MaxValue)]
    public int ExpectedVersion { get; set; }

    [BindProperty]
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedMailboxes);
        await LoadAsync(actor, cancellationToken);
        NewMailboxId = Guid.NewGuid();
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedMailboxes);
        var routeScopes = ParseRouteScopes();
        if (!Enum.TryParse<ApprovedMailboxState>(
                SelectedState,
                ignoreCase: false,
                out var state)
            || !Enum.IsDefined(state))
        {
            ModelState.AddModelError(nameof(SelectedState), "Select a supported mailbox state.");
        }
        if (MailboxId == Guid.Empty || !IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                var updated = await updateApprovedMailbox.ExecuteAsync(
                    new(
                        MailboxId,
                        Address,
                        routeScopes,
                        state,
                        ExpectedVersion,
                        actor,
                        Reason,
                        OperationKey),
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    $"Approved-mailbox policy version {updated.Version} was recorded for {updated.Address}.";
                return RedirectToPage();
            }
            catch (ApprovedMailboxUpdateException exception)
            {
                ModelState.AddModelError(string.Empty, exception.Error switch
                {
                    ApprovedMailboxUpdateError.NotFound =>
                        "The mailbox policy no longer exists. Your change was not applied.",
                    ApprovedMailboxUpdateError.DuplicateAddress =>
                        "That mailbox address already has a policy. Update the existing row instead.",
                    ApprovedMailboxUpdateError.VersionConflict =>
                        "The mailbox policy changed after this form was loaded. " +
                        "Your change was not applied; review the current row and retry.",
                    ApprovedMailboxUpdateError.OperationConflict =>
                        "This form was already used for another mailbox change. Review the current row and retry.",
                    _ => "The approved-mailbox change was not accepted."
                });
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(
                    nameof(Address),
                    "Enter a supported mailbox address and route scope.");
            }
        }

        await LoadAsync(actor, cancellationToken);
        if (ExpectedVersion > 0)
        {
            var current = Mailboxes.SingleOrDefault(item => item.Id == MailboxId);
            if (current is not null)
            {
                ExpectedVersion = current.Version;
            }
        }
        NewMailboxId = ExpectedVersion == 0 && MailboxId != Guid.Empty
            ? MailboxId
            : Guid.NewGuid();
        OperationKey = NewOperationKey();
        return Page();
    }

    public string AddressFor(ApprovedMailbox mailbox) =>
        mailbox.Id == MailboxId && ExpectedVersion > 0 ? Address : mailbox.Address;

    public string ReasonFor(ApprovedMailbox mailbox) =>
        mailbox.Id == MailboxId && ExpectedVersion > 0 ? Reason : string.Empty;

    public bool IsRouteSelected(
        ApprovedMailbox mailbox,
        ApprovedMailboxRouteScope routeScope) =>
        mailbox.Id == MailboxId && ExpectedVersion > 0
            ? SelectedRouteScopes.Contains(routeScope.ToString(), StringComparer.Ordinal)
            : mailbox.RouteScopes.Contains(routeScope);

    public bool IsStateSelected(
        ApprovedMailbox mailbox,
        ApprovedMailboxState state) =>
        mailbox.Id == MailboxId && ExpectedVersion > 0
            ? SelectedState == state.ToString()
            : mailbox.State == state;

    public string NewAddress => ExpectedVersion == 0 ? Address : string.Empty;

    public string NewReason => ExpectedVersion == 0 ? Reason : string.Empty;

    public bool IsNewRouteSelected(ApprovedMailboxRouteScope routeScope) =>
        ExpectedVersion == 0
            && SelectedRouteScopes.Contains(routeScope.ToString(), StringComparer.Ordinal);

    public bool IsNewStateSelected(ApprovedMailboxState state) =>
        ExpectedVersion == 0
            ? SelectedState == state.ToString()
            : state == ApprovedMailboxState.Approved;

    private HashSet<ApprovedMailboxRouteScope> ParseRouteScopes()
    {
        var routeScopes = new HashSet<ApprovedMailboxRouteScope>();
        foreach (var value in SelectedRouteScopes)
        {
            if (!Enum.TryParse<ApprovedMailboxRouteScope>(value, ignoreCase: false, out var routeScope)
                || !Enum.IsDefined(routeScope))
            {
                ModelState.AddModelError(
                    nameof(SelectedRouteScopes),
                    "Select only supported mailbox route scopes.");
                continue;
            }

            routeScopes.Add(routeScope);
        }
        if (routeScopes.Count == 0)
        {
            ModelState.AddModelError(
                nameof(SelectedRouteScopes),
                "Select at least one mailbox route scope.");
        }

        return routeScopes;
    }

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken) =>
        Mailboxes = await listApprovedMailboxes.ExecuteAsync(actor, cancellationToken);
}
