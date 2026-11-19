using System.Globalization;
using System.Text;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// The item the part list gives the quick repair panel for a part
/// (<c>createRepairViewItem</c> in <c>list.php</c>): its head row, marks,
/// operations, paint levels by operation, the time row of each operation the
/// panel offers, its part code and the paint areas it is painted in.
/// </summary>
internal sealed record GlassPanelItem(
    string Head,
    string Marks,
    string Operations,
    string PaintStats,
    string ReplaceTime,
    string RemoveTime,
    string RepairTime,
    string Code,
    IReadOnlyList<string> Zones)
{
    /// <summary>A part that can be painted (<c>L</c>); its paint is metal unless it is marked plastic (<c>P</c>).</summary>
    public bool Paintable => Marks.Contains('L', StringComparison.Ordinal);

    public bool Metal => !Marks.Contains('P', StringComparison.Ordinal);
}

/// <summary>
/// The quick repair panel under the editor's drawing (<c>rpvwpart.php</c>
/// with <c>rpvwpaint.php</c>, R1206): a part is added without its dialog,
/// by one press of New, Remove and Install or Repair (with the repair hours
/// its spinner holds), for one side or, with the Selection side's Both, for
/// each side in turn. Each press is one command to the editor
/// (<c>service.php?…&amp;partadd=…&amp;addpart2g=1</c>), as the panel's own
/// AddOrDeletePart builds it:
/// <c>head|time row|operation|repair hours|paint level|paint area|value|value</c>.
/// The paint level is the panel's own choice for the operation, from the
/// part's paint levels or, when the part is painted in more than one area,
/// from the area chosen in the paint box, whose row id is then sent. The two
/// values are only for a truck paint system. Judged offline on 19 November
/// 2026: the panel's own script, given the item as the list builds it, sent
/// glasss2missing.har #536 and #537 byte for byte (the front door skin, both
/// sides, Remove and Install), and 16 commands over seven parts. Only the
/// account's paint system <c>1</c> (AZT) is ported, the one every capture
/// shows.
/// </summary>
internal static class GlassEditorPanel
{
    /// <summary>The panel's New, Remove and Install and Repair, by the operation each sends.</summary>
    public const string Replace = "ER";

    public const string RemoveAndRefit = "AE";

    public const string Repair = "I ";

    /// <summary>The marks of levels that name their operations, matched as the panel's /E|I/i does.</summary>
    private static readonly char[] OperationMarks = ['E', 'I', 'e', 'i'];

    private static readonly (string Key, string Value)[] MetalLevels =
        [("I", "1"), ("II", "2"), ("III", "3"), ("IV", "4"), ("B", "B"), ("SP", "S")];

    private static readonly (string Key, string Value)[] PlasticLevels =
        [("K1R", "R"), ("K1N", "N"), ("K1G", "G"), ("K2", "2"), ("K3", "3"), ("B", "B"), ("SP", "S")];

    /// <summary>
    /// The item for the head row at <paramref name="head"/>, built as the
    /// list's <c>createRepairViewItem</c> builds it from the head and the
    /// time rows (<c>t</c>) of its block: the last time row of each operation,
    /// Remove and Install and Repair falling back to New's. Null when the row
    /// is not a part's head.
    /// </summary>
    public static GlassPanelItem? ItemOf(IReadOnlyList<GlassEditorPartRow> rows, int head)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (head < 0 || head >= rows.Count || !rows[head].IsPart)
        {
            return null;
        }

        var row = rows[head];
        string replace = string.Empty, remove = string.Empty, repair = string.Empty, first = string.Empty;
        for (var index = head + 1; index < rows.Count && !rows[index].Has('b'); index++)
        {
            var time = rows[index];
            if (!time.Has('t'))
            {
                continue;
            }
            if (first.Length == 0)
            {
                first = time.Id;
            }
            if (time.Operations.Contains("ER", StringComparison.Ordinal))
            {
                replace = time.Id;
            }
            if (time.Operations.Contains("AE", StringComparison.Ordinal))
            {
                remove = time.Id;
            }
            if (time.Operations.Contains("I ", StringComparison.Ordinal))
            {
                repair = time.Id;
            }
        }

