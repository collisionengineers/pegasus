using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Email;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The e-mail dialogue's new message (FRD-21; operator, 27 November 2026;
/// R1323, R1326, R1327): the rejection e-mail of a Case closed as Collision
/// Engineers rejected that has no instruction e-mail to answer. Close case's
/// landing opens it in the Case page's e-mail dialogue, fetched from the
/// <c>Dialog</c> handler; this page is the same composer for no script.
/// </summary>
/// <remarks>
/// It opens on the Case rejection template, To on the addresses the Case's
/// report would be sent to, the subject the registration and the claimant, and
/// From on the Case's default sender; staff change any of it before sending.
/// Any other Case has no new message: one that is not so closed, and one with
/// an instruction e-mail, whose rejection is a reply in the same dialogue.
/// </remarks>
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class NewMessageModel(
    IStaffMailSend staffMailSend,
    IGetCaseHeader getCaseHeader,
    ResolveCaseMailSender senders,
    IStaffMailAttachmentResolver attachmentResolver,
    RenderCaseRejectionEmail renderCaseRejection,
    IReportRecipientSuggestionQueries reportRecipients,
    TimeProvider clock,
    ILogger<NewMessageModel> logger) : StaffPageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public long ExpectedContextVersion { get; set; }

    /// <summary>The send the dialogue reopens on, for its status.</summary>
    [BindProperty(SupportsGet = true, Name = "mailOperationId")]
    public Guid? OperationId { get; set; }

    [BindProperty]
    public Guid? FromMailboxId { get; set; }

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

    public bool InDialog { get; private set; }

    public CaseHeader? Case { get; private set; }

    public CaseMailSenderOptions Senders { get; private set; } = CaseMailSenderOptions.None;

    public StaffMailOperation? Operation { get; private set; }

    public IReadOnlyList<StaffMailAttachmentOption> AvailableAttachments { get; private set; } = [];

    /// <summary>
    /// A send still in flight or of unknown outcome is never sent again from
    /// here, and one that was sent has nothing left to send.
    /// </summary>
    public bool OffersForm => Senders.Mailboxes.Count > 0
        && Operation?.State is null or StaffMailState.Failed;

    public Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        ShowAsync(inDialog: false, cancellationToken);

    /// <summary>The same composer as the Case page's e-mail dialogue, fetched on open.</summary>
    public Task<IActionResult> OnGetDialogAsync(CancellationToken cancellationToken) =>
        ShowAsync(inDialog: true, cancellationToken);

    private async Task<IActionResult> ShowAsync(bool inDialog, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        CaseRejectionEmail? rejection;
        try
        {
            rejection = await LoadAsync(actor, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        if (rejection is null)
        {
            return NotFound();
        }

        InDialog = inDialog;
        ExpectedContextVersion = Case!.Workflow.Version;
        FromMailboxId ??= Senders.Default?.Mailbox.Id;
        AvailableAttachments = await attachmentResolver.ListCaseAsync(actor, Id, cancellationToken);
        if (OperationId is { } operationId)
        {
            Operation = await staffMailSend.GetAsync(actor, operationId, cancellationToken);
        }
        if (OffersForm)
        {
            await PrefillAsync(rejection, cancellationToken);
        }
        if (inDialog)
        {
            return Partial("Shared/_CaseNewMessageDialog", this);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostSendAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
            if (await LoadAsync(actor, cancellationToken) is null)
            {
                return NotFound();
            }
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        var to = StaffMailReplyRecipients.Parse(To);
        var cc = StaffMailReplyRecipients.Parse(Cc);
        if (to.Length == 0)
        {
            ModelState.AddModelError(nameof(To), "At least one recipient is required.");
        }
        if (to.Concat(cc).Any(recipient => !StaffMailReplyRecipients.IsMailboxAddress(recipient.Address)))
        {
            ModelState.AddModelError(nameof(To), "Enter valid recipient addresses.");
        }
        if (string.IsNullOrWhiteSpace(Subject))
        {
            ModelState.AddModelError(nameof(Subject), "A subject is required.");
        }
        if (string.IsNullOrWhiteSpace(Body))
        {
            ModelState.AddModelError(nameof(Body), "A message is required.");
        }
        var sender = Senders.Sender(FromMailboxId);
        if (sender is null)
        {
            ModelState.AddModelError(nameof(FromMailboxId), new CaseMailSenderRequiredException().Message);
        }
        if (Case!.Workflow.Version != ExpectedContextVersion)
        {
            ModelState.AddModelError(
                string.Empty,
                "The Case changed after this correspondence was selected. Select it again before sending.");
        }

        var selection = await attachmentResolver.SelectCaseAsync(actor, Id, SelectedAttachments, cancellationToken);
        AvailableAttachments = selection.Options;
        if (selection.SelectionError is { } selectionError)
        {
            ModelState.AddModelError(nameof(SelectedAttachments), selectionError);
        }

        if (!ModelState.IsValid || sender is null)
        {
            return Page();
        }

        try
        {
            Operation = await staffMailSend.SendAsync(
                new(
                    actor,
                    sender.Mailbox.Id,
                    sender.Mailbox.Generation,
                    StaffMailPurpose.GeneralCorrespondence,
                    Id,
                    ExpectedContextVersion,
                    StaffMailComposeMode.New,
                    OriginalMessage: null,
                    to,
                    cc,
                    Subject!.Trim(),
                    Body!.Trim(),
                    selection.Attachments,
                    OperationKey),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
        catch (StaffMailOutcomeUnknownException exception)
        {
            // Recorded Unknown: staff stay on the composer with the sentence
            // and its Send status (R1069). The form keeps this operation key,
            // so sending it again replays the Unknown send.
            Pegasus.Web.Pages.Mail.StaffMailSendLog.OutcomeUnknown(
                logger, exception.Operation.Id, sender.Mailbox.Id, exception);
            Operation = exception.Operation;
            ModelState.AddModelError(string.Empty, CaseWorkspaceLabels.ReportDelivery.SendUnknown);
            return Page();
        }

        if (Operation.State == StaffMailState.Failed)
        {
            Pegasus.Web.Pages.Mail.StaffMailSendLog.Refused(logger, Operation.Id, sender.Mailbox.Id, Operation.FailureCode);
        }
        // Back on the Case, where the dialogue reopens on the send's status;
        // the message itself joins the Case's Correspondence once its Sent
        // item is observed.
        return RedirectToPage(
            "/Cases/Details",
            new
            {
                id = Id,
                section = "files",
                compose = ClosureModel.RejectionNewMessage,
                mailOperationId = Operation.Id
            });
    }

    /// <summary>
    /// The Case and its rejection e-mail; null for a Case that has no new
    /// message: unknown, not closed as Collision Engineers rejected, one with
    /// an instruction e-mail to answer, or with staff mail unavailable.
    /// </summary>
    private async Task<CaseRejectionEmail?> LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        if (staffMailSend is UnavailableStaffMailSend)
        {
            return null;
        }
        Case = await getCaseHeader.ExecuteAsync(new(Id, actor), cancellationToken);
        if (Case is null
            || await renderCaseRejection.ExecuteAsync(actor, Case, cancellationToken) is not { } rejection)
        {
            return null;
        }
        Senders = await senders.OptionsAsync(Id, cancellationToken);
        return Senders.ReplyTo is null ? rejection : null;
    }

    /// <summary>
    /// Only a field still empty is filled: To from the report's recipients
    /// (the Principal's sending rules, as the Send report dialog plans them;
    /// R1326), the subject and the body from the rejection.
    /// </summary>
    private async Task PrefillAsync(CaseRejectionEmail rejection, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(To)
            && await reportRecipients.GetAsync(Id, CaseWorkSelector.Current, cancellationToken) is { } facts)
        {
            To = string.Join(
                "; ",
                ReportDispatchPolicy.Plan(facts, clock.GetUtcNow()).To.Select(recipient => recipient.Address));
        }
        if (string.IsNullOrWhiteSpace(Subject))
        {
            Subject = rejection.Subject;
        }
        if (string.IsNullOrWhiteSpace(Body))
        {
            Body = rejection.Body;
        }
    }
}
