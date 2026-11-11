using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Web.Presentation;
using CaptureRecord = Pegasus.Core.PhotoCapture.PhotoCapture;
using Labels = Pegasus.Web.Presentation.OperatorLabels.PhotoCapture;

namespace Pegasus.Web.Pages.PhotoCapture;

/// <summary>
/// One photo link (FRD-28 Staff pages): its photographs as tiles that open
/// the evidence viewer in place, the paperwork and video beside them, what is
/// missing on a Direct link, and a Summary of its facts, what the sender told
/// us and its key history, with Resend, Log call, Extend and Cancel
/// (operator, 11 November 2026; v51 A to G, Q, R; R948). Resend opens the
/// composer (<see cref="ResendModel"/>), since no button sends an e-mail on
/// its own (operator, 10 November 2026; R915). A standing or website capture
/// is not chased: it reads Uploading, then Received, and nothing on this page
/// makes it a one-off link (v51 Q; R948).
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class DetailsModel(
    IPhotoCaptureQueries queries,
    IPhotoCaptureStore captures,
    PhotoLinkActions actions,
    TimeProvider timeProvider) : PhotoCapturePageModel
{

    /// <summary>One line of the link's history, from the record's own times.</summary>
    public sealed record HistoryLine(string Label, DateTimeOffset AtUtc, string? Detail = null);

    /// <summary>
    /// One photograph's tile (v51 A, B; R948): <see cref="Href"/> is what the
    /// viewer and Save as read, the settled intake asset or, before the
    /// settle, the staged image; <see cref="TileHref"/> is the tile's own
    /// rendering where the route serves one. <see cref="Category"/> is the
    /// filter it falls under (<see cref="OperatorLabels.PhotoCapture.PhotoFilterKey"/>), null for
    /// All only; <see cref="FileName"/> is the caption with the file's
    /// extension, the name Save as offers.
    /// </summary>
    public sealed record PhotoTile(
        int Number,
        string Caption,
        string? Category,
        string Href,
        string TileHref,
        string FileName,
        string MediaType);

    /// <summary>One paperwork or video item of the Files strip; <see cref="Href"/> is null until it reaches intake.</summary>
    public sealed record FileChip(string Caption, string? Href);

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

    /// <summary>The capture's photographs in story order, numbered and captioned.</summary>
    public IReadOnlyList<PhotoTile> Photos { get; private set; } = [];

    /// <summary>The capture's paperwork and video, in story order.</summary>
    public IReadOnlyList<FileChip> Files { get; private set; } = [];

    /// <summary>The key milestones, oldest first (v51 G; R948).</summary>
    public IReadOnlyList<HistoryLine> History { get; private set; } = [];

    public PhotoCaptureState State { get; private set; }

    public DueCaptureReminder? Due { get; private set; }

    public DateTimeOffset Now { get; private set; }

    public bool IsLive => PhotoCaptureAccess.IsLive(Capture, Now, Row.CaseArchived);

    public bool IsCaseLink => Capture.Origin == PhotoCaptureOrigin.CaseLink;

    /// <summary>Whether photos it received are still to settle (v51 L1, Q; R948).</summary>
    public bool IsUploading => PhotoCaptureStatus.IsUploading(Capture);

    /// <summary>
    /// The header chip's words (v51 L3, Q; R948): a Direct link's derived
    /// state; a standing or website capture is not graded against the guided
    /// checklist, so it reads Uploading while its photos settle and Received
    /// once they have. One that has received nothing reads its derived state,
    /// Opened, Expired or Cancelled, as a Direct link would.
    /// </summary>
    public string ChipLabel =>
        IsCaseLink || Capture.AcceptedItemCount == 0 ? Labels.State(State)
        : IsUploading ? Labels.Uploading
        : Labels.Received;

    /// <summary>The header chip's tone: amber while uploading, green once received.</summary>
    public string ChipTone =>
        IsCaseLink || Capture.AcceptedItemCount == 0 ? Labels.StateTone(State)
        : IsUploading ? "amber"
        : "green";

    /// <summary>
    /// The inline Missing bar (v51 C; R948): only a Direct link is graded
    /// against the checklist, and a cancelled or expired one is not chased.
    /// </summary>
    public bool ShowsMissing =>
        IsCaseLink
        && Row.MissingItems.Count > 0
        && State is not (PhotoCaptureState.Cancelled or PhotoCaptureState.Expired);

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

    /// <summary>
    /// Where the photos are, after the follow-up act (v51 D, R; R948): the
    /// Case's Files, else the Vehicle images record its photos became; null
    /// while it has neither.
    /// </summary>
    public string? DestinationHref =>
        Capture.CaseId is { } caseId ? Pegasus.Core.Notifications.StaffNotificationPolicy.CaseRoute(caseId, "files")
        : Row.ImageIntakeId is { } recordId ? $"/VehicleImages/{recordId:D}"
        : null;

    public string DestinationLabel => Capture.CaseId is not null ? Labels.OpenCase : Labels.OpenVehicleImages;

    /// <summary>The Summary's Chase row on a live Direct link: the reminder due, else how many were sent.</summary>
    public string? Chase => IsCaseLink && IsLive ? Labels.ChaseCue(Due, Capture.ReminderCount) : null;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out _))
        {
            return Forbid();
        }

        return await LoadAsync(cancellationToken) ? Page() : NotFound();
    }

    /// <summary>Log call, from this page or a Direct links row, which it returns to (v50 P; R912).</summary>
    public Task<IActionResult> OnPostLogCallAsync(string? returnUrl, CancellationToken cancellationToken) =>
        ActAsync((actor, token) => actions.LogCallAsync(actor, Id, OperationKey, token), Labels.CallLogged, returnUrl, cancellationToken);

    public Task<IActionResult> OnPostExtendAsync(CancellationToken cancellationToken) =>
        ActAsync((actor, token) => actions.ExtendAsync(actor, Id, Days, OperationKey, token), Labels.LinkExtended, returnUrl: null, cancellationToken);

    /// <summary>
    /// Cancel, from this page or the Case page (R795): the same act and the
    /// same refusal, which this page shows; a success goes back to where it
    /// was asked from.
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
        // Only the items whose upload reached intake are photos the link has,
        // as the phone and the Send count them; the story decides their order.
        var ordered = PhotoCaptureStoryView.Groups(
                items.Result.Where(item => item.StagedReceiptId is not null).ToArray(),
                found.Capture.Details.MarkedAreas)
            .SelectMany(group => group.Stages)
            .SelectMany(stage => stage.Items)
            .ToArray();
        var photographs = ordered.Where(item => PhotoCaptureStory.IsPhotographKind(item.Kind)).ToArray();
        var captions = Labels.Captions(photographs);
        Photos = [.. photographs.Select((item, index) => Tile(item, index + 1, captions[index]))];
        Files = [.. ordered
            .Where(item => !PhotoCaptureStory.IsPhotographKind(item.Kind))
            .Select(item => new FileChip(Labels.ItemLabel(item), FileHref(item)))];
        History = KeyMilestones(found, acts.Result, State);
        return true;
    }

    private PhotoTile Tile(PhotoCaptureItem item, int number, string caption)
    {
        // Settled, the intake asset, whose tile is a rendering at tile size;
        // before the settle, the staged image (v51 A; R948).
        var href = item.IntakeReceiptId is { } receiptId && item.IntakeAssetId is { } assetId
            ? Url.Page("/Intake/Asset", new { id = receiptId, assetId, v = item.ContentHash })!
            : Url.Page("/Intake/Image", new { id = item.StagedReceiptId })!;
        var tileHref = new GalleryImage(
            href,
            href,
            caption,
            item.MediaType,
            IntakeAssetId: item.IntakeAssetId,
            IntakeReceiptId: item.IntakeReceiptId,
            ContentHash: item.ContentHash).TileHref;
        return new(
            number,
            caption,
            Labels.PhotoFilterKey(item.Kind),
            href,
            tileHref,
            caption + Path.GetExtension(item.FileName),
            item.MediaType);
    }

    /// <summary>
    /// Where a paperwork or video item opens (R807): the received file, served
    /// by its own route under its own access rule. Null for one not yet
    /// received into intake.
    /// </summary>
    private string? FileHref(PhotoCaptureItem item) =>
        item.IntakeReceiptId is { } receiptId && item.IntakeAssetId is { } assetId
            ? Url.Page("/Intake/Asset", new { id = receiptId, assetId })
            : null;

    /// <summary>
    /// The key milestones (operator, 11 November 2026; v51 G; R948): Created
    /// with who sent it, Opened, Received with how many photos, each staff act
    /// with who did it, Reminded, Link extended, Sent by text, Text not sent,
    /// Call logged and Cancelled (R805, R870, R921), and Expired.
    /// </summary>
    private static IReadOnlyList<HistoryLine> KeyMilestones(
        PhotoCaptureListRow row,
        IReadOnlyList<PhotoLinkAct> acts,
        PhotoCaptureState state)
    {
        var capture = row.Capture;
        var lines = new List<HistoryLine> { new(Labels.Created, capture.CreatedAtUtc, row.IssuedByName) };
        if (capture.OpenedAtUtc is { } opened)
        {
            lines.Add(new(Labels.Opened, opened));
        }

        if (capture.AcceptedItemCount > 0 && capture.LastItemAtUtc is { } received)
        {
            lines.Add(new(Labels.Received, received, row.Photographs > 0 ? Labels.PhotosOf(row.Photographs) : null));
        }

        foreach (var act in acts)
        {
            var label = act.Kind switch
            {
                PhotoLinkActs.Reminded => Labels.Reminded,
                PhotoLinkActs.Extended => Labels.LinkExtended,
                PhotoLinkActs.TextSent => Labels.SentByText,
                PhotoLinkActs.TextFailed => Labels.TextNotSent,
                PhotoLinkActs.CallLogged => Labels.CallLoggedAt,
                PhotoLinkActs.Cancelled => Labels.Cancelled,
                _ => null
            };
            if (label is not null)
            {
                lines.Add(new(label, act.AtUtc, act.ActorName));
            }
        }

        if (state == PhotoCaptureState.Expired && !row.CaseArchived)
        {
            lines.Add(new(Labels.State(PhotoCaptureState.Expired), capture.ExpiresAtUtc));
        }

        return [.. lines.OrderBy(line => line.AtUtc)];
    }
}
