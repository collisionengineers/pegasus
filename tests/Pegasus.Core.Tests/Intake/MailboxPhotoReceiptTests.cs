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
        Assert.True(ProcessIntake.BecomesUnidentified(receipt));
        Assert.False(ProcessIntake.IsUnidentifiedEligible(receipt));
    }

    [Fact]
    public void SignatureAndBannerWithoutPhotographsAreAnEMailThatNeverBecomesUnidentified()
    {
        // Only images become Unidentified (operator, 13 October 2026): a
        // message with no selected photograph is an e-mail, which waits in the
        // Inbox's No Case Found scope instead.
        var receipt = Receipt([
            Asset("photos.pdf", "application/pdf", IntakeAssetKind.Attachment),
            Asset("signature.png", "image/png", IntakeAssetKind.InlineImage),
            Asset("banner.png", "image/png", IntakeAssetKind.EmbeddedImage,
                length: 110_783, width: 1990, height: 437)]);

        Assert.False(ImageIntakeLifecycleRules.IsImageOnlyMaterial(receipt));
        Assert.Empty(InstructionEvidenceImages.Select(receipt.AssetRecords));
        Assert.False(ProcessIntake.IsDeferredForAutomation(receipt));
        Assert.False(ProcessIntake.BecomesUnidentified(receipt));
        Assert.False(ProcessIntake.IsUnidentifiedEligible(receipt));
    }

    [Fact]
    public void UncategorisedImageOnlyMailIsEligibleAndACategoryIsNot()
    {
        var photos = Receipt([Asset("vehicle.jpg", "image/jpeg", IntakeAssetKind.Attachment)]);
        var unclassified = photos with
        {
            MailClassificationDecision = MailClassificationResult.Unclassified(
                [], "No category.", "shared-mail-policy", 1)
        };
        var classified = photos with
        {
            MailClassificationDecision = MailClassificationResult.Classified(
                MailCategory.Cancellation,
                [], "Corrected by staff.", "shared-mail-policy", 1)
        };

        Assert.True(ImageIntakeLifecycleRules.IsImageAutomationEligible(photos));
        Assert.True(ImageIntakeLifecycleRules.IsImageRegistrationEligible(photos));
        Assert.True(ImageIntakeLifecycleRules.IsImageAutomationEligible(unclassified));
        Assert.True(ImageIntakeLifecycleRules.IsImageRegistrationEligible(unclassified));
        Assert.False(ImageIntakeLifecycleRules.IsImageAutomationEligible(classified));
        Assert.False(ImageIntakeLifecycleRules.IsImageRegistrationEligible(classified));
        Assert.False(ImageIntakeLifecycleRules.IsImageRegistrationEligible(photos with { AcceptedCaseId = Guid.NewGuid() }));
    }

    /// <summary>
    /// R186 and R298 (operator, 22 October 2026): a mailbox receipt carrying a
    /// recognised report or a PDF is a document carrier. Its photographs stay
    /// selected, but it is neither Image Intake nor Unidentified material, and
    /// a category does not make it eligible to register as images.
    /// </summary>
    [Fact]
    public void AMailboxDocumentCarrierIsNeverImageMaterial()
    {
        var photo = Asset("vehicle.jpg", "image/jpeg", IntakeAssetKind.Attachment);
        var carrier = Receipt([Asset("report.pdf", "application/pdf", IntakeAssetKind.Attachment), photo]) with
        {
            Evidence =
            [
                new(
                    IntakeEvidenceSource.SystemDefault,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.Information,
                    IntakeEvidenceSignals.RecognizedNonImageDocument,
                    "A known document role or instruction selection requires the original intake route.")
            ],
            MailClassificationDecision = MailClassificationResult.Classified(
                MailCategory.Cancellation,
                [], "Corrected by staff.", "shared-mail-policy", 1)
        };

        Assert.Equal(photo.Id, Assert.Single(InstructionEvidenceImages.Select(carrier.AssetRecords)).Id);
        Assert.False(ImageIntakeLifecycleRules.IsImageOnlyMaterial(carrier));
        Assert.False(ImageIntakeLifecycleRules.IsImageRegistrationEligible(carrier));
        Assert.False(ProcessIntake.IsDeferredForAutomation(carrier));
        Assert.False(ProcessIntake.BecomesUnidentified(carrier));
    }

    [Fact]
    public void InstructionBearingMailboxReceiptDoesNotBecomeImageIntake()
    {
        var receipt = Receipt([Asset("vehicle.jpg", "image/jpeg", IntakeAssetKind.Attachment)]) with
        {
            InstructionDraft = new(null, null, null, null, null, null, null, null, null, null)
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
        Assert.True(ProcessIntake.BecomesUnidentified(receipt));
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
