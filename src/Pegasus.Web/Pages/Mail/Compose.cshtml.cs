using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
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
    IStaffMailAttachmentResolver attachmentResolver) : StaffPageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid? CaseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? OperationId { get; set; }

    [BindProperty]
    public Guid ApprovedMailboxId { get; set; }

    [BindProperty]
    public long ExpectedContextVersion { get; set; }

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

    public IReadOnlyList<ApprovedMailbox> SendableMailboxes { get; private set; } = [];

    public CaseSearchItem? Case { get; private set; }

    public StaffMailOperation? Operation { get; private set; }

    public IReadOnlyList<StaffMailAttachmentOption> AvailableAttachments { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        await LoadSendableMailboxesAsync(cancellationToken);

        if (CaseId is { } caseId)
        {
            var details = await getCase.ExecuteAsync(new(caseId, actor), cancellationToken);
            if (details is null)
            {
                return NotFound();
            }
            Case = details.Summary;
            ExpectedContextVersion = details.Workflow.Version;
            AvailableAttachments = await attachmentResolver.ListCaseAsync(
                actor, caseId, cancellationToken);
        }

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

        await LoadSendableMailboxesAsync(cancellationToken);

        if (CaseId is not { } caseId)
        {
            ModelState.AddModelError(nameof(CaseId), "Choose the Case this correspondence belongs to.");
        }

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

        var mailbox = SendableMailboxes.FirstOrDefault(item => item.Id == ApprovedMailboxId);
        if (mailbox is null)
        {
            ModelState.AddModelError(nameof(ApprovedMailboxId), "Choose an approved mailbox to send from.");
        }

        CaseDetails? details = null;
        IReadOnlyList<StaffMailAttachment> attachments = [];
        if (CaseId is { } presentCaseId)
        {
            details = await getCase.ExecuteAsync(new(presentCaseId, actor), cancellationToken);
            if (details is null)
            {
                return NotFound();
            }
            Case = details.Summary;
            AvailableAttachments = await attachmentResolver.ListCaseAsync(
                actor, presentCaseId, cancellationToken);
            if (details.Workflow.Version != ExpectedContextVersion)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The Case changed after this page was loaded. Review it and try again.");
            }
            try
            {
                attachments = await attachmentResolver.ResolveCaseAsync(
                    actor, presentCaseId, SelectedAttachments, cancellationToken);
            }
            catch (StaffMailAttachmentSelectionException exception)
            {
                ModelState.AddModelError(nameof(SelectedAttachments), exception.Message);
            }
        }

        if (!ModelState.IsValid || mailbox is null || details is null)
        {
            return Page();
        }

        try
        {
            StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
            Operation = await staffMailSend.SendAsync(
                new(
                    actor,
                    mailbox.Id,
                    mailbox.Generation,
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
        return RedirectToPage(new { caseId = details.Summary.CaseId, operationId = Operation.Id });
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

        await LoadSendableMailboxesAsync(cancellationToken);
        try
        {
            Operation = await staffMailSend.ReconcileAsync(
                actor, operationId, expectedOperationVersion, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        return Page();
    }

    private static StaffMailRecipient[] ParseRecipients(string? value) =>
        (value ?? string.Empty)
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(address => new StaffMailRecipient(address, DisplayName: null))
            .ToArray();

    private async Task LoadSendableMailboxesAsync(CancellationToken cancellationToken)
    {
        var mailboxes = await approvedMailboxes.ListAsync(cancellationToken);
        // Stream A's ruling: SentEvidence is not send authorization. A
        // mailbox must be Approved, carry StaffSend, and have a positive
        // Generation before it is offered — no fallback.
        SendableMailboxes = mailboxes
            .Where(item => item.State == ApprovedMailboxState.Approved
                && item.RouteScopes.Contains(ApprovedMailboxRouteScope.StaffSend)
                && item.Generation > 0)
            .ToArray();
    }
}
