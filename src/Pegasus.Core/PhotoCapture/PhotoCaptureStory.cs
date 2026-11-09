using System.Globalization;
using Pegasus.Core.Documents;

namespace Pegasus.Core.PhotoCapture;

/// <summary>
/// The one owner of a capture's story: the order its items are shown and
/// named in, the words the names use, and what each kind arrives as on the
/// Case (FRD-28, FRD-05). The order is the app's own: the overview all the
/// way round, each damaged area in marking order through its four stages,
/// the number plate, the VIN, the dashboard, then the additional information
/// that never goes into the report by itself.
/// </summary>
public static class PhotoCaptureStory
{
    /// <summary>The twelve damage zones the app's car diagram offers, in clockwise order from the front, keyed as the app keys them.</summary>
    public static IReadOnlyList<(string Key, string Label)> Zones { get; } =
    [
        ("front", "Front"),
        ("fd", "Front driver corner"),
        ("ds-f", "Driver side front"),
        ("ds-m", "Driver side middle"),
        ("ds-r", "Driver side rear"),
        ("rd", "Rear driver corner"),
        ("rear", "Rear"),
        ("rp", "Rear passenger corner"),
        ("ps-r", "Passenger side rear"),
        ("ps-m", "Passenger side middle"),
        ("ps-f", "Passenger side front"),
        ("fp", "Front passenger corner")
    ];

    /// <summary>The eight overview angles, clockwise from the front, by position 1 to 8.</summary>
    public static IReadOnlyList<string> OverviewAngles { get; } =
    [
        "Front",
        "Front driver corner",
        "Driver side",
        "Rear driver corner",
        "Rear",
        "Rear passenger corner",
        "Passenger side",
        "Front passenger corner"
    ];

    public const int OverviewCount = 8;

    /// <summary>The story groups that are not a damaged area; areas take the numbers between Overview and Number plate.</summary>
    public const int OverviewGroup = 1;
    private const int FirstAreaGroup = 2;
    public const int MaximumAreas = 12;
    public const int NumberPlateGroup = FirstAreaGroup + MaximumAreas;
    public const int VinGroup = NumberPlateGroup + 1;
    public const int DashboardGroup = VinGroup + 1;
    public const int SceneGroup = DashboardGroup + 1;
    public const int OtherVehicleGroup = SceneGroup + 1;
    public const int PaperworkGroup = OtherVehicleGroup + 1;
    public const int VideoGroup = PaperworkGroup + 1;

    public static bool IsZone(string? key) =>
        key is not null && Zones.Any(zone => string.Equals(zone.Key, key, StringComparison.Ordinal));

    public static string ZoneLabel(string key) =>
        Zones.FirstOrDefault(zone => string.Equals(zone.Key, key, StringComparison.Ordinal)).Label
        ?? throw new ArgumentException($"'{key}' is not a damage zone.", nameof(key));

