using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.PhotoCapture;
using CaptureRecord = Pegasus.Core.PhotoCapture.PhotoCapture;

namespace Pegasus.Web.PhotoCapture;

/// <summary>
/// What the capture app is told about the link it was opened on: the page
/// embeds it and the API's context read returns it. It never carries the
/// token; <see cref="LinkKey"/> is a stable opaque value the app keys its
/// local queue by. <see cref="RegistrationDisplay"/> is the registration as
/// a plate shows it, the way the hand-off message spaces it.
/// <see cref="QuickLinkFor"/> names the firm a quick link sent from a Case
/// is for, when the app shows that firm's staff start and form (R798, R808).
/// </summary>
public sealed record CaptureContext(
    string Kind,
    string Mode,
    bool LiveCameraOnly,
    bool Originals,
    string Origin,
    string? QuickLinkFor,
    string? Registration,
    string? RegistrationDisplay,
    string? CustomerFirstName,
    DateTimeOffset? ExpiresAtUtc,
    bool Sent,
    string LinkKey,
    CaptureDetailsView? Details,
    CaptureCountsView? Counts,
    CaptureStandingView? Standing);

public sealed record CaptureDetailsView(
    string? Handler,
    string? InspectionLocation,
    string? InspectionLocationChoice,
    string? Roadworthy,
    string? Outcome,
    string? InHire,
    string? RepairView,
    string? VehicleRole,
    long? Mileage,
    string? Airbags,
    string? Steering,
    IReadOnlyList<string> DamageAreas,
    bool NoKeys);

public sealed record CaptureCountsView(
    int Total,
    IReadOnlyDictionary<string, int> ByKind,
    IReadOnlyList<int> OverviewPositions,
    IReadOnlyList<CaptureDamageCount> Damage);

public sealed record CaptureDamageCount(string Area, string Stage, int Count);

public sealed record CaptureStandingView(
    string? Name,
    string Kind,
    bool IsRepairer,
    IReadOnlyList<StandingPhotoLinkAddress> Addresses,
    string ForwardedMode,
    bool LiveCameraOnly,
    int LifetimeDays);

/// <summary>The wire codes of the capture surface's enumerations, both ways; one owner for the page and the API.</summary>
public static class CaptureCodes
{
    public const string CaptureKind = "capture";
    public const string StandingKind = "standing";

    /// <summary>The standing view's kind for a quick link sent from a Case: neither a contact's link nor the website's.</summary>
    public const string CaseKind = "case";

    private static readonly (PhotoCaptureItemKind Kind, string Code)[] ItemKinds =
    [
        (PhotoCaptureItemKind.Overview, "overview"),
        (PhotoCaptureItemKind.Damage, "damage"),
        (PhotoCaptureItemKind.NumberPlate, "plate"),
        (PhotoCaptureItemKind.Vin, "vin"),
        (PhotoCaptureItemKind.Dashboard, "dashboard"),
        (PhotoCaptureItemKind.Scene, "scene"),
        (PhotoCaptureItemKind.OtherVehicle, "other-vehicle"),
        (PhotoCaptureItemKind.Paperwork, "paperwork"),
        (PhotoCaptureItemKind.Video, "video")
    ];

    private static readonly PhotoCaptureStage[] Stages =
    [
        PhotoCaptureStage.Overview,
        PhotoCaptureStage.Closer,
        PhotoCaptureStage.CloseUp,
        PhotoCaptureStage.Extra
    ];

    public static string Kind(PhotoCaptureItemKind kind) =>
        ItemKinds.First(entry => entry.Kind == kind).Code;

    public static PhotoCaptureItemKind? ParseKind(string? code)
    {
        foreach (var entry in ItemKinds)
        {
            if (string.Equals(entry.Code, code, StringComparison.Ordinal))
            {
                return entry.Kind;
            }
        }

        return null;
    }

    public static string Stage(PhotoCaptureStage stage) => PhotoCaptureStory.StageLabel(stage);

    public static PhotoCaptureStage? ParseStage(string? code)
    {
        foreach (var stage in Stages)
        {
            if (string.Equals(PhotoCaptureStory.StageLabel(stage), code, StringComparison.Ordinal))
            {
                return stage;
            }
        }

        return null;
    }

    public static string Mode(PhotoCaptureMode mode) => mode == PhotoCaptureMode.Quick ? "quick" : "guided";

