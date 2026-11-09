using Pegasus.Core.Intake;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Core.Tests.PhotoCapture;

public sealed class PhotoCaptureIntakeRulesTests
{
    [Fact]
    public void MemberTokensFollowTheGroupedMemberConvention()
    {
        var captureId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        Assert.Equal("11111111111111111111111111111111-1", PhotoCaptureIntakeRules.BatchToken(captureId, 1));
        Assert.Equal("11111111111111111111111111111111-1", PhotoCaptureIntakeRules.MemberToken(captureId, 1, 0));
        Assert.Equal("11111111111111111111111111111111-2:3", PhotoCaptureIntakeRules.MemberToken(captureId, 2, 3));
        Assert.Contains(
            PhotoCaptureIntakeRules.BatchToken(captureId, 2),
            GroupedIntakeMemberToken.ParentTokenCandidates(PhotoCaptureIntakeRules.MemberToken(captureId, 2, 3)));
    }

    [Fact]
    public void ABatchGroupIdIsDeterministicAndDiffersPerBatch()
    {
        var captureId = Guid.NewGuid();

        Assert.Equal(PhotoCaptureIntakeRules.BatchGroupId(captureId, 1), PhotoCaptureIntakeRules.BatchGroupId(captureId, 1));
        Assert.NotEqual(PhotoCaptureIntakeRules.BatchGroupId(captureId, 1), PhotoCaptureIntakeRules.BatchGroupId(captureId, 2));
        Assert.NotEqual(captureId, PhotoCaptureIntakeRules.BatchGroupId(captureId, 1));
    }

    [Fact]
    public void ACaptureReceiptIsNeverHeldNeverUnidentifiedAndDefersImageCompletion()
    {
        Assert.False(PhotoCaptureIntakeRules.IsHeld(IntakeSourceChannel.PhotoCapture));
        Assert.True(PhotoCaptureIntakeRules.IsHeld(IntakeSourceChannel.ManualUpload));
        Assert.False(PhotoCaptureIntakeRules.BecomesUnidentified(IntakeSourceChannel.PhotoCapture));
        Assert.True(PhotoCaptureIntakeRules.BecomesUnidentified(IntakeSourceChannel.Mailbox));
        Assert.True(PhotoCaptureIntakeRules.DefersCaseImageCompletion(IntakeSourceChannel.PhotoCapture));
        Assert.False(PhotoCaptureIntakeRules.DefersCaseImageCompletion(IntakeSourceChannel.ManualUpload));
    }

    [Fact]
    public void TheActorIdentityRoundTrips()
    {
        var captureId = Guid.NewGuid();
        var actor = IntakeActorIdentity.PhotoCapture(captureId);

        Assert.StartsWith("photo-capture:", actor, StringComparison.Ordinal);
        Assert.True(IntakeActorIdentity.TryParsePhotoCapture(actor, out var parsed));
        Assert.Equal(captureId, parsed);
        Assert.False(IntakeActorIdentity.TryParseStaff(actor, out _));
        Assert.False(IntakeActorIdentity.TryParsePhotoCapture("staff:abc", out _));
    }

    [Fact]
    public void NoPerFileLimitExceedsTheIntakeCap()
    {
        foreach (var kind in Enum.GetValues<PhotoCaptureItemKind>())
        {
            Assert.True(CaptureLimits.MaximumBytesFor(kind, original: false) <= IntakeEnvelopeLimits.MaximumContentLength);
            Assert.True(CaptureLimits.MaximumBytesFor(kind, original: true) <= IntakeEnvelopeLimits.MaximumContentLength);
        }

        Assert.Equal(3, CaptureLimits.PartCount(2 * CaptureLimits.PartBytes + 1));
        Assert.Equal(1, CaptureLimits.PartLength(2 * CaptureLimits.PartBytes + 1, 3));
        Assert.Equal(CaptureLimits.PartBytes, CaptureLimits.PartLength(2 * CaptureLimits.PartBytes + 1, 1));
        Assert.True(CaptureLimits.IsLifetime(7));
        Assert.False(CaptureLimits.IsLifetime(30));
    }

    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    private static readonly byte[] PdfHeader = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31];
    private static readonly byte[] ZipHeader = [0x50, 0x4B];

    [Fact]
    public void LeadingBytesMustMatchTheDeclaredType()
    {
        Assert.True(IntakeUploadFilePolicy.HeaderMatches("file", "image/jpeg", JpegHeader));
        Assert.False(IntakeUploadFilePolicy.HeaderMatches("file", "image/jpeg", PngHeader));
        Assert.True(IntakeUploadFilePolicy.HeaderMatches("file", "image/png", PngHeader));
        Assert.True(IntakeUploadFilePolicy.HeaderMatches("file", "application/pdf", PdfHeader));
        Assert.False(IntakeUploadFilePolicy.HeaderMatches("file", "application/pdf", JpegHeader));
    }

    [Fact]
    public void AVideoIsCheckedByItsIsoBaseMediaHeaderAndAnUnknownTypeIsNotChecked()
    {
        var mp4 = new byte[32];
        "ftyp"u8.CopyTo(mp4.AsSpan(4));
        "isom"u8.CopyTo(mp4.AsSpan(8));

        Assert.True(IntakeUploadFilePolicy.HeaderMatches("clip.mp4", "video/mp4", mp4));
        Assert.False(IntakeUploadFilePolicy.HeaderMatches("clip.mp4", "video/mp4", new byte[32]));
        Assert.Null(IntakeUploadFilePolicy.HeaderMatches("file.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ZipHeader));
    }
}
