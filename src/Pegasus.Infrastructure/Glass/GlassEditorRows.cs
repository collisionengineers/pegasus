using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// The rows one line of an outside estimate is placed by: the head row it was
/// found by and the row pair its dialog is asked for with (<c>17|18</c>), or
/// the reason no row was chosen. <see cref="Same"/> is what makes two heads
/// the same part: part number, price and the operation's hours (Replace's,
/// or the operation's own time row's).
/// </summary>
internal sealed record GlassEditorRowChoice(
    GlassEditorPartRow? Head, string? Rows, string? ReasonCode, string? Same = null)
{
    public static GlassEditorRowChoice Refused(string reasonCode) => new(null, null, reasonCode);
}

/// <summary>
/// Which row of Glass's part list a part line is placed by (operator, 18
/// November 2026; R1169, R1172). A part is placed only where the choice is
/// certain: where the editor's own list code, given that row, asks no
/// equipment question and gives the same row pair. Anything less is left off
/// with its reason, and nothing is guessed.
///
/// <para>
/// <b>Finding the part.</b> A Glass's export's line is found by Glass's part
/// code and the side its description names: the list's head rows (marked
/// <c>p</c> and <c>b</c>) with that code and side marking; a line with no
/// side takes only a row marked neither left nor right. An Audatex line is
/// found by its part number on any part row; a number on a variant row (not a
/// head) is a variant that only an equipment answer chooses.
/// </para>
///
/// <para>
/// <b>When a head is certain.</b> Its block (the head and the rows after it
/// up to the next row marked <c>b</c>) carries no equipment criteria; neither
/// the head nor its drawing number's row is shown or hidden by equipment; it
/// is not a wheel, whose rows the editor chooses with its own wheel
/// selector; it offers Replace; and its equipment map (<c>reqcritmap3</c>)
/// lists, in its part group and its Replace group, no entry that needs
/// equipment. The pair is the head and the map's Replace entry, or else the
/// block's Replace time row that needs no equipment.
/// </para>
///
/// <para>
/// <b>Several heads.</b> Glass's lists one part on more than one drawing.
/// Every head found must be certain and be the same part (part number, price
/// and Replace hours); the first in the list's order is placed. Heads that
/// are not the same part are ambiguous.
/// </para>
///
/// <para>
/// <b>Repair and remove-and-refit (R1172).</b> The editor asks for a part's
/// dialog by the same row pair whatever the operation (its list code's
/// <c>getSelPartTime</c>); the operation is chosen in the dialog, which
/// serves the operation's hours from the operation's own time row. So a
/// Repair (<c>I</c>) or remove-and-refit (<c>AE</c>) line is placed by the
/// pair Replace would be, only when the head is certain as above, offers
/// the operation as well as Replace, its map's remove-and-refit group lists
/// no entry that needs equipment and at most one entry, and the block has at
/// most one time row of the operation (the map's own <c>AE</c> entry where
/// it names one). A head that offers no Replace is left off: the editor
/// asks for it with another time row, chosen differently with and without
/// the row's map.
/// </para>
///
/// <para>
/// <b>Judged offline.</b> On the captured list of 7 September 2026 (5,163
/// rows) the editor's own code, run with the row's side as a browser sets it,
/// agreed with this rule on every one of the 536 code-and-side lines and 417
/// part numbers it calls certain. For the two other operations it agreed,
/// with no equipment question open, on the same row pair and on its own
/// choice of the operation's time rows, for every one of the 499
/// code-and-side lines it calls certain for remove-and-refit and the 491 for
/// Repair, with the rows' equipment maps fed to it and without (18 November
/// 2026).
/// </para>
/// </summary>
internal static class GlassEditorRows
{
    /// <summary>Glass's code for Replace, the operation a part is placed with.</summary>
    public const string Replace = "ER";

    /// <summary>Glass's code for Repair ("R").</summary>
    public const string Repair = "I";

    /// <summary>Glass's code for Uninstall and install ("UI"): remove and refit.</summary>
    public const string RemoveAndRefit = "AE";