    public static string QuickLinkFor(PhotoCaptureQuickLinkFor quickLinkFor) => quickLinkFor switch
    {
        PhotoCaptureQuickLinkFor.AccidentManagement => "accident-management",
        PhotoCaptureQuickLinkFor.Bodyshop => "bodyshop",
        _ => throw new ArgumentOutOfRangeException(nameof(quickLinkFor))
    };

    public static string Origin(PhotoCaptureOrigin origin) => origin switch
    {
        PhotoCaptureOrigin.CaseLink => "case",
        PhotoCaptureOrigin.StandingStaff => "staff",
        PhotoCaptureOrigin.StandingForwarded => "forwarded",
        PhotoCaptureOrigin.Website => "website",
        _ => throw new ArgumentOutOfRangeException(nameof(origin))
    };

    public static IEnumerable<string> AllKinds => ItemKinds.Select(entry => entry.Code);
}

/// <summary>
/// Builds <see cref="CaptureContext"/> for a capture or a standing link. One
/// item list and, for a capture started from a standing link, that link and
/// its addresses are read; nothing is written. A quick link sent from a Case
/// opens its firm's staff start and form, read from the Case's Principal; the
/// customer it is handed on to, on the Handoff token, gets the guided app
/// (R798, R808).
/// </summary>
public sealed class CaptureContextFactory(
    IPhotoCaptureStore captures,
    IStandingPhotoLinkStore standingLinks,
    IPhotoCaptureCaseReader cases,
    TimeProvider timeProvider)
{
    /// <summary>
    /// camelCase, and the default encoder, which escapes <c>&lt;</c>, <c>&gt;</c>
    /// and <c>&amp;</c>, so a typed value cannot close the page's script block.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    /// <param name="tokenPurpose">The presented token's purpose: a quick Case link's Handoff token is its customer's.</param>
    public async Task<CaptureContext> ForCaptureAsync(
        CaptureRecord capture,
        PhotoLinkTokenPurpose? tokenPurpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var items = (await captures.ListItemsAsync(capture.Id, cancellationToken))
            .Where(item => item.StagedReceiptId is not null)
            .ToArray();
        var link = capture.StandingLinkId is { } linkId
            ? await standingLinks.GetAsync(linkId, cancellationToken)
            : null;
        var quickCaseLink = HandOnCaseLink.IsQuickCaseLink(capture);
        var customerOfHandOn = quickCaseLink && tokenPurpose == PhotoLinkTokenPurpose.Handoff;
        var staffFor = quickCaseLink && !customerOfHandOn ? capture.QuickLinkFor : null;
        var standing = link is not null
            ? await StandingAsync(link, cancellationToken)
            : staffFor is { } quickLinkFor
                ? await QuickCaseLinkAsync(capture, quickLinkFor, cancellationToken)
                : null;
        return new(
            CaptureCodes.CaptureKind,
            // The customer a quick Case link is handed on to takes the guided app (R798).
            customerOfHandOn ? CaptureCodes.Mode(PhotoCaptureMode.Guided) : CaptureCodes.Mode(capture.Mode),
            capture.LiveCameraOnly,
            // Off while the capture has no Case, so the phone sends and
            // retries no original (R801).
            capture.TakesOriginals,
            CaptureCodes.Origin(capture.Origin),
            staffFor is { } staffQuickLinkFor ? CaptureCodes.QuickLinkFor(staffQuickLinkFor) : null,
            capture.NormalizedRegistration,
            capture.NormalizedRegistration is { } registration ? PhotoCaptureText.DisplayRegistration(registration) : null,
            FirstName(capture.CustomerName),
            capture.ExpiresAtUtc,
            capture.SentAtUtc is not null,
            LinkKey(capture.Id),
            Details(capture.Details),
            Counts(items),
            standing);
    }

    public async Task<CaptureContext> ForStandingAsync(StandingPhotoLink link, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);
        var website = link.Kind == StandingPhotoLinkKind.Website;
        return new(
            CaptureCodes.StandingKind,
            // What a capture started here is: guided on the website, quick for a contact's own staff.
            website ? CaptureCodes.Mode(PhotoCaptureMode.Guided) : CaptureCodes.Mode(PhotoCaptureMode.Quick),
            link.LiveCameraOnly,
            link.OriginalsInBackground,
            website ? CaptureCodes.Origin(PhotoCaptureOrigin.Website) : CaptureCodes.Origin(PhotoCaptureOrigin.StandingStaff),
            QuickLinkFor: null,
            Registration: null,
            RegistrationDisplay: null,
            CustomerFirstName: null,
            ExpiresAtUtc: null,
            Sent: false,
            link.Slug,
            Details: null,
            Counts: null,
            await StandingAsync(link, cancellationToken));
    }

    /// <summary>The capture id's SHA-256, base64url: stable for the capture, and naming nothing a store can be asked for.</summary>
    public static string LinkKey(Guid captureId) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(captureId.ToString("N"))));

    public static string Serialize(CaptureContext context) => JsonSerializer.Serialize(context, JsonOptions);

    private async Task<CaptureStandingView> StandingAsync(StandingPhotoLink link, CancellationToken cancellationToken)
    {
        var addresses = await standingLinks.ListAddressesAsync(link.Id, cancellationToken);
        return new(
            link.OrganizationName,
            link.Kind == StandingPhotoLinkKind.Website ? "website" : "contact",
            link.IsRepairer,
            addresses,
            CaptureCodes.Mode(link.ForwardedMode),
            link.LiveCameraOnly,
            link.LinkLifetimeDays);
    }

    /// <summary>
    /// A quick Case link's firm, as the start and form need it: for Accident
    /// management the Case's Principal, with the saved addresses of the
    /// Principal's own standing link when it has one; a Bodyshop is not the
    /// Principal, so it is named nowhere and offered no saved address. The
    /// customer link it hands on is guided and lasts as long as this link.
    /// </summary>
    private async Task<CaptureStandingView> QuickCaseLinkAsync(
        CaptureRecord capture,
        PhotoCaptureQuickLinkFor quickLinkFor,
        CancellationToken cancellationToken)
    {
        string? name = null;
        IReadOnlyList<StandingPhotoLinkAddress> addresses = [];
        if (quickLinkFor == PhotoCaptureQuickLinkFor.AccidentManagement
            && capture.CaseId is { } caseId
            && await cases.GetAsync(caseId, cancellationToken) is { } facts)
        {
            name = facts.PrincipalName;
            if (facts.PrincipalOrganizationId is { } organizationId
                && await standingLinks.FindForOrganizationAsync(organizationId, cancellationToken) is { } principalLink)
            {
                addresses = await standingLinks.ListAddressesAsync(principalLink.Id, cancellationToken);
            }
        }

        var daysLeft = (int)Math.Ceiling((capture.ExpiresAtUtc - timeProvider.GetUtcNow()).TotalDays);
        return new(
            name,
            CaptureCodes.CaseKind,
            quickLinkFor == PhotoCaptureQuickLinkFor.Bodyshop,
            addresses,
            CaptureCodes.Mode(PhotoCaptureMode.Guided),
            capture.LiveCameraOnly,
            Math.Max(1, daysLeft));
    }

    private static string? FirstName(string? customerName) =>
        (customerName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

    private static CaptureDetailsView? Details(PhotoCaptureDetails? details) =>
        details is null
            ? null
            : new(
                details.Handler,
                details.InspectionLocation,
                details.InspectionLocationChoice,
                details.Roadworthy,
                details.Outcome,
                details.InHire,
                details.RepairView,
                details.VehicleRole,
                details.Mileage,
                details.Airbags,
                details.Steering,
                details.MarkedAreas,
                details.NoKeys);

    private static CaptureCountsView Counts(PhotoCaptureItem[] items)
    {
        var byKind = CaptureCodes.AllKinds.ToDictionary(code => code, _ => 0, StringComparer.Ordinal);
        foreach (var item in items)
        {
            byKind[CaptureCodes.Kind(item.Kind)]++;
        }

        var overviewPositions = items
            .Where(item => item.Kind == PhotoCaptureItemKind.Overview)
            .Select(item => item.Position)
            .Distinct()
            .Order()
            .ToArray();
        var damage = items
            .Where(item => item.Kind == PhotoCaptureItemKind.Damage && item.Area is not null && item.Stage is not null)
            .GroupBy(item => (Area: item.Area!, Stage: item.Stage!.Value))
            .OrderBy(group => group.Key.Area, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Stage)
            .Select(group => new CaptureDamageCount(group.Key.Area, CaptureCodes.Stage(group.Key.Stage), group.Count()))
            .ToArray();
        return new(items.Length, byKind, overviewPositions, damage);
    }
}
