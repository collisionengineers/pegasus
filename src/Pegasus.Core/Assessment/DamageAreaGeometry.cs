namespace Pegasus.Core.Assessment;

/// <summary>
/// The plan areas as geometry (v28 P5): where each of the eight areas sits on
/// a unit plan (x from 0 at the left to 1 at the right, y from 0 at the front
/// to 1 at the rear), the bands that divide the plan into them, and the disc a
/// recorded damage is drawn as. A damage drawn on the plan keeps its disc as
/// drawn and names the areas that disc touches (ruled 23 September 2026,
/// replacing the 20 September areas-only record); a damage recorded by area
/// alone is drawn with the disc <see cref="Disc"/> gives its areas. The Case
/// workspace and the assessment report map the unit plan onto their own
/// silhouettes, so both draw the same disc.
/// </summary>
public static class DamageAreaGeometry
{
    /// <summary>The front ends here and the rear begins at <see cref="RearBand"/>; between them are the sides.</summary>
    public const double FrontBand = 0.34;
    public const double RearBand = 0.72;

    /// <summary>The left ends here and the right begins at <see cref="RightBand"/>; between them is the centre.</summary>
    public const double LeftBand = 0.372;
    public const double RightBand = 0.628;

    /// <summary>A one-area disc's radius, and the margin a wider disc keeps beyond its areas, as fractions of the plan's width.</summary>
    public const double BaseRadius = 0.12;
    public const double Margin = 0.08;

    /// <summary>
    /// The smallest and largest disc the operator can draw, as fractions of the
    /// plan's width: the smallest is ten units of the workspace's 156-unit body
    /// box, and no disc is wider than the vehicle.
    /// </summary>
    public const double MinRadius = 10d / 156d;
    public const double MaxRadius = 0.5;

    /// <summary>
    /// The canonical plan Core judges coverage on: one unit wide and as tall as
    /// the Case workspace's body box (156 by 394), so the areas a disc touches
    /// here are the areas it touches where the operator draws it. A disc's
    /// radius is a fraction of the width, so it stays round on every renderer.
    /// </summary>
    public const double CanonicalWidth = 1;
    public const double CanonicalHeight = 394d / 156d;

    /// <summary>The centre of each plan area on the unit plan.</summary>
    public static IReadOnlyDictionary<string, DamagePlanPoint> Centres { get; } =
        new Dictionary<string, DamagePlanPoint>(StringComparer.Ordinal)
        {
            ["front"] = new(0.5, 0.1), ["left_front"] = new(0.18, 0.2), ["right_front"] = new(0.82, 0.2),
            ["left_side"] = new(0.1, 0.53), ["right_side"] = new(0.9, 0.53), ["rear"] = new(0.5, 0.92),
            ["left_rear"] = new(0.18, 0.84), ["right_rear"] = new(0.82, 0.84)
        };

    /// <summary>
    /// The disc drawn for a damage recorded by area alone, in unit-plan terms
    /// (centre as fractions of the plan, radius as a fraction of its width):
    /// centred between the areas and wide enough to reach them, never wider
    /// than <see cref="MaxRadius"/>. Null when the damage names no plan area.
    /// </summary>
    public static DamageDisc? Disc(IReadOnlyList<string> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        var points = areas
            .Where(Centres.ContainsKey)
            .Select(area => Centres[area])
            .ToArray();
        if (points.Length == 0)
        {
            return null;
        }
        var centreX = points.Average(point => point.X);
        var centreY = points.Average(point => point.Y);
        // The spread is measured on the canonical plan, so it is a true
        // distance on the shape the operator sees, in widths.
        var spread = points.Max(point => Math.Sqrt(
            Math.Pow((point.X - centreX) * CanonicalWidth, 2) + Math.Pow((point.Y - centreY) * CanonicalHeight, 2))) / CanonicalWidth;
        return new(centreX, centreY, Math.Clamp(spread + Margin, BaseRadius, MaxRadius));
    }

    /// <summary>
    /// Draws a damage in renderer coordinates: the <paramref name="drawn"/>
    /// disc when the operator drew one, otherwise the disc <see cref="Disc"/>
    /// gives its areas. The centre maps onto the renderer's plan and the
    /// radius scales with its width, so the disc stays round. Null when there
    /// is nothing to draw.
    /// </summary>
    public static DamageDisc? RenderDisc(IReadOnlyList<string> areas, double width, double height, DamageDisc? drawn = null)
    {
        ArgumentNullException.ThrowIfNull(areas);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var unit = drawn ?? Disc(areas);
        return unit is null ? null : new(unit.CentreX * width, unit.CentreY * height, unit.Radius * width);
    }

    /// <summary>
    /// The plan areas a drawn disc (unit-plan terms) touches on the canonical
    /// plan, in the vocabulary's order.
    /// </summary>
    public static IReadOnlyList<string> AreasUnder(DamageDisc disc)
    {
        ArgumentNullException.ThrowIfNull(disc);
        return IntersectedPlanAreas(
            new(disc.CentreX * CanonicalWidth, disc.CentreY * CanonicalHeight, disc.Radius * CanonicalWidth),
            CanonicalWidth,
            CanonicalHeight);
    }

    /// <summary>
    /// Returns the plan areas with a positive-area intersection with a disc.
    /// Tangency at an area boundary is not coverage. The plan areas are the
    /// canonical rectangles defined by the four band boundaries; the caller's
    /// width and height map that unit plan onto its rendering surface.
    /// </summary>
    public static IReadOnlyList<string> IntersectedPlanAreas(
        DamageDisc disc,
        double width,
        double height)
    {
        ArgumentNullException.ThrowIfNull(disc);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        return Centres.Keys
            .Where(area => HasPositiveAreaIntersection(disc, area, width, height))
            .ToArray();
    }

    private static bool HasPositiveAreaIntersection(
        DamageDisc disc,
        string area,
        double width,
        double height)
    {
        var bounds = area switch
        {
            "front" => (LeftBand * width, 0d, RightBand * width, FrontBand * height),
            "left_front" => (0d, 0d, LeftBand * width, FrontBand * height),
            "right_front" => (RightBand * width, 0d, width, FrontBand * height),
            "left_side" => (0d, FrontBand * height, width / 2d, RearBand * height),
            "right_side" => (width / 2d, FrontBand * height, width, RearBand * height),
            "rear" => (LeftBand * width, RearBand * height, RightBand * width, height),
            "left_rear" => (0d, RearBand * height, LeftBand * width, height),
            "right_rear" => (RightBand * width, RearBand * height, width, height),
            _ => throw new ArgumentException($"'{area}' is not a plan area.", nameof(area))
        };

        var closestX = Math.Clamp(disc.CentreX, bounds.Item1, bounds.Item3);
        var closestY = Math.Clamp(disc.CentreY, bounds.Item2, bounds.Item4);
        var deltaX = disc.CentreX - closestX;
        var deltaY = disc.CentreY - closestY;
        return deltaX * deltaX + deltaY * deltaY < disc.Radius * disc.Radius;
    }
}

public sealed record DamagePlanPoint(double X, double Y);

/// <summary>
/// A disc on a plan. A recorded damage's disc is in unit-plan terms (centre as
/// fractions of the plan's width and height, radius as a fraction of its
/// width); a rendered disc is in the renderer's own coordinates.
/// </summary>
public sealed record DamageDisc(double CentreX, double CentreY, double Radius);