        remove = remove.Length == 0 ? replace : remove;
        repair = repair.Length == 0 ? replace : repair;
        var operations = row.Operations;
        if (replace.Length + remove.Length + repair.Length == 0)
        {
            // No time row: the panel's buttons stay greyed.
            operations = string.Empty;
            replace = first;
        }

        return new GlassPanelItem(
            row.Id, row.Marks, operations, row.PaintStats, replace, remove, repair, row.Code,
            row.PaintZones.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// The value of one <c>partadd</c> command, or why the panel would not
    /// send it: the button greyed for the part, a repair hours text the
    /// spinner would not hold, or a paint area the part is not painted in.
    /// <paramref name="zone"/> is the area chosen in the paint box (its first
    /// when null), which the panel shows only for a part painted in more than
    /// one; <paramref name="zoneLevels"/> holds each area's paint levels from
    /// the paint list.
    /// </summary>
    public static (string? Value, string? Problem) PartAdd(
        GlassPanelItem item, string operation, string? hours, string? zone,
        IReadOnlyDictionary<string, string>? zoneLevels)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(operation);
        if (operation is not (Replace or RemoveAndRefit or Repair))
        {
            return (null, "The panel has no button for " + operation.Trim() + ".");
        }
        if (!item.Operations.Contains(operation, StringComparison.Ordinal))
        {
            return (null, "The panel greys its button for " + operation.Trim() + " on this part.");
        }

        var zoneShown = item.Zones.Count >= 2;
        var area = zone ?? (zoneShown ? item.Zones[0] : null);
        if (zone is not null && (!zoneShown || !item.Zones.Contains(zone)))
        {
            return (null, "The panel's paint box does not offer the area " + zone + " for this part.");
        }

        var repairHours = string.Empty;
        if (operation == Repair)
        {
            repairHours = hours ?? "0.0";
        }
        else if (hours is not null)
        {
            return (null, "The panel takes hours with Repair only.");
        }

        var level = string.Empty;
        if (item.Paintable)
        {
            var stats = item.PaintStats.Split('.')[0];
            if (zoneShown)
            {
                if (zoneLevels is null || !zoneLevels.TryGetValue(area!, out var areaStats))
                {
                    return (null, "The paint list holds no paint levels for the area " + area + ".");
                }

                stats = areaStats;
            }

            level = Level(stats, operation, item.Metal);
        }

        var time = operation switch
        {
            Replace => item.ReplaceTime,
            RemoveAndRefit => item.RemoveTime,
            _ => item.RepairTime,
        };
        var value = new StringBuilder()
            .Append(item.Head).Append('|').Append(time).Append('|').Append(operation).Append('|').Append(repairHours)
            .Append('|').Append(level).Append('|').Append(zoneShown ? area : string.Empty).Append("||")
            .ToString();
        return (value, null);
    }

    /// <summary>
    /// The commands for the values in order, as the panel sends a multiple
    /// selection and Both: each but the last asks the editor not to refresh
    /// its list of positions (<c>nofresh3=1</c>), and each after the first
    /// comes straight from the editor's answer, marked as from the parts
    /// frame (<c>actFrame3=1</c>); the first goes through the panel's repair
    /// cost check, which marks nothing.
    /// </summary>
    public static IReadOnlyList<string> Commands(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var commands = new List<string>(values.Count);
        for (var index = 0; index < values.Count; index++)
        {
            commands.Add("partadd=" + values[index]
                + (index + 1 < values.Count ? "&nofresh3=1" : string.Empty)
                + (index > 0 ? "&actFrame3=1" : string.Empty)
                + "&addpart2g");
        }

        return commands;
    }