    /// <summary>
    /// The head rows a line is found by, as indexes into the list in its
    /// order, or the reason there are none to try.
    /// </summary>
    public static (IReadOnlyList<int> Heads, string? ReasonCode) Find(
        IReadOnlyList<GlassEditorPartRow> list, GlassEstimateBuildKeyKind kind, string? key, string? side)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (string.IsNullOrEmpty(key) || kind is not (GlassEstimateBuildKeyKind.PartCode or GlassEstimateBuildKeyKind.PartNumber))
        {
            return (NoHeads, GlassEstimateBuildPolicy.NoRowCode);
        }

        var heads = new List<int>();
        for (var index = 0; index < list.Count; index++)
        {
            var row = list[index];
            if (!row.IsPart)
            {
                continue;
            }
            if (kind == GlassEstimateBuildKeyKind.PartCode)
            {
                if (row.IsHead && row.Code == key && row.Side == side)
                {
                    heads.Add(index);
                }

                continue;
            }
            if (row.Number != key)
            {
                continue;
            }
            if (!row.IsHead)
            {
                return (NoHeads, GlassEstimateBuildPolicy.EquipmentCode);
            }

            heads.Add(index);
        }

        if (heads.Count == 0)
        {
            return (NoHeads, GlassEstimateBuildPolicy.NoRowCode);
        }

