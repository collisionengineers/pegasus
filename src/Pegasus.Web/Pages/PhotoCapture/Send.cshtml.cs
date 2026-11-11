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
/// Send a photo link (FRD-28 Photo links; R773), opened from the Case
/// ribbon's Send photo link. The Case, its registration and the Principal are
/// read-only; the customer's name, accident date and mobile start from the
/// Case; staff choose the mode, Live camera only, the lifetime and the
/// originals. <b>Send by e-mail</b> issues the link and sends it as the Send
/// chaser sends (<see cref="PhotoLinkMail"/>); <b>Create link to copy</b>
/// issues it for staff to carry. Either way the link is shown once, here,
/// because only the issuing response holds its token. The message shows the
/// standard wording with the customer's name, the registration, the
/// Principal and the expiry filled in; <c>{photo link}</c> stays until the
/// link exists. A message staff edited is sent as edited, and one they did
/// not is composed again from what the form holds when it is sent (R806).
/// An archived Case is not found; a closed one shows "The Case cannot take a
/// photo link." and no form (operator, 10 November 2026; R847).
/// <b>Send by text</b> issues it and texts the form's mobile the Photo link
/// (text) wording, edited or not, through <see cref="SendPhotoLinkText"/>
/// (R918, R919), where a text provider is configured. The form's operation
/// key issues one link: the form posted again issues, sends and texts
/// nothing more. The composer shows one channel at a time and opens as a
/// dialog over the Case (<c>?handler=Composer</c>); opened from a page it
/// returns to, a sent message or text goes back there with its notice
/// (operator, 11 November 2026; v51 L, M; R948).
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class SendModel(
    IPhotoCaptureCaseReader caseReader,
    IssuePhotoLink issuePhotoLink,
    SendPhotoLinkText sendText,
    IStaffMailSend staffMailSend,
    IGetCaseHeader getCaseHeader,
    ResolveCaseMailSender senders,
    RenderEmailTemplate renderTemplate,
    IOptions<PhotoCaptureOptions> options,
    TimeProvider timeProvider,
    Pegasus.Core.Workflow.ICaseWorkflowConfiguration workflowConfiguration,
    ILogger<SendModel> logger) : PhotoLinkComposerPageModel
{
    private readonly PhotoLinkMail _mail = new(staffMailSend, getCaseHeader, senders, renderTemplate, options);

    /// <summary>The Administrators' settings Send starts from (R813) and the lifetimes it offers (R817).</summary>
    private Pegasus.Core.Workflow.CaseWorkflowConfiguration? _settings;

    /// <summary>The lifetimes Send offers, from Administration › Configuration (R817).</summary>
    public IReadOnlyList<int> LifetimesOffered => _settings?.PhotoLinkLifetimesOffered ?? CaptureLimits.LifetimeDays;

    [BindProperty(SupportsGet = true)]
    public Guid CaseId { get; set; }

    [BindProperty, Display(Name = Labels.CustomerName), StringLength(CaptureLimits.NameLength)]
    public string? CustomerName { get; set; }

    [BindProperty, Display(Name = Labels.AccidentDate)]
    public DateOnly? AccidentDate { get; set; }

    [BindProperty, Display(Name = Labels.Email), StringLength(CaptureLimits.EmailLength)]
    public string? Email { get; set; }

    [BindProperty]
    public PhotoCaptureMode Mode { get; set; } = PhotoCaptureMode.Guided;

    /// <summary>Who a quick link is for, "Type" (R808): asked, and required, only when Quick is chosen.</summary>
    [BindProperty]
    public PhotoCaptureQuickLinkFor? QuickLinkFor { get; set; }

    [BindProperty]
    public bool LiveCameraOnly { get; set; } = true;

    [BindProperty]
    public int Days { get; set; } = CaptureLimits.DefaultLifetimeDays;

    [BindProperty]
    public bool OriginalsInBackground { get; set; } = true;

    public PhotoCaptureCaseFacts? Facts { get; private set; }

    /// <summary>Whether the link can go by e-mail here: a staff send and a mailbox that can send.</summary>
    public override bool CanEmail => _mail.Available && Senders.Mailboxes.Count > 0;

    /// <summary>Whether the link can go by text here: a text provider is configured (R918).</summary>
    public override bool CanText => sendText.Available;

    public override string ComposerTitle => Labels.SendAPhotoLink;

    public override string? EyebrowReference => Facts?.Reference;

    public override string EyebrowRegistration => Labels.Registration(Facts?.NormalizedRegistration);

    public override string? EyebrowPrincipal => Facts?.PrincipalName;

    public override string ComposerPage => "/PhotoCapture/Send";

    public override IDictionary<string, string> ComposerRoute =>
        new Dictionary<string, string>(StringComparer.Ordinal) { ["caseId"] = CaseId.ToString("D") };

    /// <summary>Cancel returns to the Case.</summary>
    protected override string DefaultBackHref => $"/Cases/{CaseId:D}";

    /// <summary>What the text provider answered for the text just sent; null when none was sent.</summary>
    public PhotoLinkTextOutcome? TextOutcome { get; private set; }

    public string? IssuedUrl { get; private set; }

    public Pegasus.Core.PhotoCapture.PhotoCapture? Issued { get; private set; }

    public StaffMailOperation? Operation { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        await StartAsync(cancellationToken) ?? Page();

    /// <summary>
    /// The composer as a dialog over the Case (v51 M; R948), fetched when Send
    /// photo link is pressed; its form posts to this page.
    /// </summary>
    public async Task<IActionResult> OnGetComposerAsync(CancellationToken cancellationToken)
    {
        InDialog = true;
        return await StartAsync(cancellationToken) ?? Partial(ComposerDialog, this);
    }

    /// <summary>The form as Send opens it; null when it is drawn, else the answer instead.</summary>
    private async Task<IActionResult?> StartAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        // A closed Case shows the refusal and no form (R847).
        if (Facts!.IsClosed)
        {
            return null;
        }

        var facts = Facts!;
        // Send starts where the Administrators' settings say (R813, R817).
        Mode = _settings!.PhotoLinkStartMode;
        LiveCameraOnly = _settings.PhotoLinkStartLiveCameraOnly;
        OriginalsInBackground = _settings.PhotoLinkStartOriginals;
        Days = _settings.PhotoLinkDefaultLifetimeDays;
        CustomerName = facts.ClaimantName;
        Mobile = facts.ClaimantContactNumber;
        AccidentDate = facts.IncidentDate;
        FromMailboxId = Senders.Default?.Mailbox.Id;
        Subject = PhotoLinkMail.Subject(facts.NormalizedRegistration);
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
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        // A closed Case issues nothing: the page answers with the refusal (R847).
        if (Facts!.IsClosed)
        {
            return Page();
        }

        var byEmail = Submit == ByEmail;
        var byText = Submit == ByText;
        if (Submit is not (ByEmail or ByText or ToCopy or StandardWording or StandardTextWording)
            || !LifetimesOffered.Contains(Days)
            || !Enum.IsDefined(Mode))
        {
            return BadRequest();
        }

        SetAsideOtherChannels();

        // Put the standard wording back, without script: the page returns with
        // the standard wording for what the form holds now.
        if (Submit is StandardWording or StandardTextWording)
        {
            var email = Submit == StandardWording;
            if (email ? !CanEmail : !CanText)
            {
                return BadRequest();
            }

            try
            {
                await DrawStandardWordingAsync(actor, email, !email, cancellationToken);
            }
            catch (StaffAuthorizationException)
            {
                return Forbid();
            }

            return Page();
        }

        // A Type that is not one of the page's own is not the page's post; a
        // Quick link sent without one is refused beside the Type (operator,
        // 10 November 2026; R868), as the browser also asks for it first.
        if ((QuickLinkFor is { } posted && !Enum.IsDefined(posted))
            || ModelState.GetFieldValidationState(nameof(QuickLinkFor)) == Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
        {
            return BadRequest();
        }

        if (Mode == PhotoCaptureMode.Quick && QuickLinkFor is null)
        {
            ModelState.AddModelError(nameof(QuickLinkFor), Labels.ChooseAQuickLinkType);
        }

        var edited = BodyEdited;
        var textEdited = TextBodyEdited;

        if (byText)
        {
            if (!CanText)
            {
                return BadRequest();
            }

            if (UkMobile.ToE164(Mobile) is null)
            {
                ModelState.AddModelError(nameof(Mobile), SendPhotoLinkText.NotAUkMobile);
            }
            if (string.IsNullOrWhiteSpace(TextBody))
            {
                ModelState.AddModelError(nameof(TextBody), SendPhotoLinkText.MessageRequired);
            }
        }

        if (byEmail)
        {
            if (!CanEmail)
            {
                return BadRequest();
            }

            if (PhotoCaptureText.Email(Email) is null)
            {
                ModelState.AddModelError(nameof(Email), Labels.NeededToSendByEmail);
            }
            if (Senders.Sender(FromMailboxId) is null)
            {
                ModelState.AddModelError(nameof(FromMailboxId), new CaseMailSenderRequiredException().Message);
            }
            if (string.IsNullOrWhiteSpace(Subject))
            {
                ModelState.AddModelError(nameof(Subject), "A subject is required.");
            }
            if (string.IsNullOrWhiteSpace(Body))
            {
                ModelState.AddModelError(nameof(Body), "A message is required.");
            }
        }

        if (!ModelState.IsValid)
        {
            if ((CanEmail && !edited) || (CanText && !textEdited))
            {
                try
                {
                    // An unedited message follows the name and lifetime the form holds now.
                    await DrawStandardWordingAsync(actor, !edited, !textEdited, cancellationToken);
                }
                catch (StaffAuthorizationException)
                {
                    return Forbid();
                }
            }

            return Page();
        }

        IssuedPhotoLink issued;
        try
        {
            issued = await issuePhotoLink.ExecuteAsync(
                new(
                    CaseId,
                    actor,
                    CustomerName,
                    Mobile,
                    Email,
                    AccidentDate,
                    Mode,
                    Mode == PhotoCaptureMode.Quick ? QuickLinkFor : null,
                    LiveCameraOnly,
                    OriginalsInBackground,
                    Days,
                    byEmail ? PhotoLinkTokenPurpose.Email
                        : byText ? PhotoLinkTokenPurpose.Text
                        : PhotoLinkTokenPurpose.Copy,
                    OperationKey),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (PhotoCaptureRefusalException refusal)
        {
            ModelState.AddModelError(string.Empty, refusal.Message);
            return Page();
        }

        // The form posted again (a refresh, or Back and send) made nothing:
        // its link was shown to the first post only. A text is refused as a
        // repeated text post is (FRD-28 The photo link text); otherwise the
        // link's own page shows the link as it is.
        if (issued.Replayed)
        {
            if (byText)
            {
                ModelState.AddModelError(string.Empty, SendPhotoLinkText.AlreadySent);
                return Page();
            }

            return RedirectToPage("/PhotoCapture/Details", new { id = issued.Capture.Id });
        }

        Issued = issued.Capture;
        IssuedUrl = _mail.CaptureUrl(issued.Token);
        if (byText)
        {
            var capture = issued.Capture;
            var values = PhotoLinkMail.Values(
                capture.NormalizedRegistration,
                capture.CustomerName,
                Facts!.PrincipalName,
                IssuedUrl,
                capture.ExpiresAtUtc);
            try
            {
                // An edited text is sent as staff wrote it; an unedited one is
                // the standard wording for the link as issued.
                var message = textEdited
                    ? PhotoLinkMail.Complete(TextBody!, values, IssuedUrl)
                    : PhotoLinkMail.Complete(await _mail.RenderTextAsync(actor, values, cancellationToken), values, IssuedUrl);
                TextOutcome = await sendText.ExecuteAsync(actor, capture.Id, Mobile, message, OperationKey, cancellationToken);
                if (TextOutcome.Accepted && HasReturnUrl)
                {
                    // Opened from a page (the Case): back there with the notice (v51 M; R948).
                    LeaveSentNotice(Labels.LinkTextedTo(capture.CustomerName));
                    return LocalRedirect(BackHref);
                }
            }
            catch (StaffAuthorizationException)
            {
                return Forbid();
            }
            catch (PhotoCaptureRefusalException refusal)
            {
                ModelState.AddModelError(string.Empty, refusal.Message);
            }
        }

        if (byEmail)
        {
            var capture = issued.Capture;
            var values = PhotoLinkMail.Values(
                capture.NormalizedRegistration,
                capture.CustomerName,
                Facts!.PrincipalName,
                IssuedUrl,
                capture.ExpiresAtUtc);
            try
            {
                // An edited message is sent as staff wrote it; an unedited one
                // is the standard wording for the link as issued.
                var message = edited
                    ? PhotoLinkMail.Complete(Body!, values, IssuedUrl)
                    : PhotoLinkMail.Complete(await _mail.RenderAsync(actor, values, cancellationToken), values, IssuedUrl);
                Operation = await _mail.SendAsync(
                    actor,
                    CaseId,
                    FromMailboxId,
                    capture.Email!,
                    Subject!,
                    message,
                    OperationKey,
                    cancellationToken);
                if (Operation.State == StaffMailState.Sent && HasReturnUrl)
                {
                    // Opened from a page (the Case): back there with the notice (v51 M; R948).
                    LeaveSentNotice(Labels.LinkSentTo(capture.CustomerName));
                    return LocalRedirect(BackHref);
                }

                if (Operation.State == StaffMailState.Failed)
                {
                    Pegasus.Web.Pages.Mail.StaffMailSendLog.Refused(logger, Operation.Id, Operation.ApprovedMailboxId, Operation.FailureCode);
                }
            }
            catch (StaffAuthorizationException)
            {
                return Forbid();
            }
            catch (CaseMailSenderRequiredException exception)
            {
                // The link exists; staff copy it instead.
                ModelState.AddModelError(string.Empty, exception.Message);
            }
            catch (PhotoCaptureRefusalException refusal)
            {
                ModelState.AddModelError(string.Empty, refusal.Message);
            }
        }

        return Page();
    }

    /// <summary>
    /// The message box and the standard wording it is compared with: the
    /// Photo link template with the customer's name, the registration, the
    /// Principal and the expiry the form's lifetime gives filled in, and
    /// <c>{photo link}</c> kept until the link exists (R806).
    /// </summary>
    private async Task DrawStandardWordingAsync(ActionActor actor, bool email, bool text, CancellationToken cancellationToken)
    {
        var values = PhotoLinkMail.Values(
            Facts!.NormalizedRegistration,
            CustomerName,
            Facts.PrincipalName,
            Labels.PhotoLinkPlaceholder,
            timeProvider.GetUtcNow().AddDays(Days));
        // The drawn wording replaces what was posted.
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

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Facts = await caseReader.GetAsync(CaseId, cancellationToken);
        if (Facts is null || Facts.IsArchived)
        {
            return false;
        }

        var senderOptions = _mail.SendersAsync(CaseId, cancellationToken);
        var settings = workflowConfiguration.GetCurrentAsync(cancellationToken);
        await Task.WhenAll(senderOptions, settings);
        Senders = senderOptions.Result;
        _settings = settings.Result;
        return true;
    }
}
