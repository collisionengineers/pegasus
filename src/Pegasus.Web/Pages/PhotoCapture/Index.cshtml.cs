using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Web.PhotoCapture;
using Pegasus.Web.Presentation;
using Labels = Pegasus.Web.Presentation.OperatorLabels.PhotoCapture;

namespace Pegasus.Web.Pages.PhotoCapture;

/// <summary>
/// Photo capture (FRD-28 Staff pages; R771): <b>Coming in</b>, the captures
/// standing and website links started, and <b>Links we sent</b>, the links
/// HQ sent from a Case. The left pane is the tab's headline states with their
/// queried counts, as the Cases list's scopes are; the list is one query
/// (<see cref="IPhotoCaptureQueries.ListAsync"/>), newest first. Below the
/// list an Administrator manages the one website link (R777).
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class IndexModel(
    IPhotoCaptureQueries queries,
    IStandingPhotoLinkStore standingLinks,
    IOptions<PhotoCaptureOptions> options,
    TimeProvider timeProvider) : PhotoCapturePageModel
{
    private const int PageSize = 25;

    /// <summary>One list row with what is derived from it as it is read (FRD-28: never stored).</summary>
    public sealed record Row(PhotoCaptureListRow Source, PhotoCaptureState State, DueCaptureReminder? Due)
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

    [BindProperty]
    public long ExpectedVersion { get; set; }

    [TempData]
    public string? Notice { get; set; }

    public bool SentByUs => Tab == Labels.LinksWeSentKey;

    public PhotoCaptureState? State => Labels.StateFromKey(StateFilter);

    public PhotoCaptureOrigin? Origin => SentByUs ? null : Labels.OriginFromKey(OriginFilter);

    public IReadOnlyList<Row> Rows { get; private set; } = [];

    public int TotalCount { get; private set; }

    public IReadOnlyDictionary<PhotoCaptureState, int> Counts { get; private set; } =
        new Dictionary<PhotoCaptureState, int>();

    public DateTimeOffset? LoadedAtUtc { get; private set; }

    public bool IsUnavailable { get; private set; }

    public bool IsAdministrator => User.IsInRole(StaffRoleNames.Administrator);

    /// <summary>The website standing link, read for Administrators only; null when none exists.</summary>
    public StandingPhotoLink? WebsiteLink { get; private set; }

    public string? WebsiteUrl => WebsiteLink is { } link ? PhotoLinkMail.StandingUrl(options.Value, link.Slug) : null;

    public string? WebsiteQrSvg => WebsiteUrl is { } url ? PhotoLinkQr.Svg(url) : null;

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage * PageSize < TotalCount;

    public DateTimeOffset Now { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
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
                new(SentByUs, Query, State, Origin, PageSize, CurrentPage),
                cancellationToken);
            var counts = queries.CountByStateAsync(SentByUs, cancellationToken);
            var website = IsAdministrator && StaffAuthorization.IsAuthorized(actor, StaffAccessRight.ManageOrganizationsAndPrincipals)
                ? standingLinks.FindWebsiteAsync(cancellationToken)
                : Task.FromResult<StandingPhotoLink?>(null);
            await Task.WhenAll(page, counts, website);
            TotalCount = page.Result.TotalCount;
            Rows = [.. page.Result.Rows.Select(row => Derive(row, Now))];
            Counts = counts.Result;
            WebsiteLink = website.Result;
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

    /// <summary>The state and due reminder a row derives, from its own record and missing items.</summary>
    public static Row Derive(PhotoCaptureListRow row, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        IReadOnlyList<MissingCaptureItem> missing = [.. row.MissingItems.Select(label => new MissingCaptureItem(label, label))];
        return new(
            row,
            PhotoCaptureStatus.Evaluate(row.Capture, missing, now),
            row.Capture.Origin == PhotoCaptureOrigin.CaseLink ? PhotoCaptureStatus.DueReminder(row.Capture, missing, now) : null);
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

    /// <summary>How a Coming in capture reached its Case: it was already instructed, or it was matched when the instruction came.</summary>
    public static string? CaseHow(Pegasus.Core.PhotoCapture.PhotoCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        if (capture.CaseId is null || capture.Origin == PhotoCaptureOrigin.CaseLink)
        {
            return null;
        }

        return capture.Destination == PhotoCaptureDestination.Case ? Labels.AlreadyInstructed : Labels.MatchedAutomatically;
    }

    // The website link (R777): Administrators create, pause, resume and
    // replace it here. Its defaults are a contact link's; the website starts
    // guided captures.
    public Task<IActionResult> OnPostCreateWebsiteLinkAsync(CancellationToken cancellationToken) =>
        ChangeWebsiteLinkAsync("create", cancellationToken);

    public Task<IActionResult> OnPostPauseWebsiteLinkAsync(CancellationToken cancellationToken) =>
        ChangeWebsiteLinkAsync("pause", cancellationToken);

    public Task<IActionResult> OnPostResumeWebsiteLinkAsync(CancellationToken cancellationToken) =>
        ChangeWebsiteLinkAsync("resume", cancellationToken);

    public Task<IActionResult> OnPostReplaceWebsiteLinkAsync(CancellationToken cancellationToken) =>
        ChangeWebsiteLinkAsync("replace", cancellationToken);

    /// <summary>The website link's QR code as a file to print.</summary>
    public async Task<IActionResult> OnGetWebsiteQrAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor) || !MayManageLinks(actor))
        {
            return Forbid();
        }

        var link = await standingLinks.FindWebsiteAsync(cancellationToken);
        if (link is null)
        {
            return NotFound();
        }

        return File(
            Encoding.UTF8.GetBytes(PhotoLinkQr.Svg(PhotoLinkMail.StandingUrl(options.Value, link.Slug))),
            "image/svg+xml",
            PhotoLinkQr.FileName(link.Slug));
    }

    private async Task<IActionResult> ChangeWebsiteLinkAsync(string action, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor) || !MayManageLinks(actor))
        {
            return Forbid();
        }

        var now = timeProvider.GetUtcNow();
        var link = await standingLinks.FindWebsiteAsync(cancellationToken);
        switch (action)
        {
            case "create" when link is null:
                await standingLinks.CreateAsync(
                    StandingPhotoLinkKind.Website,
                    organizationId: null,
                    PhotoLinkToken.CreateSlug(),
                    PhotoCaptureMode.Guided,
                    liveCameraOnly: true,
                    CaptureLimits.DefaultLifetimeDays,
                    originalsInBackground: true,
                    actor,
                    now,
                    cancellationToken);
                break;
            case "pause" when link is not null:
                await standingLinks.SetPausedAsync(link.Id, ExpectedVersion, now, cancellationToken);
                break;
            case "resume" when link is not null:
                await standingLinks.SetPausedAsync(link.Id, ExpectedVersion, null, cancellationToken);
                break;
            case "replace" when link is not null:
                await standingLinks.ReplaceSlugAsync(link.Id, ExpectedVersion, PhotoLinkToken.CreateSlug(), now, cancellationToken);
                break;
            default:
                return BadRequest();
        }

        return Redirect(Href() + "#website-link");
    }

    private bool MayManageLinks(ActionActor actor) =>
        IsAdministrator && StaffAuthorization.IsAuthorized(actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
}
