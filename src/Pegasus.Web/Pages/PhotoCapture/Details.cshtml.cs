using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.PhotoCapture;
using Pegasus.Web.PhotoCapture;
using Pegasus.Web.Presentation;
using CaptureRecord = Pegasus.Core.PhotoCapture.PhotoCapture;
using Labels = Pegasus.Web.Presentation.OperatorLabels.PhotoCapture;

namespace Pegasus.Web.Pages.PhotoCapture;

/// <summary>
/// One photo link (FRD-28 Staff pages): its facts, what the sender told us,
/// the note, the status and what is missing, the items in story order and the
/// history, with Resend, Remind, Log call, Extend and Cancel. Resend and
/// Remind mint a fresh token (<see cref="PhotoLinkActions.ReissueAsync"/>):
/// it is e-mailed when the link has an address on a Case, and otherwise shown
/// once here to copy, since only the issuing response ever holds it.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class DetailsModel(
    IPhotoCaptureQueries queries,
    IPhotoCaptureStore captures,
    PhotoLinkActions actions,
    IStaffMailSend staffMailSend,
    IGetCaseHeader getCaseHeader,
    ResolveCaseMailSender senders,
    RenderEmailTemplate renderTemplate,
    IOptions<PhotoCaptureOptions> options,
    TimeProvider timeProvider,
    ILogger<DetailsModel> logger) : PhotoCapturePageModel
{
    private readonly PhotoLinkMail _mail = new(staffMailSend, getCaseHeader, senders, renderTemplate, options);

    /// <summary>One line of the link's history, from the record's own times.</summary>
    public sealed record HistoryLine(string Label, DateTimeOffset AtUtc, string? Detail = null);

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    [BindProperty]
    public int Days { get; set; } = CaptureLimits.DefaultLifetimeDays;

    [TempData]
    public string? Notice { get; set; }

    public PhotoCaptureListRow Row { get; private set; } = null!;

    public CaptureRecord Capture => Row.Capture;

    public IReadOnlyList<PhotoCaptureStoryGroup> Story { get; private set; } = [];

    public PhotoCaptureState State { get; private set; }

    public DueCaptureReminder? Due { get; private set; }

    public DateTimeOffset Now { get; private set; }

    /// <summary>The link a Resend or Remind just minted, shown once to copy; null otherwise.</summary>
    public string? IssuedUrl { get; private set; }

    /// <summary>The e-mail a Resend or Remind just sent, read off as the Send chaser reads its own.</summary>
    public StaffMailOperation? Operation { get; private set; }

    public bool IsLive => PhotoCaptureAccess.IsLive(Capture, Now, Row.CaseArchived);

    public bool IsCaseLink => Capture.Origin == PhotoCaptureOrigin.CaseLink;

    /// <summary>Resend and Remind: a live link HQ sent from a Case.</summary>
    public bool OffersResend => IsCaseLink && IsLive;

    public bool OffersLogCall => IsCaseLink && !Capture.IsRevoked;

    /// <summary>A link whose Case is archived is dead and is not extended (R793).</summary>
    public bool OffersExtend => !Capture.IsRevoked && !Row.CaseArchived;

    public bool OffersCancel => IsLive;

    /// <summary>Where the photos are: the Case's Files section, else the Vehicle images record.</summary>
    public string? PhotosHref => IndexModel.PhotosHref(Row);

    public IReadOnlyList<HistoryLine> History
    {
        get
        {
            var capture = Capture;
            var lines = new List<HistoryLine> { new(Labels.Created, capture.CreatedAtUtc, Row.IssuedByName) };
            void Add(string label, DateTimeOffset? at, string? detail = null)
            {
                if (at is { } value)
                {
                    lines.Add(new(label, value, detail));
                }
            }

            Add(Labels.Opened, capture.OpenedAtUtc);
            Add(Labels.FirstPhoto, capture.FirstItemAtUtc);
            if (capture.LastItemAtUtc != capture.FirstItemAtUtc)
            {
                Add(Labels.LastPhoto, capture.LastItemAtUtc);
            }
            Add(Labels.Sent, capture.SentAtUtc);
            Add(
                Labels.Reminded,
                capture.LastRemindedAtUtc,
                capture.ReminderCount > 1 ? capture.ReminderCount.ToString(System.Globalization.CultureInfo.InvariantCulture) : null);
            Add(Labels.CallLoggedAt, capture.CallLoggedAtUtc);
            Add(Labels.Filed, capture.SettledAtUtc);
            Add(Labels.Cancelled, capture.RevokedAtUtc);
            return [.. lines.OrderBy(line => line.AtUtc)];
        }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out _))
        {
            return Forbid();
        }

        return await LoadAsync(cancellationToken) ? Page() : NotFound();
    }

    public Task<IActionResult> OnPostResendAsync(CancellationToken cancellationToken) =>
        ReissueAsync(isReminder: false, cancellationToken);

    public Task<IActionResult> OnPostRemindAsync(CancellationToken cancellationToken) =>
        ReissueAsync(isReminder: true, cancellationToken);

    public Task<IActionResult> OnPostLogCallAsync(CancellationToken cancellationToken) =>
        ActAsync((actor, token) => actions.LogCallAsync(actor, Id, token), Labels.CallLogged, cancellationToken);

    public Task<IActionResult> OnPostExtendAsync(CancellationToken cancellationToken) =>
        ActAsync((actor, token) => actions.ExtendAsync(actor, Id, Days, token), Labels.LinkExtended, cancellationToken);

    public Task<IActionResult> OnPostCancelAsync(CancellationToken cancellationToken) =>
        ActAsync((actor, token) => actions.RevokeAsync(actor, Id, token), Labels.LinkCancelled, cancellationToken);

    private async Task<IActionResult> ActAsync(
        Func<ActionActor, CancellationToken, Task<CaptureRecord>> act,
        string notice,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await act(actor, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (PhotoCaptureRefusalException refusal)
        {
            ModelState.AddModelError(string.Empty, refusal.Message);
            return await LoadAsync(cancellationToken) ? Page() : NotFound();
        }

        Notice = notice;
        return RedirectToPage(new { id = Id });
    }

    /// <summary>
    /// Resend and Remind. The token is minted for e-mail when the link can be
    /// e-mailed (an address, a Case and a staff send), else for copying; the
    /// page then renders directly, because the raw token is never stored.
    /// </summary>
    private async Task<IActionResult> ReissueAsync(bool isReminder, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        var capture = Capture;
        var byEmail = _mail.Available && capture.CaseId is not null && capture.Email is { Length: > 0 };
        IssuedPhotoLink issued;
        try
        {
            issued = await actions.ReissueAsync(
                actor,
                Id,
                byEmail ? PhotoLinkTokenPurpose.Email : PhotoLinkTokenPurpose.Copy,
                isReminder,
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (PhotoCaptureRefusalException refusal)
        {
            ModelState.AddModelError(string.Empty, refusal.Message);
            return Page();
        }

        IssuedUrl = _mail.CaptureUrl(issued.Token);
        if (byEmail)
        {
            try
            {
                var body = await _mail.RenderAsync(
                    actor,
                    PhotoLinkMail.Values(capture.NormalizedRegistration, capture.CustomerName, Row.OrganizationName, IssuedUrl, capture.ExpiresAtUtc),
                    cancellationToken);
                Operation = await _mail.SendAsync(
                    actor,
                    capture.CaseId!.Value,
                    fromMailboxId: null,
                    capture.Email!,
                    PhotoLinkMail.Subject(capture.NormalizedRegistration),
                    body,
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
                // The token exists; staff copy it instead.
                ModelState.AddModelError(string.Empty, exception.Message);
            }
            catch (PhotoCaptureRefusalException refusal)
            {
                ModelState.AddModelError(string.Empty, refusal.Message);
            }
        }

        // The page draws the record as the reissue left it.
        await LoadAsync(cancellationToken);
        OperationKey = NewOperationKey();
        return Page();
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        var row = queries.GetRowAsync(Id, cancellationToken);
        var items = captures.ListItemsAsync(Id, cancellationToken);
        await Task.WhenAll(row, items);
        if (row.Result is not { } found)
        {
            return false;
        }

        Row = found;
        Now = timeProvider.GetUtcNow();
        var derived = IndexModel.Derive(found, Now);
        State = derived.State;
        Due = derived.Due;
        Story = PhotoCaptureStoryView.Groups(items.Result, found.Capture.Details.MarkedAreas);
        return true;
    }
}
