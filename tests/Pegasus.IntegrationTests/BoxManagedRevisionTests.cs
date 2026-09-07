using Pegasus.Infrastructure.Custody;

namespace Pegasus.IntegrationTests;

/// <summary>
/// DOCS-010. Box does not return <c>content_type</c> for a file, so every
/// managed read compared a null against the recorded media type and refused a
/// file that was otherwise exactly right. The Evidence gallery, the
/// case-document download and the case export all failed identically, each
/// turning the exception into a 404 or a flat refusal, and nothing caught it
/// because the EVA hand-off — the only other caller — had never run.
/// </summary>
public sealed class BoxManagedRevisionTests
{
    private const string CaseFolder = "411135978094";
    private const long Length = 462_270;

    [Fact]
    public void AFileBoxReportsNoTypeForIsStillTheRevisionWeAskedFor()
    {
        Assert.True(IsExpected(File(mediaType: null)));
    }

    [Fact]
    public void AnEmptyTypeIsTreatedTheSameAsAnAbsentOne()
    {
        Assert.True(IsExpected(File(mediaType: string.Empty)));
    }

    [Fact]
    public void ATypeBoxDoesSupplyIsStillChecked()
    {
        Assert.True(IsExpected(File(mediaType: "image/jpeg")));
        Assert.True(IsExpected(File(mediaType: "IMAGE/JPEG")));
        Assert.False(IsExpected(File(mediaType: "application/pdf")));
    }

    [Fact]
    public void TheWrongLengthIsStillRefused()
    {
        Assert.False(IsExpected(File(mediaType: null) with { Size = Length - 1 }));
        Assert.False(IsExpected(File(mediaType: null) with { Size = null }));
    }

    [Fact]
    public void AFileInAnotherCasesFolderIsStillRefused()
    {
        Assert.False(IsExpected(File(mediaType: null) with { ParentId = "411080648902" }));
        Assert.False(IsExpected(File(mediaType: null) with { ParentId = null }));
    }

    private static bool IsExpected(BoxContentClient.BoxItem file) =>
        BoxDocumentContentStore.IsExpectedRevision(file, CaseFolder, "image/jpeg", Length);

    private static BoxContentClient.BoxItem File(string? mediaType) => new(
        "2421244761500",
        "002 1_CLVoffside-V1.jpg",
        "file",
        "0",
        "2419892871001",
        Length,
        mediaType,
        CaseFolder);
}
