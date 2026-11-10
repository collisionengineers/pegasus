using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The streamed receive path hands over one file whose bytes stay out of Web
/// memory: the staff upload's and, since FRD-28, the photo link's. Every other
/// channel arrives whole and is refused here, as before.
/// </summary>
public sealed class StreamedIntakeChannelTests
{
    private static StreamedIntakeSource Source(IntakeSourceChannel channel) =>
        new(
            "01-03 Driver side.jpg",
            "image/jpeg",
            1024,
            _ => ValueTask.FromResult<Stream>(new MemoryStream(new byte[1024])),
            new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero),
            IntakeActorIdentity.PhotoCapture(Guid.NewGuid()),
            new IntakeSourceIdentity(channel, "11111111111111111111111111111111-1:2"));

    [Theory]
    [InlineData(IntakeSourceChannel.ManualUpload)]
    [InlineData(IntakeSourceChannel.PhotoCapture)]
    public void TheStaffUploadAndThePhotoLinkStreamTheirFiles(IntakeSourceChannel channel) =>
        Assert.Equal("01-03 Driver side.jpg", ReceiveIntake.ValidateStreamedSource(Source(channel), "photo-capture-item:1"));

    [Theory]
    [InlineData(IntakeSourceChannel.Mailbox)]
    [InlineData(IntakeSourceChannel.Automation)]
    [InlineData(IntakeSourceChannel.PrincipalApi)]
    public void EveryOtherChannelIsRefusedTheStreamedPath(IntakeSourceChannel channel) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ReceiveIntake.ValidateStreamedSource(Source(channel), "op"));
}
