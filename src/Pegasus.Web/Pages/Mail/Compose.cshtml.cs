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
/// The command's <c>ExpectedMailboxGeneration</c> is read here from
/// <see cref="ApprovedMailbox.Version"/> (<see cref="IApprovedMailboxStore"/>,
/// the pre-existing Administration/Mailboxes query) filtered to the
/// <see cref="ApprovedMailboxRouteScope.SentEvidence"/> scope. The
/// approved-mailbox entity also carries a distinct <c>MailboxGeneration</c>/
/// <c>AllowStaffSend</c> pair meant for this exact command, but no Core query
/// yet exposes either to Web — a follow-up must add one and correct this
/// mapping (recorded as a C08 deviation).
/// </remarks>
public sealed class ComposeModel(
    IStaffMailSend staffMailSend,
    IApprovedMailboxStore approvedMailboxes,
    IGetCase getCase) : StaffPageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid? CaseId { get; set; }

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
    public string OperationKey { get; set; } = NewOperationKey();

    [TempData]
    public string? SendNotice { get; set; }

    public IReadOnlyList<ApprovedMailbox> SendableMailboxes { get; private set; } = [];

    public CaseSearchItem? Case { get; private set; }

    public StaffMailOperation? Operation { get; private set; }

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
        if (CaseId is { } presentCaseId)
        {
            details = await getCase.ExecuteAsync(new(presentCaseId, actor), cancellationToken);
            if (details is null)
            {
                return NotFound();
            }
            Case = details.Summary;
            if (details.Workflow.Version != ExpectedContextVersion)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The Case changed after this page was loaded. Review it and try again.");
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
                    mailbox.Version,
                    StaffMailPurpose.GeneralCorrespondence,
                    details.Summary.CaseId,
                    details.Workflow.Version,
                    StaffMailComposeMode.New,
                    OriginalMessage: null,
                    to,
                    ParseRecipients(Cc),
                    Subject!.Trim(),
                    Body!.Trim(),
                    Attachments: [],
                    OperationKey),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        SendNotice = $"Correspondence {OperatorLabels.StaffMail.State(Operation.State).ToLowerInvariant()}.";
        return RedirectToPage(new { caseId = details.Summary.CaseId });
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
        SendableMailboxes = mailboxes
            .Where(item => item.State == ApprovedMailboxState.Approved
                && item.RouteScopes.Contains(ApprovedMailboxRouteScope.SentEvidence))
            .ToArray();
    }
}
