using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Web.Presentation;
using Labels = Pegasus.Web.Presentation.OperatorLabels.PhotoCapture;

namespace Pegasus.Web.Pages.PhotoCapture;

/// <summary>
/// Photo capture (FRD-28 Staff pages; R774): <b>Coming in</b>, the captures
/// standing and website links started, and <b>Links we sent</b>, the links
/// HQ sent from a Case. The left pane is the tab's headline states with their
/// queried counts, as the Cases list's scopes are; the list and those counts
/// are one query (<see cref="IPhotoCaptureQueries.ListAsync"/>), newest first. The
/// website link is managed on Administration › Photo links (v50 F; R911).
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class IndexModel(
    IPhotoCaptureQueries queries,
    IPhotoLinkTextSend textSend,
    TimeProvider timeProvider) : PhotoCapturePageModel
{
    private const int PageSize = 25;

    /// <summary>
    /// One list row with what is derived from it as it is read (FRD-28: never
    /// stored). <see cref="IsLive"/> is whether the link still takes photos,
    /// which Resend needs (Core refuses it on a dead link).
    /// </summary>
    public sealed record Row(PhotoCaptureListRow Source, PhotoCaptureState State, DueCaptureReminder? Due, bool IsLive)
    {
        public Pegasus.Core.PhotoCapture.PhotoCapture Capture => Source.Capture;
    }

    [BindProperty(SupportsGet = true, Name = "tab")]
    public string? Tab { get; set; }

    [BindProperty(SupportsGet = true, Name = "state")]
    public string? StateFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "origin")]
    public string? OriginFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    // "page" is also the Razor Pages route value naming this page, which
    // is why it is read from the query explicitly (as the Cases list does).
    [FromQuery(Name = "page")]
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [TempData]
    public string? Notice { get; set; }

    public bool SentByUs => Tab == Labels.LinksWeSentKey;

    public PhotoCaptureState? State => Labels.StateFromKey(StateFilter);

    public PhotoCaptureOrigin? Origin => SentByUs ? null : Labels.OriginFromKey(OriginFilter);

    /// <summary>Coming in's "No case yet": only the captures with no Case (R806), chosen where the From filter is.</summary>
    public bool NoCaseYet => !SentByUs && OriginFilter == Labels.NoCaseYetKey;

    /// <summary>How many captures each tab holds, shown on the tabs (R806).</summary>
    public PhotoCaptureTabCounts? TabCounts { get; private set; }

    public IReadOnlyList<Row> Rows { get; private set; } = [];

    public int TotalCount { get; private set; }

    public IReadOnlyDictionary<PhotoCaptureState, int> Counts { get; private set; } =
        new Dictionary<PhotoCaptureState, int>();

    public DateTimeOffset? LoadedAtUtc { get; private set; }

    public bool IsUnavailable { get; private set; }

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage * PageSize < TotalCount;

    public DateTimeOffset Now { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out _))
        {
            return Forbid();
        }

        Tab = Tab is Labels.LinksWeSentKey ? Labels.LinksWeSentKey : Labels.ComingInKey;
        if (CurrentPage is < 1 or > 10_000)
        {
            return NotFound();
        }

        Query = string.IsNullOrWhiteSpace(Query) ? null : Query.Trim();
        if (Query is { Length: > 100 })
        {
            Query = Query[..100];
        }

        Now = timeProvider.GetUtcNow();
        try
        {
            var page = queries.ListAsync(
                new(SentByUs, Query, State, Origin, NoCaseYet, PageSize, CurrentPage),
                cancellationToken);
            var tabCounts = queries.CountByTabAsync(cancellationToken);
            await Task.WhenAll(page, tabCounts);
            TotalCount = page.Result.TotalCount;
            Rows = [.. page.Result.Rows.Select(row => Derive(row, Now))];
            Counts = page.Result.CountsByState;
            TabCounts = tabCounts.Result;
            LoadedAtUtc = Now;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A failed live read is not an empty list.
            IsUnavailable = true;
            Rows = [];
        }

        return Page();
    }

    /// <summary>
    /// The state and due reminder a row derives, from its own record and
    /// missing items; a link whose Case is archived is dead (R793).
    /// </summary>
    public static Row Derive(PhotoCaptureListRow row, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new(
            row,
            PhotoCaptureStatus.Evaluate(row.Capture, PhotoCaptureStatus.MissingOf(row), row.Photographs, now, row.CaseArchived),
            PhotoCaptureStatus.DueReminder(row, now),
            PhotoCaptureAccess.IsLive(row.Capture, now, row.CaseArchived));
    }

    /// <summary>The headline states this tab counts, in the order the left pane lists them.</summary>
    public IReadOnlyList<PhotoCaptureState> CountedStates =>
        [.. Enum.GetValues<PhotoCaptureState>().Where(Counts.ContainsKey)];

    public string Href(string? tab = null, string? state = null, bool keepState = true, int? page = null)
    {
        var target = tab ?? Tab ?? Labels.ComingInKey;
        var sameTab = target == (Tab ?? Labels.ComingInKey);
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["tab"] = target == Labels.ComingInKey ? null : target,
            ["state"] = state ?? (keepState && sameTab ? StateFilter : null),
            ["origin"] = sameTab && target == Labels.ComingInKey ? OriginFilter : null,
            ["q"] = sameTab ? Query : null,
            ["page"] = page is > 1 ? page.Value.ToString(CultureInfo.InvariantCulture) : null
        };
        return QueryHelpers.AddQueryString(
            "/PhotoCapture",
            values.Where(item => !string.IsNullOrWhiteSpace(item.Value)));
    }

    /// <summary>Where a row's photos are: the Case's Files section, else the Vehicle images record.</summary>
    public static string? PhotosHref(PhotoCaptureListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Capture.CaseId is { } caseId)
        {
            return Pegasus.Core.Notifications.StaffNotificationPolicy.CaseRoute(caseId, "files");
        }

        return row.ImageIntakeId is { } recordId ? $"/VehicleImages/{recordId:D}" : null;
    }

    /// <summary>
    /// How a Coming in capture reached its Case: it was already instructed,
    /// it was matched when the instruction came, or a member of staff chose
    /// it with Match to a case (operator, 10 November 2026; R864). A capture
    /// with no Case shows none.
    /// </summary>
    public static string? CaseHow(PhotoCaptureListRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Pairing switch
        {
            PhotoCapturePairing.FiledAsItArrived => Labels.AlreadyInstructed,
            PhotoCapturePairing.MatchedAutomatically => Labels.MatchedAutomatically,
            PhotoCapturePairing.ChosenByStaff => Labels.MatchedByStaff,
            _ => null
        };
    }

    /// <summary>
    /// Where Resend, and Ask for it on a capture that can be e-mailed, open:
    /// the composer, since no button sends an e-mail on its own (operator, 10
    /// November 2026; R915). It returns to <paramref name="returnUrl"/>.
    /// </summary>
    public static string ResendHref(Guid captureId, string returnUrl) =>
        QueryHelpers.AddQueryString($"/PhotoCapture/{captureId:D}/Resend", "returnUrl", returnUrl);

    /// <summary>
    /// Whether Ask for it opens the composer: the capture can be e-mailed, a
    /// capture on a Case that holds an address (R816), or texted, one that
    /// holds a mobile where texting is configured (R920).
    /// </summary>
    public bool CanSend(Pegasus.Core.PhotoCapture.PhotoCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        return (capture.CaseId is not null && !string.IsNullOrWhiteSpace(capture.Email))
            || (textSend.Available && !string.IsNullOrWhiteSpace(capture.Mobile));
    }
}
