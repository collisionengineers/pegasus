using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Email;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Mail;

/// <summary>
/// New staff correspondence (C08), unrelated to any retained message —
/// contrast <c>Message.cshtml.cs</c>'s Reply/ReplyAll/Forward, each of which
/// quotes one. Route <c>/Inbox/Compose</c>.
/// </summary>
/// <remarks>
/// <see cref="Pegasus.Core.Operations.StaffMailSendCommand"/> ties every send
/// to a context (Case) with an expected version — general correspondence with
/// no Case is not representable by the contract as written, so this page
/// requires one (a C08 deviation, recorded in scratch/execution.md).
///
/// The command's <c>ExpectedMailboxGeneration</c> is read here from the
/// shared <see cref="ApprovedMailbox.Generation"/> field (G14), the exact
/// field the command means — not <see cref="ApprovedMailbox.Version"/>
/// (Administration's own optimistic-concurrency counter for mailbox edits, a
/// different field this page no longer conflates it with).
///
/// A mailbox is offered to send from only when it is Approved, carries
/// <see cref="ApprovedMailboxRouteScope.StaffSend"/> (G14's dedicated
/// capability for this exact command), and has a positive
/// <see cref="ApprovedMailbox.Generation"/> — per Stream A's ruling (PR 673
/// comment 5561214716, items 1-2): <see cref="ApprovedMailboxRouteScope.SentEvidence"/>
/// alone is not send authorization, so the earlier <c>StaffSend</c>-or-
/// <c>SentEvidence</c> fallback is removed.
/// </remarks>
public sealed class ComposeModel(
    IStaffMailSend staffMailSend,
    IApprovedMailboxStore approvedMailboxes,
    IGetCase getCase,
    ISearchCases searchCases,
    IStaffMailAttachmentResolver attachmentResolver) : StaffPageModel
{
    [BindProperty(SupportsGet = true, Name = "caseReference")]
    public string? CaseReference { get; set; }

    [BindProperty(SupportsGet = true, Name = "caseQuery")]
    public string? CaseQuery { get; set; }

    [BindProperty]
    public long ExpectedContextVersion { get; set; }

    [BindProperty]
    public string? SelectedCaseReference { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? OperationId { get; set; }

    [BindProperty]
    public string? To { get; set; }

    [BindProperty]
    public string? Cc { get; set; }

    [BindProperty]
    public string? Subject { get; set; }

    [BindProperty]
    public string? Body { get; set; }

    [BindProperty]
    public List<string> SelectedAttachments { get; set; } = [];

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    [TempData]
    public string? SendNotice { get; set; }

    public ApprovedMailbox? DefaultMailbox { get; private set; }

    public IReadOnlyList<CaseSearchItem> CaseResults { get; private set; } = [];

    public bool StaffMailAvailable => staffMailSend is not UnavailableStaffMailSend;

    public CaseSearchItem? Case { get; private set; }

    public StaffMailOperation? Operation { get; private set; }

    public IReadOnlyList<StaffMailAttachmentOption> AvailableAttachments { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!StaffMailAvailable)
        {
            return Page();
        }

        await LoadDefaultMailboxAsync(cancellationToken);
        await LoadCaseContextAsync(actor, cancellationToken);

        // Carries the just-sent operation's identity across the post-send
        // redirect, so the Send-status panel — and, for Unknown, the
        // Reconcile form that is its only caller — actually renders on the
        // page the operator lands on instead of being silently discarded.
        if (OperationId is { } operationId)
        {
            Operation = await staffMailSend.GetAsync(actor, operationId, cancellationToken);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSendAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!StaffMailAvailable)
        {
            return NotFound();
        }

        await LoadDefaultMailboxAsync(cancellationToken);

        var to = ParseRecipients(To);
        if (to.Length == 0)
        {
            ModelState.AddModelError(nameof(To), "At least one recipient is required.");
        }
        if (string.IsNullOrWhiteSpace(Subject))
        {
            ModelState.AddModelError(nameof(Subject), "A subject is required.");
        }
        if (string.IsNullOrWhiteSpace(Body))
        {
            ModelState.AddModelError(nameof(Body), "A message is required.");
        }

        if (DefaultMailbox is null)
        {
            ModelState.AddModelError(string.Empty, "No default approved mailbox is configured for correspondence.");
        }

        var details = await ResolveCaseAsync(actor, CaseReference, cancellationToken);
        if (details is null)
        {
            ModelState.AddModelError(nameof(CaseReference), "Choose one Case by its Case / PO reference.");
            if (!string.IsNullOrWhiteSpace(CaseReference))
            {
                CaseQuery = CaseReference;
                CaseResults = await SearchCasesAsync(actor, CaseQuery, cancellationToken);
            }
        }

        IReadOnlyList<StaffMailAttachment> attachments = [];
        if (details is not null)
        {
            Case = details.Summary;
            AvailableAttachments = await attachmentResolver.ListCaseAsync(
                actor, details.Summary.CaseId, cancellationToken);
            try
            {
                attachments = await attachmentResolver.ResolveCaseAsync(
                    actor, details.Summary.CaseId, SelectedAttachments, cancellationToken);
            }
            catch (StaffMailAttachmentSelectionException exception)
            {
                ModelState.AddModelError(nameof(SelectedAttachments), exception.Message);
            }

            if (ExpectedContextVersion < 0 || details.Workflow.Version != ExpectedContextVersion)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The Case changed after this correspondence was selected. Select it again before sending.");
            }
        }

        if (!ModelState.IsValid || DefaultMailbox is null || details is null)
        {
            return Page();
        }

        try
        {
            StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
            Operation = await staffMailSend.SendAsync(
                new(
                    actor,
                    DefaultMailbox.Id,
                    DefaultMailbox.Generation,
                    StaffMailPurpose.GeneralCorrespondence,
                    details.Summary.CaseId,
                    details.Workflow.Version,
                    StaffMailComposeMode.New,
                    OriginalMessage: null,
                    to,
                    ParseRecipients(Cc),
                    Subject!.Trim(),
                    Body!.Trim(),
                    attachments,
                    OperationKey),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        // Only a terminal Sent outcome is a success notice. Anything else —
        // Submitted (still in flight) or Unknown (ambiguous, never resend) —
        // must be read off the Send-status panel the redirected GET renders,
        // never announced as if it had reached the provider.
        if (Operation.State == StaffMailState.Sent)
        {
            SendNotice = "Correspondence sent.";
        }
        return RedirectToPage(new { caseReference = details.Summary.Reference, operationId = Operation.Id });
    }

    public async Task<IActionResult> OnPostSearchCaseAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!StaffMailAvailable)
        {
            return NotFound();
        }

        await LoadDefaultMailboxAsync(cancellationToken);
        await LoadSelectedCaseAsync(actor, cancellationToken);
        if (!TryNormalizeCaseQuery(out var query) || string.IsNullOrWhiteSpace(query))
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ModelState.AddModelError(nameof(CaseQuery), "Enter a Case search term.");
            }
            return Page();
        }

        CaseQuery = query;
        CaseResults = await SearchCasesAsync(actor, query, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSelectCaseAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!StaffMailAvailable)
        {
            return NotFound();
        }

        await LoadDefaultMailboxAsync(cancellationToken);
        var details = await ResolveCaseAsync(actor, SelectedCaseReference, cancellationToken);
        if (details is null)
        {
            ModelState.AddModelError(nameof(CaseReference), "Choose one Case by its Case / PO reference.");
            return Page();
        }

        Case = details.Summary;
        ModelState.Remove(nameof(CaseReference));
        ModelState.Remove(nameof(ExpectedContextVersion));
        CaseReference = details.Summary.Reference;
        ExpectedContextVersion = details.Workflow.Version;
        AvailableAttachments = await attachmentResolver.ListCaseAsync(
            actor, details.Summary.CaseId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostReconcileAsync(
        Guid operationId,
        long expectedOperationVersion,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!StaffMailAvailable)
        {
            return NotFound();
        }

        if (operationId == Guid.Empty || expectedOperationVersion < 0)
        {
            SendNotice = "The send status request was incomplete. Reload the correspondence and try again.";
            return RedirectToPage();
        }

        try
        {
            Operation = await staffMailSend.GetAsync(actor, operationId, cancellationToken);
            if (Operation is null)
            {
                SendNotice = "That send status is no longer available. Reload the correspondence and try again.";
                return RedirectToPage();
            }
            Operation = await staffMailSend.ReconcileAsync(actor, operationId, expectedOperationVersion, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            SendNotice = "The send status request was invalid. Reload the correspondence and try again.";
            return RedirectToPage();
        }
        catch (InvalidOperationException)
        {
            SendNotice = "The send status could not be reconciled. Reload the correspondence and try again.";
            return RedirectToPage();
        }

        var context = await getCase.ExecuteAsync(new(Operation.ContextId, actor), cancellationToken);
        return context is null
            ? NotFound()
            : RedirectToPage(new { caseReference = context.Summary.Reference, operationId = Operation.Id });
    }

    private static StaffMailRecipient[] ParseRecipients(string? value) =>
        (value ?? string.Empty)
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(address => new StaffMailRecipient(address, DisplayName: null))
            .ToArray();

    private async Task LoadDefaultMailboxAsync(CancellationToken cancellationToken)
    {
        var mailboxes = await approvedMailboxes.ListAsync(cancellationToken);
        // A correspondence sender is also the sent-evidence source. It must
        // be completely ready for both roles before the page offers it.
        var defaults = mailboxes
            .Where(item => item.State == ApprovedMailboxState.Approved
                && item.RouteScopes.Contains(ApprovedMailboxRouteScope.StaffSend)
                && item.RouteScopes.Contains(ApprovedMailboxRouteScope.SentEvidence)
                && item.ActivatedAtUtc is not null
                && !string.IsNullOrWhiteSpace(item.MailboxIdentity)
                && !string.IsNullOrWhiteSpace(item.SentFolderIdentity)
                && item.Generation > 0
                && item.VerifiedEncodedMessageSizeLimit is > 0
                && item.IsDefaultStaffSend)
            .ToArray();
        DefaultMailbox = defaults.Length == 1 ? defaults[0] : null;
    }

    private async Task LoadCaseContextAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        if (TryNormalizeCaseQuery(out var query) && query is not null)
        {
            CaseQuery = query;
            CaseResults = await SearchCasesAsync(actor, query, cancellationToken);
        }

        var details = await ResolveCaseAsync(actor, CaseReference, cancellationToken);
        if (details is null)
        {
            return;
        }

        Case = details.Summary;
        CaseReference = details.Summary.Reference;
        ExpectedContextVersion = details.Workflow.Version;
        AvailableAttachments = await attachmentResolver.ListCaseAsync(
            actor, details.Summary.CaseId, cancellationToken);
    }

    private async Task LoadSelectedCaseAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        var details = await ResolveCaseAsync(actor, CaseReference, cancellationToken);
        if (details is null)
        {
            return;
        }

        Case = details.Summary;
        CaseReference = details.Summary.Reference;
        AvailableAttachments = await attachmentResolver.ListCaseAsync(
            actor, details.Summary.CaseId, cancellationToken);
    }

    private async Task<CaseDetails?> ResolveCaseAsync(
        ActionActor actor,
        string? reference,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeCaseReference(reference, out var value))
        {
            return null;
        }

        var matches = await searchCases.ExecuteAsync(
            new(actor, new(CaseSearchFilters(CaseReference: value), PageSize: 2), cancellationToken);
        var match = matches.Items.SingleOrDefault(item =>
            string.Equals(item.Reference, value, StringComparison.OrdinalIgnoreCase));
        return match is null
            ? null
            : await getCase.ExecuteAsync(new(match.CaseId, actor), cancellationToken);
    }

    private async Task<IReadOnlyList<CaseSearchItem>> SearchCasesAsync(
        ActionActor actor,
        string? query,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeCaseQueryValue(query, out var value))
        {
            return [];
        }

        return (await searchCases.ExecuteAsync(
            new(actor, new(CaseSearchFilters(Query: value), PageSize: 10), cancellationToken)).Items;
    }

    private bool TryNormalizeCaseQuery(out string? query)
    {
        if (TryNormalizeCaseQueryValue(CaseQuery, out query))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(CaseQuery))
        {
            ModelState.AddModelError(nameof(CaseQuery), "Case searches must be 300 characters or fewer.");
        }
        return false;
    }

    private static bool TryNormalizeCaseQueryValue(string? value, out string? normalized)
    {
        normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length <= 300;
    }

    private static bool TryNormalizeCaseReference(string? value, out string? normalized)
    {
        normalized = value?.Trim();
        return !string.IsNullOrWhiteSpace(normalized) && normalized.Length <= 100;
    }
}
