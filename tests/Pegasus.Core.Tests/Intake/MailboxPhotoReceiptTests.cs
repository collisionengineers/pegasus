using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class MailboxPhotoReceiptTests
{
    [Fact]
    public void MailboxPdfPhotosUseTheirParentReceiptAndExcludeSignatureAndBanner()
    {
        var pdf = Asset("photos.pdf", "application/pdf", IntakeAssetKind.Attachment);
        var photo = Asset("page-1-photo.jpg", "image/jpeg", IntakeAssetKind.EmbeddedImage,
            length: 121_652, width: 547, height: 650);
        var banner = Asset("banner.png", "image/png", IntakeAssetKind.EmbeddedImage,
            length: 110_783, width: 1990, height: 437);
        var signature = Asset("signature.png", "image/png", IntakeAssetKind.InlineImage);
        var receipt = Receipt([pdf, photo, banner, signature]);

        Assert.True(ImageIntakeLifecycleRules.IsImageOnlyMaterial(receipt));
        Assert.Equal(photo.Id, Assert.Single(InstructionEvidenceImages.Select(receipt.AssetRecords)).Id);
        Assert.True(ProcessIntake.IsDeferredForAutomation(receipt));
        Assert.False(ProcessIntake.IsUnidentifiedEligible(receipt));
    }

    [Fact]
    public void SignatureAndBannerWithoutPhotographsDoNotDeferUnidentified()
    {
        var receipt = Receipt([
            Asset("photos.pdf", "application/pdf", IntakeAssetKind.Attachment),
            Asset("signature.png", "image/png", IntakeAssetKind.InlineImage),
            Asset("banner.png", "image/png", IntakeAssetKind.EmbeddedImage,
                length: 110_783, width: 1990, height: 437)]);

        Assert.False(ImageIntakeLifecycleRules.IsImageOnlyMaterial(receipt));
        Assert.Empty(InstructionEvidenceImages.Select(receipt.AssetRecords));
        Assert.True(ProcessIntake.IsUnidentifiedEligible(receipt));
    }

    [Fact]
    public void InstructionBearingMailboxReceiptDoesNotBecomeImageIntake()
    {
        var receipt = Receipt([Asset("vehicle.jpg", "image/jpeg", IntakeAssetKind.Attachment)]) with
        {
            InstructionDraft = new(null, null, null, null, null, null, null, null, null, null, null)
        };

        Assert.False(ImageIntakeLifecycleRules.IsImageOnlyMaterial(receipt));
        Assert.False(ProcessIntake.IsDeferredForAutomation(receipt));
    }

    [Theory]
    [InlineData(IntakeDecision.TechnicalFailure)]
    [InlineData(IntakeDecision.OcrRequired)]
    [InlineData(IntakeDecision.Unsupported)]
    public void FailedOrUnreadableMailboxPhotosRemainVisibleUnidentified(IntakeDecision decision)
    {
        var receipt = Receipt([Asset("vehicle.jpg", "image/jpeg", IntakeAssetKind.Attachment)]) with
        {
            Decision = decision
        };

        Assert.False(ProcessIntake.IsDeferredForAutomation(receipt));
        Assert.True(ProcessIntake.IsUnidentifiedEligible(receipt));
    }

    private static IntakeReceipt Receipt(IReadOnlyList<IntakeAssetRecord> attachments)
    {
        var id = Guid.NewGuid();
        return new(id, "message.eml", "message/rfc822", 1, new('A', 64),
            new(IntakeSourceChannel.Mailbox, $"mail-{id:N}"),
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            IntakeDecision.NeedsSorting, "No usable identification.", [], [], null, [],
            null, null, false, "mail-reader", "1", null, null,
            Assets: [Asset("message.eml", "message/rfc822", IntakeAssetKind.Source), .. attachments]);
    }

    private static IntakeAssetRecord Asset(string fileName, string mediaType,
        IntakeAssetKind kind, long length = 100, int? width = null, int? height = null)
    {
        var id = Guid.NewGuid();
        return new(id, fileName, fileName, mediaType, kind, kind switch
        {
            IntakeAssetKind.Source => IntakeAssetDisposition.Source,
            IntakeAssetKind.InlineImage => IntakeAssetDisposition.Inline,
            IntakeAssetKind.EmbeddedImage => IntakeAssetDisposition.Embedded,
            _ => IntakeAssetDisposition.Attachment
        }, length, id.ToString("N").PadRight(64, '0'), $"asset/{id:N}",
            null, null, width, height);
    }
}
