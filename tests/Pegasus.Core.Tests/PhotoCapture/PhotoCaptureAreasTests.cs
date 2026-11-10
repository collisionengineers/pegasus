using Pegasus.Core.PhotoCapture;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>Mirrors the app's dmgAreas(): the two must agree on what an area is and which zone keys it.</summary>
public sealed class PhotoCaptureAreasTests
{
    private static readonly string[] Nothing = [];
    private static readonly string[] OneZone = ["fd"];
    private static readonly string[] TwoTouching = ["fd", "ds-f"];
    private static readonly string[] ThreeTouching = ["front", "fd", "ds-f"];
    private static readonly string[] FourTouching = ["fd", "ds-f", "ds-m", "ds-r"];
    private static readonly string[] TwoApart = ["rear", "fd"];
    private static readonly string[] ThreeApart = ["front", "ds-m", "rp"];
    private static readonly string[] AcrossTheFront = ["fp", "front", "fd"];
    private static readonly string[] Everything = ["front", "fd", "ds-f", "ds-m", "ds-r", "rd", "rear", "rp", "ps-r", "ps-m", "ps-f", "fp"];

    [Fact]
    public void NothingMarkedIsNoArea() => Assert.Empty(PhotoCaptureAreas.Derive(Nothing));

    [Fact]
    public void OneZoneIsOneAreaKeyedByItself()
    {
        var area = Assert.Single(PhotoCaptureAreas.Derive(OneZone));

        Assert.Equal("fd", area.Key);
        Assert.Equal("Front driver corner", area.Label);
    }

    [Fact]
    public void TouchingZonesUpToThreeAreOneAreaKeyedByTheMiddleZone()
    {
        var two = Assert.Single(PhotoCaptureAreas.Derive(TwoTouching));
        Assert.Equal("fd", two.Key);
        Assert.Equal("Front driver corner and driver side front", two.Label);

        var three = Assert.Single(PhotoCaptureAreas.Derive(ThreeTouching));
        Assert.Equal("fd", three.Key);
        Assert.Equal("Front to driver side front", three.Label);
    }

    [Fact]
    public void AGapStartsANewArea()
    {
        var areas = PhotoCaptureAreas.Derive(TwoApart);

        Assert.Equal(2, areas.Count);
        Assert.Equal(["fd", "rear"], areas.Select(area => area.Key));
    }

    [Fact]
    public void FourTouchingZonesSplitInTwo()
    {
        var areas = PhotoCaptureAreas.Derive(FourTouching);

        Assert.Equal(2, areas.Count);
        Assert.Equal(["fd", "ds-f"], areas[0].Zones);
        Assert.Equal(["ds-m", "ds-r"], areas[1].Zones);
    }

    [Fact]
    public void MoreThanTwoAreasMergeTheClosestUntilTwoRemain()
    {
        var areas = PhotoCaptureAreas.Derive(ThreeApart);

        Assert.Equal(2, areas.Count);
        Assert.Equal(3, areas.Sum(area => area.Zones.Count));
    }

    [Fact]
    public void ARunAcrossTheFrontStartsFromThePassengerCorner()
    {
        var area = Assert.Single(PhotoCaptureAreas.Derive(AcrossTheFront));

        Assert.Equal(["fp", "front", "fd"], area.Zones);
        Assert.Equal("front", area.Key);
    }

    [Fact]
    public void EveryZoneMarkedIsTwoHalves()
    {
        var areas = PhotoCaptureAreas.Derive(Everything);

        Assert.Equal(2, areas.Count);
        Assert.Equal(6, areas[0].Zones.Count);
        Assert.Equal(6, areas[1].Zones.Count);
    }

    [Fact]
    public void APhotoIsFoundByItsAreaKeyOrByAnyZoneOfTheArea()
    {
        var areas = PhotoCaptureAreas.Derive(ThreeTouching);

        Assert.Equal(areas[0], PhotoCaptureAreas.Find(areas, "fd"));
        Assert.Equal(areas[0], PhotoCaptureAreas.Find(areas, "ds-f"));
        Assert.Null(PhotoCaptureAreas.Find(areas, "rear"));
    }
}
