using System.Text;
using Xunit;
using Pegasus.Infrastructure.Intake.DocumentExtraction.Msg;

namespace Pegasus.IntegrationTests.DocumentExtraction;

public sealed class MsgFileBuilderTests
{
    [Fact]
    public void BuildProducesRealCompoundFileThatMsgReaderParses()
    {
        var bytes = new MsgFileBuilder()
            .WithRootMessage(
                "IPM.Note",
                "Synthetic subject",
                "Synthetic body text",
                senderSmtpAddress: "sender@example.invalid")
            .Build();

        var document = MsgReader.Read(bytes);

        Assert.True(
            document.Outcome is MsgReadOutcome.Complete or MsgReadOutcome.Partial,
            $"outcome={document.Outcome}; issues={string.Join(",", document.Issues.Select(issue => issue.Code))}");
        Assert.Equal("Synthetic subject", document.Projection.Fields["subject"]);
        Assert.Equal("sender@example.invalid", document.Projection.Fields["senderAddress"]);
        Assert.Equal("Synthetic body text", document.Bodies.CanonicalText);
    }

    [Fact]
    public void BuildWithAttachmentSurfacesContentBytes()
    {
        var payload = Encoding.ASCII.GetBytes("attachment-bytes");
        var bytes = new MsgFileBuilder()
            .WithRootMessage("IPM.Note", "Subject", "Body")
            .WithByValueAttachment("evidence.txt", "text/plain", payload)
            .Build();

        var document = MsgReader.Read(bytes);

        var attachment = Assert.Single(document.Attachments);
        Assert.Equal("evidence.txt", attachment.FileName);
        Assert.Equal(payload, attachment.Content.ToArray());
    }
}
