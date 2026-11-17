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
/// Photo capture (FRD-28 Staff pages), in the two kinds of link Collision
/// Engineers runs (operator, 11 November 2026; v51 N, P; R948):
/// <b>Direct links</b>, the links HQ sent for a Case, filtered by workflow
/// scope; and <b>Standing links</b>, the directory of standing and website
/// links, which any member of staff may copy or show as a QR code, beside the
/// captures they started. The list and the scopes' counts are one query
/// (<see cref="IPhotoCaptureQueries.ListAsync"/>), newest first.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class IndexModel(
    IPhotoCaptureQueries queries,
    TimeProvider timeProvider,
    IOptions<PhotoCaptureOptions>? options = null) : PhotoCapturePageModel
{
    private const int PageSize = 25;

    /// <summary>One standing link of the directory with its address.</summary>
    public sealed record StandingLink(StandingPhotoLinkListing Listing, string? Url)
    {
        public StandingPhotoLink Link => Listing.Link;
    }

    [BindProperty(SupportsGet = true, Name = "tab")]
    public string? Tab { get; set; }

    [BindProperty(SupportsGet = true, Name = "scope")]
    public string? ScopeKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "link")]
    public Guid? LinkFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    // "page" is also the Razor Pages route value naming this page, which
    // is why it is read from the query explicitly (as the Cases list does).
    [FromQuery(Name = "page")]
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [TempData]
    public string? Notice { get; set; }

    /// <summary>The Direct links tab, the default; otherwise Standing links.</summary>
    public bool Direct => Tab != Labels.StandingLinksKey;

    public PhotoCaptureScope Scope => Labels.ScopeFromKey(ScopeKey);

    /// <summary>The page's rows, each with what Core derives from it as it is read (#2130; R1092).</summary>
    public IReadOnlyList<PhotoCaptureRowStatus> Rows { get; private set; } = [];

    public int TotalCount { get; private set; }

    public IReadOnlyDictionary<PhotoCaptureScope, int> Counts { get; private set; } =
        new Dictionary<PhotoCaptureScope, int>();

    /// <summary>The Direct links tab's count: its Awaiting links.</summary>
    public int? AwaitingCount { get; private set; }

    public IReadOnlyList<StandingLink> StandingLinks { get; private set; } = [];

    /// <summary>The standing link the Standing links tab is narrowed to, if any.</summary>
    public StandingLink? SelectedLink { get; private set; }

    public int StandingSubmissions => StandingLinks.Sum(link => link.Listing.Submissions);

    public DateTimeOffset? LoadedAtUtc { get; private set; }

    public bool IsUnavailable { get; private set; }

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage * PageSize < TotalCount;

    public DateTimeOffset Now { get; private set; }

    /// <summary>Whether the reader manages standing links: Defaults and Manage lead to Administration.</summary>
    public bool ManagesLinks { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        Tab = Direct ? null : Labels.StandingLinksKey;
        if (CurrentPage is < 1 or > 10_000)
        {
            return NotFound();
        }

        Query = string.IsNullOrWhiteSpace(Query) ? null : Query.Trim();
        if (Query is { Length: > 100 })
        {
            Query = Query[..100];
        }

        ManagesLinks = StaffAuthorization.IsAuthorized(actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
        Now = timeProvider.GetUtcNow();
        try
        {
            var links = await queries.ListStandingLinksAsync(cancellationToken);
            StandingLinks = [.. links.Select(listing => new StandingLink(listing, AddressOf(listing.Link)))];
            SelectedLink = Direct || LinkFilter is not { } linkId
                ? null
                : StandingLinks.FirstOrDefault(link => link.Link.Id == linkId);
            var page = await queries.ListAsync(
                new(Direct, Query, Scope, SelectedLink?.Link.Id, PageSize, CurrentPage),
                cancellationToken);
            TotalCount = page.TotalCount;
            Rows = [.. page.Rows.Select(row => PhotoCaptureRowStatus.For(row, Now))];
            Counts = page.CountsByScope;
            AwaitingCount = Direct
                ? Counts.GetValueOrDefault(PhotoCaptureScope.Awaiting)
                : await queries.CountAwaitingAsync(cancellationToken);
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

    /// <summary>A standing link's QR code (R780), shown in its dialog and saved by Download QR, for any member of staff (v51 P; R948).</summary>
    public async Task<IActionResult> OnGetQrAsync(Guid id, bool download, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out _))
        {
            return Forbid();
        }

        var link = (await queries.ListStandingLinksAsync(cancellationToken))
            .Select(listing => listing.Link)
            .FirstOrDefault(link => link.Id == id);
        if (link is null || AddressOf(link) is not { } url)
        {
            return NotFound();
        }

        var svg = Encoding.UTF8.GetBytes(PhotoLinkQr.Svg(url));
        return download
            ? File(svg, "image/svg+xml", PhotoLinkQr.FileName(link.Slug))
            : File(svg, "image/svg+xml");
    }

    private string? AddressOf(StandingPhotoLink link) =>
        options?.Value is { } value ? PhotoLinkMail.StandingUrl(value, link.Slug) : null;

    /// <summary>
    /// This list's address with what is given changed: <paramref name="direct"/>
    /// picks the tab, <paramref name="scope"/> the Direct links scope and
    /// <paramref name="link"/> the standing link; the search stays on its tab.
    /// </summary>
    public string Href(bool? direct = null, string? scope = null, Guid? link = null, bool keepLink = true, bool keepQuery = true, int? page = null)
    {
        var toDirect = direct ?? Direct;
        var sameTab = toDirect == Direct;
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["tab"] = toDirect ? null : Labels.StandingLinksKey,
            ["scope"] = toDirect ? scope ?? (sameTab ? ScopeKey : null) : null,
            ["link"] = toDirect
                ? null
                : (link ?? (keepLink && sameTab ? SelectedLink?.Link.Id : null))?.ToString("D"),
            ["q"] = sameTab && keepQuery ? Query : null,
            ["page"] = page is > 1 ? page.Value.ToString(CultureInfo.InvariantCulture) : null
        };
        return QueryHelpers.AddQueryString(
            "/PhotoCapture",
            values.Where(item => !string.IsNullOrWhiteSpace(item.Value)));
    }

    /// <summary>
    /// Where Resend opens: the composer, since no button sends an e-mail or a
    /// text on its own (operator, 10 November 2026; R915). It returns to
    /// <paramref name="returnUrl"/>.
    /// </summary>
    public static string ResendHref(Guid captureId, string returnUrl) =>
        QueryHelpers.AddQueryString($"/PhotoCapture/{captureId:D}/Resend", "returnUrl", returnUrl);

    /// <summary>A row's preview photograph: the settled asset's tile, else the staged image.</summary>
    public string PreviewHref(PhotoCapturePreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var href = preview.AssetId is { } assetId
            ? Url.Page("/Intake/Asset", new { id = preview.ReceiptId, assetId, v = preview.ContentHash })!
            : Url.Page("/Intake/Image", new { id = preview.ReceiptId })!;
        return new GalleryImage(
            href,
            href,
            preview.FileName,
            preview.MediaType,
            IntakeAssetId: preview.AssetId,
            IntakeReceiptId: preview.AssetId is null ? null : preview.ReceiptId,
            ContentHash: preview.ContentHash).TileHref;
    }
}