        return (heads, null);
    }

    private static readonly int[] NoHeads = [];

    /// <summary>
    /// What the list alone says of a head for an operation (Replace when
    /// none is named): the reason it is not certain, or null when its
    /// equipment map is to be read next.
    /// </summary>
    public static string? ListRefusal(IReadOnlyList<GlassEditorPartRow> list, int head, string operation = Replace)
    {
        ArgumentNullException.ThrowIfNull(list);
        var row = list[head];
        if (row.Has('w'))
        {
            return GlassEstimateBuildPolicy.EquipmentCode;
        }
        if (!row.Operations.Contains(Replace, StringComparison.Ordinal))
        {
            return GlassEstimateBuildPolicy.NoRowCode;
        }
        if (operation != Replace && !OperationsOf(row).Contains(operation))
        {
            return GlassEstimateBuildPolicy.NoRowCode;
        }
        if (row.ShownByEquipment || list.Any(other => other.Has('c') && other.Drawing == row.Drawing
            && other.DrawingNumber == row.DrawingNumber && other.ShownByEquipment))
        {
            return GlassEstimateBuildPolicy.EquipmentCode;
        }

        return Block(list, head).Any(other => other.Criteria.Length > 0)
            ? GlassEstimateBuildPolicy.EquipmentCode
            : null;
    }

    /// <summary>
    /// One head, decided for an operation (Replace when none is named) with
    /// its equipment map as the editor serves it.
    /// </summary>
    public static GlassEditorRowChoice Decide(
        IReadOnlyList<GlassEditorPartRow> list, int head, IReadOnlyList<IReadOnlyList<string>> map,
        string operation = Replace)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (ListRefusal(list, head, operation) is { } refused)
        {
            return GlassEditorRowChoice.Refused(refused);
        }

        var row = list[head];
        if (map.Count < 2 || map[0].Count < 2 || map[0][1] != row.Code)
        {
            // Not the map of this row: nothing is chosen from it.
            return GlassEditorRowChoice.Refused(GlassFailure.EditorAnswer);
        }

        var parts = map[1];
        var replace = Group(map, Replace);
        if (parts.Count > 1 || replace.Count > 1 || !parts.All(Plain) || !replace.All(Plain))
        {
            return GlassEditorRowChoice.Refused(GlassEstimateBuildPolicy.EquipmentCode);
        }

        var refit = Group(map, RemoveAndRefit);
        if (operation != Replace && (refit.Count > 1 || !refit.All(Plain)))
        {
            return GlassEditorRowChoice.Refused(GlassEstimateBuildPolicy.EquipmentCode);
        }
        if (parts.Count == 1 && RowOf(parts[0]) != row.Id)
        {
            return GlassEditorRowChoice.Refused(GlassEstimateBuildPolicy.EquipmentCode);
        }

        var block = Block(list, head).ToList();
        var time = replace.Count == 1
            ? block.Find(other => other.Id == RowOf(replace[0]))
            : block.Find(other => other.Has('t') && other.Operations == Replace && other.Criteria.Length == 0);
        if (time is null)
        {
            return GlassEditorRowChoice.Refused(GlassEstimateBuildPolicy.NoRowCode);
        }
        if (operation == Replace)
        {
            return new GlassEditorRowChoice(row, row.Id + "|" + time.Id, null, row.Number + "|" + row.Price + "|" + time.Price);
        }

        // The operation's own hours: the map's remove-and-refit entry where it
        // names one, else the block's time rows of the operation; more than
        // one is a choice the dialog makes and Pegasus cannot see.
        var own = operation == RemoveAndRefit && refit.Count == 1
            ? block.FindAll(other => other.Id == RowOf(refit[0]))
            : block.FindAll(other => other.Has('t') && other.Operations.Trim() == operation);
        if (own.Count > 1)
        {
            return GlassEditorRowChoice.Refused(GlassEstimateBuildPolicy.AmbiguousRowCode);
        }

        return new GlassEditorRowChoice(
            row, row.Id + "|" + time.Id, null, row.Number + "|" + row.Price + "|" + (own.Count == 1 ? own[0].Price : string.Empty));
    }

    /// <summary>
    /// The line's choice from its heads' decisions: the first head's rows when
    /// every head is certain and the same part; otherwise the first head's
    /// reason, or ambiguous.
    /// </summary>
    public static GlassEditorRowChoice Choose(IReadOnlyList<GlassEditorRowChoice> decided)
    {
        ArgumentNullException.ThrowIfNull(decided);
        if (decided.Count == 0)
        {
            return GlassEditorRowChoice.Refused(GlassEstimateBuildPolicy.NoRowCode);
        }
        if (decided.FirstOrDefault(choice => choice.Rows is null) is { } notCertain)
        {
            return notCertain;
        }

        return decided.Select(choice => choice.Same).Distinct(StringComparer.Ordinal).Skip(1).Any()
            ? GlassEditorRowChoice.Refused(GlassEstimateBuildPolicy.AmbiguousRowCode)
            : decided[0];
    }

    /// <summary>The operations a row offers, two letters each (<c>ERAEI </c> is Replace, remove-and-refit and Repair).</summary>
    private static List<string> OperationsOf(GlassEditorPartRow row)
    {
        var operations = new List<string>();
        for (var index = 0; index < row.Operations.Length; index += 2)
        {
            var operation = row.Operations.Substring(index, Math.Min(2, row.Operations.Length - index)).Trim();
            if (operation.Length > 0)
            {
                operations.Add(operation);
            }
        }

        return operations;
    }

    /// <summary>A head and the rows after it, up to the next row marked <c>b</c>: the rows its map covers.</summary>
    private static IEnumerable<GlassEditorPartRow> Block(IReadOnlyList<GlassEditorPartRow> list, int head)
    {
        yield return list[head];
        for (var index = head + 1; index < list.Count && !list[index].Has('b'); index++)
        {
            yield return list[index];
        }
    }

    /// <summary>The entries of a map's group for one operation (<c>["ER", "e|0|196", …]</c>), without the operation.</summary>
    private static List<string> Group(IReadOnlyList<IReadOnlyList<string>> map, string operation)
    {
        for (var index = 2; index < map.Count; index++)
        {
            if (map[index].Count > 1 && map[index][0] == operation)
            {
                return map[index].Skip(1).ToList();
            }
        }

        return [];
    }

    /// <summary>A map entry that needs no equipment: <c>e|0|row</c>.</summary>
    private static bool Plain(string entry)
    {
        var parts = entry.Split('|');
        return parts.Length > 1 && parts[0] == "e" && parts[1] is "" or "0";
    }

    private static string? RowOf(string entry)
    {
        var parts = entry.Split('|');
        return parts.Length > 2 ? parts[2] : null;
    }
}
