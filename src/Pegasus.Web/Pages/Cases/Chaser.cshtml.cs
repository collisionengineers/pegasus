using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;
using Pegasus.Infrastructure.Email;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case ribbon's Send chaser (operator, 12 October 2026): a reply to the
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

    [TempData]
    public string? SendNotice { get; set; }

    public bool StaffMailAvailable => staffMailSend is not UnavailableStaffMailSend;

    public CaseHeader? Case { get; private set; }

    public CaseMailSenderOptions Senders { get; private set; } = CaseMailSenderOptions.None;

    public StaffMailOperation? Operation { get; private set; }

    public IReadOnlyList<StaffMailAttachmentOption> AvailableAttachments { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
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
        else
        {
            await PrefillAsync(actor, cancellationToken);
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
                    ParseRecipients(Cc),
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
        return RedirectToPage(new { id = Id, operationId = Operation.Id });
    }

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
        if (string.IsNullOrWhiteSpace(To))
        {
            var recipients = await chaserRecipients.GetAsync(Id, cancellationToken)
                ?? CaseChaserRecipients.None;
            To = string.Join("; ", CaseChaserAddressing.To(recipients));
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

    private static StaffMailRecipient[] ParseRecipients(string? value) =>
        (value ?? string.Empty)
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(address => new StaffMailRecipient(address, DisplayName: null))
            .ToArray();
}
