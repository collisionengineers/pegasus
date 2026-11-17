using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.PhotoCapture;
using Pegasus.Web.PhotoCapture;
using Labels = Pegasus.Web.Presentation.OperatorLabels.PhotoCapture;

namespace Pegasus.Web.Pages.PhotoCapture;

/// <summary>
/// The composer every photo-link e-mail goes through: Resend from Links we
/// sent, the link's page or the Case, and Ask for it on a Coming in capture
/// on a Case with an address. No button sends an e-mail on its own (operator,
/// 10 November 2026: "any email sending always needs a dialogue box - a
/// button cannot just send an email on its own"; R915). It is the Send
/// chaser's composer: From, To (the link's address), Subject and Message in
/// the standard Photo link wording with <c>{photo link}</c> kept until the
/// link exists, "Edited for this one only." and Put the standard wording back
/// (R806); <b>Send by e-mail</b> or <b>Create link to copy</b>. Core's
/// <see cref="PhotoLinkActions.ResendAsync"/> makes the link and counts the
/// reminder (R914, R842). A sent message returns to where it was asked from
/// with "Link sent to … by e-mail"; a link to copy, or a message whose sending
/// is not yet settled, is shown here, once; the form posted again makes,
/// sends and texts nothing more. Where a text provider is
/// configured the composer also offers <b>Send by text</b> to the mobile in
/// To (the link's own, which staff may change) with the Photo link (text)
/// wording (R918, R919); a text counts as a reminder as an e-mail does.
/// The composer shows one channel at a time and opens as a dialog over the
/// page it was asked from (<c>?handler=Composer</c>; operator, 11 November
/// 2026; v51 L, M; R948).
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class ResendModel(
    IPhotoCaptureQueries queries,
    PhotoLinkActions actions,
    SendPhotoLinkText sendText,
    IPhotoCaptureCaseReader caseReader,
    IStaffMailSend staffMailSend,
    IGetCaseHeader getCaseHeader,
    ResolveCaseMailSender senders,
    RenderEmailTemplate renderTemplate,
    IOptions<PhotoCaptureOptions> options,
    ILogger<ResendModel> logger) : PhotoLinkComposerPageModel
{
    private readonly PhotoLinkMail _mail = new(staffMailSend, getCaseHeader, senders, renderTemplate, options);
    private string? _principalName;
    private string? _caseReference;

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public PhotoCaptureListRow Row { get; private set; } = null!;

    public Pegasus.Core.PhotoCapture.PhotoCapture Capture => Row.Capture;

    /// <summary>The composer's title: the act it was opened as (v50 R).</summary>
    public string Title => Labels.Resend;

    /// <summary>Whether the link can go by e-mail: a staff send, a sending mailbox, a Case and an address.</summary>
    public override bool CanEmail => _mail.Available && Senders.Mailboxes.Count > 0
        && Capture.CaseId is not null && !string.IsNullOrWhiteSpace(Capture.Email);

    /// <summary>Whether the link can go by text: a text provider is configured (R918).</summary>
    public override bool CanText => sendText.Available;

    public override string ComposerTitle => Title;

    public override string? EyebrowReference => _caseReference;

    public override string EyebrowRegistration => Labels.Registration(Capture.NormalizedRegistration);

    public override string? EyebrowPrincipal => _principalName;

    public override string ComposerPage => "/PhotoCapture/Resend";

    public override IDictionary<string, string> ComposerRoute =>
        new Dictionary<string, string>(StringComparer.Ordinal) { ["id"] = Id.ToString("D") };

    /// <summary>Cancel and a sent message return to the link's page when no page asked.</summary>
    protected override string DefaultBackHref => $"/PhotoCapture/{Id:D}";

    /// <summary>What the text provider answered when it did not accept the text just sent.</summary>
    public PhotoLinkTextOutcome? TextOutcome { get; private set; }

    /// <summary>The link just made, shown once to copy; null otherwise.</summary>
    public string? IssuedUrl { get; private set; }

    /// <summary>The e-mail just sent when its sending is not settled, read off as the Send chaser reads its own.</summary>
    public StaffMailOperation? Operation { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        await StartAsync(cancellationToken) ?? Page();

    /// <summary>
    /// The composer as a dialog over the page Resend was pressed on (v51 M;
    /// R948); its form posts to this page.
    /// </summary>
    public async Task<IActionResult> OnGetComposerAsync(CancellationToken cancellationToken)
    {
        InDialog = true;
        return await StartAsync(cancellationToken) ?? Partial(ComposerDialog, this);
    }

    /// <summary>The composer as it opens; null when it is drawn, else the answer instead.</summary>
    private async Task<IActionResult?> StartAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!await LoadAsync(cancellationToken)) return NotFound();
        if (CanEmail)
        {
            FromMailboxId = Senders.Default?.Mailbox.Id;
            Subject = PhotoLinkMail.Subject(Capture.NormalizedRegistration);
        }

        Mobile = Capture.Mobile;
        try
        {
            await DrawStandardWordingAsync(actor, email: true, text: true, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        return null;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!await LoadAsync(cancellationToken)) return NotFound();
        if (Submit is not (ByEmail or ByText or ToCopy or StandardWording or StandardTextWording)) return BadRequest();

        var byEmail = Submit == ByEmail;
        var byText = Submit == ByText;
        if ((Submit is ByEmail or StandardWording) && !CanEmail) return BadRequest();
        if ((Submit is ByText or StandardTextWording) && !CanText) return BadRequest();
        SetAsideOtherChannels();
        try
        {
            // Put the standard wording back, without script.
            if (Submit is StandardWording or StandardTextWording)
            {
                var email = Submit == StandardWording;
                await DrawStandardWordingAsync(actor, email, !email, cancellationToken);
                return Page();
            }

            var edited = BodyEdited;
            var textEdited = TextBodyEdited;
            if (byText)
            {
                if (UkMobile.ToE164(Mobile) is null)
                    ModelState.AddModelError(nameof(Mobile), SendPhotoLinkText.NotAUkMobile);
                if (string.IsNullOrWhiteSpace(TextBody))
                    ModelState.AddModelError(nameof(TextBody), SendPhotoLinkText.MessageRequired);
                if (!ModelState.IsValid)
                {
                    await DrawStandardWordingAsync(actor, !edited, !textEdited, cancellationToken);
                    return Page();
                }
            }

            if (byEmail)
            {
                if (Senders.Sender(FromMailboxId) is null)
                    ModelState.AddModelError(nameof(FromMailboxId), new CaseMailSenderRequiredException().Message);
                if (string.IsNullOrWhiteSpace(Subject))
                    ModelState.AddModelError(nameof(Subject), "A subject is required.");
                if (string.IsNullOrWhiteSpace(Body))
                    ModelState.AddModelError(nameof(Body), "A message is required.");
                if (!ModelState.IsValid)
                {
                    await DrawStandardWordingAsync(actor, !edited, !textEdited, cancellationToken);
                    return Page();
                }
            }

            IssuedPhotoLink issued;
            try
            {
                issued = await actions.ResendAsync(
                    actor,
                    Id,
                    byEmail ? PhotoLinkTokenPurpose.Email
                        : byText ? PhotoLinkTokenPurpose.Text
                        : PhotoLinkTokenPurpose.Copy,
                    OperationKey,
                    cancellationToken);
            }
            catch (PhotoCaptureRefusalException refusal)
            {
                ModelState.AddModelError(string.Empty, refusal.Message);
                return Page();
            }

            // The form posted again (a refresh, or Back and send) made
            // nothing: its link was shown to the first post only. A text is
            // refused as a repeated text post is (FRD-28 The photo link
            // text); otherwise it returns to where it was asked from.
            if (issued.Replayed)
            {
                if (byText)
                {
                    ModelState.AddModelError(string.Empty, SendPhotoLinkText.AlreadySent);
                    return Page();
                }

                return LocalRedirect(BackHref);
            }

            IssuedUrl = _mail.CaptureUrl(issued.Token);
            var capture = issued.Capture;
            var values = PhotoLinkMail.Values(capture.NormalizedRegistration, capture.CustomerName, _principalName, IssuedUrl, capture.ExpiresAtUtc);
            if (byText)
            {
                // An edited text is sent as staff wrote it; an unedited one is
                // the standard wording for the link as made.
                var text = await _mail.ComposeTextAsync(actor, textEdited ? TextBody : null, values, IssuedUrl, cancellationToken);
                try
                {
                    TextOutcome = await sendText.ExecuteAsync(actor, capture.Id, Mobile, text, OperationKey, cancellationToken);
                }
                catch (PhotoCaptureRefusalException refusal)
                {
                    ModelState.AddModelError(string.Empty, refusal.Message);
                    return Page();
                }

                if (TextOutcome.Accepted)
                {
                    LeaveSentNotice(Labels.LinkTextedTo(capture.CustomerName));
                    return LocalRedirect(BackHref);
                }

                return Page();
            }

            if (!byEmail) return Page();

            try
            {
                // An edited message is sent as staff wrote it; an unedited one is
                // the standard wording for the link as made.
                var message = await _mail.ComposeAsync(actor, edited ? Body : null, values, IssuedUrl, cancellationToken);
                Operation = await _mail.SendAsync(
                    actor,
                    capture.CaseId!.Value,
                    FromMailboxId,
                    capture.Email!,
                    Subject!,
                    message,
                    OperationKey,
                    cancellationToken);
            }
            catch (StaffMailOutcomeUnknownException exception)
            {
                // Recorded Unknown: the link stands, with the sentence and its
                // Send status (R1069, R1123).
                Pegasus.Web.Pages.Mail.StaffMailSendLog.OutcomeUnknown(
                    logger, exception.Operation.Id, exception.Operation.ApprovedMailboxId, exception);
                Operation = exception.Operation;
                ModelState.AddModelError(
                    string.Empty, Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendUnknown);
                return Page();
            }
            catch (CaseMailSenderRequiredException exception)
            {
                // The link exists; staff copy it instead.
                ModelState.AddModelError(string.Empty, exception.Message);
                return Page();
            }
            catch (PhotoCaptureRefusalException refusal)
            {
                ModelState.AddModelError(string.Empty, refusal.Message);
                return Page();
            }

            if (Operation.State == StaffMailState.Sent)
            {
                LeaveSentNotice(Labels.LinkSentTo(capture.CustomerName));
                return LocalRedirect(BackHref);
            }

            if (Operation.State == StaffMailState.Failed)
            {
                Pegasus.Web.Pages.Mail.StaffMailSendLog.Refused(logger, Operation.Id, Operation.ApprovedMailboxId, Operation.FailureCode);
            }

            return Page();
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// The message box and the standard wording it is compared with: the Photo
    /// link template with the customer's name, the registration, the Principal
    /// and the link's expiry filled in, and <c>{photo link}</c> kept until the
    /// link exists (R806).
    /// </summary>
    private async Task DrawStandardWordingAsync(ActionActor actor, bool email, bool text, CancellationToken cancellationToken)
    {
        var values = PhotoLinkMail.Values(
            Capture.NormalizedRegistration,
            Capture.CustomerName,
            _principalName,
            Labels.PhotoLinkPlaceholder,
            Capture.ExpiresAtUtc);
        if (email && CanEmail)
        {
            Body = StandardBody = await _mail.RenderAsync(actor, values, cancellationToken);
            ModelState.Remove(nameof(Body));
            ModelState.Remove(nameof(StandardBody));
        }

        if (text && CanText)
        {
            TextBody = StandardTextBody = await _mail.RenderTextAsync(actor, values, cancellationToken);
            ModelState.Remove(nameof(TextBody));
            ModelState.Remove(nameof(StandardTextBody));
        }
    }

    /// <summary>Resend's mobile is the text's alone, so it rides along unchecked on any other post.</summary>
    protected override void SetAsideOtherChannels()
    {
        base.SetAsideOtherChannels();
        if (Submit is not (ByText or StandardTextWording))
        {
            ModelState.Remove(nameof(Mobile));
        }
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        // Only a link sent from a Case is sent again: a standing or website
        // capture has no composer (v51 Q; R948).
        if (await queries.GetRowAsync(Id, cancellationToken) is not { } row
            || row.Capture.Origin != PhotoCaptureOrigin.CaseLink)
        {
            return false;
        }

        Row = row;
        if (row.Capture.CaseId is { } caseId)
        {
            // {principal name} is the Case's Principal, whichever link the capture came from.
            var facts = caseReader.GetAsync(caseId, cancellationToken);
            var senderOptions = _mail.SendersAsync(caseId, cancellationToken);
            await Task.WhenAll(facts, senderOptions);
            _principalName = facts.Result?.PrincipalName;
            _caseReference = facts.Result?.Reference;
            Senders = senderOptions.Result;
        }

        return true;
    }
}
