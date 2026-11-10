using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Tests.ImageIntake;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>
/// The photo link text (FRD-28 The photo link text; R918 to R921): only a UK
/// mobile is texted, the text act is written before the provider is called so
/// one operation key sends one text, and the provider's answer is written
/// after it as Sent by text or Text not sent.
/// </summary>
public sealed class SendPhotoLinkTextTests
{
    private const string Message = "Hi Sam Taylor, please open https://example.invalid/c/link";

    private static readonly ActionActor Staff =
        ActionActor.Staff(Guid.Parse("44444444-4444-4444-4444-444444444444"), [StaffRole.Administrator]);

    private static readonly Guid CaptureId = Capture().Id;

    /// <summary>
    /// A UK mobile is read however it is written, and becomes E.164; a
    /// landline, a short number, letters or a number abroad is not one (R918).
    /// </summary>
    [Theory]
    [InlineData("07700 900123", "+447700900123")]
    [InlineData("07700900123", "+447700900123")]
    [InlineData("+44 7700 900123", "+447700900123")]
    [InlineData("+44 (0)7700 900123", "+447700900123")]
    [InlineData("0044 7700 900123", "+447700900123")]
    [InlineData("447700900123", "+447700900123")]
    [InlineData(" 07700-900.123 ", "+447700900123")]
    [InlineData("020 7946 0123", null)]
    [InlineData("07700 9001", null)]
    [InlineData("07700 90012a", null)]
    [InlineData("+1 202 555 0123", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void AUkMobileIsReadInE164AndAnythingElseIsNot(string? typed, string? expected) =>
        Assert.Equal(expected, UkMobile.ToE164(typed));

    /// <summary>
    /// An accepted text writes Text submitted to the link's history before the
    /// provider is called, then Text sent with the provider's message id, each
    /// under its own key from the form's operation key (R921).
    /// </summary>
    [Fact]
    public async Task AnAcceptedTextIsWrittenBeforeTheProviderAndSentAfterIt()
    {
        var records = new List<RecordedText>();
        var captures = Store(records, firstWrites: true);
        var sender = new RecordingTextSend(PhotoLinkTextOutcome.Sent("message-1"), () => records.Count);

        var outcome = await Send(captures, sender).ExecuteAsync(
            Staff, CaptureId, "07700 900123", "  " + Message + "\r\n", "operation-1", CancellationToken.None);

        Assert.True(outcome.Accepted);
        Assert.Equal("message-1", outcome.MessageId);
        var sent = Assert.Single(sender.Sent);
        Assert.Equal("+447700900123", sent.To);
        Assert.Equal(Message, sent.Message);
        Assert.Equal(1, sent.RecordsBefore);
        Assert.Collection(
            records,
            submitted =>
            {
                Assert.Equal("operation-1:text", submitted.OperationKey);
                Assert.Equal(PhotoLinkActs.TextSubmitted, submitted.Act);
                Assert.Equal(new PhotoLinkTextRecord("+447700900123"), submitted.Record);
                Assert.Equal(CaptureId, submitted.CaptureId);
                Assert.Same(Staff, submitted.Actor);
                Assert.Equal(Now, submitted.OccurredAtUtc);
            },
            answered =>
            {
                Assert.Equal("operation-1:text-outcome", answered.OperationKey);
                Assert.Equal(PhotoLinkActs.TextSent, answered.Act);
                Assert.Equal(new PhotoLinkTextRecord("+447700900123", "message-1"), answered.Record);
            });
    }

    /// <summary>A text the provider refuses is written as Text not sent with its failure code (R921).</summary>
    [Fact]
    public async Task ARefusedTextIsWrittenAsNotSent()
    {
        var records = new List<RecordedText>();
        var captures = Store(records, firstWrites: true);
        var sender = new RecordingTextSend(PhotoLinkTextOutcome.Failed("http_500"), () => records.Count);

        var outcome = await Send(captures, sender).ExecuteAsync(
            Staff, CaptureId, "+44 7700 900123", Message, "operation-2", CancellationToken.None);

        Assert.False(outcome.Accepted);
        Assert.Equal("http_500", outcome.FailureCode);
        Assert.Single(sender.Sent);
        Assert.Equal(2, records.Count);
        var answered = records[1];
        Assert.Equal("operation-2:text-outcome", answered.OperationKey);
        Assert.Equal(PhotoLinkActs.TextFailed, answered.Act);
        Assert.Equal(new PhotoLinkTextRecord("+447700900123", null, "http_500"), answered.Record);
    }

    /// <summary>
    /// The same operation key sent again finds its text already submitted:
    /// it is refused as already sent and the provider is not called again.
    /// </summary>
    [Fact]
    public async Task AReplayedKeyIsRefusedAndTextsNothing()
    {
        var records = new List<RecordedText>();
        var captures = Store(records, firstWrites: false);
        var sender = new RecordingTextSend(PhotoLinkTextOutcome.Sent("message-1"), () => records.Count);

        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => Send(captures, sender).ExecuteAsync(
            Staff, CaptureId, "07700 900123", Message, "operation-3", CancellationToken.None));

        Assert.Equal(PhotoCaptureRefusals.InvalidRequest, refusal.Code);
        Assert.Equal(SendPhotoLinkText.AlreadySent, refusal.Message);
        Assert.Empty(sender.Sent);
        Assert.Single(captures.Calls);
    }

    /// <summary>A number that is not a UK mobile is refused in the operator's words, and nothing is written or sent.</summary>
    [Fact]
    public async Task ANumberThatIsNotAUkMobileIsRefusedBeforeAnythingIsWritten()
    {
        var records = new List<RecordedText>();
        var captures = Store(records, firstWrites: true);
        var sender = new RecordingTextSend(PhotoLinkTextOutcome.Sent("message-1"), () => records.Count);

        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => Send(captures, sender).ExecuteAsync(
            Staff, CaptureId, "+1 202 555 0123", Message, "operation-4", CancellationToken.None));

        Assert.Equal(PhotoCaptureRefusals.InvalidRequest, refusal.Code);
        Assert.Equal("Enter a UK mobile number to send by text.", refusal.Message);
        Assert.Empty(captures.Calls);
        Assert.Empty(sender.Sent);
    }

