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
/// Send a photo link (FRD-28 Photo links; R770), opened from the Case
/// ribbon's Send photo link. The Case, its registration and the Principal are
/// read-only; the customer's name, accident date and mobile start from the
/// Case; staff choose the mode, Live camera only, the lifetime and the
/// originals. <b>Send by e-mail</b> issues the link and sends it as the Send
/// chaser sends (<see cref="PhotoLinkMail"/>); <b>Create link to copy</b>
/// issues it for staff to carry. Either way the link is shown once, here,
/// because only the issuing response holds its token.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class SendModel(
    IPhotoCaptureCaseReader caseReader,
    IssuePhotoLink issuePhotoLink,
    IStaffMailSend staffMailSend,
    IGetCaseHeader getCaseHeader,
    ResolveCaseMailSender senders,
    RenderEmailTemplate renderTemplate,
    IOptions<PhotoCaptureOptions> options,
    ILogger<SendModel> logger) : PhotoCapturePageModel
{
    public const string ByEmail = "email";
    public const string ToCopy = "copy";

    private readonly PhotoLinkMail _mail = new(staffMailSend, getCaseHeader, senders, renderTemplate, options);

    [BindProperty(SupportsGet = true)]
    public Guid CaseId { get; set; }

    [BindProperty, Display(Name = Labels.CustomerName), StringLength(CaptureLimits.NameLength)]
    public string? CustomerName { get; set; }

    [BindProperty, Display(Name = Labels.AccidentDate)]
    public DateOnly? AccidentDate { get; set; }

    [BindProperty, Display(Name = Labels.Mobile), StringLength(CaptureLimits.MobileLength)]
    public string? Mobile { get; set; }

    [BindProperty, Display(Name = Labels.Email), StringLength(CaptureLimits.EmailLength)]
    public string? Email { get; set; }

    [BindProperty]
    public PhotoCaptureMode Mode { get; set; } = PhotoCaptureMode.Guided;

    [BindProperty]
    public bool LiveCameraOnly { get; set; } = true;

    [BindProperty]
    public int Days { get; set; } = CaptureLimits.DefaultLifetimeDays;

    [BindProperty]
    public bool OriginalsInBackground { get; set; } = true;

    [BindProperty]
    public Guid? FromMailboxId { get; set; }

    [BindProperty, Display(Name = Labels.Subject), StringLength(998)]
    public string? Subject { get; set; }

    [BindProperty, Display(Name = Labels.Message), StringLength(EmailTemplates.MaximumBodyLength)]
    public string? Body { get; set; }

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    /// <summary>Which button sent the form: <see cref="ByEmail"/> or <see cref="ToCopy"/>.</summary>
    [BindProperty]
    public string? Submit { get; set; }

    public PhotoCaptureCaseFacts? Facts { get; private set; }

    public CaseMailSenderOptions Senders { get; private set; } = CaseMailSenderOptions.None;

    /// <summary>Whether the link can go by e-mail here: a staff send and a mailbox that can send.</summary>
    public bool CanEmail => _mail.Available && Senders.Mailboxes.Count > 0;

    public string? IssuedUrl { get; private set; }

    public Pegasus.Core.PhotoCapture.PhotoCapture? Issued { get; private set; }

    public StaffMailOperation? Operation { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        var facts = Facts!;
        CustomerName = facts.ClaimantName;
        Mobile = facts.ClaimantContactNumber;
        AccidentDate = facts.IncidentDate;
        FromMailboxId = Senders.Default?.Mailbox.Id;
        Subject = PhotoLinkMail.Subject(facts.NormalizedRegistration);
        try
        {
            // The values staff may still change, and the link itself, stay
            // as their placeholders until the link is issued.
            var values = PhotoLinkMail.Values(facts.NormalizedRegistration, null, facts.PrincipalName, null, null);
            values[Pegasus.Core.Operations.EmailTemplates.Claimant] = "{" + Pegasus.Core.Operations.EmailTemplates.Claimant + "}";
            values[Pegasus.Core.Operations.EmailTemplates.PhotoLink] = Labels.PhotoLinkPlaceholder;
            values[Pegasus.Core.Operations.EmailTemplates.LinkExpiry] = "{" + Pegasus.Core.Operations.EmailTemplates.LinkExpiry + "}";
            Body = await _mail.RenderAsync(actor, values, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        return Page();
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

        var byEmail = Submit == ByEmail;
        if (Submit is not (ByEmail or ToCopy)
            || !CaptureLimits.IsLifetime(Days)
            || !Enum.IsDefined(Mode))
        {
            return BadRequest();
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
                    LiveCameraOnly,
                    OriginalsInBackground,
                    Days,
                    byEmail ? PhotoLinkTokenPurpose.Email : PhotoLinkTokenPurpose.Copy),
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

        Issued = issued.Capture;
        IssuedUrl = _mail.CaptureUrl(issued.Token);
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
                Operation = await _mail.SendAsync(
                    actor,
                    CaseId,
                    FromMailboxId,
                    capture.Email!,
                    Subject!,
                    PhotoLinkMail.Complete(Body!, values, IssuedUrl),
                    OperationKey,
                    cancellationToken);
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

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Facts = await caseReader.GetAsync(CaseId, cancellationToken);
        if (Facts is null || Facts.IsArchived)
        {
            return false;
        }

        Senders = await _mail.SendersAsync(CaseId, cancellationToken);
        return true;
    }
}
