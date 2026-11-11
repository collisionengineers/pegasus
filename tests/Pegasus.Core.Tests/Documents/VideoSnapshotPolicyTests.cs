using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Documents;

/// <summary>
/// Case video snapshot rules (operator, 11 November 2026; R964): supported video
/// media types, JPEG/PNG magic-byte verification, replay-stable per-operation
/// occurrence identities, and custody-safe 120-character file naming with
/// millisecond and ordinal collision disambiguation.
/// </summary>
public sealed class VideoSnapshotPolicyTests
{
    private static readonly ActionActor Engineer =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

    private static readonly byte[] MinimalJpeg =
        [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0x00, 0x01];

    private static readonly byte[] MinimalPng =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    [Theory]
    [InlineData("video/mp4", true)]
    [InlineData("video/quicktime", true)]
    [InlineData("VIDEO/MP4; codecs=\"avc1.42E01E\"", true)]
    [InlineData(" video/quicktime ; profile=main ", true)]
    [InlineData("video/webm", false)]
    [InlineData("image/jpeg", false)]
    [InlineData("application/pdf", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SupportedVideoMediaTypeMatchesMp4AndQuickTimeOnly(string? mediaType, bool expected)
    {
        Assert.Equal(expected, VideoSnapshotPolicy.IsSupportedVideoMediaType(mediaType));
    }

    [Theory]
    [InlineData("image/jpeg", "image/jpeg")]
    [InlineData("IMAGE/JPEG; quality=92", "image/jpeg")]
    [InlineData("image/png", "image/png")]
    [InlineData(" image/png ", "image/png")]
    public void NormalizeImageMediaTypeKeepsCanonicalJpegAndPng(string mediaType, string expected)
    {
        Assert.Equal(expected, VideoSnapshotPolicy.NormalizeImageMediaType(mediaType));
    }

    [Theory]
    [InlineData("image/webp")]
    [InlineData("application/pdf")]
    [InlineData("video/mp4")]
    [InlineData("")]
    [InlineData(null)]
    public void NormalizeImageMediaTypeRefusesEveryOtherFormat(string? mediaType)
    {
        Assert.Throws<InvalidOperationException>(() => VideoSnapshotPolicy.NormalizeImageMediaType(mediaType));
    }

    [Theory]
    [InlineData("principal actor", typeof(StaffAuthorizationException))]
    [InlineData("empty case", typeof(ArgumentException))]
    [InlineData("empty video occurrence", typeof(ArgumentException))]
    [InlineData("negative timestamp", typeof(ArgumentOutOfRangeException))]
    [InlineData("blank key", typeof(ArgumentException))]
    [InlineData("long key", typeof(ArgumentOutOfRangeException))]
    [InlineData("empty content", typeof(ArgumentException))]
    [InlineData("unsupported media type", typeof(InvalidOperationException))]
    [InlineData("jpeg declared with png bytes", typeof(InvalidOperationException))]
    [InlineData("png declared with jpeg bytes", typeof(InvalidOperationException))]
    [InlineData("jpeg declared with text bytes", typeof(InvalidOperationException))]
    public void ValidateRefusesInvalidCommands(string scenario, Type expectedException)
    {
        var valid = ValidCommand();
        var invalid = scenario switch
        {
            "principal actor" => valid with { Actor = ActionActor.Principal(Guid.NewGuid()) },
            "empty case" => valid with { CaseId = Guid.Empty },
            "empty video occurrence" => valid with { VideoOccurrenceId = Guid.Empty },
            "negative timestamp" => valid with { TimestampMs = -1 },
            "blank key" => valid with { OperationKey = "   " },
            "long key" => valid with { OperationKey = new string('k', 101) },
            "empty content" => valid with { Content = ReadOnlyMemory<byte>.Empty },
            "unsupported media type" => valid with { MediaType = "image/webp" },
            "jpeg declared with png bytes" => valid with { MediaType = "image/jpeg", Content = MinimalPng },
            "png declared with jpeg bytes" => valid with { MediaType = "image/png", Content = MinimalJpeg },
            "jpeg declared with text bytes" => valid with { MediaType = "image/jpeg", Content = "not-an-image"u8.ToArray() },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        Assert.IsType(expectedException, Record.Exception(() => VideoSnapshotPolicy.Validate(invalid)));
    }

    [Fact]
    public void ValidateRefusesContentAboveTwentyFiveMegabytesAndAcceptsMatchingJpegAndPng()
    {
        var oversize = new byte[VideoSnapshotPolicy.MaximumSnapshotBytes + 1];
        MinimalJpeg.CopyTo(oversize.AsSpan());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VideoSnapshotPolicy.Validate(ValidCommand() with { Content = oversize }));

        Assert.Null(Record.Exception(() => VideoSnapshotPolicy.Validate(ValidCommand())));
        Assert.Null(Record.Exception(() => VideoSnapshotPolicy.Validate(
            ValidCommand() with { MediaType = "image/png", Content = MinimalPng })));
    }

    [Fact]
    public void SourceOccurrenceIdentityIsDeterministicOnReplayAndDistinctPerOperation()
    {
        var occurrenceId = Guid.Parse("71a2bc34-56de-4012-89ab-cdef01234567");
        var first = VideoSnapshotPolicy.SourceOccurrenceIdentityFor(occurrenceId, 14_250, " op-key-1 ");
        var replay = VideoSnapshotPolicy.SourceOccurrenceIdentityFor(occurrenceId, 14_250, "op-key-1");
        var secondAtSameTimestamp = VideoSnapshotPolicy.SourceOccurrenceIdentityFor(occurrenceId, 14_250, "op-key-2");
        var differentTimestamp = VideoSnapshotPolicy.SourceOccurrenceIdentityFor(occurrenceId, 14_500, "op-key-1");

        Assert.Equal(first, replay);
        Assert.NotEqual(first, secondAtSameTimestamp);
        Assert.NotEqual(first, differentTimestamp);
    }

    [Fact]
    public void FileNameForFormatsMinutesAndSecondsAndDisambiguatesCollisionsInOrder()
    {
        var existing = new List<string>();

        var first = VideoSnapshotPolicy.FileNameFor("walkaround.mp4", 14_250, "image/jpeg", existing);
        Assert.Equal("walkaround-frame-00m14s.jpg", first);
        existing.Add(first);

        var secondSameSecond = VideoSnapshotPolicy.FileNameFor("walkaround.mp4", 14_250, "image/jpeg", existing);
        Assert.Equal("walkaround-frame-00m14s250ms.jpg", secondSameSecond);
        existing.Add(secondSameSecond);

        var thirdSameMillisecond = VideoSnapshotPolicy.FileNameFor("walkaround.mp4", 14_250, "image/jpeg", existing);
        Assert.Equal("walkaround-frame-00m14s250ms (2).jpg", thirdSameMillisecond);
        existing.Add(thirdSameMillisecond);

        var fourthSameMillisecond = VideoSnapshotPolicy.FileNameFor("walkaround.mp4", 14_250, "image/jpeg", existing);
        Assert.Equal("walkaround-frame-00m14s250ms (3).jpg", fourthSameMillisecond);

        var multiMinutePng = VideoSnapshotPolicy.FileNameFor("inspection-clip.MOV", 125_000, "image/png");
        Assert.Equal("inspection-clip-frame-02m05s.png", multiMinutePng);
    }

    [Fact]
    public void FileNameForSanitizesUnsafeCharactersAndStaysWithinOneHundredTwentyCharacters()
    {
        var sanitized = VideoSnapshotPolicy.FileNameFor("folder\\front:bumper*<1>?.mp4", 3_000, "image/jpeg");
        Assert.Equal("front_bumper__1__-frame-00m03s.jpg", sanitized);

        var fallback = VideoSnapshotPolicy.FileNameFor("...", 0, "image/jpeg");
        Assert.Equal("video-frame-00m00s.jpg", fallback);

        var longStem = new string('a', 150) + ".mp4";
        var primary = VideoSnapshotPolicy.FileNameFor(longStem, 65_432, "image/jpeg");
        Assert.Equal(VideoSnapshotPolicy.MaximumFileNameLength, primary.Length);
        Assert.EndsWith("-frame-01m05s.jpg", primary, StringComparison.Ordinal);

        var msDisambiguated = VideoSnapshotPolicy.FileNameFor(longStem, 65_432, "image/jpeg", [primary]);
        Assert.Equal(VideoSnapshotPolicy.MaximumFileNameLength, msDisambiguated.Length);
        Assert.EndsWith("-frame-01m05s432ms.jpg", msDisambiguated, StringComparison.Ordinal);

        var numbered = VideoSnapshotPolicy.FileNameFor(longStem, 65_432, "image/jpeg", [primary, msDisambiguated]);
        Assert.Equal(VideoSnapshotPolicy.MaximumFileNameLength, numbered.Length);
        Assert.EndsWith("-frame-01m05s432ms (2).jpg", numbered, StringComparison.Ordinal);
    }

    private static SnapshotCaseVideoCommand ValidCommand() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            14_250,
            "image/jpeg",
            MinimalJpeg,
            Engineer,
            "snapshot-op-1");
}
