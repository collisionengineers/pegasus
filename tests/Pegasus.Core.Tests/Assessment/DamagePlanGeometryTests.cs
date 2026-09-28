using Pegasus.Core.Assessment;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// The one vehicle drawing (operator, 27 September 2026): the Case page and
/// the report draw the same silhouette and place a damage's disc on it by the
/// same rule.
/// </summary>
public sealed class DamagePlanGeometryTests
{
    /// <summary>
    /// Core judges which areas a disc touches on a plan the shape of this
    /// body box, so a disc drawn here touches the areas Core says it does.
    /// </summary>
    [Fact]
    public void TheBodyBoxHasTheShapeOfTheCanonicalPlan()
    {
        Assert.Equal(
            DamageAreaGeometry.CanonicalHeight / DamageAreaGeometry.CanonicalWidth,
            (double)DamagePlanGeometry.PlanHeight / DamagePlanGeometry.PlanWidth,
            10);
    }

    [Fact]
    public void ADrawnDiscIsPlacedOnTheBodyBoxAsDrawn()
    {
        var drawn = new DamageDisc(0.5, 0.25, 0.1);

        var disc = DamagePlanGeometry.Disc(["front"], drawn);

        Assert.NotNull(disc);
        Assert.Equal(DamagePlanGeometry.PlanLeft + 0.5 * DamagePlanGeometry.PlanWidth, disc.CentreX, 10);
        Assert.Equal(DamagePlanGeometry.PlanTop + 0.25 * DamagePlanGeometry.PlanHeight, disc.CentreY, 10);
        Assert.Equal(0.1 * DamagePlanGeometry.PlanWidth, disc.Radius, 10);
    }

    /// <summary>
    /// The Case page holds a damage as it was recorded and the report holds
    /// its areas and its disc. Both give the same disc.
    /// </summary>
    [Fact]
    public void TheCasePageAndTheReportPlaceADamageAlike()
    {
        var drawn = new AssessmentImpact(["right_side", "right_rear"], "moderate", "Quarter", new DamageDisc(0.8, 0.72, 0.12));
        var byArea = new AssessmentImpact(["left_front"], "light", "Wing");

        Assert.Equal(DamagePlanGeometry.Disc(drawn), DamagePlanGeometry.Disc(drawn.Areas, drawn.Disc));
        Assert.Equal(DamagePlanGeometry.Disc(byArea), DamagePlanGeometry.Disc(byArea.Areas, null));
        Assert.NotNull(DamagePlanGeometry.Disc(byArea));
    }

    /// <summary>
    /// Underside, Interior and Mechanical are not on the plan, so a damage
    /// that names only one of them draws nothing.
    /// </summary>
    [Theory]
    [InlineData("underside")]
    [InlineData("interior")]
    [InlineData("mechanical")]
    public void ADamageThatNamesNoPlanAreaDrawsNothing(string area)
    {
        Assert.Null(DamagePlanGeometry.Disc([area], null));
    }
}
