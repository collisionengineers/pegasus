using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class MailboxesModel(
    ListApprovedMailboxes listApprovedMailboxes,
    UpdateApprovedMailbox updateApprovedMailbox,
    IApprovedMailboxPollStatusQueries pollStatusQueries,
    IResolveApprovedMailboxIdentity resolveApprovedMailboxIdentity)
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
        // Loaded once, up front: an existing row's own identities are read from here
        // (never from the client — the form no longer carries them) so a save that only
        // changes route scope or state still resends the identity UpdateApprovedMailbox
        // requires for an Approved row, without the operator ever seeing it.
        await LoadAsync(actor, cancellationToken);
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

        var isNewMailbox = ExpectedVersion == 0;
        var existingMailbox = isNewMailbox
            ? null
            : Mailboxes.SingleOrDefault(mailbox => mailbox.Id == MailboxId);
        ApprovedMailboxIdentityResolution? resolution = null;
        if (ModelState.IsValid && isNewMailbox)
        {
            string normalizedAddress;
            try
            {
                normalizedAddress = ApprovedMailboxAddress.Normalize(Address);
            }
            catch (ArgumentException)
            {
                normalizedAddress = string.Empty;
                ModelState.AddModelError(nameof(Address), "Enter a supported mailbox address and route scope.");
            }

            if (ModelState.IsValid)
            {
                resolution = await resolveApprovedMailboxIdentity.ResolveAsync(normalizedAddress, cancellationToken);
                if (resolution is null)
                {
                    ModelState.AddModelError(
                        nameof(Address),
                        "The address could not be found in the mail system.");
                }
            }
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
                        OperationKey,
                        resolution?.MailboxIdentity ?? existingMailbox?.MailboxIdentity,
                        resolution?.InboxFolderIdentity ?? existingMailbox?.InboxFolderIdentity,
                        resolution?.SentFolderIdentity ?? existingMailbox?.SentFolderIdentity,
                        resolution?.FolderBindings),
                    cancellationToken);
                TempData["AdministrationStatus"] = $"The mailbox policy for {updated.Address} was saved.";
                return RedirectToPage();
            }
            catch (ApprovedMailboxUpdateException exception)
            {
                ModelState.AddModelError(string.Empty, MailboxErrorMessage(exception));
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(
                    nameof(Address),
                    "Enter a supported mailbox address and route scope.");
            }
        }

        // Reloaded again: the up-front load above may now be stale (this failure can
        // itself be a version conflict with another save that landed in between).
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

    public async Task<IActionResult> OnPostResolveFoldersAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageApprovedMailboxes);
        await LoadAsync(actor, cancellationToken);
        var mailbox = Mailboxes.SingleOrDefault(item => item.Id == MailboxId);
        if (mailbox is null
            || mailbox.MailboxIdentity is null
            || ExpectedVersion != mailbox.Version
            || !IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(
                string.Empty,
                "The mailbox policy changed after this form was loaded. Review it and retry.");
        }

        ApprovedMailboxIdentityResolution? resolution = null;
        if (ModelState.IsValid)
        {
            resolution = await resolveApprovedMailboxIdentity.ResolveAsync(
                mailbox!.Address,
                cancellationToken);
            if (resolution is null
                || !string.Equals(
                    resolution.MailboxIdentity,
                    mailbox.MailboxIdentity,
                    StringComparison.Ordinal))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The logical folders could not be resolved for this exact mailbox.");
            }
        }

        if (ModelState.IsValid)
        {
            try
            {
                var updated = await updateApprovedMailbox.ExecuteAsync(
                    new(
                        mailbox!.Id,
                        mailbox.Address,
                        mailbox.RouteScopes,
                        mailbox.State,
                        mailbox.Version,
                        actor,
                        "Refresh approved logical folder bindings from the mail system.",
                        OperationKey,
                        mailbox.MailboxIdentity,
                        mailbox.InboxFolderIdentity,
                        mailbox.SentFolderIdentity,
                        resolution!.FolderBindings ?? []),
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    $"{updated.FolderBindings.Count} logical folder bindings were saved for {updated.Address}.";
                return RedirectToPage();
            }
            catch (ApprovedMailboxUpdateException exception)
            {
                ModelState.AddModelError(string.Empty, MailboxErrorMessage(exception));
            }
        }

        await LoadAsync(actor, cancellationToken);
        NewMailboxId = Guid.NewGuid();
        OperationKey = NewOperationKey();
        return Page();
    }

    public IReadOnlyList<ApprovedMailboxPollStatus> PollStatuses { get; private set; } = [];

    public string AddressFor(ApprovedMailbox mailbox) =>
        mailbox.Id == MailboxId && ExpectedVersion > 0 ? Address : mailbox.Address;

    /// <summary>
    /// What the last poll of this mailbox actually did. A mailbox with no cursor row has
    /// never been polled; a mailbox the tenant has not admitted reports that plainly,
    /// because approving an address in Pegasus grants no Exchange access.
    /// </summary>
    public string PollStatusFor(ApprovedMailbox mailbox)
    {
        var status = PollStatuses.SingleOrDefault(item =>
            string.Equals(item.MailboxAddress, mailbox.Address, StringComparison.OrdinalIgnoreCase));
        if (status is null)
        {
            return mailbox.State == ApprovedMailboxState.Approved
                && mailbox.RouteScopes.Contains(ApprovedMailboxRouteScope.InboundIntake)
                ? "Not yet polled."
                : "Not polled.";
        }

        var completed = status.LastCompletedAtUtc is { } lastCompletedAtUtc
            ? $"Last completed {Presentation.OperatorLabels.OfficeTime(lastCompletedAtUtc)}."
            : "No completed poll yet.";
        var due = $" Next due {Presentation.OperatorLabels.OfficeTime(status.DueAtUtc)}.";
        var failure = status.LastFailureCode switch
        {
            null => string.Empty,
            "mailbox_access_denied" =>
                " The tenant has not granted this application access to this mailbox.",
            "mailbox_not_approved" =>
                " The last attempt stopped because this mailbox was no longer approved.",
            var code => $" Last failure: {Presentation.OperatorLabels.Humanise(code)}."
        };
        return $"{completed}{due}{failure}";
    }

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

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        Mailboxes = await listApprovedMailboxes.ExecuteAsync(actor, cancellationToken);
        PollStatuses = await pollStatusQueries.ListAsync(cancellationToken);
    }

    private static string MailboxErrorMessage(ApprovedMailboxUpdateException exception) =>
        exception.Error switch
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
            ApprovedMailboxUpdateError.MissingMailboxIdentity =>
                "This mailbox cannot be approved for that route scope yet.",
            ApprovedMailboxUpdateError.InvalidMailboxIdentity =>
                "The resolved identity for this mailbox was not valid. Try again.",
            ApprovedMailboxUpdateError.MailboxIdentityImmutable =>
                "This mailbox's address cannot be changed once saved. Disable it and add a new one.",
            ApprovedMailboxUpdateError.DuplicateMailboxIdentity =>
                "That address already resolves to a mailbox approved under another row.",
            _ => "The approved-mailbox change was not accepted."
        };
}
