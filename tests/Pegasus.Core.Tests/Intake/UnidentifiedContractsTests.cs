using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Tests.Intake;

public sealed class UnidentifiedContractsTests
{
    [Theory]
    [InlineData(1, "U1")]
    [InlineData(99999, "U99999")]
    [InlineData(long.MaxValue, "U9223372036854775807")]
    public void ReferenceFormatIsCanonicalAndUnbounded(long sequence, string expected)
    {
        var reference = UnidentifiedReferenceFormat.Create(sequence);

        Assert.Equal(expected, reference);
        Assert.True(UnidentifiedReferenceFormat.TryParse(reference, out var parsed));
        Assert.Equal(sequence, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("u1")]
    [InlineData("U0")]
    [InlineData("U01")]
    [InlineData("U 1")]
    [InlineData("U9223372036854775808")]
    public void ReferenceParserRejectsNoncanonicalValues(string? value)
    {
        Assert.False(UnidentifiedReferenceFormat.TryParse(value, out _));
    }

    [Fact]
    public void ResolutionRequiresStaffOrAutomationActor()
    {
        var request = new ResolveUnidentifiedRequest(
            Guid.NewGuid(),
            0,
            ActionActor.SystemWorker("worker"),
            "op-1",
            "resolved",
            UnidentifiedResolutionTargetKind.ExternalReference,
            "target",
            null,
            DateTimeOffset.UtcNow);

        Assert.Throws<UnauthorizedAccessException>(() => UnidentifiedValidation.ValidateResolve(request));
    }

    [Fact]
    public void GroupOriginIsExplicitAndNonempty()
    {
        var id = Guid.NewGuid();

        var origin = UnidentifiedOrigin.SubmissionGroup(id);

        Assert.Equal(UnidentifiedOriginKind.SubmissionGroup, origin.Kind);
        Assert.Equal(id, origin.Id);
        Assert.Throws<ArgumentException>(() => UnidentifiedOrigin.Validate(new(UnidentifiedOriginKind.Receipt, Guid.Empty)));
    }

    [Theory]
    [InlineData(IntakeSourceChannel.Mailbox, "application/pdf", UnidentifiedMediaKind.Email)]
    [InlineData(IntakeSourceChannel.Mailbox, "image/jpeg", UnidentifiedMediaKind.Email)]
    [InlineData(IntakeSourceChannel.ManualUpload, "image/jpeg", UnidentifiedMediaKind.Image)]
    [InlineData(IntakeSourceChannel.ManualUpload, "image/png", UnidentifiedMediaKind.Image)]
    [InlineData(IntakeSourceChannel.ManualUpload, "application/pdf", UnidentifiedMediaKind.Document)]
    [InlineData(IntakeSourceChannel.Automation, "application/msword", UnidentifiedMediaKind.Document)]
    public void MediaKindPolicyClassifiesByChannelThenContentType(
        IntakeSourceChannel channel,
        string mediaType,
        UnidentifiedMediaKind expected)
    {
        Assert.Equal(expected, UnidentifiedMediaKindPolicy.Classify(channel, mediaType));
    }
}
