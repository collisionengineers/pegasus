using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class InstructionEvidenceImagesTests
{
    [Fact]
    public void SelectsAttachedImagesAndLargeEmbeddedImagesOnly()
    {
        var attachedPhoto = Asset(IntakeAssetKind.Attachment, "image/jpeg", 90_000, "AA");
        var attachedPdf = Asset(IntakeAssetKind.Attachment, "application/pdf", 90_000, "BB");
        var embeddedPhoto = Asset(IntakeAssetKind.EmbeddedImage, "image/jpeg", 60_000, "CC");
        var letterheadArt = Asset(IntakeAssetKind.EmbeddedImage, "image/png", 4_039, "DD");
        var inlineSignature = Asset(IntakeAssetKind.InlineImage, "image/png", 90_000, "EE");
        var source = Asset(IntakeAssetKind.Source, "message/rfc822", 90_000, "FF");

        var selected = InstructionEvidenceImages.Select(
            [source, inlineSignature, letterheadArt, embeddedPhoto, attachedPdf, attachedPhoto]);

        Assert.Equal([attachedPhoto.Id, embeddedPhoto.Id], selected.Select(item => item.Id));
    }

    [Fact]
    public void TheThresholdIsABoundaryNotAGuess()
    {
        var atFloor = Asset(
            IntakeAssetKind.EmbeddedImage,
            "image/jpeg",
            InstructionEvidenceImages.EmbeddedPhotographMinimumBytes,
            "AA");
        var underFloor = Asset(
            IntakeAssetKind.EmbeddedImage,
            "image/jpeg",
            InstructionEvidenceImages.EmbeddedPhotographMinimumBytes - 1,
            "BB");

        var selected = InstructionEvidenceImages.Select([atFloor, underFloor]);

        Assert.Equal([atFloor.Id], selected.Select(item => item.Id));
    }

    [Fact]
    public void OnePhotographCarriedTwiceAppearsOncePreferringTheAttachedCopy()
    {
        var attached = Asset(IntakeAssetKind.Attachment, "image/jpeg", 90_000, "AA", "damage.jpg");
        var embeddedCopy = Asset(IntakeAssetKind.EmbeddedImage, "image/jpeg", 90_000, "aa", "page-1-image-1.jpg");
        var repeatedAcrossPages = Asset(IntakeAssetKind.EmbeddedImage, "image/jpeg", 90_000, "aa", "page-2-image-1.jpg");

        var selected = InstructionEvidenceImages.Select(
            [embeddedCopy, repeatedAcrossPages, attached]);

        var only = Assert.Single(selected);
        Assert.Equal(attached.Id, only.Id);
    }

    [Fact]
    public void QdosTwentySixZeroZeroEightsLetterheadBannersAreNotEvidence()
    {
        // INTK-030, measured from production. The operator reported the
        // first two images as signatures/logos. These are those two, at
        // their real sizes and dimensions, beside one of the nine genuine
        // photographs from the same receipt. Note the byte floor admits
        // both banners and one of them is a JPEG, so neither size nor
        // format could have told them apart — only the shape does.
        var pngBanner = Asset(
            IntakeAssetKind.EmbeddedImage, "image/png", 110_783, "b1",
            "page-1-image-1.png", width: 1990, height: 437);
        var jpegBanner = Asset(
            IntakeAssetKind.EmbeddedImage, "image/jpeg", 77_972, "b2",
            "page-1-image-2.jpg", width: 2214, height: 248);
        var photograph = Asset(
            IntakeAssetKind.EmbeddedImage, "image/jpeg", 156_740, "p1",
            "page-2-image-3.jpg", width: 709, height: 646);

        var selected = InstructionEvidenceImages.Select(
            [pngBanner, jpegBanner, photograph]);

        var only = Assert.Single(selected);
        Assert.Equal(photograph.Id, only.Id);
    }

    [Fact]
    public void AnImageWithNoRecordedDimensionsIsStillAdmitted()
    {
        // Failing open is deliberate: refusing to show a genuine
        // photograph is the worse of the two errors.
        var unmeasured = Asset(
            IntakeAssetKind.EmbeddedImage, "image/jpeg", 90_000, "u1");

        Assert.Single(InstructionEvidenceImages.Select([unmeasured]));
    }

    private static IntakeAssetRecord Asset(
        IntakeAssetKind kind,
        string mediaType,
        long contentLength,
        string hash,
        string fileName = "asset.bin",
        int? width = null,
        int? height = null) => new(
        Guid.NewGuid(),
        "test",
        fileName,
        mediaType,
        kind,
        kind switch
        {
            IntakeAssetKind.Source => IntakeAssetDisposition.Source,
            IntakeAssetKind.Attachment => IntakeAssetDisposition.Attachment,
            IntakeAssetKind.InlineImage => IntakeAssetDisposition.Inline,
            _ => IntakeAssetDisposition.Embedded
        },
        contentLength,
        hash,
        $"storage/{hash}",
        null,
        null,
        width,
        height);
}