    /// <summary>
    /// The story group an item sits in; a damaged area's group follows the
    /// app's own area order (<see cref="PhotoCaptureAreas.Derive"/>), the
    /// item's area being the key zone the app tagged it with.
    /// </summary>
    public static int Group(PhotoCaptureItemKind kind, string? area, IReadOnlyList<string> markedZones)
    {
        ArgumentNullException.ThrowIfNull(markedZones);
        switch (kind)
        {
            case PhotoCaptureItemKind.Overview:
                return OverviewGroup;
            case PhotoCaptureItemKind.Damage:
                var areas = PhotoCaptureAreas.Derive(markedZones);
                var found = PhotoCaptureAreas.Find(areas, area);
                var index = found is null ? -1 : areas.ToList().IndexOf(found);
                return FirstAreaGroup + (index < 0 ? Math.Min(areas.Count, MaximumAreas - 1) : Math.Min(index, MaximumAreas - 1));
            case PhotoCaptureItemKind.NumberPlate:
                return NumberPlateGroup;
            case PhotoCaptureItemKind.Vin:
                return VinGroup;
            case PhotoCaptureItemKind.Dashboard:
                return DashboardGroup;
            case PhotoCaptureItemKind.Scene:
                return SceneGroup;
            case PhotoCaptureItemKind.OtherVehicle:
                return OtherVehicleGroup;
            case PhotoCaptureItemKind.Paperwork:
                return PaperworkGroup;
            case PhotoCaptureItemKind.Video:
                return VideoGroup;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    /// <summary>
    /// The order inside a group: an overview by its angle, a damage photo by
    /// stage then by the order it was taken, everything else by the order it
    /// was taken.
    /// </summary>
    public static int Sequence(PhotoCaptureItemKind kind, PhotoCaptureStage? stage, int position) => kind switch
    {
        PhotoCaptureItemKind.Overview => position,
        PhotoCaptureItemKind.Damage => (int)(stage ?? PhotoCaptureStage.Extra) * 100 + position,
        _ => position
    };

    /// <summary>The operator-facing heading of one group, for the Case's Files → Images story and the Box name.</summary>
    public static string GroupLabel(int group, IReadOnlyList<string> markedZones)
    {
        ArgumentNullException.ThrowIfNull(markedZones);
        if (group == OverviewGroup)
        {
            return "Overview all the way round";
        }

        if (group >= FirstAreaGroup && group < NumberPlateGroup)
        {
            var index = group - FirstAreaGroup;
            var areas = PhotoCaptureAreas.Derive(markedZones);
            return index < areas.Count
                ? $"Damage – {areas[index].Label}"
                : "Damage";
        }

        return group switch
        {
            NumberPlateGroup => "Number plate",
            VinGroup => "VIN",
            DashboardGroup => "Dashboard",
            SceneGroup => "Accident scene",
            OtherVehicleGroup => "Other vehicle",
            PaperworkGroup => "Paperwork",
            VideoGroup => "Video",
            _ => "Photos"
        };
    }

    public static string StageLabel(PhotoCaptureStage stage) => stage switch
    {
        PhotoCaptureStage.Overview => "overview",
        PhotoCaptureStage.Closer => "closer",
        PhotoCaptureStage.CloseUp => "close-up",
        PhotoCaptureStage.Extra => "extra",
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    /// <summary>
    /// The item's file name: <c>GG-SS label.ext</c>, so the Case folder's own
    /// numbering (<c>NNN name</c>) is followed by the story position and a
    /// plain label. The Case folder stays flat (operator direction,
    /// 21 August 2026; 9 November 2026). The label uses only the app's own
    /// words: the zone and angle names and the stage.
    /// </summary>
    public static string FileName(
        PhotoCaptureItemKind kind,
        string? area,
        PhotoCaptureStage? stage,
        int position,
        int group,
        int sequence,
        string mediaType,
        string? originalFileName = null)
    {
        var label = kind switch
        {
            PhotoCaptureItemKind.Overview => position is >= 1 and <= OverviewCount
                ? OverviewAngles[position - 1]
                : "Overview",
            PhotoCaptureItemKind.Damage => (area is not null && IsZone(area) ? ZoneLabel(area) : "Damage")
                + (stage is { } s ? " " + StageLabel(s) : string.Empty),
            PhotoCaptureItemKind.NumberPlate => "Number plate",
            PhotoCaptureItemKind.Vin => "VIN",
            PhotoCaptureItemKind.Dashboard => "Dashboard",
            PhotoCaptureItemKind.Scene => "Accident scene",
            PhotoCaptureItemKind.OtherVehicle => "Other vehicle",
            PhotoCaptureItemKind.Paperwork => Path.GetFileNameWithoutExtension(originalFileName) is { Length: > 0 } paperwork
                ? "Paperwork " + paperwork
                : "Paperwork",
            PhotoCaptureItemKind.Video => "Walk-around video",
            _ => "Photo"
        };
        var name = string.Format(
            CultureInfo.InvariantCulture,
            "{0:00}-{1:00} {2}{3}",
            group,
            sequence % 100,
            label,
            ExtensionFor(mediaType, originalFileName));
        return name.Length <= 120 ? name : name[..120];
    }

    /// <summary>Inspection images arrive In report; additional information does not (operator, 9 November 2026).</summary>
    public static bool StartsInReport(PhotoCaptureItemKind kind) => kind is
        PhotoCaptureItemKind.Overview
        or PhotoCaptureItemKind.Damage
        or PhotoCaptureItemKind.NumberPlate
        or PhotoCaptureItemKind.Vin
        or PhotoCaptureItemKind.Dashboard;

    /// <summary>Whether the kind is one of the inspection images, as against additional information.</summary>
    public static bool IsInspectionImage(PhotoCaptureItemKind kind) => StartsInReport(kind);

    /// <summary>The tag an item carries on arrival: another vehicle's photo is Third party (CONTEXT: Third-party vehicle evidence).</summary>
    public static IReadOnlyList<Guid> ArrivalTags(PhotoCaptureItemKind kind) =>
        kind == PhotoCaptureItemKind.OtherVehicle ? [ImageTagVocabulary.ThirdPartyId] : [];

    /// <summary>What a non-image item files as on the Case: paperwork and video are documents of type Other.</summary>
    public static DocumentSemanticRole? DeclaredDocumentType(PhotoCaptureItemKind kind, string mediaType) =>
        InstructionEvidenceImagesMediaTypes.IsImage(mediaType) ? null : DocumentSemanticRole.Other;

    public static bool IsPhotographKind(PhotoCaptureItemKind kind) => kind is not (PhotoCaptureItemKind.Paperwork or PhotoCaptureItemKind.Video);

    public static bool AcceptsMediaType(PhotoCaptureItemKind kind, string mediaType) => kind switch
    {
        PhotoCaptureItemKind.Video => mediaType is Intake.IntakeUploadFilePolicy.Mp4MediaType
            or Intake.IntakeUploadFilePolicy.MovMediaType,
        PhotoCaptureItemKind.Paperwork => mediaType is "application/pdf" or "image/jpeg" or "image/png",
        _ => mediaType is "image/jpeg" or "image/png"
    };

    private static string ExtensionFor(string mediaType, string? originalFileName)
    {
        var extension = Intake.IntakeUploadFilePolicy.ExtensionFor(mediaType);
        if (extension.Length > 0)
        {
            return extension == ".jpeg" ? ".jpg" : extension;
        }

        var original = Path.GetExtension(originalFileName ?? string.Empty);
        return original.Length is > 0 and <= 5 ? original.ToLowerInvariant() : string.Empty;
    }
}

/// <summary>The media types a photograph has, the one place the capture asks the question.</summary>
internal static class InstructionEvidenceImagesMediaTypes
{
    public static bool IsImage(string mediaType) =>
        string.Equals(mediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "image/png", StringComparison.OrdinalIgnoreCase);
}
