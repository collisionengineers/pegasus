using System.Net;
using System.Text.RegularExpressions;
using MimeKit;

namespace Pegasus.Infrastructure.Brego;

/// <summary>
/// Reads Brego's sign-in e-mail from the Brego account's own mailbox
/// (26 November 2026, R1294). The mailbox is addressed by the account's
/// e-mail address; it is not an approved mailbox, nothing in it is taken into
/// intake, and nothing in it is changed: messages are only listed and read.
/// A mailbox that cannot be read answers <see langword="false"/> from
/// <see cref="CanReadAsync"/> and throws
/// <see cref="BregoSignInMailUnreadableException"/> from the reads.
/// </summary>
internal interface IBregoSignInMailReader
{
    /// <summary>Whether this host can read the Inbox of <paramref name="mailboxAddress"/>.</summary>
    Task<bool> CanReadAsync(string mailboxAddress, CancellationToken cancellationToken);

    /// <summary>
    /// The whole sign-in link, HTML-decoded, of the newest sign-in e-mail
    /// Brego sent to <paramref name="mailboxAddress"/> that arrived at or after
    /// <paramref name="sinceUtc"/>; none when none has arrived yet.
    /// </summary>
    Task<string?> ReadLinkAsync(string mailboxAddress, DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    /// <summary>
    /// The six-digit code of the newest "verify this browser" e-mail Brego
    /// sent to <paramref name="mailboxAddress"/> that arrived at or after
    /// <paramref name="sinceUtc"/>; none when none has arrived yet.
    /// </summary>
    Task<string?> ReadCodeAsync(string mailboxAddress, DateTimeOffset sinceUtc, CancellationToken cancellationToken);
}

/// <summary>The Brego account's mailbox could not be read: refused, throttled or unreachable. It names no value.</summary>
internal sealed class BregoSignInMailUnreadableException : Exception
{
    public BregoSignInMailUnreadableException()
        : base("Brego's sign-in mail could not be read.")
    {
    }

    public BregoSignInMailUnreadableException(Exception innerException)
        : base("Brego's sign-in mail could not be read.", innerException)
    {
    }
}

/// <summary>
/// Which messages are Brego's sign-in mail and what they carry (R1294). A
/// message is Brego's only when its one From address is
/// <c>noreply@brego.io</c>, its Subject is exactly the expected one, and the
/// receiving server's Authentication-Results header (the topmost) says
/// <c>dmarc=pass</c>. The link is read from the HTML, never from Brego's
/// click-tracking link, and must be for the account's own e-mail address;
/// the code is the one six-digit number in the visible text.
/// </summary>
internal static partial class BregoSignInMail
{
    internal const string Sender = "noreply@brego.io";
    internal const string LinkSubject = "Brego - Your sign-in link";
    internal const string CodeSubject = "Brego - Verify this browser";

    /// <summary>The account's sign-in link in <paramref name="message"/>, HTML-decoded, or none.</summary>
    internal static string? ReadLink(MimeMessage message, string accountEmail)
    {
        if (!IsFromBrego(message, LinkSubject) || Html(message) is not { } html)
        {
            return null;
        }

        string? found = null;
        string? foundToken = null;
        foreach (Match match in LinkPattern().Matches(WebUtility.HtmlDecode(html)))
        {
            if (!BregoClient.TryReadLinkToken(match.Value, accountEmail, out var token, out _))
            {
                continue;
            }

            if (foundToken is not null && !string.Equals(foundToken, token, StringComparison.Ordinal))
            {
                // Two different links in one message: neither is trusted.
                return null;
            }

            found ??= match.Value;
            foundToken = token;
        }

        return found;
    }

    /// <summary>The one six-digit code in the visible text of <paramref name="message"/>, or none.</summary>
    internal static string? ReadCode(MimeMessage message)
    {
        if (!IsFromBrego(message, CodeSubject) || Html(message) is not { } html)
        {
            return null;
        }

        var visible = HiddenBlockPattern().Replace(html, " ");
        visible = CommentPattern().Replace(visible, " ");
        visible = TagPattern().Replace(visible, " ");
        visible = WebUtility.HtmlDecode(visible);
        visible = UrlPattern().Replace(visible, " ");
        string? code = null;
        foreach (Match match in CodePattern().Matches(visible))
        {
            if (code is not null && !string.Equals(code, match.Value, StringComparison.Ordinal))
            {
                return null;
            }

            code = match.Value;
        }

        return code;
    }

    private static bool IsFromBrego(MimeMessage message, string subject)
    {
        var senders = message.From.Mailboxes.ToList();
        if (senders.Count != 1
            || !string.Equals(senders[0].Address, Sender, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(message.Subject, subject, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var header in message.Headers)
        {
            if (string.Equals(header.Field, "Authentication-Results", StringComparison.OrdinalIgnoreCase))
            {
                return header.Value.Contains("dmarc=pass", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    private static string? Html(MimeMessage message) =>
        string.IsNullOrWhiteSpace(message.HtmlBody) ? null : message.HtmlBody;

    // The link as Brego writes it, standing on its own: not inside another
    // URL, which is how the click-tracking link carries it.
    [GeneratedRegex(
        @"(?<![\w%/.:=-])https://www\.brego\.ai/login[^\s""'<>#]*#magic-link=[^\s""'<>]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex LinkPattern();

    [GeneratedRegex(
        @"<(style|script|head|title)\b[^>]*>.*?</\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex HiddenBlockPattern();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"(?:https?://|www\.)\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"(?<![0-9])[0-9]{6}(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
