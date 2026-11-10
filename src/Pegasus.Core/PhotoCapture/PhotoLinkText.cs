using Pegasus.Core.Identity;

namespace Pegasus.Core.PhotoCapture;

/// <summary>
/// The text provider port (FRD-28 The photo link text; R916): one text to
/// one UK mobile from Pegasus's own number. Infrastructure answers it with
/// Azure Communication Services, or with an unavailable send where no number
/// is configured, so no page offers Send by text.
/// </summary>
public interface IPhotoLinkTextSend
{
    /// <summary>Whether a text can be sent in this deployment at all.</summary>
    bool Available { get; }

    /// <summary>Hands one text to the provider. <paramref name="recipient"/> is E.164 (<see cref="UkMobile.ToE164"/>).</summary>
    Task<PhotoLinkTextOutcome> SendAsync(string recipient, string message, CancellationToken cancellationToken);
}

/// <summary>
/// What the provider answered: accepted with its message id, or refused with
/// a failure code. Accepted is not delivered: delivery reports are not read
/// (R919).
/// </summary>
public sealed record PhotoLinkTextOutcome(bool Accepted, string? MessageId, string? FailureCode)
{
    public static PhotoLinkTextOutcome Sent(string? messageId) => new(true, messageId, null);

    public static PhotoLinkTextOutcome Failed(string failureCode) => new(false, null, failureCode);
}

/// <summary>
/// A UK mobile number, the only number a photo link is texted to (R916):
/// 07… with eleven digits, or the same as +44 7…, 0044 7… or 44 7…, with
/// spaces, dashes, dots and brackets ignored and "+44 (0)7…" read as written.
/// </summary>
public static class UkMobile
{
    /// <summary>The number in E.164 (+447…), or null when it is not a UK mobile.</summary>
    public static string? ToE164(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var digits = new System.Text.StringBuilder(trimmed.Length);
        for (var index = 0; index < trimmed.Length; index++)
        {
            var character = trimmed[index];
            if (char.IsAsciiDigit(character))
            {
                digits.Append(character);
            }
            else if (!(character == '+' && index == 0) && character is not (' ' or '-' or '.' or '(' or ')'))
            {
                return null;
            }
        }

        var all = digits.ToString();
        string national;
        if (trimmed[0] == '+')
        {
            if (!all.StartsWith("44", StringComparison.Ordinal))
            {
                return null;
            }

            national = all[2..];
        }
        else if (all.StartsWith("0044", StringComparison.Ordinal))
        {
            national = all[4..];
        }
        else if (all.StartsWith("44", StringComparison.Ordinal) && all.Length == 12)
        {
            national = all[2..];
        }
        else if (all.StartsWith('0'))
        {
            national = all;
        }
        else
        {
            return null;
        }

        if (national.StartsWith('0'))
        {
            national = national[1..];
        }

        return national.Length == 10 && national[0] == '7' ? "+44" + national : null;
    }
}

/// <summary>
/// Texts a photo link that already exists (FRD-28 The photo link text; R916
/// to R919). Staff reach it only through a composer (R915): the Send page or
/// the Resend composer makes the link, then this sends the message staff
/// confirmed. The text act is written to the link's history before the
/// provider is called, so one operation key sends one text however often the
/// form is posted; the provider's answer is written after it, as "Sent by
/// text" or "Text not sent", naming who sent it.
/// </summary>
public sealed class SendPhotoLinkText(
    IPhotoLinkTextSend sender,
    IPhotoCaptureStore captures,
    TimeProvider timeProvider)
{
    public const string NotAUkMobile = "Enter a UK mobile number to send by text.";
    public const string MessageRequired = "A text message is required.";
    public const string AlreadySent = "This text was already sent.";

    /// <summary>The longest text staff may send: ten segments of 153 characters.</summary>
    public const int MaximumLength = 1530;

    public bool Available => sender.Available;

    /// <summary>
    /// Sends <paramref name="message"/> to <paramref name="to"/> for the
    /// capture. Refuses a number that is not a UK mobile, an empty or overlong
    /// message, and an operation key that already sent a text.
    /// </summary>
    public async Task<PhotoLinkTextOutcome> ExecuteAsync(
        ActionActor actor,
        Guid captureId,
        string? to,
        string? message,
        string operationKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (!sender.Available)
        {
            throw new InvalidOperationException("No text provider is configured.");
        }

        var number = UkMobile.ToE164(to)
            ?? throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.InvalidRequest, NotAUkMobile);
        var text = message?.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (string.IsNullOrEmpty(text) || text.Length > MaximumLength)
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.InvalidRequest, MessageRequired);
        }

        var key = operationKey.Trim();
        var first = await captures.RecordTextAsync(
            captureId,
            actor,
            key + ":text",
            PhotoLinkActs.TextSubmitted,
            new(number),
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (!first)
        {
            throw new PhotoCaptureRefusalException(PhotoCaptureRefusals.InvalidRequest, AlreadySent);
        }

        var outcome = await sender.SendAsync(number, text, cancellationToken);

        // The provider has answered: its answer is recorded even if the
        // request is abandoned now, so the history says what was sent.
        await captures.RecordTextAsync(
            captureId,
            actor,
            key + ":text-outcome",
            outcome.Accepted ? PhotoLinkActs.TextSent : PhotoLinkActs.TextFailed,
            new(number, outcome.MessageId, outcome.FailureCode),
            timeProvider.GetUtcNow(),
            CancellationToken.None);
        return outcome;
    }
}
