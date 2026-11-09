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
/// local queue by.
/// </summary>
public sealed record CaptureContext(
    string Kind,
    string Mode,
    bool LiveCameraOnly,
    bool Originals,
    string Origin,
    string? Registration,
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
    IReadOnlyList<string> DamageAreas);

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
/// its addresses are read; nothing is written.
/// </summary>
public sealed class CaptureContextFactory(
    IPhotoCaptureStore captures,
    IStandingPhotoLinkStore standingLinks)
{
    /// <summary>
    /// camelCase, and the default encoder, which escapes <c>&lt;</c>, <c>&gt;</c>
    /// and <c>&amp;</c>, so a typed value cannot close the page's script block.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public async Task<CaptureContext> ForCaptureAsync(CaptureRecord capture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var items = (await captures.ListItemsAsync(capture.Id, cancellationToken))
            .Where(item => item.StagedReceiptId is not null)
            .ToArray();
        var link = capture.StandingLinkId is { } linkId
            ? await standingLinks.GetAsync(linkId, cancellationToken)
            : null;
        return new(
            CaptureCodes.CaptureKind,
            CaptureCodes.Mode(capture.Mode),
            capture.LiveCameraOnly,
            // Off while the capture has no Case, so the phone sends and
            // retries no original (R801).
            capture.TakesOriginals,
            CaptureCodes.Origin(capture.Origin),
            capture.NormalizedRegistration,
            FirstName(capture.CustomerName),
            capture.ExpiresAtUtc,
            capture.SentAtUtc is not null,
            LinkKey(capture.Id),
            Details(capture.Details),
            Counts(items),
            link is null ? null : await StandingAsync(link, cancellationToken));
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
            Registration: null,
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
                details.MarkedAreas);

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
