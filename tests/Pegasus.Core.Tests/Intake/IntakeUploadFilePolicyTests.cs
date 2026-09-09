using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class IntakeUploadFilePolicyTests
{
    [Theory]
    [InlineData("walkaround.mp4", IntakeUploadFilePolicy.Mp4MediaType)]
    [InlineData("walkaround.mov", IntakeUploadFilePolicy.MovMediaType)]
    public void AdmitsOnlyAFileNameMediaTypeAndContentThatAgree(string fileName, string mediaType)
    {
        var content = mediaType == IntakeUploadFilePolicy.Mp4MediaType
            ? "\0\0\0\u0018ftypisom\0\0\x02\0isommp42"u8.ToArray()
            : "\0\0\0\u0014ftypqt  \0\0\0\0qt  "u8.ToArray();

        Assert.True(IntakeUploadFilePolicy.IsAccepted(fileName, mediaType, content));
        Assert.False(IntakeUploadFilePolicy.IsAccepted(fileName, mediaType, "not a video"u8));
    }

    [Fact]
    public void RejectsAVideoWithMismatchedNameAndMediaType()
    {
        var mp4 = "\0\0\0\u0018ftypisom\0\0\x02\0isommp42"u8.ToArray();

        Assert.False(IntakeUploadFilePolicy.IsAccepted("walkaround.mov", IntakeUploadFilePolicy.Mp4MediaType, mp4));
    }
}
