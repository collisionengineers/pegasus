using Pegasus.Core.PhotoCapture;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

public sealed class PhotoCaptureRequiredItemsTests
{
    private static readonly string[] EmptyCaptureCodes =
        ["overview", "damage:fd:overview", "damage:fd:closer", "damage:fd:close-up", "plate", "dashboard"];

    [Fact]
    public void AnEmptyCaptureIsMissingTheOverviewsEachMarkedAreasStagesThePlateAndTheDashboard()
    {
        var capture = Capture(details: new PhotoCaptureDetails(DamageAreas: ["fd"]));

        var missing = PhotoCaptureRequiredItems.Evaluate(capture, [], vinRequired: false);

        Assert.Equal(EmptyCaptureCodes, missing.Select(item => item.Code));
        Assert.Equal("No overview photos", missing[0].Label);
        Assert.Equal("Front driver corner: closer", missing[2].Label);
    }

    [Fact]
    public void ACompleteCaptureIsMissingNothingAndExtrasAreNeverRequired()
    {
        var capture = Capture(details: new PhotoCaptureDetails(DamageAreas: ["fd"]));

        Assert.Empty(PhotoCaptureRequiredItems.Evaluate(capture, CompleteItems("fd"), vinRequired: false));
    }

    [Fact]
    public void PartialOverviewsSayHowMany()
    {
        var items = Enumerable.Range(1, 5).Select(position => Item(PhotoCaptureItemKind.Overview, position)).ToList();

        var missing = PhotoCaptureRequiredItems.Evaluate(Capture(), items, vinRequired: false);

        Assert.Equal("Overview 5 of 8", missing.Single(item => item.Code == "overview").Label);
    }

    [Fact]
    public void TheVinIsRequiredOnlyWhenAsked()
    {
        var items = CompleteItems();

        Assert.Contains(PhotoCaptureRequiredItems.Evaluate(Capture(), items, vinRequired: true), item => item.Code == "vin");
        Assert.DoesNotContain(PhotoCaptureRequiredItems.Evaluate(Capture(), items, vinRequired: false), item => item.Code == "vin");
        Assert.True(PhotoCaptureRequiredItems.VinRequired(PhotoCaptureOrigin.StandingStaff, standingLinkIsRepairer: true));
        Assert.False(PhotoCaptureRequiredItems.VinRequired(PhotoCaptureOrigin.StandingForwarded, standingLinkIsRepairer: true));
        Assert.False(PhotoCaptureRequiredItems.VinRequired(PhotoCaptureOrigin.StandingStaff, standingLinkIsRepairer: false));
    }

    [Fact]
    public void ASenderWithNoKeysIsNotMissingTheDashboard()
    {
        var items = CompleteItems().Where(item => item.Kind != PhotoCaptureItemKind.Dashboard).ToList();

        Assert.Contains(
            PhotoCaptureRequiredItems.Evaluate(Capture(), items, vinRequired: false),
            item => item.Code == "dashboard");
        Assert.DoesNotContain(
            PhotoCaptureRequiredItems.Evaluate(Capture(details: new PhotoCaptureDetails(NoKeys: true)), items, vinRequired: false),
            item => item.Code == "dashboard");
    }
}