    /// <summary>Where no text provider is configured nothing can be texted, and nothing is written.</summary>
    [Fact]
    public async Task NoTextIsSentWhereNoProviderIsConfigured()
    {
        var records = new List<RecordedText>();
        var captures = Store(records, firstWrites: true);
        var sender = new RecordingTextSend(PhotoLinkTextOutcome.Sent("message-1"), () => records.Count) { Available = false };
        var send = Send(captures, sender);

        Assert.False(send.Available);
        await Assert.ThrowsAsync<InvalidOperationException>(() => send.ExecuteAsync(
            Staff, CaptureId, "07700 900123", Message, "operation-5", CancellationToken.None));

        Assert.Empty(captures.Calls);
        Assert.Empty(sender.Sent);
    }

    private static SendPhotoLinkText Send(AutomaticImageIntakeTests.Forwarder captures, RecordingTextSend sender) =>
        new(sender, (IPhotoCaptureStore)captures.Proxy, new FixedClock(Now));

    /// <summary>
    /// A store that records each text act; the first answers whether it was
    /// written (false: its key already sent a text), every later one writes.
    /// </summary>
    private static AutomaticImageIntakeTests.Forwarder Store(List<RecordedText> records, bool firstWrites)
    {
        var captures = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);
        captures.Overrides[nameof(IPhotoCaptureStore.RecordTextAsync)] = args =>
        {
            records.Add(new(
                (Guid)args[0]!,
                (ActionActor)args[1]!,
                (string)args[2]!,
                (string)args[3]!,
                (PhotoLinkTextRecord)args[4]!,
                (DateTimeOffset)args[5]!));
            return Task.FromResult(records.Count > 1 || firstWrites);
        };
        return captures;
    }

    private sealed record RecordedText(
        Guid CaptureId,
        ActionActor Actor,
        string OperationKey,
        string Act,
        PhotoLinkTextRecord Record,
        DateTimeOffset OccurredAtUtc);

    private sealed record SentText(string To, string Message, int RecordsBefore);

    /// <summary>A text provider that answers one outcome and records each text with how many acts were written before it.</summary>
    private sealed class RecordingTextSend(PhotoLinkTextOutcome outcome, Func<int> recordsWritten) : IPhotoLinkTextSend
    {
        public bool Available { get; init; } = true;

        public List<SentText> Sent { get; } = [];

        public Task<PhotoLinkTextOutcome> SendAsync(string recipient, string message, CancellationToken cancellationToken)
        {
            Sent.Add(new(recipient, message, recordsWritten()));
            return Task.FromResult(outcome);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
