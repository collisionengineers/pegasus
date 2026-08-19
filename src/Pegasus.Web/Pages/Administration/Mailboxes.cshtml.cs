using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class MailboxesModel(
    ListApprovedMailboxes listApprovedMailboxes,
    UpdateApprovedMailbox updateApprovedMailbox,
    IApprovedMailboxPollStatusQueries pollStatusQueries)
    : AdministrationPageModel
{
    // The one place this page's route-scope labels are written, so the table
    // column and both sets of route-scope checkboxes never drift apart. The
    // shared OperatorLabels.Humanise fallback would render "Inbound intake",
    // which carries the banned "intake" word (docs/design/README.md).
    public static string RouteScopeLabel(ApprovedMailboxRouteScope routeScope) => routeScope switch
    {
        ApprovedMailboxRouteScope.InboundIntake => "New instructions and Triage mail (Inbox)",
        ApprovedMailboxRouteScope.SentEvidence => "Exact report and Triage evidence (Sent Items)",
        _ => routeScope.ToString()
    };

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

    // Exact tenant identifiers, not credentials. They are shown in full here, on the
    // only surface that already requires Administrator and ManageApprovedMailboxes,
    // and nowhere else.
    [BindProperty]
    [StringLength(100)]
    public string? MailboxIdentity { get; set; }

    [BindProperty]
    [StringLength(200)]
    public string? InboxFolderIdentity { get; set; }

    [BindProperty]
    [StringLength(200)]
    public string? SentFolderIdentity { get; set; }

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
                        OperationKey,
                        MailboxIdentity,
                        InboxFolderIdentity,
                        SentFolderIdentity),
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
                    ApprovedMailboxUpdateError.MissingMailboxIdentity =>
                        "An approved mailbox needs its mailbox identity, plus the Inbox folder " +
                        "identity for new instructions and the Sent folder identity for Sent evidence. " +
                        "Save the mailbox as Disabled while you are still waiting for them.",
                    ApprovedMailboxUpdateError.InvalidMailboxIdentity =>
                        "A mailbox or folder identity must be an exact identifier with no spaces: " +
                        "up to 100 characters for the mailbox and 200 for a folder.",
                    ApprovedMailboxUpdateError.MailboxIdentityImmutable =>
                        "A mailbox identity and address cannot be changed once saved. " +
                        "Disable this mailbox and add a new one.",
                    ApprovedMailboxUpdateError.DuplicateMailboxIdentity =>
                        "That mailbox identity already belongs to another row. " +
                        "Two rows cannot share one mailbox.",
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

    public IReadOnlyList<ApprovedMailboxPollStatus> PollStatuses { get; private set; } = [];

    public string AddressFor(ApprovedMailbox mailbox) =>
        mailbox.Id == MailboxId && ExpectedVersion > 0 ? Address : mailbox.Address;

    public string MailboxIdentityFor(ApprovedMailbox mailbox) =>
        Identity(mailbox, mailbox.MailboxIdentity, MailboxIdentity);

    public string InboxFolderIdentityFor(ApprovedMailbox mailbox) =>
        Identity(mailbox, mailbox.InboxFolderIdentity, InboxFolderIdentity);

    public string SentFolderIdentityFor(ApprovedMailbox mailbox) =>
        Identity(mailbox, mailbox.SentFolderIdentity, SentFolderIdentity);

    private string Identity(ApprovedMailbox mailbox, string? saved, string? posted) =>
        saved ?? (mailbox.Id == MailboxId && ExpectedVersion > 0 ? posted ?? string.Empty : string.Empty);

    public string NewMailboxIdentity => ExpectedVersion == 0 ? MailboxIdentity ?? string.Empty : string.Empty;

    public string NewInboxFolderIdentity =>
        ExpectedVersion == 0 ? InboxFolderIdentity ?? string.Empty : string.Empty;

    public string NewSentFolderIdentity =>
        ExpectedVersion == 0 ? SentFolderIdentity ?? string.Empty : string.Empty;

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
            ? $"Last completed {lastCompletedAtUtc:u}."
            : "No completed poll yet.";
        var due = $" Next due {status.DueAtUtc:u}.";
        var failure = status.LastFailureCode switch
        {
            null => string.Empty,
            "mailbox_access_denied" =>
                " The tenant has not granted this application access to this mailbox.",
            "mailbox_not_approved" =>
                " The last attempt stopped because this mailbox was no longer approved.",
            var code => $" Last failure: {code}."
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
}
