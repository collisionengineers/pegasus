using Pegasus.Core.PhotoCapture;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

public sealed class PhotoCaptureRequiredItemsTests
{
    private static readonly string[] EmptyCaptureCodes =
        ["overview", "damage:fd:overview", "damage:fd:closer", "damage:fd:close-up", "plate", "dashboard"];

    private static readonly string[] MarkedAreaStageCodes =
        ["damage:fd:overview", "damage:fd:closer", "damage:fd:close-up"];

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

        Assert.Empty(PhotoCaptureRequiredItems.Evaluate(capture, PhotoCaptureItemPresence.Of(CompleteItems("fd")), vinRequired: false));
    }

    /// <summary>
    /// A damage photo taken outside the marked areas counts towards no
    /// area's required stages, whatever stage it was sent with (operator,
    /// 9 November 2026; R802).
    /// </summary>
    [Fact]
    public void ALooseDamagePhotoCountsTowardsNoAreasStages()
    {
        var capture = Capture(details: new PhotoCaptureDetails(DamageAreas: ["fd"]));
        var items = CompleteItems("fd")
            .Where(item => item.Kind != PhotoCaptureItemKind.Damage)
            .Append(Item(PhotoCaptureItemKind.Damage, 1, null, PhotoCaptureStage.Overview))
            .Append(Item(PhotoCaptureItemKind.Damage, 1, null, PhotoCaptureStage.Closer))
            .Append(Item(PhotoCaptureItemKind.Damage, 1, null, PhotoCaptureStage.CloseUp))
            .Append(Item(PhotoCaptureItemKind.Damage, 2))
            .ToList();

        var missing = PhotoCaptureRequiredItems.Evaluate(capture, PhotoCaptureItemPresence.Of(items), vinRequired: false);

        Assert.Equal(MarkedAreaStageCodes, missing.Select(item => item.Code));
    }

    [Fact]
    public void PartialOverviewsSayHowMany()
    {
        var items = Enumerable.Range(1, 5).Select(position => Item(PhotoCaptureItemKind.Overview, position)).ToList();

        var missing = PhotoCaptureRequiredItems.Evaluate(Capture(), PhotoCaptureItemPresence.Of(items), vinRequired: false);

        Assert.Equal("Overview 5 of 8", missing.Single(item => item.Code == "overview").Label);
    }

    [Fact]
    public void TheVinIsRequiredOnlyWhenAsked()
    {
        var items = CompleteItems();

        Assert.Contains(PhotoCaptureRequiredItems.Evaluate(Capture(), PhotoCaptureItemPresence.Of(items), vinRequired: true), item => item.Code == "vin");
        Assert.DoesNotContain(PhotoCaptureRequiredItems.Evaluate(Capture(), PhotoCaptureItemPresence.Of(items), vinRequired: false), item => item.Code == "vin");
        Assert.True(PhotoCaptureRequiredItems.VinRequired(Capture(PhotoCaptureOrigin.StandingStaff), standingLinkIsRepairer: true, handedOn: false));
        Assert.False(PhotoCaptureRequiredItems.VinRequired(Capture(PhotoCaptureOrigin.StandingForwarded), standingLinkIsRepairer: true, handedOn: false));
        Assert.False(PhotoCaptureRequiredItems.VinRequired(Capture(PhotoCaptureOrigin.StandingStaff), standingLinkIsRepairer: false, handedOn: false));
    }

    /// <summary>
    /// A quick link sent from a Case for a Bodyshop asks the bodyshop's staff
    /// for the VIN as their own standing link does (R808); one for Accident
    /// management, and a guided link, do not.
    /// </summary>
    [Fact]
    public void AQuickCaseLinkForABodyshopRequiresTheVin()
    {
        var quick = Capture() with { Mode = PhotoCaptureMode.Quick };

        Assert.True(PhotoCaptureRequiredItems.VinRequired(
            quick with { QuickLinkFor = PhotoCaptureQuickLinkFor.Bodyshop }, standingLinkIsRepairer: false, handedOn: false));
        Assert.False(PhotoCaptureRequiredItems.VinRequired(
            quick with { QuickLinkFor = PhotoCaptureQuickLinkFor.AccidentManagement }, standingLinkIsRepairer: false, handedOn: false));
        Assert.False(PhotoCaptureRequiredItems.VinRequired(Capture(), standingLinkIsRepairer: false, handedOn: false));
    }

    /// <summary>
    /// A quick Case link a bodyshop's staff handed on to their customer no
    /// longer asks for the VIN: the customer takes the photos (operator, 10
    /// November 2026; R874, R808). A standing link's rule is unchanged.
    /// </summary>
    [Theory]
    [InlineData(PhotoCaptureQuickLinkFor.Bodyshop, false, true)]
    [InlineData(PhotoCaptureQuickLinkFor.Bodyshop, true, false)]
    [InlineData(PhotoCaptureQuickLinkFor.AccidentManagement, false, false)]
    [InlineData(PhotoCaptureQuickLinkFor.AccidentManagement, true, false)]
    public void AHandedOnQuickLinkAsksNoVin(PhotoCaptureQuickLinkFor quickLinkFor, bool handedOn, bool required)
    {
        var quick = Capture() with { Mode = PhotoCaptureMode.Quick, QuickLinkFor = quickLinkFor };

        Assert.Equal(required, PhotoCaptureRequiredItems.VinRequired(quick, standingLinkIsRepairer: false, handedOn));
        Assert.True(PhotoCaptureRequiredItems.VinRequired(
            Capture(PhotoCaptureOrigin.StandingStaff), standingLinkIsRepairer: true, handedOn));
    }

    [Fact]
    public void ASenderWithNoKeysIsNotMissingTheDashboard()
    {
        var items = CompleteItems().Where(item => item.Kind != PhotoCaptureItemKind.Dashboard).ToList();

        Assert.Contains(
            PhotoCaptureRequiredItems.Evaluate(Capture(), PhotoCaptureItemPresence.Of(items), vinRequired: false),
            item => item.Code == "dashboard");
        Assert.DoesNotContain(
            PhotoCaptureRequiredItems.Evaluate(Capture(details: new PhotoCaptureDetails(NoKeys: true)), PhotoCaptureItemPresence.Of(items), vinRequired: false),
            item => item.Code == "dashboard");
    }
}
