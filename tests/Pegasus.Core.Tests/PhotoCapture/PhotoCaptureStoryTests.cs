using Pegasus.Core.Documents;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Core.Tests.PhotoCapture;

public sealed class PhotoCaptureStoryTests
{
    private static readonly System.Buffers.SearchValues<char> WindowsInvalid =
        System.Buffers.SearchValues.Create("<>:\"/\\|?*");

    [Fact]
    public void TheStoryRunsOverviewAreasPlateVinDashboardThenAdditionalInformation()
    {
        string[] areas = ["rear", "ps-m"];
        var groups = new[]
        {
            PhotoCaptureStory.Group(PhotoCaptureItemKind.Overview, null, areas),
            PhotoCaptureStory.Group(PhotoCaptureItemKind.Damage, "rear", areas),
            PhotoCaptureStory.Group(PhotoCaptureItemKind.Damage, "ps-m", areas),
            PhotoCaptureStory.Group(PhotoCaptureItemKind.NumberPlate, null, areas),
            PhotoCaptureStory.Group(PhotoCaptureItemKind.Vin, null, areas),
            PhotoCaptureStory.Group(PhotoCaptureItemKind.Dashboard, null, areas),
            PhotoCaptureStory.Group(PhotoCaptureItemKind.Scene, null, areas),
            PhotoCaptureStory.Group(PhotoCaptureItemKind.OtherVehicle, null, areas),
            PhotoCaptureStory.Group(PhotoCaptureItemKind.Paperwork, null, areas),
            PhotoCaptureStory.Group(PhotoCaptureItemKind.Video, null, areas)
        };

        Assert.Equal(groups.OrderBy(group => group), groups);
        Assert.Equal(groups.Distinct().Count(), groups.Length);
        Assert.Equal("Damage – Rear", PhotoCaptureStory.GroupLabel(groups[1], areas));
        Assert.Equal("Damage – Passenger side middle", PhotoCaptureStory.GroupLabel(groups[2], areas));
        Assert.Equal("Overview all the way round", PhotoCaptureStory.GroupLabel(groups[0], areas));
    }

    [Fact]
    public void DamageStagesOrderOverviewCloserCloseUpExtraWithinAnArea()
    {
        var sequences = new[]
        {
            PhotoCaptureStory.Sequence(PhotoCaptureItemKind.Damage, PhotoCaptureStage.Overview, 3),
            PhotoCaptureStory.Sequence(PhotoCaptureItemKind.Damage, PhotoCaptureStage.Closer, 1),
            PhotoCaptureStory.Sequence(PhotoCaptureItemKind.Damage, PhotoCaptureStage.CloseUp, 1),
            PhotoCaptureStory.Sequence(PhotoCaptureItemKind.Damage, PhotoCaptureStage.Extra, 1)
        };

        Assert.Equal(sequences.OrderBy(sequence => sequence), sequences);
    }

    public static TheoryData<PhotoCaptureItemKind, string?, PhotoCaptureStage?, int, string, string> Names => new()
    {
        { PhotoCaptureItemKind.Overview, null, null, 3, "image/jpeg", "01-03 Driver side.jpg" },
        { PhotoCaptureItemKind.Damage, "fd", PhotoCaptureStage.Closer, 2, "image/jpeg", "Front driver corner closer.jpg" },
        { PhotoCaptureItemKind.NumberPlate, null, null, 1, "image/png", "Number plate.png" },
        { PhotoCaptureItemKind.Video, null, null, 1, "video/mp4", "Walk-around video.mp4" },
        { PhotoCaptureItemKind.OtherVehicle, null, null, 2, "image/jpeg", "Other vehicle.jpg" }
    };

    [Theory]
    [MemberData(nameof(Names))]
    public void FileNamesCarryTheStoryPositionAndAPlainLabel(
        PhotoCaptureItemKind kind,
        string? area,
        PhotoCaptureStage? stage,
        int position,
        string mediaType,
        string expectedEnding)
    {
        string[] areas = area is null ? [] : [area];
        var group = PhotoCaptureStory.Group(kind, area, areas);
        var sequence = PhotoCaptureStory.Sequence(kind, stage, position);

        var name = PhotoCaptureStory.FileName(kind, area, stage, position, group, sequence, mediaType);

        Assert.EndsWith(expectedEnding, name, StringComparison.Ordinal);
        Assert.Matches("^[0-9]{2}-[0-9]{2} ", name);
        Assert.True(name.Length <= 120);
        Assert.Equal(-1, name.AsSpan().IndexOfAny(WindowsInvalid));
    }

