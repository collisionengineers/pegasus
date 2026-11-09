using Pegasus.Core.PhotoCapture;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one owner of the stored codes of the photo capture tables (FRD-28): a
/// short lowercase code per enum value, as the intake tables store theirs.
/// The check constraints are built from the same lists.
/// </summary>
internal static class PhotoCaptureCodes
{
    public static readonly IReadOnlyList<string> Origins = Enum.GetValues<PhotoCaptureOrigin>().Select(value => ToCode(value)).ToArray();
    public static readonly IReadOnlyList<string> Destinations = Enum.GetValues<PhotoCaptureDestination>().Select(value => ToCode(value)).ToArray();
    public static readonly IReadOnlyList<string> Modes = Enum.GetValues<PhotoCaptureMode>().Select(value => ToCode(value)).ToArray();
    public static readonly IReadOnlyList<string> ItemKinds = Enum.GetValues<PhotoCaptureItemKind>().Select(value => ToCode(value)).ToArray();
    public static readonly IReadOnlyList<string> Stages = Enum.GetValues<PhotoCaptureStage>().Select(value => ToCode(value)).ToArray();
    public static readonly IReadOnlyList<string> StandingLinkKinds = Enum.GetValues<StandingPhotoLinkKind>().Select(value => ToCode(value)).ToArray();
    public static readonly IReadOnlyList<string> TokenPurposes = Enum.GetValues<PhotoLinkTokenPurpose>().Select(value => ToCode(value)).ToArray();

    public static string ToCode(PhotoCaptureOrigin value) => value switch
    {
        PhotoCaptureOrigin.CaseLink => "case_link",
        PhotoCaptureOrigin.StandingStaff => "standing_staff",
        PhotoCaptureOrigin.StandingForwarded => "standing_forwarded",
        PhotoCaptureOrigin.Website => "website",
        _ => throw UnknownEnum(value)
    };

    public static PhotoCaptureOrigin ParseOrigin(string value) => value switch
    {
        "case_link" => PhotoCaptureOrigin.CaseLink,
        "standing_staff" => PhotoCaptureOrigin.StandingStaff,
        "standing_forwarded" => PhotoCaptureOrigin.StandingForwarded,
        "website" => PhotoCaptureOrigin.Website,
        _ => throw UnknownCode("photo capture origin", value)
    };

    public static string ToCode(PhotoCaptureDestination value) => value switch
    {
        PhotoCaptureDestination.Undecided => "undecided",
        PhotoCaptureDestination.Case => "case",
        PhotoCaptureDestination.VehicleImages => "vehicle_images",
        _ => throw UnknownEnum(value)
    };

    public static PhotoCaptureDestination ParseDestination(string value) => value switch
    {
        "undecided" => PhotoCaptureDestination.Undecided,
        "case" => PhotoCaptureDestination.Case,
        "vehicle_images" => PhotoCaptureDestination.VehicleImages,
        _ => throw UnknownCode("photo capture destination", value)
    };

    public static string ToCode(PhotoCaptureMode value) => value switch
    {
        PhotoCaptureMode.Guided => "guided",
        PhotoCaptureMode.Quick => "quick",
        _ => throw UnknownEnum(value)
    };

    public static PhotoCaptureMode ParseMode(string value) => value switch
    {
        "guided" => PhotoCaptureMode.Guided,
        "quick" => PhotoCaptureMode.Quick,
        _ => throw UnknownCode("photo capture mode", value)
    };

    public static string ToCode(PhotoCaptureItemKind value) => value switch
    {
        PhotoCaptureItemKind.Overview => "overview",
        PhotoCaptureItemKind.Damage => "damage",
        PhotoCaptureItemKind.NumberPlate => "number_plate",
        PhotoCaptureItemKind.Vin => "vin",
        PhotoCaptureItemKind.Dashboard => "dashboard",
        PhotoCaptureItemKind.Scene => "scene",
        PhotoCaptureItemKind.OtherVehicle => "other_vehicle",
        PhotoCaptureItemKind.Paperwork => "paperwork",
        PhotoCaptureItemKind.Video => "video",
        _ => throw UnknownEnum(value)
    };

    public static PhotoCaptureItemKind ParseItemKind(string value) => value switch
    {
        "overview" => PhotoCaptureItemKind.Overview,
        "damage" => PhotoCaptureItemKind.Damage,
        "number_plate" => PhotoCaptureItemKind.NumberPlate,
        "vin" => PhotoCaptureItemKind.Vin,
        "dashboard" => PhotoCaptureItemKind.Dashboard,
        "scene" => PhotoCaptureItemKind.Scene,
        "other_vehicle" => PhotoCaptureItemKind.OtherVehicle,
        "paperwork" => PhotoCaptureItemKind.Paperwork,
        "video" => PhotoCaptureItemKind.Video,
        _ => throw UnknownCode("photo capture item kind", value)
    };

    public static string ToCode(PhotoCaptureStage value) => value switch
    {
        PhotoCaptureStage.Overview => "overview",
        PhotoCaptureStage.Closer => "closer",
        PhotoCaptureStage.CloseUp => "close_up",
        PhotoCaptureStage.Extra => "extra",
        _ => throw UnknownEnum(value)
    };

    public static string? ToCode(PhotoCaptureStage? value) => value is { } stage ? ToCode(stage) : null;

    public static PhotoCaptureStage ParseStage(string value) => value switch
    {
        "overview" => PhotoCaptureStage.Overview,
        "closer" => PhotoCaptureStage.Closer,
        "close_up" => PhotoCaptureStage.CloseUp,
        "extra" => PhotoCaptureStage.Extra,
        _ => throw UnknownCode("photo capture stage", value)
    };

    public static PhotoCaptureStage? ParseOptionalStage(string? value) => value is null ? null : ParseStage(value);

    public static string ToCode(StandingPhotoLinkKind value) => value switch
    {
        StandingPhotoLinkKind.Contact => "contact",
        StandingPhotoLinkKind.Website => "website",
        _ => throw UnknownEnum(value)
    };

    public static StandingPhotoLinkKind ParseStandingLinkKind(string value) => value switch
    {
        "contact" => StandingPhotoLinkKind.Contact,
        "website" => StandingPhotoLinkKind.Website,
        _ => throw UnknownCode("standing photo link kind", value)
    };

    public static string ToCode(PhotoLinkTokenPurpose value) => value switch
    {
        PhotoLinkTokenPurpose.Email => "email",
        PhotoLinkTokenPurpose.Copy => "copy",
        PhotoLinkTokenPurpose.Handoff => "handoff",
        PhotoLinkTokenPurpose.Session => "session",
        _ => throw UnknownEnum(value)
    };

    public static PhotoLinkTokenPurpose ParseTokenPurpose(string value) => value switch
    {
        "email" => PhotoLinkTokenPurpose.Email,
        "copy" => PhotoLinkTokenPurpose.Copy,
        "handoff" => PhotoLinkTokenPurpose.Handoff,
        "session" => PhotoLinkTokenPurpose.Session,
        _ => throw UnknownCode("photo link token purpose", value)
    };

    /// <summary>The SQL list of a check constraint: <c>'a', 'b'</c>.</summary>
    public static string SqlList(IEnumerable<string> codes) =>
        string.Join(", ", codes.Select(code => $"'{code}'"));

    private static InvalidOperationException UnknownEnum<T>(T value)
        where T : struct, Enum =>
        new($"Unknown {typeof(T).Name} value '{value}'.");

    private static InvalidDataException UnknownCode(string what, string value) =>
        new($"Unknown persisted {what} code '{value}'.");
}
