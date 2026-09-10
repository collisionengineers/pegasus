namespace Pegasus.Core.Assessment;

/// <summary>
/// The top-down vehicle geometry shared by the Case damage editor and the
/// assessment report. Region codes are the canonical detailed damage zones.
/// </summary>
public static class DamageDiagramGeometry
{
    public const string ViewBox = "20 0 200 390";
    public const string BodyPath = "M75 62 L58 84 L58 316 L75 338 Q120 372 165 338 L182 316 L182 84 L165 62 Q120 8 75 62 Z";
    public const string FrontGlassPath = "M92 100 Q120 86 148 100 L152 150 L88 150 Z";
    public const string RearGlassPath = "M92 250 L148 250 L146 300 Q120 312 94 300 Z";
    public const string StrongStructuralLinesPath = "M92 150 V250 M148 150 V250";
    public const string StructuralLinesPath = "M58 150 H182 M58 250 H182";

    public static IReadOnlyList<DamageDiagramZone> Zones { get; } =
    [
        new("front_centre", "M75 30 Q120 8 165 30 L160 62 L80 62 Z"),
        new("front_left_corner", "M75 62 L58 84 L58 104 L88 104 L92 62 Z"),
        new("front_right_corner", "M165 62 L182 84 L182 104 L152 104 L148 62 Z"),
        new("bonnet", "M92 62 L148 62 L152 100 Q120 86 88 100 Z"),
        new("windscreen", FrontGlassPath),
        new("left_front_wing", "M58 104 L88 104 L88 150 L58 150 Z"),
        new("right_front_wing", "M152 104 L182 104 L182 150 L152 150 Z"),
        new("left_front_door", "M58 150 L92 150 L92 200 L58 200 Z"),
        new("right_front_door", "M148 150 L182 150 L182 200 L148 200 Z"),
        new("left_rear_door", "M58 200 L92 200 L92 250 L58 250 Z"),
        new("right_rear_door", "M148 200 L182 200 L182 250 L148 250 Z"),
        new("roof", "M88 150 L152 150 L152 250 L88 250 Z"),
        new("left_quarter", "M58 250 L94 250 L94 300 L75 338 L58 316 Z"),
        new("right_quarter", "M146 250 L182 250 L182 316 L165 338 L146 300 Z"),
        new("rear_screen", RearGlassPath),
        new("tailgate", "M94 300 Q120 312 146 300 L152 338 L88 338 Z"),
        new("rear_left_corner", "M75 338 L94 338 L101 360 Q87 356 75 338 Z"),
        new("rear_centre", "M94 338 L146 338 L139 360 Q120 369 101 360 Z"),
        new("rear_right_corner", "M146 338 L165 338 Q153 356 139 360 Z")
    ];

    public static IReadOnlyList<DamageDiagramWheel> Wheels { get; } =
    [
        new("wheel_left_front", 44, 96), new("wheel_right_front", 176, 96),
        new("wheel_left_rear", 44, 286), new("wheel_right_rear", 176, 286)
    ];

    public static IReadOnlyList<DamageDiagramMarker> Markers { get; } =
    [
        new("front_centre", 120, 44), new("front_left_corner", 73, 82), new("front_right_corner", 167, 82),
        new("bonnet", 120, 80), new("windscreen", 120, 123), new("left_front_wing", 73, 127), new("right_front_wing", 167, 127),
        new("left_front_door", 75, 175), new("right_front_door", 165, 175), new("left_rear_door", 75, 225), new("right_rear_door", 165, 225),
        new("roof", 120, 200), new("left_quarter", 76, 280), new("right_quarter", 164, 280), new("rear_screen", 120, 275), new("tailgate", 120, 318),
        new("rear_left_corner", 89, 352), new("rear_centre", 120, 357), new("rear_right_corner", 151, 352),
        new("wheel_left_front", 44, 96), new("wheel_right_front", 176, 96), new("wheel_left_rear", 44, 286), new("wheel_right_rear", 176, 286)
    ];
}

public sealed record DamageDiagramZone(string Code, string Path);

public sealed record DamageDiagramWheel(string Code, int CentreX, int CentreY);

public sealed record DamageDiagramMarker(string Code, int CentreX, int CentreY);