    [Fact]
    public void APaperworkNameKeepsTheSendersFileNameButStaysBounded()
    {
        var longName = new string('x', 200) + ".pdf";
        var name = PhotoCaptureStory.FileName(
            PhotoCaptureItemKind.Paperwork, null, null, 1, PhotoCaptureStory.PaperworkGroup, 1, "application/pdf", longName);

        Assert.StartsWith($"{PhotoCaptureStory.PaperworkGroup:00}-01 Paperwork x", name, StringComparison.Ordinal);
        Assert.Equal(120, name.Length);
    }

    [Theory]
    [InlineData(PhotoCaptureItemKind.Overview, true)]
    [InlineData(PhotoCaptureItemKind.Damage, true)]
    [InlineData(PhotoCaptureItemKind.NumberPlate, true)]
    [InlineData(PhotoCaptureItemKind.Vin, true)]
    [InlineData(PhotoCaptureItemKind.Dashboard, true)]
    [InlineData(PhotoCaptureItemKind.Scene, false)]
    [InlineData(PhotoCaptureItemKind.OtherVehicle, false)]
    [InlineData(PhotoCaptureItemKind.Paperwork, false)]
    [InlineData(PhotoCaptureItemKind.Video, false)]
    public void InspectionImagesStartInTheReportAndAdditionalInformationDoesNot(PhotoCaptureItemKind kind, bool inReport) =>
        Assert.Equal(inReport, PhotoCaptureStory.StartsInReport(kind));

    [Fact]
    public void AnotherVehiclesPhotoArrivesTaggedThirdParty()
    {
        Assert.Equal([ImageTagVocabulary.ThirdPartyId], PhotoCaptureStory.ArrivalTags(PhotoCaptureItemKind.OtherVehicle));
        Assert.Empty(PhotoCaptureStory.ArrivalTags(PhotoCaptureItemKind.Scene));
    }

    [Theory]
    [InlineData(PhotoCaptureItemKind.Overview, "image/jpeg", true)]
    [InlineData(PhotoCaptureItemKind.Overview, "application/pdf", false)]
    [InlineData(PhotoCaptureItemKind.Paperwork, "application/pdf", true)]
    [InlineData(PhotoCaptureItemKind.Video, "video/mp4", true)]
    [InlineData(PhotoCaptureItemKind.Video, "video/quicktime", true)]
    [InlineData(PhotoCaptureItemKind.Video, "image/jpeg", false)]
    [InlineData(PhotoCaptureItemKind.Dashboard, "image/heic", false)]
    public void EachKindAcceptsItsOwnMediaTypes(PhotoCaptureItemKind kind, string mediaType, bool accepted) =>
        Assert.Equal(accepted, PhotoCaptureStory.AcceptsMediaType(kind, mediaType));

    /// <summary>
    /// A full-size original is the next version of its photo's own image
    /// (FRD-28 Routing and filing), so it takes the photo's name; only an
    /// original of another type changes the extension.
    /// </summary>
    [Theory]
    [InlineData("02-03 Front driver corner closer.jpg", "image/jpeg", "02-03 Front driver corner closer.jpg")]
    [InlineData("02-03 Front driver corner closer.jpg", "image/png", "02-03 Front driver corner closer.png")]
    [InlineData("01-01 Front.jpg", "image/jpeg", "01-01 Front.jpg")]
    public void AnOriginalKeepsThePhotosNameAndTakesItsOwnExtension(string photo, string mediaType, string expected) =>
        Assert.Equal(expected, PhotoCaptureStory.OriginalFileName(photo, mediaType));

    [Fact]
    public void PaperworkAndVideoFileAsOtherDocumentsAndPhotographsAsImages()
    {
        Assert.Equal(DocumentSemanticRole.Other, PhotoCaptureStory.DeclaredDocumentType(PhotoCaptureItemKind.Video, "video/mp4"));
        Assert.Equal(DocumentSemanticRole.Other, PhotoCaptureStory.DeclaredDocumentType(PhotoCaptureItemKind.Paperwork, "application/pdf"));
        Assert.Null(PhotoCaptureStory.DeclaredDocumentType(PhotoCaptureItemKind.Paperwork, "image/jpeg"));
    }
}
