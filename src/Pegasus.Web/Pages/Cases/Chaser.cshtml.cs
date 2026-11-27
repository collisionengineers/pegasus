using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;
using Pegasus.Infrastructure.Email;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case ribbon's Send chaser (operator, 12 October 2026), in the one
/// send-e-mail dialogue over the Case (R1330; <c>?handler=Dialog</c>), and as
/// this page without script or after a refused send: a reply to the
/// Case's instruction email; with no instruction email to reply to, a new
/// message. It opens addressed, titled and worded from the Case, its From on
/// the default sender (<see cref="ResolveCaseMailSender"/>: the mailbox the
/// instruction arrived in, else the Principal's Send from; R654, R655); staff
/// change any of it before sending.
/// </summary>
public sealed class ChaserModel(
    IStaffMailSend staffMailSend,
    IGetCaseHeader getCaseHeader,
    ResolveCaseMailSender senders,
    IStaffMailAttachmentResolver attachmentResolver,
    RenderEmailTemplate renderTemplate,
    ICaseChaserRecipientQueries chaserRecipients,
    ILogger<ChaserModel> logger) : StaffPageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public long ExpectedContextVersion { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? OperationId { get; set; }

    /// <summary>The chaser was sent from the dialogue over the Case, so the send lands back there.</summary>
    [BindProperty]
    public bool Dialog { get; set; }

    public bool InDialog { get; private set; }

    [BindProperty]
    public Guid? FromMailboxId { get; set; }

    /// <summary>One box per address (R1329).</summary>
    [BindProperty]
    public List<string> To { get; set; } = [];

    [BindProperty]
    public List<string> Cc { get; set; } = [];

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

    public bool StaffMailAvailable => staffMailSend is not UnavailableStaffMailSend;

    public CaseHeader? Case { get; private set; }

    public CaseMailSenderOptions Senders { get; private set; } = CaseMailSenderOptions.None;

    public StaffMailOperation? Operation { get; private set; }

    public IReadOnlyList<StaffMailAttachmentOption> AvailableAttachments { get; private set; } = [];

    /// <summary>
    /// A send still in flight or of unknown outcome is never sent again from
    /// here, and one that was sent has nothing left to send.
    /// </summary>
    public bool OffersForm => StaffMailAvailable && Senders.Mailboxes.Count > 0
        && Operation?.State is null or StaffMailState.Failed;

    public Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        ShowAsync(inDialog: false, cancellationToken);

    /// <summary>The same composer in the dialogue over the Case, fetched on open.</summary>
    public Task<IActionResult> OnGetDialogAsync(CancellationToken cancellationToken) =>
        ShowAsync(inDialog: true, cancellationToken);

    private async Task<IActionResult> ShowAsync(bool inDialog, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!await LoadAsync(actor, cancellationToken))
        {
            return NotFound();
        }

        ExpectedContextVersion = Case!.Workflow.Version;
        // From starts on the default sender.
        FromMailboxId ??= Senders.Default?.Mailbox.Id;
        AvailableAttachments = await attachmentResolver.ListCaseAsync(actor, Id, cancellationToken);
        if (OperationId is { } operationId)
        {
            Operation = await staffMailSend.GetAsync(actor, operationId, cancellationToken);
        }
        if (OffersForm)
        {
            await PrefillAsync(actor, cancellationToken);
        }

        InDialog = inDialog;
        if (inDialog)
        {
            return Partial("Shared/_ChaserComposer", this);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostSendAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!StaffMailAvailable || !await LoadAsync(actor, cancellationToken))
        {
            return NotFound();
        }

        // Each box is read as typed, so an address that is not one is refused, not dropped.
        var to = StaffMailReplyRecipients.Parse(string.Join("; ", To));
        var cc = StaffMailReplyRecipients.Parse(string.Join("; ", Cc));
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
            StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
            Operation = await staffMailSend.SendAsync(
                new(
                    actor,
                    sender.Mailbox.Id,
                    sender.Mailbox.Generation,
                    StaffMailPurpose.CaseChaser,
                    Id,
                    ExpectedContextVersion,
                    sender.Mode,
                    sender.ReplyTo?.Original,
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
        catch (StaffMailOutcomeUnknownException exception)
        {
            // Recorded Unknown: staff stay on the composer with the sentence
            // and its Send status (R1069, R1123). The form keeps this
            // operation key, so sending it again replays the Unknown send.
            Pegasus.Web.Pages.Mail.StaffMailSendLog.OutcomeUnknown(
                logger, exception.Operation.Id, sender.Mailbox.Id, exception);
            Operation = exception.Operation;
            ModelState.AddModelError(string.Empty, CaseWorkspaceLabels.ReportDelivery.SendUnknown);
            return Page();
        }

        // Only a terminal Sent outcome is a success notice. Anything else —
        // Submitted (still in flight) or Unknown (ambiguous, never resend) —
        // must be read off the Send-status panel the redirected GET renders,
        // never announced as if it had reached the provider.
        if (Operation.State == StaffMailState.Sent)
        {
            SendNotice = "Correspondence sent.";
        }
        else if (Operation.State == StaffMailState.Failed)
        {
            Pegasus.Web.Pages.Mail.StaffMailSendLog.Refused(logger, Operation.Id, sender.Mailbox.Id, Operation.FailureCode);
        }
        if (Dialog)
        {
            // Back on the Case, where the dialogue reopens on the send's status.
            return RedirectToPage(
                "/Cases/Details",
                new { id = Id, compose = ComposeLanding, mailOperationId = Operation.Id });
        }
        return RedirectToPage(new { id = Id, operationId = Operation.Id });
    }

    /// <summary>The landing value that reopens this composer over the Case (site.js).</summary>
    public const string ComposeLanding = "chaser";

    private async Task<bool> LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        Case = await getCaseHeader.ExecuteAsync(new(Id, actor), cancellationToken);
        if (Case is null)
        {
            return false;
        }

        if (StaffMailAvailable)
        {
            Senders = await senders.OptionsAsync(Id, cancellationToken);
        }
        return true;
    }

    /// <summary>
    /// Only a field still empty is filled. A reply keeps the instruction
    /// email's subject with one "Re:"; a new chaser opens with the
    /// registration and claimant.
    /// </summary>
    private async Task PrefillAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        var details = Case!;
        var facts = new CaseChaserFacts(
            details.Summary.Registration,
            details.Workflow.DueWork?.MissingMaterialReason,
            details.Summary.Principal,
            details.Summary.Claimant);
        if (To.All(string.IsNullOrWhiteSpace))
        {
            var recipients = await chaserRecipients.GetAsync(Id, cancellationToken)
                ?? CaseChaserRecipients.None;
            To = [.. CaseChaserAddressing.To(recipients)];
        }
        if (string.IsNullOrWhiteSpace(Subject))
        {
            Subject = Senders.ReplySubject ?? facts.Subject();
        }
        if (string.IsNullOrWhiteSpace(Body))
        {
            Body = await renderTemplate.ExecuteAsync(
                actor, EmailTemplatePurpose.CaseChaser, facts.Values(), cancellationToken);
        }
    }
}
