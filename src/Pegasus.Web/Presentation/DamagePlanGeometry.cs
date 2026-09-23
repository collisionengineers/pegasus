using Pegasus.Core.Assessment;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The plan (v28 P5): one top-down silhouette drawn as the actual panels —
/// bumper ends wrap to the corners, wings sit beside the bonnet, doors beside
/// the glasshouse, quarters beside the rear screen. Presentation only: the
/// areas, the bands that divide the plan and the disc a damage is drawn as
/// are Core's own (<see cref="DamageAreaGeometry"/>), mapped onto this
/// silhouette's body box, so the discs here and on the report read the same
/// facts. The paths are the mockup's <c>PLAN_*</c> verbatim.
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

    /// <summary>
    /// The body box the unit plan maps onto: x 42 to 198, y 16 to 410. Core's
    /// canonical plan has this box's shape, so a disc touches the same areas
    /// here as where Core judges them.
    /// </summary>
    public const int PlanLeft = 42;
    public const int PlanTop = 16;
    public const int PlanWidth = 156;
    public const int PlanHeight = 394;

    /// <summary>The dashed band lines shown while editing: Core's bands on this plan.</summary>
    public static string GuidesPath { get; } = FormattableString.Invariant(
        $"M{PlanLeft} {PlanY(DamageAreaGeometry.FrontBand)} H{PlanLeft + PlanWidth} M{PlanLeft} {PlanY(DamageAreaGeometry.RearBand)} H{PlanLeft + PlanWidth} M{PlanX(DamageAreaGeometry.LeftBand)} {PlanTop} V{PlanTop + PlanHeight} M{PlanX(DamageAreaGeometry.RightBand)} {PlanTop} V{PlanTop + PlanHeight}");

    /// <summary>
    /// The disc a damage draws as, in this plan's coordinates: its drawn disc,
    /// or for a damage recorded by area the disc its areas give; null for the
    /// other areas.
    /// </summary>
    public static DamageDisc? Disc(AssessmentImpact impact)
    {
        ArgumentNullException.ThrowIfNull(impact);
        var disc = DamageAreaGeometry.RenderDisc(impact.Areas, PlanWidth, PlanHeight, impact.Disc);
        return disc is null ? null : disc with { CentreX = disc.CentreX + PlanLeft, CentreY = disc.CentreY + PlanTop };
    }

    private static double PlanX(double fraction) => Math.Round(PlanLeft + fraction * PlanWidth, 1);

    private static double PlanY(double fraction) => Math.Round(PlanTop + fraction * PlanHeight, 1);

    /// <summary>The four wheels as 16×48 rounded rects centred on (x, y).</summary>
    public static IReadOnlyList<DamagePlanWheel> Wheels { get; } =
    [
        new("wheel_left_front", 36, 112),
        new("wheel_right_front", 204, 112),
        new("wheel_left_rear", 36, 324),
        new("wheel_right_rear", 204, 324)
    ];
}

public sealed record DamagePlanWheel(string Code, int CentreX, int CentreY);
