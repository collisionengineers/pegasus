using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Web.PhotoCapture;
using Pegasus.Web.Presentation;
using CaptureRecord = Pegasus.Core.PhotoCapture.PhotoCapture;
using Labels = Pegasus.Web.Presentation.OperatorLabels.PhotoCapture;

namespace Pegasus.Web.Pages.PhotoCapture;

/// <summary>
/// One photo link (FRD-28 Staff pages): its facts, what the sender told us,
/// the note, the status and what is missing, the items in story order and the
/// history, with Resend, Log call, Extend and Cancel (v50 N, O; R913). Resend
/// opens the composer (<see cref="ResendModel"/>), since no button sends an
/// e-mail on its own (operator, 10 November 2026; R915). This page's own
/// Resend handler only makes a link to copy: Coming in's Ask for it on a
/// capture with no Case or no address (R816), shown once here, since only the
/// issuing response ever holds it.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class DetailsModel(
    IPhotoCaptureQueries queries,
    IPhotoCaptureStore captures,
    PhotoLinkActions actions,
    IOptions<PhotoCaptureOptions> options,
    TimeProvider timeProvider) : PhotoCapturePageModel
{

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

    /// <summary>The staff acts on the link, each naming who did it (R805).</summary>
    public IReadOnlyList<PhotoLinkAct> Acts { get; private set; } = [];

    public PhotoCaptureState State { get; private set; }

    public DueCaptureReminder? Due { get; private set; }

    public DateTimeOffset Now { get; private set; }

    /// <summary>The link Ask for it just made to copy, shown once; null otherwise.</summary>
    public string? IssuedUrl { get; private set; }

    public bool IsLive => PhotoCaptureAccess.IsLive(Capture, Now, Row.CaseArchived);

    public bool IsCaseLink => Capture.Origin == PhotoCaptureOrigin.CaseLink;

    /// <summary>Resend, one act since Remind sent the same message (R914): a live link HQ sent from a Case.</summary>
    public bool OffersResend => IsCaseLink && IsLive;

    /// <summary>
    /// The page's one primary act (R913): Resend when a reminder is due, Log
    /// call when the link needs a phone call, else Resend while it is live.
    /// </summary>
    public string? PrimaryAct =>
        OffersResend && Due is not null ? Labels.Resend
        : OffersLogCall && State == PhotoCaptureState.NeedsCall ? Labels.LogCall
        : OffersResend ? Labels.Resend
        : null;

    /// <summary>Where Resend opens the composer, returning here.</summary>
    public string ResendHref => IndexModel.ResendHref(Id, $"/PhotoCapture/{Id:D}");

    public bool OffersLogCall => IsCaseLink && !Capture.IsRevoked;

    /// <summary>A link whose Case is archived is dead and is not extended (R793).</summary>
    public bool OffersExtend => !Capture.IsRevoked && !Row.CaseArchived;

    public bool OffersCancel => IsLive;

    /// <summary>Where the photos are: the Case's Files section, else the Vehicle images record.</summary>
    public string? PhotosHref => IndexModel.PhotosHref(Row);

    /// <summary>
    /// Where a kept file opens (R807): paperwork or video a capture with no
    /// Case holds, served by the existing received-file page under its own
    /// access rule. Null for anything else, and for a file not yet received
    /// into intake.
    /// </summary>
    public string? KeptFileHref(PhotoCaptureItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Capture.CaseId is null
            && item.Kind is (PhotoCaptureItemKind.Paperwork or PhotoCaptureItemKind.Video)
            && item.IntakeReceiptId is { } receiptId
            && item.IntakeAssetId is { } assetId
                ? $"/Received/{receiptId:D}/Asset/{assetId:D}"
                : null;
    }

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
            Add(Labels.Filed, capture.SettledAtUtc);
            // Cancel, Extend, Log call and each reminder, Ask for it's
            // included (R842), name who acted: "Cancelled · name", "Link
            // extended · name", "Call logged · name" (R805) and one
            // "Reminded · name" per reminder (operator, 10 November 2026; R870),
            // and each text, "Sent by text · name" or "Text not sent · name" (R919).
            foreach (var act in Acts)
            {
                var label = act.Kind switch
                {
                    PhotoLinkActs.Cancelled => Labels.Cancelled,
                    PhotoLinkActs.Extended => Labels.LinkExtended,
                    PhotoLinkActs.CallLogged => Labels.CallLoggedAt,
                    PhotoLinkActs.Reminded => Labels.Reminded,
                    PhotoLinkActs.TextSent => Labels.SentByText,
                    PhotoLinkActs.TextFailed => Labels.TextNotSent,
                    _ => null
                };
                if (label is not null)
                {
                    lines.Add(new(label, act.AtUtc, act.ActorName));
                }
            }

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

    /// <summary>Log call, from this page or a Links we sent row, which it returns to (v50 P; R912).</summary>
    public Task<IActionResult> OnPostLogCallAsync(string? returnUrl, CancellationToken cancellationToken) =>
        ActAsync((actor, token) => actions.LogCallAsync(actor, Id, OperationKey, token), Labels.CallLogged, returnUrl, cancellationToken);

    public Task<IActionResult> OnPostExtendAsync(CancellationToken cancellationToken) =>
        ActAsync((actor, token) => actions.ExtendAsync(actor, Id, Days, OperationKey, token), Labels.LinkExtended, returnUrl: null, cancellationToken);

    /// <summary>
    /// Cancel, from this page, a Links we sent row or the Case page (R795):
    /// the same act and the same refusal, which this page shows; a success
    /// goes back to where it was asked from.
    /// </summary>
    public Task<IActionResult> OnPostCancelAsync(string? returnUrl, CancellationToken cancellationToken) =>
        ActAsync((actor, token) => actions.RevokeAsync(actor, Id, OperationKey, token), Labels.LinkCancelled, returnUrl, cancellationToken);

    private async Task<IActionResult> ActAsync(
        Func<ActionActor, CancellationToken, Task<CaptureRecord>> act,
        string notice,
        string? returnUrl,
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
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage(new { id = Id });
    }

    /// <summary>
    /// A fresh link to copy for this capture (Ask for it with nothing to e-mail;
    /// R816), counted as Core's Resend says (R914, R842). The page renders
    /// directly, because the raw token is never stored.
    /// </summary>
    public async Task<IActionResult> OnPostResendAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        IssuedPhotoLink issued;
        try
        {
            issued = await actions.ResendAsync(actor, Id, PhotoLinkTokenPurpose.Copy, OperationKey, cancellationToken);
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

        IssuedUrl = PhotoLinkUrls.CaptureUrl(options.Value.PublicOrigin, issued.Token);
        // The page draws the record as the resend left it.
        await LoadAsync(cancellationToken);
        OperationKey = NewOperationKey();
        return Page();
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        var row = queries.GetRowAsync(Id, cancellationToken);
        var items = captures.ListItemsAsync(Id, cancellationToken);
        var acts = queries.ListHistoryAsync(Id, cancellationToken);
        await Task.WhenAll(row, items, acts);
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
        Acts = acts.Result;
        return true;
    }
}
