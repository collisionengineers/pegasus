using Pegasus.Core.Intake;

namespace Pegasus.Web.Presentation;

/// <summary>
/// View shaping for a retained message body: the leading forwarded
/// From:/Sent:/To:/Subject: block becomes a quoted header, and each remaining
/// line becomes a paragraph — a blank line is a paragraph break, consecutive
/// lines stay tight (run-on). Pure presentation over already-cleaned text.
/// </summary>
public static class MailBodyPresentation
{
    public sealed record Paragraph(string Text, bool RunOn);

    public sealed record Presented(
        IReadOnlyList<string> QuotedHeader,
        IReadOnlyList<Paragraph> Paragraphs);

    public static Presented Present(string bodyPlainText)
    {
        ArgumentNullException.ThrowIfNull(bodyPlainText);
        var (headerLines, body) = StaffForwardBodyCleaner.SplitForwardedHeader(bodyPlainText);
        body = StaffForwardBodyCleaner.TrimProviderFooter(body);
        var lines = body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var paragraphs = new List<Paragraph>();
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }

            var next = index + 1 < lines.Length ? lines[index + 1].TrimEnd() : string.Empty;
            paragraphs.Add(new(line, RunOn: next.Length > 0));
        }

        return new(headerLines, paragraphs);
    }
}
