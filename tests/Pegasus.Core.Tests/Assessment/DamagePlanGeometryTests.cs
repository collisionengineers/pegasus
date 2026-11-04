using Pegasus.Core.Assessment;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// The vehicle drawings (operator, 28 September 2026): the Case page and the
/// report draw the recorded vehicle's plan and place a damage marker's disc on
/// it by the same rule, as the same comic burst.
/// </summary>
public sealed class DamagePlanGeometryTests
{
    [Fact]
    public void AMarkerIsPlacedOnTheBodyBoxAsDrawn()
    {
        var drawn = new DamageDisc(0.5, 0.25, 0.1);

        var disc = DamagePlanGeometry.Disc(drawn);

        Assert.Equal(DamagePlanGeometry.PlanLeft + 0.5 * DamagePlanGeometry.PlanWidth, disc.CentreX, 10);
        Assert.Equal(DamagePlanGeometry.PlanTop + 0.25 * DamagePlanGeometry.PlanHeight, disc.CentreY, 10);
        Assert.Equal(0.1 * DamagePlanGeometry.PlanWidth, disc.Radius, 10);
    }

    /// <summary>
    /// A van draws as the van, a motorcycle or scooter as the motorbike, and
    /// anything else, recorded or not, as the car.
    /// </summary>
    [Theory]
    [InlineData("car", DamagePlanGeometry.Car)]
    [InlineData("van", DamagePlanGeometry.Van)]
    [InlineData("motorcycle", DamagePlanGeometry.Motorbike)]
    [InlineData("scooter", DamagePlanGeometry.Motorbike)]
    [InlineData("bicycle", DamagePlanGeometry.Car)]
    [InlineData("trailer", DamagePlanGeometry.Car)]
    [InlineData("caravan", DamagePlanGeometry.Car)]
    [InlineData("other", DamagePlanGeometry.Car)]
    [InlineData(null, DamagePlanGeometry.Car)]
    public void TheDrawingFollowsTheRecordedVehicleType(string? vehicleType, string profile)
    {
        Assert.Equal(profile, DamagePlanGeometry.ProfileFor(vehicleType));
    }

    /// <summary>
    /// Each drawing, shaded and flat, is embedded, carries no words and no
    /// clip of its own, and is outlined for presses by its hit paths.
    /// </summary>
    [Theory]
    [InlineData(DamagePlanGeometry.Car)]
    [InlineData(DamagePlanGeometry.Van)]
    [InlineData(DamagePlanGeometry.Motorbike)]
    public void EveryDrawingIsEmbeddedWithoutWords(string profile)
    {
        foreach (var drawing in new[] { DamagePlanGeometry.Artwork(profile), DamagePlanGeometry.FlatArtwork(profile) })
        {
            Assert.StartsWith("<", drawing, StringComparison.Ordinal);
            foreach (var banned in new[] { "<svg", "<text", "<title", "<desc", "clip" })
            {
                Assert.DoesNotContain(banned, drawing, StringComparison.OrdinalIgnoreCase);
            }
        }
        Assert.DoesNotContain("Gradient", DamagePlanGeometry.FlatArtwork(profile), StringComparison.Ordinal);
        Assert.NotEmpty(DamagePlanGeometry.HitPaths(profile));
    }

    /// <summary>
    /// The burst is the pack's comic star: ten spikes and ten valleys around
    /// the disc's centre, its tips about the disc's edge (stretched wider than
    /// tall), written at a tenth as the Case page's script writes it.
    /// </summary>
    [Fact]
    public void TheBurstIsTenSpikesAroundTheDisc()
    {
        var disc = new DamageDisc(120, 200, 20);

        var path = DamagePlanGeometry.BurstPath(disc);

        Assert.StartsWith("M", path, StringComparison.Ordinal);
        Assert.EndsWith(" Z", path, StringComparison.Ordinal);
        var points = path[1..^2].Split(" L")
            .Select(point => point.Split(' ').Select(value => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray())
            .ToArray();
        Assert.Equal(DamageBurst.Points * 2, points.Length);
        var reach = points.Select(point => double.Hypot(point[0] - disc.CentreX, point[1] - disc.CentreY)).ToArray();
        for (var index = 0; index < reach.Length; index += 2)
        {
            // A tip is further out than the valleys either side of it.
            Assert.True(reach[index] > reach[index + 1]);
            Assert.InRange(reach[index], disc.Radius * 0.75, disc.Radius * DamageBurst.StretchX * 1.2);
        }
        Assert.Equal(path, DamagePlanGeometry.BurstPath(disc));
    }
}
