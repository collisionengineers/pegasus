using System.Net;
using System.Text.RegularExpressions;
using MimeKit;

namespace Pegasus.Infrastructure.Intake;

public sealed record LocalEmailDisplay(
    string From,
    string To,
    string Cc,
    string SentAt,
    string Subject,
    string Body,
    IReadOnlyList<string> AttachmentNames);

/// <summary>
/// Reads only decoded message display data. It never renders HTML or resolves resources.
/// </summary>
public static partial class LocalEmailDisplayReader
{
    public static async Task<LocalEmailDisplay> ReadAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var message = await MimeMessage.LoadAsync(source, cancellationToken);
        var body = message.TextBody;
        if (string.IsNullOrWhiteSpace(body) && !string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            body = ToInertText(message.HtmlBody);
        }

        return new LocalEmailDisplay(
            message.From.ToString(),
            message.To.ToString(),
            message.Cc.ToString(),
            message.Date == DateTimeOffset.MinValue ? string.Empty : message.Date.ToString("u"),
            message.Subject ?? string.Empty,
            body ?? string.Empty,
            message.Attachments
                .Select(part => part.ContentDisposition?.FileName ?? part.ContentType.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToArray());
    }

    private static string ToInertText(string html) =>
        WebUtility.HtmlDecode(HtmlTagRegex().Replace(html, " "))
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();
}
