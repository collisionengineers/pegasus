namespace Pegasus.Web.Presentation;

/// <summary>
/// How a release note's body is drawn: blank lines separate paragraphs, and a
/// run of lines beginning with "- " is a list. Nothing else is interpreted; the
/// words are the Administrator's as typed.
/// </summary>
public static class ReleaseNoteBody
{
    public sealed record Block(bool IsList, IReadOnlyList<string> Lines);

    public static IReadOnlyList<Block> Parse(string? body)
    {
        var blocks = new List<Block>();
        var paragraph = new List<string>();
        var list = new List<string>();
        foreach (var raw in (body ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                Flush(blocks, paragraph, list);
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (paragraph.Count > 0)
                {
                    blocks.Add(new Block(false, paragraph.ToArray()));
                    paragraph.Clear();
                }

                list.Add(line[2..].Trim());
                continue;
            }

            if (list.Count > 0)
            {
                blocks.Add(new Block(true, list.ToArray()));
                list.Clear();
            }

            paragraph.Add(line);
        }

        Flush(blocks, paragraph, list);
        return blocks;
    }

    private static void Flush(List<Block> blocks, List<string> paragraph, List<string> list)
    {
        if (paragraph.Count > 0)
        {
            blocks.Add(new Block(false, paragraph.ToArray()));
            paragraph.Clear();
        }

        if (list.Count > 0)
        {
            blocks.Add(new Block(true, list.ToArray()));
            list.Clear();
        }
    }
}
