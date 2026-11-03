namespace Pegasus.Core.Assessment;

/// <summary>
/// The unit plan a damage marker is drawn on (v28 P5): x from 0 at the left to
/// 1 at the right, y from 0 at the front to 1 at the rear. A marker is a disc
/// on it, kept as drawn, and records a position only (v46, operator,
/// 3 November 2026): the impacts staff enter name the location, so no area is
/// read off a disc. The Case workspace and the assessment report map the unit
/// plan onto the one silhouette (<see cref="DamagePlanGeometry"/>), so both
/// draw the same disc.
/// </summary>
public static class DamageAreaGeometry
{
    /// <summary>
    /// The dashed guides the plan shows while editing: the front ends here and
    /// the rear begins at <see cref="RearBand"/>; the left ends at
    /// <see cref="LeftBand"/> and the right begins at <see cref="RightBand"/>.
    /// </summary>
    public const double FrontBand = 0.34;
    public const double RearBand = 0.72;
    public const double LeftBand = 0.372;
    public const double RightBand = 0.628;

    /// <summary>
    /// The smallest and largest disc the operator can draw, as fractions of the
    /// plan's width: the smallest is ten units of the workspace's 156-unit body
    /// box, and no disc is wider than the vehicle.
    /// </summary>
    public const double MinRadius = 10d / 156d;
    public const double MaxRadius = 0.5;
}

/// <summary>
/// A disc on a plan. A recorded marker's disc is in unit-plan terms (centre as
/// fractions of the plan's width and height, radius as a fraction of its
/// width); a rendered disc is in the renderer's own coordinates.
/// </summary>
public sealed record DamageDisc(double CentreX, double CentreY, double Radius);
