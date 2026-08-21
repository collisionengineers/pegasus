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

    private static IntakeAssetRecord Asset(
        IntakeAssetKind kind,
        string mediaType,
        long contentLength,
        string hash,
        string fileName = "asset.bin") => new(
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
        null,
        null);
}
