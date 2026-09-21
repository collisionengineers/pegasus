namespace Pegasus.Core.Assessment;

/// <summary>
/// The plan areas as geometry (v28 P5): where each of the eight areas sits on
/// a unit plan (x from 0 at the left to 1 at the right, y from 0 at the front
/// to 1 at the rear), the bands that divide the plan into them, and the disc a
/// recorded damage is drawn as. The Case workspace and the assessment report
/// map the unit plan onto their own silhouettes, so both draw the same disc
/// from the same areas; the record keeps the areas only (ruled 20 September
/// 2026).
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

    /// <summary>The centre of each plan area on the unit plan.</summary>
    public static IReadOnlyDictionary<string, DamagePlanPoint> Centres { get; } =
        new Dictionary<string, DamagePlanPoint>(StringComparer.Ordinal)
        {
            ["front"] = new(0.5, 0.1), ["left_front"] = new(0.18, 0.2), ["right_front"] = new(0.82, 0.2),
            ["left_side"] = new(0.1, 0.53), ["right_side"] = new(0.9, 0.53), ["rear"] = new(0.5, 0.92),
            ["left_rear"] = new(0.18, 0.84), ["right_rear"] = new(0.82, 0.84)
        };

    /// <summary>The area under a unit-plan point, or null off the plan.</summary>
    public static string? AreaAt(double x, double y)
    {
        if (x < 0 || x > 1 || y < 0 || y > 1)
        {
            return null;
        }
        var band = y < FrontBand ? "front" : y > RearBand ? "rear" : "side";
        var lateral = x < LeftBand ? "left" : x > RightBand ? "right" : "centre";
        if (band == "side")
        {
            return (lateral == "centre" ? (x < 0.5 ? "left" : "right") : lateral) + "_side";
        }
        return lateral == "centre" ? band : lateral + "_" + band;
    }

    /// <summary>
    /// The disc drawn for a damage's plan areas on a plan of the given size:
    /// centred between the areas, wide enough to reach each of them. Null
    /// when the damage names no plan area.
    /// </summary>
    public static DamageDisc? Disc(IReadOnlyList<string> areas, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(areas);
        var points = areas
            .Where(Centres.ContainsKey)
            .Select(area => (X: Centres[area].X * width, Y: Centres[area].Y * height))
            .ToArray();
        if (points.Length == 0)
        {
            return null;
        }
        var centreX = points.Average(point => point.X);
        var centreY = points.Average(point => point.Y);
        var spread = points.Max(point => Math.Sqrt(Math.Pow(point.X - centreX, 2) + Math.Pow(point.Y - centreY, 2)));
        return new(centreX, centreY, Math.Max(BaseRadius * width, spread + Margin * width));
    }
}

public sealed record DamagePlanPoint(double X, double Y);

public sealed record DamageDisc(double CentreX, double CentreY, double Radius);
