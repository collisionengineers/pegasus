using Pegasus.Core.Assessment;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The Plan clicker (v26, decided 13 September): one top-down silhouette
/// drawn as the actual panels — bumper ends wrap to the corners, wings sit
/// beside the bonnet, doors beside the glasshouse, quarters beside the rear
/// screen. Presentation only: the zone codes are Core's own
/// (<see cref="DamageDiagramGeometry"/>: the 19 detailed panels and the four
/// wheels), so the recorded-zones list, the derived impact location and
/// severity and the report's diagram read the same facts. The paths are the
/// mockup's <c>PLAN_*</c> verbatim.
/// </summary>
public static class DamagePlanGeometry
{
    public const string ViewBox = "0 0 240 434";

    public const string BodyPath =
        "M72 30 C90 16 150 16 168 30 L190 60 C196 70 198 80 198 100 L198 330 C198 352 194 366 186 380 C170 410 70 410 54 380 C46 366 42 352 42 330 L42 100 C42 80 44 70 50 60 Z";

    public const string FrontGlassPath = "M58 118 H182 L172 160 C150 154 90 154 68 160 Z";

    public const string RearGlassPath = "M68 300 C90 306 150 306 172 300 L168 334 H72 Z";

    public const string SeamPath =
        "M64 46 C90 34 150 34 176 46 M64 46 L58 118 M176 46 L182 118 M176 46 L182 100 M64 46 L58 100 M58 100 H42 M182 100 H198 M68 160 V300 M172 160 V300 M42 160 H68 M172 160 H198 M42 238 H68 M172 238 H198 M42 300 H68 M172 300 H198 M42 366 H68 M172 366 H198 M72 334 H168 M68 366 Q120 392 172 366 M92 382 V402 M148 382 V402";

    public const string SoftSeamPath = "M102 54 L98 112 M138 54 L142 112";

    public static IReadOnlyList<string> FrontLampPaths { get; } =
    [
        "M74 34 Q86 27 100 28 L98 40 Q84 40 72 44 Z",
        "M166 34 Q154 27 140 28 L142 40 Q156 40 168 44 Z"
    ];

    public static IReadOnlyList<string> RearLampPaths { get; } =
    [
        "M62 372 Q72 384 92 386 L92 396 Q70 392 58 380 Z",
        "M178 372 Q168 384 148 386 L148 396 Q170 392 182 380 Z"
    ];

    /// <summary>The two door mirrors as (x, y, width, height) rects.</summary>
    public static IReadOnlyList<(int X, int Y, int Width, int Height)> Mirrors { get; } =
    [
        (22, 160, 22, 11),
        (196, 160, 22, 11)
    ];

    /// <summary>The 19 panels, in the mockup's draw order, each with its marker centre.</summary>
    public static IReadOnlyList<DamagePlanZone> Zones { get; } =
    [
        new("front_centre", "M64 0 H176 V46 C150 34 90 34 64 46 Z", 120, 30),
        new("front_right_corner", "M176 0 H240 V100 H182 L176 46 Z", 184, 68),
        new("front_left_corner", "M64 0 H0 V100 H58 L64 46 Z", 56, 68),
        new("bonnet", "M64 46 C90 34 150 34 176 46 L182 118 H58 Z", 120, 84),
        new("windscreen", "M58 118 H182 L172 160 C150 154 90 154 68 160 Z", 120, 140),
        new("right_front_wing", "M182 100 H240 V160 H172 L182 118 Z", 188, 132),
        new("left_front_wing", "M58 100 H0 V160 H68 L58 118 Z", 52, 132),
        new("right_front_door", "M172 160 H240 V238 H172 Z", 186, 199),
        new("left_front_door", "M68 160 H0 V238 H68 Z", 54, 199),
        new("right_rear_door", "M172 238 H240 V300 H172 Z", 186, 269),
        new("left_rear_door", "M68 238 H0 V300 H68 Z", 54, 269),
        new("roof", "M68 160 C90 154 150 154 172 160 V300 C150 306 90 306 68 300 Z", 120, 232),
        new("right_quarter", "M172 300 H240 V366 H172 Z", 186, 334),
        new("left_quarter", "M68 300 H0 V366 H68 Z", 54, 334),
        new("rear_screen", "M68 300 C90 306 150 306 172 300 L168 334 H72 Z", 120, 318),
        new("tailgate", "M72 334 H168 L172 366 Q120 392 68 366 Z", 120, 356),
        new("rear_right_corner", "M172 366 H240 V430 H148 V382 Q166 378 172 366 Z", 180, 390),
        new("rear_left_corner", "M68 366 H0 V430 H92 V382 Q74 378 68 366 Z", 60, 390),
        new("rear_centre", "M92 380 Q120 390 148 380 V430 H92 Z", 120, 400)
    ];

    /// <summary>The four wheels as 16×48 rounded rects centred on (x, y).</summary>
    public static IReadOnlyList<DamagePlanWheel> Wheels { get; } =
    [
        new("wheel_left_front", 36, 112),
        new("wheel_right_front", 204, 112),
        new("wheel_left_rear", 36, 324),
        new("wheel_right_rear", 204, 324)
    ];

    /// <summary>The three areas the diagram cannot draw, offered as chips.</summary>
    public static IReadOnlyList<string> ExtraZones { get; } = ["underside", "interior", "mechanical"];
}

public sealed record DamagePlanZone(string Code, string Path, int MarkerX, int MarkerY);

public sealed record DamagePlanWheel(string Code, int CentreX, int CentreY);
