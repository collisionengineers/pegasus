using Pegasus.Core.Intake;

namespace Pegasus.Web.Presentation;

/// <summary>
/// View shaping for a retained message body: the leading forwarded
/// From:/Sent:/To:/Subject: block becomes a quoted header, the Principal's
/// footer and signature-image placeholder lines are dropped, and the rest is
/// the sender's paragraphs: a blank line starts a new paragraph and a single
/// line break stays a line break inside it (v54 D9). Pure presentation over
/// already-cleaned text.
/// </summary>
public static class MailBodyPresentation
{
    /// <param name="Paragraphs">Each paragraph's lines, in order; a paragraph has at least one line.</param>
    public sealed record Presented(
        IReadOnlyList<string> QuotedHeader,
        IReadOnlyList<IReadOnlyList<string>> Paragraphs);

    public static Presented Present(string bodyPlainText)
    {
        ArgumentNullException.ThrowIfNull(bodyPlainText);
        var (headerLines, body) = StaffForwardBodyCleaner.SplitForwardedHeader(bodyPlainText);
        body = StaffForwardBodyCleaner.TrimPrincipalFooter(body);
        body = StaffForwardBodyCleaner.WithoutImagePlaceholderLines(body);

        var paragraphs = new List<IReadOnlyList<string>>();
        var lines = new List<string>();
        foreach (var line in body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n'))
        {
            var trimmed = line.TrimEnd();
            if (trimmed.Length > 0)
            {
                lines.Add(trimmed);
                continue;
            }
            if (lines.Count > 0)
            {
                paragraphs.Add(lines);
                lines = [];
            }
        }
        if (lines.Count > 0)
        {
            paragraphs.Add(lines);
        }

        return new(headerLines, paragraphs);
    }
}