    /// <summary>
    /// The paint level the panel sends for an operation, as its
    /// paintOpValidateAztOem chooses it: New offers I (metal) or K1R
    /// (plastic), Remove and Install and Repair III or K2. Levels that name
    /// operations (<c>E…I…</c>) give New the level after E and Repair the one
    /// after I, and Remove and Install none; otherwise the offered level
    /// stands if the levels hold it, else the first level they hold.
    /// </summary>
    private static string Level(string stats, string operation, bool metal)
    {
        var levels = metal ? MetalLevels : PlasticLevels;
        var level = operation switch
        {
            Replace => metal ? "I" : "K1R",
            _ => metal ? "III" : "K2",
        };
        if (stats.Length == 0)
        {
            return level;
        }

        if (stats.IndexOfAny(OperationMarks) >= 0)
        {
            var mark = operation switch
            {
                Replace => 'E',
                Repair => 'I',
                _ => '\0',
            };
            var at = mark == '\0' ? -1 : stats.IndexOf(mark, StringComparison.Ordinal);
            if (at < 0 || at + 1 >= stats.Length)
            {
                return string.Empty;
            }

            var after = stats[(at + 1)..(at + 2)];
            return Array.Find(levels, pair => pair.Value == after).Key ?? string.Empty;
        }

        var offered = Array.Find(levels, pair => pair.Key == level).Value;
        if (!stats.Contains(offered, StringComparison.Ordinal))
        {
            var first = stats[..1];
            var found = Array.Find(levels, pair => pair.Value == first).Key;
            level = found ?? level;
        }

        return level;
    }

    /// <summary>
    /// Each paint area's paint levels from the paint list
    /// (<c>list_paint.php</c>'s <c>lacklist</c>: field 16 the area's row id,
    /// field 15 its levels), as the list's paintZone reads them. Empty when
    /// the page holds no list.
    /// </summary>
    public static Dictionary<string, string> ZoneLevels(string html)
    {
        var levels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in PaintList(html))
        {
            if (row.Count > 16 && row[16].Length > 0)
            {
                levels.TryAdd(row[16], row[15]);
            }
        }

        return levels;
    }

    /// <summary>
    /// The paint list's rows (<c>list_paint.php</c>'s <c>lacklist</c>), each
    /// as its strings: field 0 the row a paint dialog is asked for by
    /// (<c>Lxx4200.480</c>), 1 its marks, 2 its name, 3 its group's name on
    /// the group's first row (R1206). Empty when the page holds no list.
    /// </summary>
    public static List<IReadOnlyList<string>> PaintList(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var rows = new List<IReadOnlyList<string>>();
        var start = html.IndexOf("lacklist = [", StringComparison.Ordinal);
        if (start < 0)
        {
            return rows;
        }

        var index = start + "lacklist = [".Length;
        var row = new List<string>();
        var depth = 1;
        while (index < html.Length && depth > 0)
        {
            var character = html[index];
            if (character == '\'')
            {
                var text = new StringBuilder();
                index++;
                while (index < html.Length && html[index] != '\'')
                {
                    if (html[index] == '\\' && index + 1 < html.Length)
                    {
                        index++;
                    }

                    text.Append(html[index]);
                    index++;
                }

                row.Add(text.ToString());
            }
            else if (character == '[')
            {
                depth++;
                row = [];
            }
            else if (character == ']')
            {
                depth--;
                if (depth == 1 && row.Count > 0)
                {
                    rows.Add(row);
                }
            }

            index++;
        }

        return rows;
    }

    /// <summary>The hours the panel's repair spinner holds, typed as the Automation types them: one decimal place.</summary>
    public static string? HoursText(decimal hours) =>
        hours < 0 || decimal.Round(hours, 1) != hours ? null : hours.ToString("0.0", CultureInfo.InvariantCulture);
}
