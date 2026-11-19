using System.Globalization;
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
/// The equipment question one head's block asks, as the editor's list code
/// builds it (R1206): what the Automation is shown (<see cref="Asked"/>),
/// the criteria the vehicle fixes and the dialog adds to every answer
/// unshown, every criterion of the block the vehicle's list knows in number
/// order, the criteria of its part rows and of its time rows, and the block's
/// rows as the editor takes them.
/// </summary>
internal sealed record GlassEditorQuestion(
    GlassEquipmentQuestion Asked,
    IReadOnlyList<string> Frozen,
    IReadOnlyList<string> All,
    IReadOnlyList<string> PartCriteria,
    IReadOnlyList<string> TimeCriteria,
    IReadOnlyList<GlassEditorPartRow> Block);

/// <summary>
/// What one equipment answer leads to: the part row and its Replace time
/// row, the value stored for the vehicle as the editor stores it (null when
/// it stores nothing), and the values it changes; or the reason it leads to
/// none.
/// </summary>
internal sealed record GlassEditorAnswered(
    GlassEditorPartRow? Part,
    GlassEditorPartRow? Time,
    string? Store,
    IReadOnlyList<GlassEquipmentChange> Changes,
    string? ReasonCode)
{
    /// <summary>The row pair the dialog is asked for (<c>0|5</c>).</summary>
    public string? Rows => Part is null || Time is null ? null : Part.Id + "|" + Time.Id;

    public static GlassEditorAnswered Refused(string reasonCode) => new(null, null, null, [], reasonCode);
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
    /// <remarks>
    /// With <paramref name="vehicle"/>, the criteria the vehicle has, it is the
    /// Automation's live add or preview (R1206): a head shown or hidden on its
    /// drawing by equipment is decided as any other when the vehicle's
    /// criteria show it, as the editor shows it and a click opens its dialog
    /// with no question, and each refusal has a code of its own, so
    /// <see cref="GlassEstimateBuildPolicy.EquipmentCode"/> never comes back
    /// without its question. Judged offline on 19 November 2026 against the
    /// editor's own click path, over every head that asks no question and is
    /// shown by equipment for the vehicle: on the SEAT's list 40 of 61 gave
    /// the editor's pair and the rest are refused; on the BMW's 46 of 72, the
    /// same; no pair differed. Without it, a rebuild's rule is unchanged.
    /// </remarks>
    public static string? ListRefusal(
        IReadOnlyList<GlassEditorPartRow> list, int head, string operation = Replace, IReadOnlySet<string>? vehicle = null)
    {
        ArgumentNullException.ThrowIfNull(list);
        var row = list[head];
        if (row.Has('w'))
        {
            return vehicle is null ? GlassEstimateBuildPolicy.EquipmentCode : GlassAutomationPositionPolicy.EquipmentWheelCode;
        }
        if (!row.Operations.Contains(Replace, StringComparison.Ordinal))
        {
            return GlassEstimateBuildPolicy.NoRowCode;
        }
        if (operation != Replace && !OperationsOf(row).Contains(operation))
        {
            return GlassEstimateBuildPolicy.NoRowCode;
        }
        if (ShownByEquipment(list, head) && (vehicle is null || !Visible(list, head, vehicle)))
        {
            return vehicle is null ? GlassEstimateBuildPolicy.EquipmentCode : GlassAutomationPositionPolicy.EquipmentHiddenCode;
        }

        return Block(list, head).Any(other => other.Criteria.Length > 0)
            ? Uncertain(vehicle)
            : null;
    }

    /// <summary>A rule's own refusal of rows that depend on equipment: a rebuild's, or the Automation's own code.</summary>
    private static string Uncertain(IReadOnlySet<string>? vehicle) =>
        vehicle is null ? GlassEstimateBuildPolicy.EquipmentCode : GlassAutomationPositionPolicy.EquipmentUncertainCode;

    /// <summary>
    /// Whether the vehicle's criteria show a head on its drawing: the head's
    /// own show and hide fields, and those of its drawing number's rows.
    /// </summary>
    private static bool Visible(IReadOnlyList<GlassEditorPartRow> list, int head, IReadOnlySet<string> vehicle)
    {
        var row = list[head];
        return Shown(row, vehicle) == 1
            && list.All(other => !(other.Has('c') && other.Drawing == row.Drawing && other.DrawingNumber == row.DrawingNumber)
                || Shown(other, vehicle) == 1);
    }

    /// <summary>
    /// The most answers a search lists for one row (design 10 section 3, an
    /// engineering bound). Above it the row's equipment map's own part and
    /// Replace hours entries are given instead.
    /// </summary>
    public const int AnswerCap = 256;

    /// <summary>
    /// Glass's own note on an option another position uses, as its list page
    /// sets it (<c>partlist.ttReferenced</c>, list.php of 7 September 2026),
    /// for a page that does not set one.
    /// </summary>
    private const string GlassReferencedNote = "Criteria affects other parts!";

    /// <summary>
    /// The equipment question a head's block asks (R1206), as the editor's
    /// list code builds it (<c>getPart</c>, <c>addResCrit</c>,
    /// <c>critConvPart</c>), or null when it asks none: the editor then opens
    /// the part's dialog at once.
    /// </summary>
    /// <remarks>
    /// The block is the head and the rows after it up to the next row marked
    /// <c>b</c>, leaving out a row marked <c>T</c> of the other side and the
    /// rows of a drawing number the vehicle's criteria hide. It asks when any
    /// of its rows names a criterion. The options are the block's criteria
    /// the vehicle's list knows, in number order, but those the vehicle fixes
    /// (mark <c>f</c>), which the dialog adds to every answer when the vehicle
    /// has them. An option is greyed out when the head's own show or hide
    /// fields name it, when every part row of the block names it and Glass's
    /// does not keep it for the vehicle, or when it is a radio partner of a
    /// greyed radio option. Judged offline against the editor's own list code
    /// on the list of 7 September 2026: the options, the greying, the fixed
    /// criteria and the ticks agreed on all 396 heads accepted (19 November
    /// 2026).
    /// </remarks>
    public static GlassEditorQuestion? Question(GlassEditorPartList list, int head)
    {
        ArgumentNullException.ThrowIfNull(list);
        var rows = list.Rows;
        var block = AskedBlock(rows, head, list.Vehicle);
        if (!block.Exists(row => row.Criteria.Length > 0))
        {
            return null;
        }

        var all = block
            .SelectMany(row => CriteriaOf(row.Criteria))
            .Distinct(StringComparer.Ordinal)
            .Where(list.Criteria.ContainsKey)
            .OrderBy(code => code, ByNumber)
            .ToList();
        var criteria = all.Select(code => list.Criteria[code]).ToList();
        var frozen = criteria.Where(criterion => criterion.Has('f') && criterion.IsSet).Select(criterion => criterion.Code).ToList();
        var shown = criteria.Where(criterion => !criterion.Has('f')).ToList();

        // Greyed as the dialog greys them (critTree.isEnabled, crit.disCritsRB).
        var common = CriteriaOf(block[0].Criteria);
        foreach (var partRow in block.Skip(1).Where(item => item.Has('p')))
        {
            var own = CriteriaOf(partRow.Criteria);
            common = own.Count > 0 ? common.Where(own.Contains).ToList() : new List<string>();
        }

        var greyed = new HashSet<string>(
            common.Where(code => !(list.Criteria.TryGetValue(code, out var criterion) && criterion.Has('g'))),
            StringComparer.Ordinal);
        greyed.UnionWith(Zone(rows[head]));
        foreach (var radio in shown.Where(item => greyed.Contains(item.Code) && item.IsRadio))
        {
            foreach (var partner in shown.Where(item => item.Code != radio.Code
                && item.Group == radio.Group && item.Radio == "1"))
            {
                greyed.Add(partner.Code);
            }
        }

        var options = shown
            .Select(criterion => new GlassEquipmentOption(
                criterion.Code,
                criterion.Name,
                criterion.IsSet,
                greyed.Contains(criterion.Code),
                criterion.IsRadio ? criterion.Group : null,
                criterion.AffectsOtherParts ? list.ReferencedNote ?? GlassReferencedNote : null))
            .ToList();
        return new GlassEditorQuestion(
            new GlassEquipmentQuestion(options),
            frozen,
            all,
            block.Where(row => row.Has('p')).SelectMany(row => CriteriaOf(row.Criteria)).Distinct(StringComparer.Ordinal).ToList(),
            block.Where(row => row.Has('t')).SelectMany(row => CriteriaOf(row.Criteria)).Distinct(StringComparer.Ordinal).ToList(),
            block);
    }

    /// <summary>
    /// What the list alone says of a head that asks a question, for a part
    /// added as Part: the reason it is refused, or null when its equipment
    /// map is to be read next. A wheel (the editor's own wheel selector) and
    /// a head the vehicle's criteria hide on its drawing wait on equipment no
    /// answer gives, each with its own code; a head that offers no Replace
    /// has no row (R1206).
    /// </summary>
    public static string? AskedRefusal(GlassEditorPartList list, int head)
    {
        ArgumentNullException.ThrowIfNull(list);
        var row = list.Rows[head];
        if (row.Has('w'))
        {
            return GlassAutomationPositionPolicy.EquipmentWheelCode;
        }
        if (!row.Operations.Contains(Replace, StringComparison.Ordinal))
        {
            return GlassEstimateBuildPolicy.NoRowCode;
        }

        return ShownByEquipment(list.Rows, head) && !Visible(list.Rows, head, list.Vehicle)
            ? GlassAutomationPositionPolicy.EquipmentHiddenCode
            : null;
    }

    /// <summary>Whether a map is the head's own: its first list names the head's part code.</summary>
    public static bool IsMapOf(IReadOnlyList<IReadOnlyList<string>> map, GlassEditorPartRow head)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(head);
        return map.Count > 0 && map[0].Count > 1 && map[0][1] == head.Code;
    }

    /// <summary>
    /// The rows one equipment answer leads to, as the dialog's OK chooses
    /// them (<c>submitCritPartInCritDlg</c>, <c>getCritResPart</c>,
    /// <c>getSelPartTime</c>), and the value the editor stores for the
    /// vehicle before it asks for the dialog (<c>histSavePart</c>,
    /// <c>saveCritToKernel</c>); or why it leads to none (R1206).
    /// </summary>
    /// <remarks>
    /// The answer's rule is the ticked codes and the fixed ones the vehicle
    /// has, in number order. Its part criteria find the part row in the map's
    /// part group (a blank rule finds <c>0</c>; an empty group is the head),
    /// and its time criteria the Replace time row in the map's Replace group,
    /// trying again with only the criteria that group knows, else the block's
    /// Replace time row with no criteria. When the map names no part row, the
    /// part row is the block's whose own criteria are exactly the part
    /// criteria, with the time row after it whose own are exactly the time
    /// criteria; failing a part, the fixed criteria are dropped one at a time.
    /// The value stored is <c>code|0 or 1|1</c> for every option of the
    /// question Glass's keeps for the vehicle (mark <c>g</c>), in number
    /// order. Judged offline against the editor's own code on the list of
    /// 7 September 2026: the same pair and the same stored value for all 985
    /// answers on accepted heads where the editor gives a part, and both
    /// refusing the other 158 (19 November 2026).
    /// </remarks>
    public static GlassEditorAnswered Answer(
        GlassEditorPartList list, int head, GlassEditorQuestion question,
        IReadOnlyList<IReadOnlyList<string>> map, IReadOnlyList<string> ticked)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(ticked);
        var rows = list.Rows;
        if (!IsMapOf(map, rows[head]))
        {
            // Not the map of this row: nothing is chosen from it.
            return GlassEditorAnswered.Refused(GlassFailure.EditorAnswer);
        }
        if (GlassAutomationPositionPolicy.EquipmentRefusal(question.Asked, ticked) is { } refused)
        {
            return GlassEditorAnswered.Refused(refused);
        }

        var rule = question.Frozen.Concat(ticked).Distinct(StringComparer.Ordinal).OrderBy(code => code, ByNumber).ToList();
        var block = question.Block;
        var parts = map.Count > 1 ? Entries(map[1]) : new List<string[]>();
        var times = Entries(Group(map, Replace));
        var (partCriteria, timeCriteria) = Split(question, rule);

        // The editor's first way: the served map (getCritResPartByCritMap).
        var part = InBlock(block, parts.Count > 0 ? Find(partCriteria, parts) : rows[head].Id);
        GlassEditorPartRow? time = null;
        if (part is not null && part.Has('p'))
        {
            var timeId = FindTime(timeCriteria, times);
            time = (timeId.Length > 0 ? rows.FirstOrDefault(row => row.Id == timeId) : null)
                ?? block.FirstOrDefault(row => row.Has('t') && row.Operations == Replace && row.Criteria.Length == 0);
        }
        else
        {
            // Its second way, when the map names no row (getCritResPartByCrit).
            part = null;
            while (true)
            {
                (partCriteria, timeCriteria) = Split(question, rule);
                var at = IndexOf(block, row => row.Has('p') && Own(row) == partCriteria);
                if (at >= 0)
                {
                    part = block[at];
                    var after = block.Skip(at + 1).Where(row => row.Has('t') && Own(row) == timeCriteria).ToList();
                    time = after.Find(row => row.Operations == Replace) ?? after.FirstOrDefault();
                    break;
                }

                var fixedAt = rule.FindIndex(code => list.Criteria.TryGetValue(code, out var criterion) && criterion.Has('f'));
                if (fixedAt < 0)
                {
                    break;
                }

                rule.RemoveAt(fixedAt);
            }

            if (part is null)
            {
                return GlassEditorAnswered.Refused(GlassAutomationPositionPolicy.EquipmentNoPartCode);
            }
        }

        if (time is null || time.Operations != Replace)
        {
            return GlassEditorAnswered.Refused(GlassAutomationPositionPolicy.EquipmentNoPartCode);
        }

        var kept = question.All.Where(code => list.Criteria[code].Has('g')).ToList();
        var store = string.Join('|', kept.Select(code => code + "|" + (rule.Contains(code) ? "1" : "0") + "|1"));
        var changes = kept
            .Where(code => rule.Contains(code) != list.Criteria[code].IsSet)
            .Select(code => new GlassEquipmentChange(code, rule.Contains(code)))
            .ToList();
        return new GlassEditorAnswered(part, time, store.Length > 0 ? store : null, changes, null);
    }

    /// <summary>
    /// The question with every allowed answer that leads to a part (R1206):
    /// each combination of the options that are not greyed, at most one of a
    /// group, with the rows it leads to and the part list's figures for
    /// them. Above <see cref="AnswerCap"/> combinations the answers are left
    /// out and the map's own part and Replace hours entries are given.
    /// </summary>
    public static GlassEquipmentQuestion WithAnswers(
        GlassEditorPartList list, int head, GlassEditorQuestion question, IReadOnlyList<IReadOnlyList<string>> map)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(map);
        var slots = new List<List<string>>();
        foreach (var option in question.Asked.Options.Where(item => !item.Greyed))
        {
            var slot = option.Group is { } group
                ? slots.Find(existing => question.Asked.Options.First(item => item.Code == existing[0]).Group == group)
                : null;
            if (slot is null)
            {
                slots.Add([option.Code]);
            }
            else
            {
                slot.Add(option.Code);
            }
        }

        long count = 1;
        foreach (var slot in slots)
        {
            count *= slot.Count + 1;
            if (count > AnswerCap)
            {
                return question.Asked with
                {
                    MapParts = MapEntries(list.Rows, map.Count > 1 ? Entries(map[1]) : new List<string[]>(), part: true),
                    MapHours = MapEntries(list.Rows, Entries(Group(map, Replace)), part: false),
                };
            }
        }

        var answers = new List<GlassEquipmentAnswer>();
        var picked = new List<string>();
        void Pick(int at)
        {
            if (at == slots.Count)
            {
                var ticked = picked.OrderBy(code => code, ByNumber).ToList();
                var answered = Answer(list, head, question, map, ticked);
                if (answered is { Part: { } part, Time: { } time, Rows: { } pair })
                {
                    answers.Add(new GlassEquipmentAnswer(ticked, pair, Present(part.Number), Amount(part.Price), Amount(time.Price)));
                }

                return;
            }

            Pick(at + 1);
            foreach (var code in slots[at])
            {
                picked.Add(code);
                Pick(at + 1);
                picked.RemoveAt(picked.Count - 1);
            }
        }

        Pick(0);
        return question.Asked with { Answers = answers };
    }

    /// <summary>A map group's entries as the part list's figures for each: the codes the entry needs and its one row.</summary>
    private static List<GlassEquipmentAnswer> MapEntries(
        IReadOnlyList<GlassEditorPartRow> rows, List<string[]> entries, bool part) =>
        entries
            .Where(entry => entry.Length > 2)
            .Select(entry =>
            {
                var row = rows.FirstOrDefault(item => item.Id == entry[2]);
                return new GlassEquipmentAnswer(
                    CriteriaOf(entry[1]),
                    entry[2],
                    part ? Present(row?.Number) : null,
                    part ? Amount(row?.Price) : null,
                    part ? null : Amount(row?.Price));
            })
            .ToList();

    /// <summary>Whether a row, or its drawing number's row, is shown or hidden on its drawing by some equipment.</summary>
    private static bool ShownByEquipment(IReadOnlyList<GlassEditorPartRow> list, int head)
    {
        var row = list[head];
        return row.ShownByEquipment || list.Any(other => other.Has('c') && other.Drawing == row.Drawing
            && other.DrawingNumber == row.DrawingNumber && other.ShownByEquipment);
    }

    /// <summary>
    /// Whether the vehicle's criteria show a row (1), hide it (2) or both
    /// (3), as the list code's <c>calcShowHide</c> decides it: alternatives
    /// joined by <c>+</c>, each one's codes by <c>_</c>.
    /// </summary>
    internal static int Shown(GlassEditorPartRow row, IReadOnlySet<string> vehicle)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(vehicle);
        bool Holds(string rule) => rule.Split('_').All(vehicle.Contains);
        string[] show = row.Show.Length > 0 ? row.Show.Split('+') : [];
        string[] hide = row.Hide.Length > 0 ? row.Hide.Split('+') : [];
        if (vehicle.Count == 0)
        {
            return show.Length == 0 ? 1 : 2;
        }
        if (show.Length == 0 && hide.Length == 0)
        {
            return 1;
        }

        var shown = show.Any(Holds);
        var hidden = hide.Any(Holds);
        if (hide.Length == 0)
        {
            return shown ? 1 : 2;
        }
        if (show.Length == 0)
        {
            return hidden ? 2 : 1;
        }

        return shown && !hidden ? 1 : shown && hidden ? 3 : 2;
    }

    /// <summary>
    /// The block the editor's question is built from: the head and the rows
    /// after it up to the next row marked <c>b</c>, leaving out the rows of a
    /// drawing number the vehicle's criteria do not show and a row marked
    /// <c>T</c> that does not carry the head's side (<c>isLeftRightOK</c>).
    /// </summary>
    private static List<GlassEditorPartRow> AskedBlock(
        IReadOnlyList<GlassEditorPartRow> list, int head, IReadOnlySet<string> vehicle)
    {
        var first = list[head];
        var side = first.Has('l') ? 'l' : first.Has('r') ? 'r' : '\0';
        var block = new List<GlassEditorPartRow> { first };
        var hidden = false;
        for (var index = head + 1; index < list.Count && !list[index].Has('b'); index++)
        {
            var row = list[index];
            if (row.Has('c'))
            {
                hidden = Shown(row, vehicle) != 1;
            }
            if (hidden || (side != '\0' && row.Has('T') && !row.Has(side)))
            {
                continue;
            }

            block.Add(row);
        }

        return block;
    }

    /// <summary>The criteria the head's own show and hide fields name: the dialog greys them out (<c>activeZoneCrits</c>).</summary>
    private static IEnumerable<string> Zone(GlassEditorPartRow head) =>
        new[] { head.Show, head.Hide, head.DrawingShow, head.DrawingHide }
            .SelectMany(field => field.Split(ZoneSeparators))
            .Where(code => code.Length > 0 && code != "0");

    private static readonly char[] ZoneSeparators = ['+', '_'];

    /// <summary>A rule's codes: <c>12000_24119</c> is 12000 and 24119; a blank and <c>0</c> are none.</summary>
    private static List<string> CriteriaOf(string? rule) =>
        (rule ?? string.Empty).Split('_').Where(code => code.Length > 0 && code != "0").ToList();

    /// <summary>A row's own rule, as the map's entries write one.</summary>
    private static string Own(GlassEditorPartRow row) => string.Join('_', CriteriaOf(row.Criteria));

    /// <summary>A rule's part criteria and its time criteria, each joined in the rule's order.</summary>
    private static (string Part, string Time) Split(GlassEditorQuestion question, List<string> rule) =>
        (string.Join('_', rule.Where(question.PartCriteria.Contains)),
            string.Join('_', rule.Where(question.TimeCriteria.Contains)));

    /// <summary>The row of the map entry for a rule (<c>cCritMap.find</c>): a blank rule finds <c>0</c>; none is blank.</summary>
    private static string Find(string rule, List<string[]> entries)
    {
        var key = rule.Length == 0 ? "0" : rule;
        var entry = entries.Find(item => item.Length > 1 && item[1] == key);
        return entry is { Length: > 2 } ? entry[2] : string.Empty;
    }

    /// <summary>The Replace time row's entry: the rule's, else the rule kept to the criteria the group knows (<c>findTER</c>).</summary>
    private static string FindTime(string rule, List<string[]> entries)
    {
        var found = Find(rule, entries);
        if (found.Length > 0 || rule.Length == 0)
        {
            return found;
        }

        var known = entries.Where(entry => entry.Length > 1).SelectMany(entry => CriteriaOf(entry[1])).ToHashSet(StringComparer.Ordinal);
        return Find(string.Join('_', CriteriaOf(rule).Where(known.Contains)), entries);
    }

    private static GlassEditorPartRow? InBlock(IReadOnlyList<GlassEditorPartRow> block, string id) =>
        id.Length == 0 ? null : block.FirstOrDefault(row => row.Id == id);

    private static int IndexOf(IReadOnlyList<GlassEditorPartRow> block, Func<GlassEditorPartRow, bool> match)
    {
        for (var index = 0; index < block.Count; index++)
        {
            if (match(block[index]))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>A map group's entries, each split at its bars (<c>e|12000_24119|3</c>).</summary>
    private static List<string[]> Entries(IReadOnlyList<string> group) =>
        group.Select(entry => entry.Split('|')).ToList();

    private static string? Present(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static decimal? Amount(string? text) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>Codes in number order, as the list code sorts them; a code that is not a number by its text.</summary>
    private static readonly Comparer<string> ByNumber = Comparer<string>.Create((left, right) =>
        long.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var first)
        && long.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var second)
            ? first.CompareTo(second)
            : string.CompareOrdinal(left, right));

    /// <summary>
    /// One head, decided for an operation (Replace when none is named) with
    /// its equipment map as the editor serves it. With
    /// <paramref name="vehicle"/>, the Automation's live add or preview, as
    /// <see cref="ListRefusal"/> says (R1206).
    /// </summary>
    public static GlassEditorRowChoice Decide(
        IReadOnlyList<GlassEditorPartRow> list, int head, IReadOnlyList<IReadOnlyList<string>> map,
        string operation = Replace, IReadOnlySet<string>? vehicle = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (ListRefusal(list, head, operation, vehicle) is { } refused)
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
            return GlassEditorRowChoice.Refused(Uncertain(vehicle));
        }

        var refit = Group(map, RemoveAndRefit);
        if (operation != Replace && (refit.Count > 1 || !refit.All(Plain)))
        {
            return GlassEditorRowChoice.Refused(Uncertain(vehicle));
        }
        if (parts.Count == 1 && RowOf(parts[0]) != row.Id)
        {
            return GlassEditorRowChoice.Refused(Uncertain(vehicle));
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
