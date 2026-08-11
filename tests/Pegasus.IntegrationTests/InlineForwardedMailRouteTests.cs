using MimeKit;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.IntegrationTests;

public sealed class InlineForwardedMailRouteTests
{
    private static readonly DateTimeOffset ReceivedAtUtc =
        new(2031, 8, 11, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PlainTextOutlookHeaderQuartetProducesAnInlineOriginalRouteIdentity()
    {
        var result = await ReadAsync(
            "desk@collisionengineers.co.uk",
            "From: QDOS Instructions <instructions@qdosassist.co.uk>\r\n"
            + "Sent: 11 August 2031 09:00\r\n"
            + "To: Instructions <instructions@collisionengineers.co.uk>\r\n"
            + "Subject: Engineer notification\r\n\r\n"
            + "Please process the attached instruction.");

        var route = new QdosMailRoutePolicy().Evaluate(result);

        Assert.Equal(
            "instructions@qdosassist.co.uk",
            Assert.Single(result.TransportEvidence, item =>
                item.SenderIdentityKind == IntakeSenderIdentityKind.InlineForwardedOriginal).Value);
        Assert.Equal(MailRouteDisposition.Accepted, route.Disposition);
        Assert.Equal("desk@collisionengineers.co.uk", Assert.Single(route.TransportIdentities).Address);
        var original = Assert.Single(route.OriginalIdentities);
        Assert.Equal("instructions@qdosassist.co.uk", original.Address);
        Assert.Equal("uploaded forwarded.eml, inline forwarded-message header", original.SourceLabel);
        Assert.Equal("instructions@qdosassist.co.uk", route.EffectiveSender?.Address);
    }

    [Fact]
    public async Task HtmlOutlookHeaderQuartetProducesAnInlineOriginalRouteIdentity()
    {
        var message = CreateMessage("desk@collisionengineers.co.uk");
        message.Body = new TextPart("html")
        {
            Text = "<p>From: QDOS Instructions &lt;instructions@qdosassist.co.uk&gt;<br>"
                + "Sent: 11 August 2031 09:00<br>"
                + "To: Instructions &lt;instructions@collisionengineers.co.uk&gt;<br>"
                + "Subject: Engineer notification</p>"
        };

        var result = await ReadAsync(message);

        Assert.Equal(
            "instructions@qdosassist.co.uk",
            Assert.Single(result.TransportEvidence, item =>
                item.SenderIdentityKind == IntakeSenderIdentityKind.InlineForwardedOriginal).Value);
    }

    [Theory]
    [InlineData("From: instructions@qdosassist.co.uk\r\nThis is normal prose.")]
    [InlineData("From: instructions@qdosassist.co.uk\r\nTo: inbox@example.invalid\r\nSent: now\r\nSubject: wrong order")]
    [InlineData("-----Original Message-----\r\nFrom: instructions@qdosassist.co.uk")]
    public async Task PartialOrMisorderedBodyHeaderDoesNotProduceOriginalIdentity(string body)
    {
        var result = await ReadAsync("desk@collisionengineers.co.uk", body);

        Assert.DoesNotContain(
            result.TransportEvidence,
            item => item.SenderIdentityKind == IntakeSenderIdentityKind.InlineForwardedOriginal);
    }

    [Fact]
    public async Task NonStaffTransportDoesNotProduceInlineOriginalIdentity()
    {
        var result = await ReadAsync(
            "outside@example.invalid",
            "From: QDOS Instructions <instructions@qdosassist.co.uk>\r\n"
            + "Sent: 11 August 2031 09:00\r\n"
            + "To: Instructions <instructions@collisionengineers.co.uk>\r\n"
            + "Subject: Engineer notification");

        Assert.DoesNotContain(
            result.TransportEvidence,
            item => item.SenderIdentityKind == IntakeSenderIdentityKind.InlineForwardedOriginal);
    }

    [Theory]
    [InlineData("From: not-an-address\r\nSent: now\r\nTo: inbox@example.invalid\r\nSubject: instruction")]
    [InlineData("From: first@qdosassist.co.uk\r\nSent: now\r\nTo: inbox@example.invalid\r\nSubject: first\r\n\r\nFrom: second@qdosassist.co.uk\r\nSent: now\r\nTo: inbox@example.invalid\r\nSubject: second")]
    public async Task MalformedOrMultipleInlineHeadersRemainNeedsSorting(string body)
    {
        var result = await ReadAsync("desk@collisionengineers.co.uk", body);

        var route = new QdosMailRoutePolicy().Evaluate(result);

        Assert.Equal(MailRouteDisposition.NeedsSorting, route.Disposition);
        Assert.Null(route.EffectiveSender);
    }

    private static async Task<IntakeSourceReadResult> ReadAsync(string sender, string body)
    {
        var message = CreateMessage(sender);
        message.Body = new TextPart("plain") { Text = body };
        return await ReadAsync(message);
    }

    private static async Task<IntakeSourceReadResult> ReadAsync(MimeMessage message)
    {
        using var stream = new MemoryStream();
        message.WriteTo(stream);
        return await new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System).ReadAsync(
            new(
                "forwarded.eml",
                "message/rfc822",
                stream.ToArray(),
                ReceivedAtUtc,
                "test",
                new(IntakeSourceChannel.Mailbox, "inline-forward-test")),
            CancellationToken.None);
    }

    private static MimeMessage CreateMessage(string sender)
    {
        var message = new MimeMessage { Subject = "Fw: engineer notification" };
        message.From.Add(MailboxAddress.Parse(sender));
        message.To.Add(MailboxAddress.Parse("instructions@collisionengineers.co.uk"));
        return message;
    }
}
