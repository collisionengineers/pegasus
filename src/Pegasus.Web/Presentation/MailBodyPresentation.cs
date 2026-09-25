using Pegasus.Core.Intake;

namespace Pegasus.Web.Presentation;

/// <summary>
/// View shaping for a retained message body: the leading forwarded
/// From:/Sent:/To:/Subject: block becomes a quoted header, and each remaining
/// non-blank line becomes a paragraph. Pure presentation over already-cleaned
/// text.
/// </summary>
public static class MailBodyPresentation
{
    public sealed record Presented(
        IReadOnlyList<string> QuotedHeader,
        IReadOnlyList<string> Paragraphs);

    public static Presented Present(string bodyPlainText)
    {
        ArgumentNullException.ThrowIfNull(bodyPlainText);
        var (headerLines, body) = StaffForwardBodyCleaner.SplitForwardedHeader(bodyPlainText);
        body = StaffForwardBodyCleaner.TrimProviderFooter(body);
        var paragraphs = body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => line.Length > 0)
            .ToList();

        return new(headerLines, paragraphs);
    }
}
