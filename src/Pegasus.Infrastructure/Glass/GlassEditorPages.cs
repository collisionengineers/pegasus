using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Glass;

/// <summary>The estimate's positions as <c>selected.php</c> lists them, and Glass's running totals from the same page.</summary>
/// <param name="Found">Whether the page carried the list at all.</param>
/// <param name="Positions">Each position's id, such as <c>C350898.20.C1060000.2464</c>.</param>
/// <param name="Codes">Each position's code, such as <c>C1060000</c> or <c>A73</c>.</param>
/// <param name="Totals">Glass's running totals in the page's order, or null when the page holds none it can be read by.</param>
internal sealed record GlassEditorEstimate(
    bool Found, IReadOnlyList<string> Positions, IReadOnlyList<string> Codes,
    IReadOnlyList<GlassEstimateTotal>? Totals = null)
{
    /// <summary>
    /// The page's <c>Groups</c>, by the number a position's id carries second
    /// (<c>C421280.12.C1060000.2102</c> is group 12), as the editor's own
    /// <c>LoadGraph</c> reads them to choose a position's dialog (R1206); null
    /// when the page has none.
    /// </summary>
    public IReadOnlyList<string>? Groups { get; init; }
}

/// <summary>One group of the special-operation list and its rows, each row as the page lists it.</summary>
internal sealed record GlassEditorSpecialGroup(string Code, string Name, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// One row of the vehicle's part list (<c>list.php</c>), as the editor's own
/// list code holds it: the fields of one <c>partlist.addPart</c> line, with
/// the drawing, drawing number, part code and name filled down from the row
/// above where the line leaves them empty, and a criteria of <c>0</c> read
/// as none, as the list's own <c>index()</c> does.
/// </summary>
/// <param name="Id">The row's id: what a dialog address names (<c>753|754</c>).</param>
/// <param name="Marks">
/// The row's marks, one letter each: <c>p</c> a part row, <c>t</c> a time
/// row, <c>b</c> the first row of a part's block, <c>c</c> a drawing
/// number's row, <c>l</c> and <c>r</c> its side, <c>L</c> a part that can be
/// painted, <c>w</c> a wheel.
/// </param>
/// <param name="Drawing">The drawing the row is on.</param>
/// <param name="DrawingNumber">The row's number on that drawing.</param>
/// <param name="Code">Glass's part code (a Glass's export's <c>MCode</c>).</param>
/// <param name="Criteria">The equipment the row needs, such as <c>12000_24119</c>; empty when none.</param>
/// <param name="Number">The manufacturer's part number.</param>
/// <param name="Price">The part's price, or a time row's hours.</param>
/// <param name="Operations">The operations the row offers (<c>ERAEI </c>), or a time row's one operation (<c>ER</c>).</param>
/// <param name="Show">The equipment that shows the row (field 10): alternatives joined by <c>+</c>, codes by <c>_</c>.</param>
/// <param name="Hide">The equipment that hides the row (field 11), written as <paramref name="Show"/> is.</param>
/// <param name="DrawingShow">The equipment that shows the row's drawing (field 12).</param>
/// <param name="DrawingHide">The equipment that hides the row's drawing (field 13).</param>
/// <param name="Name">The part's name.</param>
internal sealed record GlassEditorPartRow(
    string Id,
    string Marks,
    string Drawing,
    string DrawingNumber,
    string Code,
    string Criteria,
    string Number,
    string Price,
    string Operations,
    string Show,
    string Hide,
    string DrawingShow,
    string DrawingHide,
    string Name)
{
    public bool Has(char mark) => Marks.Contains(mark, StringComparison.Ordinal);

    /// <summary>
    /// The paint levels the row's part offers by operation (the list's
    /// <c>IPST</c>, field 28: <c>E2341BSI2341BS</c>), which the quick repair
    /// panel chooses its paint level from (R1206).
    /// </summary>
    public string PaintStats { get; init; } = string.Empty;

    /// <summary>The paint areas the row's part is painted in, by row id (<c>IPZID</c>, field 31: <c>80,88</c>).</summary>
    public string PaintZones { get; init; } = string.Empty;

    /// <summary>The same row on the other side (<c>IOSID</c>, field 33), which the panel's Both adds next.</summary>
    public string OtherSide { get; init; } = string.Empty;

    /// <summary>Whether the row is shown or hidden on its drawing by some equipment.</summary>
    public bool ShownByEquipment =>
        Show.Length > 0 || Hide.Length > 0 || DrawingShow.Length > 0 || DrawingHide.Length > 0;

    /// <summary>A part row (marked <c>p</c>).</summary>
    public bool IsPart => Has('p');

    /// <summary>A part row that starts a block (marked <c>p</c> and <c>b</c>): the row a part's dialog is asked for by.</summary>
    public bool IsHead => Has('p') && Has('b');

    /// <summary>The side its marks name: <c>L</c>, <c>R</c>, or null for a part with no side.</summary>
    public string? Side => Has('l') ? "L" : Has('r') ? "R" : null;

    /// <summary>A part that can be painted: its dialog is <c>comp.php</c>, as the editor's own pages choose it.</summary>
    public bool Paintable => Has('L');
}

/// <summary>
/// One of the vehicle's equipment criteria, as the part list's
/// <c>partlist.addCrit("…")</c> line gives it (the list code's <c>CIID</c> to
/// <c>CIRR</c> fields): its code, marks, value, how many positions use it,
/// name, radio group and radio flag. Mark <c>g</c> is a value Glass's keeps
/// for the vehicle; mark <c>f</c> one the vehicle fixes, such as a build date,
/// which the editor's dialog does not show.
/// </summary>
internal sealed record GlassEditorCriterion(
    string Code, string Marks, string Value, string Referenced, string Name, string Group, string Radio)
{
    public bool Has(char mark) => Marks.Contains(mark, StringComparison.Ordinal);

    /// <summary>The vehicle has it.</summary>
    public bool IsSet => Value == "1";

    /// <summary>A radio option: one of a group of which at most one is ticked.</summary>
    public bool IsRadio => Group is not ("" or "0") && Radio is not ("" or "0");

    /// <summary>Another position uses it, so the dialog marks it (the list code's <c>critConv</c>).</summary>
    public bool AffectsOtherParts =>
        int.TryParse(Referenced, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0;
}

/// <summary>
/// The vehicle's part list page as the editor's list code holds it: its rows,
/// the vehicle's criteria by code (the first line for a code wins), and the
/// note the page shows on an option another position uses
/// (<c>partlist.ttReferenced</c>), when the page sets one.
/// </summary>
internal sealed record GlassEditorPartList(
    IReadOnlyList<GlassEditorPartRow> Rows,
    IReadOnlyDictionary<string, GlassEditorCriterion> Criteria,
    string? ReferencedNote)
{
    /// <summary>The codes of the criteria the vehicle has.</summary>
    public IReadOnlySet<string> Vehicle { get; } =
        Criteria.Values.Where(criterion => criterion.IsSet).Select(criterion => criterion.Code).ToHashSet(StringComparer.Ordinal);
}

/// <summary>
/// What the launch page (<c>acolib/aco_call_xml.php</c>) says on the way in:
/// the editor's own name for where it came from (<c>url0</c>), the still
/// encoded request description (<c>XmlPar</c>), and the two addresses its
/// script sends the window to. <see cref="Blocked"/> is the provider's page
/// that refuses to open another session.
/// </summary>
internal sealed record GlassEditorLaunch(
    bool Blocked, string Base, string Url0, string XmlPar, long Wt, string Aco, string Aco2, string? Caller)
{
    public static readonly GlassEditorLaunch BlockedPage = new(true, "", "", "", 0, "", "", null);

    /// <summary>Says nothing the page carried: its addresses hold the return token.</summary>
    public override string ToString() => Blocked ? "GlassEditorLaunch { Blocked }" : "GlassEditorLaunch { Read }";
}

/// <summary>
/// The way home the launch page names on the way out, kept exactly as the
/// page gives it because it is relayed verbatim, and the parts of it a caller
/// checks.
/// </summary>
internal sealed record GlassEditorReturn(
    string Address, string Origin, string Path, string? Total, string? DoSave, bool HasFile)
{
    /// <summary>Says nothing the address carries: it holds the return token.</summary>
    public override string ToString() =>
        $"GlassEditorReturn {{ DoSave = {DoSave ?? "absent"}, HasFile = {HasFile} }}";
}

/// <summary>
/// The readers of the editor's pages, each a function of the page's text.
/// Scripts are read, never run: the values the editor's own scripts hold are
/// taken from their literal text, as a browser would hold them.
/// </summary>
internal static partial class GlassEditorPages
{
    /// <summary>How long one pattern may take; every pattern here is bounded.</summary>
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private const RegexOptions Patterns = RegexOptions.CultureInvariant;

    /// <summary>The path the editor's main frame ends with.</summary>
    private const string FramePath = "/ere/aco/acofr.php";

    /// <summary>
    /// The longest main-frame address read. The editor's own is a few hundred
    /// characters; nothing longer is a frame address.
    /// </summary>
    private const int MaximumFrameAddress = 4096;

    /// <summary>
    /// The value after <paramref name="key"/> in a bar-separated list of
    /// pairs (<c>ER|379.00|ET|379.00|…</c>), or null when the key is not one
    /// of the list's keys.
    /// </summary>
    public static string? Lookup(string? line, string? key)
    {
        var parts = (line ?? string.Empty).Split('|');
        for (var index = 0; index + 1 < parts.Length; index += 2)
        {
            if (parts[index] == key)
            {
                return parts[index + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// A script's array of quoted strings (<c>var name = [ 'a', "b" ];</c>),
    /// read without running it. Anything between the strings is passed over;
    /// the first closing bracket outside a string ends the array.
    /// </summary>
    public static List<string>? StringArray(string source, string name)
    {
        var start = Regex.Match(source, @"var\s+" + Regex.Escape(name) + @"\s*=\s*\[", Patterns, MatchTimeout);
        if (!start.Success)
        {
            return null;
        }

        var items = new List<string>();
        var index = start.Index + start.Length;
        while (index < source.Length)
        {
            var character = source[index];
            if (character == ']')
            {
                return items;
            }
            if (character is not '\'' and not '"')
            {
                index++;
                continue;
            }

            items.Add(Quoted(source, ref index));
        }

        return null;
    }

    /// <summary>A script's bar-separated list (<c>var name = "a|b".split( "|" );</c>), read without running it.</summary>
    public static string[]? SplitArray(string source, string name)
    {
        var found = Regex.Match(
            source, @"var\s+" + Regex.Escape(name) + @"\s*=\s*""([^""]*)""\.split\(\s*""\|""\s*\)", Patterns, MatchTimeout);
        return found.Success ? found.Groups[1].Value.Split('|') : null;
    }

    /// <summary>
    /// A script's pair of bar-separated lists, one per side
    /// (<c>var name = ["a|b".split( "|" ),"c|d".split( "|" )];</c>), each
    /// as its unsplit text, read without running it.
    /// </summary>
    public static List<string>? SplitPairs(string source, string name)
    {
        var found = Regex.Match(source, @"var\s+" + Regex.Escape(name) + @"\s*=\s*\[([^;]*)\];", Patterns, MatchTimeout);
        return found.Success
            ? SplitList().Matches(found.Groups[1].Value).Select(entry => entry.Groups[1].Value).ToList()
            : null;
    }

    /// <summary>
    /// The estimate's positions and codes (<c>selected.php</c>), and Glass's
    /// running totals from the same page (R1206). The page's lists begin with
    /// an empty entry, which is not a position.
    /// </summary>
    public static GlassEditorEstimate Selected(string html)
    {
        var ids = StringArray(html, "Partids");
        var codes = StringArray(html, "CheckIds");
        return new(
            ids is not null,
            (ids ?? []).Where(id => id.Length > 0).ToList(),
            (codes ?? []).Where(code => code.Length > 0).ToList(),
            Totals(html))
        {
            Groups = StringArray(html, "Groups"),
        };
    }

    /// <summary>
    /// The dialog the editor opens for a position on the estimate, by its
    /// list's own <c>LoadGraph</c> (<c>selected.php</c>, 19 November 2026):
    /// the position's group is <c>Groups</c> at the number its id carries
    /// second. A group with C, I, A, H or M opens <c>part</c>. One with L as
    /// well opens <c>comp</c> (a part with its paint), unless the position
    /// is a paint position (an id beginning L) or a special row (S) of a group
    /// without c, which open <c>lack</c> and <c>part</c>; one with L alone
    /// opens <c>lack</c>. Null for a group the page does not hold or names
    /// none of these, where the editor itself says "Illegal group".
    /// </summary>
    public static string? DialogPage(string positionId, IReadOnlyList<string>? groups)
    {
        ArgumentNullException.ThrowIfNull(positionId);
        var tags = positionId.Split('.');
        if (groups is null || tags.Length < 2
            || !int.TryParse(tags[1], NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            || index >= groups.Count)
        {
            return null;
        }

        var group = groups[index];
        var page = group.IndexOfAny(PartGroups) >= 0 ? "part" : string.Empty;
        if (group.Contains('L', StringComparison.Ordinal))
        {
            var ownPaint = !group.Contains('c', StringComparison.Ordinal);
            if (page.Length == 0 || (positionId.StartsWith('L') && ownPaint))
            {
                return "lack";
            }

            return positionId.StartsWith('S') && ownPaint ? "part" : "comp";
        }

        return page.Length == 0 ? null : page;
    }

    private static readonly char[] PartGroups = ['C', 'I', 'A', 'H', 'M'];

    /// <summary>
    /// Glass's running totals as the estimate's page holds them for the
    /// editor's top bar: its labels (<c>CalcToolTipTexts</c>, such as
    /// <c>'Parts '</c> or <c>'Total (£) '</c>) beside its figures
    /// (<c>CalcToolTipNums</c>, such as <c>'4932.87'</c>), in the page's
    /// order. The page lists only the totals the estimate has; an empty
    /// estimate lists the amount due alone. Null when either list is missing,
    /// the two differ in length, or a figure is not a number: no total is
    /// guessed.
    /// </summary>
    public static IReadOnlyList<GlassEstimateTotal>? Totals(string html)
    {
        var labels = StringArray(html, "CalcToolTipTexts");
        var figures = StringArray(html, "CalcToolTipNums");
        if (labels is null || figures is null || labels.Count != figures.Count)
        {
            return null;
        }

        var totals = new List<GlassEstimateTotal>(labels.Count);
        for (var index = 0; index < labels.Count; index++)
        {
            if (Amount(figures[index]) is not { } amount || labels[index].Trim().Length == 0)
            {
                return null;
            }

            totals.Add(new(labels[index].Trim(), amount));
        }

        return totals;
    }

    /// <summary>
    /// One position's figures from its record as <c>mcode.php</c> answers it
    /// (<c>{"mcode":"…","part":[…]}</c>, one entry per line of the record),
    /// read by the lines the part's own dialog reads (<see cref="GlassEditorDialogs"/>):
    /// line 1 the part code (line 2 for the right-hand side), line 3 the
    /// description, line 4 the operation, lines 6 and 8 the price and hours
    /// by operation (7 and 9 for the right-hand side), line 13 the part
    /// number (14 for the right-hand side), line 15 the side. A part with
    /// paint carries its paint position from line 64; a paint position on its
    /// own (an id beginning <c>L</c>) is that block alone. In both, the paint
    /// block's line 0 is its id, line 2 its description and line 22 its
    /// price and labour time. Null when the answer is not a record of the
    /// position asked for, or is too short to hold the lines read.
    /// </summary>
    public static GlassPositionFigures? PositionFigures(string text, string positionId)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(positionId);
        List<string> record;
        try
        {
            using var document = JsonDocument.Parse(text.Trim('\uFEFF', ' ', '\t', '\r', '\n'));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("mcode", out var asked)
                || asked.ValueKind != JsonValueKind.String
                || !string.Equals(asked.GetString(), positionId, StringComparison.Ordinal)
                || !root.TryGetProperty("part", out var part)
                || part.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            record = part.EnumerateArray()
                .Select(entry => entry.ValueKind == JsonValueKind.String ? entry.GetString()! : entry.GetRawText())
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }

        if (positionId.StartsWith(PaintPositionPrefix))
        {
            return record.Count < PaintBlockLines
                ? null
                : new(positionId, Line(record, 1), Text(Line(record, 2)), null, null, null, null, null, Paint(record, 0));
        }
        if (record.Count < PartLines)
        {
            return null;
        }

        var side = (Line(record, 15) ?? string.Empty).Trim();
        var right = SideIndex(side);
        var operation = Line(record, 4)?.Trim();
        operation = string.IsNullOrEmpty(operation) ? null : operation;
        var paint = record.Count >= PaintStart + PaintBlockLines
            && (Line(record, PaintStart) ?? string.Empty).StartsWith(PaintPositionPrefix)
                ? Paint(record, PaintStart)
                : null;
        return new(
            positionId,
            Line(record, 1 + right),
            Text(Line(record, 3)),
            operation,
            operation is null ? null : Amount(Lookup(Line(record, 6 + right), operation)),
            operation is null ? null : Amount(Lookup(Line(record, 8 + right), operation)),
            side.Length == 0 ? null : side,
            Text(Line(record, 13 + right)),
            paint);
    }

    /// <summary>A paint position's id begins with this letter (<c>L350899.20.Lxx1640.Lxx1640</c>).</summary>
    private const char PaintPositionPrefix = 'L';

    /// <summary>The lines a part's or special operation's record must hold for the lines read (0 to 15).</summary>
    private const int PartLines = 16;

    /// <summary>Where a part's record carries its paint block.</summary>
    private const int PaintStart = 64;

    /// <summary>The lines a paint block must hold for the lines read (0 to 22).</summary>
    private const int PaintBlockLines = 23;

    private static GlassPositionPaint Paint(List<string> record, int start)
    {
        var figures = (Line(record, start + 22) ?? string.Empty).Split('|');
        return new(
            (Line(record, start) ?? string.Empty).Split('|')[0],
            Text(Line(record, start + 2)),
            Amount(figures[0]),
            figures.Length > 1 ? Amount(figures[1]) : null);
    }

    /// <summary>
    /// The side's index into the record's left and right lines, as the
    /// part's dialog takes it: 1 for a right-hand side (<c>R</c>, or a front
    /// or rear one, <c>VR</c> or <c>HR</c>), otherwise 0.
    /// </summary>
    private static int SideIndex(string side)
    {
        var lead = side.Length > 0 ? side[..1] : string.Empty;
        var place = lead is "V" or "H" ? (side.Length > 1 ? side.Substring(1, 1) : string.Empty) : lead;
        return place == "R" ? 1 : 0;
    }

    private static string? Line(List<string> record, int index) => index < record.Count ? record[index] : null;

    /// <summary>A record's text field: the text before its bar, trimmed; null when empty.</summary>
    private static string? Text(string? line)
    {
        var text = (line ?? string.Empty).Split('|')[0].Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>
    /// Glass's calculation (<c>report.php?calcid=0</c>), read from the table
    /// its report heads "Summary" and the line "Repair costs incl. VAT" after
    /// it (captures of 7 September and 18 November 2026). The columns are
    /// found by Glass's own headers: "Labour rate", "Labour time", "Labour
    /// costs", "Material costs". The rows above the summary's first rule are
    /// the trades; after it, "Total Labour" gives its labour time and costs,
    /// and "Total Material", "Repair costs excl. VAT", the VAT line and
    /// "Repair costs incl. VAT" give their one figure each. A row's figure
    /// is read with the thousands separator the editor's frame asks for
    /// (<c>thsep=,</c>) and an optional leading <c>£</c>. Null when the page
    /// has no such summary or names no trade.
    /// </summary>
    public static GlassCalculation? Calculation(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var rows = TableRow().Matches(html)
            .Select(row => new ReportRow(
                CellOf().Matches(row.Groups[1].Value).Select(cell => CellText(cell.Groups[1].Value)).ToList(),
                row.Groups[1].Value.Contains("<hr", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var header = rows.FindIndex(row => row.Cells.Count > 0 && row.Cells[0] == SummaryHeader);
        if (header < 0)
        {
            return null;
        }

        var columns = rows[header].Cells;
        var rate = columns.IndexOf("Labour rate");
        var time = columns.IndexOf("Labour time");
        var costs = columns.IndexOf("Labour costs");
        var material = columns.IndexOf("Material costs");
        if (rate < 1 || time < 1 || costs < 1 || material < 1)
        {
            return null;
        }

        var trades = new List<GlassCalculationTrade>();
        var calculation = new GlassCalculation(trades, null, null, null, null, null, null, null);
        var ruled = false;
        foreach (var row in rows.Skip(header + 1))
        {
            if (row.Ruled)
            {
                ruled = true;
                continue;
            }
            if (row.Cells.Count == 0 || row.Cells[0].Length == 0)
            {
                continue;
            }

            var label = row.Cells[0];
            if (!ruled)
            {
                trades.Add(new(label, Cell(row, rate), Cell(row, time), Cell(row, costs), Cell(row, material)));
                continue;
            }

            switch (label)
            {
                case "Total Labour":
                    calculation = calculation with { TotalLabourTime = Cell(row, time), TotalLabourCosts = Cell(row, costs) };
                    break;
                case "Total Material":
                    calculation = calculation with { TotalMaterial = OneFigure(row) };
                    break;
                case "Repair costs excl. VAT":
                    calculation = calculation with { RepairCostsExclVat = OneFigure(row) };
                    break;
                case "Repair costs incl. VAT":
                    calculation = calculation with { RepairCostsInclVat = OneFigure(row) };
                    return trades.Count == 0 ? null : calculation;
                default:
                    if (label.StartsWith("VAT", StringComparison.Ordinal))
                    {
                        calculation = calculation with { VatLabel = label, Vat = OneFigure(row) };
                    }

                    break;
            }
        }

        return trades.Count == 0 ? null : calculation;
    }

    /// <summary>The first header of the report's summary table.</summary>
    private const string SummaryHeader = "Summary";

    /// <summary>One row of a report table: each cell's text, and whether the row is a rule.</summary>
    private sealed record ReportRow(List<string> Cells, bool Ruled);

    private static decimal? Cell(ReportRow row, int column) =>
        column < row.Cells.Count ? Figure(row.Cells[column]) : null;

    /// <summary>A row's one figure after its label; null when it has none or more than one.</summary>
    private static decimal? OneFigure(ReportRow row)
    {
        var given = row.Cells.Skip(1).Where(cell => cell.Length > 0).ToList();
        return given.Count == 1 ? Figure(given[0]) : null;
    }

    /// <summary>A report cell's figure: an optional <c>£</c>, then the number with its thousands separators.</summary>
    private static decimal? Figure(string cell)
    {
        var text = cell.StartsWith('£') ? cell[1..].Trim() : cell;
        return text.Length == 0 ? null : Amount(text);
    }

    /// <summary>A cell's text: its tags dropped, its entities decoded, its spaces (non-breaking ones too) trimmed.</summary>
    private static string CellText(string cell) =>
        WebUtility.HtmlDecode(Tag().Replace(cell, string.Empty)).Trim();

    /// <summary>
    /// A figure as Glass's pages write it, read whatever the host's culture:
    /// digits with a decimal point, a leading minus and the thousands
    /// separator the editor's frame asks for (<c>,</c>). Null when it is
    /// anything else.
    /// </summary>
    public static decimal? Amount(string? text) =>
        decimal.TryParse(
            text,
            NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowLeadingSign
                | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;

    /// <summary>
    /// A row's equipment map (<c>var aCritMap = [ […], … ];</c>) as the nested
    /// lists of strings the page holds, not interpreted; null when the page
    /// has no map or the map is not lists of strings.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>>? EquipmentMap(string html)
    {
        var map = ArrayLiteral(html, "aCritMap");
        if (map?.Items is not { } lists)
        {
            return null;
        }

        var read = new List<IReadOnlyList<string>>();
        foreach (var list in lists)
        {
            if (list.Items is not { } items || items.Any(item => item.Text is null))
            {
                return null;
            }

            read.Add(items.Select(item => item.Text!).ToList());
        }

        return read;
    }

    /// <summary>
    /// The vehicle's part list (<c>list.php</c>): one row per
    /// <c>partlist.addPart("…")</c> call, in the page's order, read without
    /// running the page; see <see cref="GlassEditorPartRow"/>. Null when the
    /// page has no row.
    /// </summary>
    public static IReadOnlyList<GlassEditorPartRow>? PartList(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        const string Call = "partlist.addPart(";
        var rows = new List<GlassEditorPartRow>();
        // The fields the list's own index() fills down from the row above.
        var above = new string[FilledDown.Length];
        Array.Fill(above, string.Empty);
        var at = html.IndexOf(Call, StringComparison.Ordinal);
        while (at >= 0)
        {
            var index = at + Call.Length;
            while (index < html.Length && char.IsWhiteSpace(html[index]))
            {
                index++;
            }
            if (index < html.Length && html[index] is '"' or '\'')
            {
                var fields = Quoted(html, ref index).Split('|');
                for (var filled = 0; filled < FilledDown.Length; filled++)
                {
                    if (Field(fields, FilledDown[filled]).Trim().Length > 0)
                    {
                        above[filled] = fields[FilledDown[filled]];
                    }
                }

                rows.Add(new GlassEditorPartRow(
                    Field(fields, 0),
                    Field(fields, 1),
                    above[0],
                    above[1],
                    above[2],
                    Field(fields, 6) is "0" ? string.Empty : Field(fields, 6),
                    Field(fields, 7),
                    Field(fields, 8),
                    Field(fields, 9),
                    Field(fields, 10),
                    Field(fields, 11),
                    Field(fields, 12),
                    Field(fields, 13),
                    above[3])
                {
                    PaintStats = Field(fields, 28),
                    PaintZones = Field(fields, 31),
                    OtherSide = Field(fields, 33),
                });
            }

            at = index < html.Length ? html.IndexOf(Call, index, StringComparison.Ordinal) : -1;
        }

        return rows.Count == 0 ? null : rows;
    }

    /// <summary>
    /// The vehicle's equipment criteria on the part list page: one per
    /// <c>partlist.addCrit("…")</c> call, by code, the first call for a code
    /// kept, read without running the page (R1206). Empty when the page has
    /// none.
    /// </summary>
    public static IReadOnlyDictionary<string, GlassEditorCriterion> Criteria(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        const string Call = "partlist.addCrit(";
        var criteria = new Dictionary<string, GlassEditorCriterion>(StringComparer.Ordinal);
        var at = html.IndexOf(Call, StringComparison.Ordinal);
        while (at >= 0)
        {
            var index = at + Call.Length;
            while (index < html.Length && char.IsWhiteSpace(html[index]))
            {
                index++;
            }
            if (index < html.Length && html[index] is '"' or '\'')
            {
                var fields = Quoted(html, ref index).Split('|');
                var code = Field(fields, 0);
                if (code.Length > 0)
                {
                    criteria.TryAdd(code, new GlassEditorCriterion(
                        code, Field(fields, 1), Field(fields, 2), Field(fields, 4), Field(fields, 5), Field(fields, 8),
                        Field(fields, 10)));
                }
            }

            at = index < html.Length ? html.IndexOf(Call, index, StringComparison.Ordinal) : -1;
        }

        return criteria;
    }

    /// <summary>
    /// The note the part list page shows on an option another position uses
    /// (<c>partlist.ttReferenced = '…';</c>), as the page writes it, or null.
    /// </summary>
    public static string? ReferencedNote(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var found = ReferencedTooltip().Match(html);
        return found.Success && found.Groups[1].Value.Length > 0 ? found.Groups[1].Value : null;
    }

    /// <summary>
    /// <c>service.php?…&amp;globcrit=…&amp;updatecrit2g=1</c>: whether the answer is
    /// the service page that tells the editor's list to read the vehicle's
    /// criteria again (<c>critRefresh()</c>), as it answered a browser.
    /// </summary>
    public static bool EquipmentStored(string text) => CriteriaRefresh().IsMatch(text);

    /// <summary>The part list's fields its own <c>index()</c> fills down: drawing, drawing number, part code, name.</summary>
    private static readonly int[] FilledDown = [3, 4, 5, 16];

    private static string Field(string[] fields, int index) => index < fields.Length ? fields[index] : string.Empty;

    /// <summary>
    /// The special-operation list (<c>list_1G.php</c>): its groups from
    /// <c>Parms</c> and each group's rows from <c>Tparts</c>, read without
    /// running the page. A row is any list of strings in the group, at any
    /// depth; the one-entry rows the page uses to hold an empty group's place
    /// are not rows. Null when the page has neither list.
    /// </summary>
    public static IReadOnlyList<GlassEditorSpecialGroup>? SpecialOperations(string html)
    {
        var groups = ArrayLiteral(html, "Parms")?.Items;
        var parts = ArrayLiteral(html, "Tparts")?.Items;
        if (groups is null || parts is null)
        {
            return null;
        }

        var read = new List<GlassEditorSpecialGroup>();
        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index].Items ?? [];
            var rows = new List<IReadOnlyList<string>>();
            if (index < parts.Count)
            {
                Rows(parts[index], rows);
            }

            read.Add(new(
                group.Count > 0 ? group[0].Text ?? string.Empty : string.Empty,
                group.Count > 1 ? group[1].Text ?? string.Empty : string.Empty,
                rows.Where(row => row.Count > 1).ToList()));
        }

        return read;
    }

    private static void Rows(ScriptValue node, List<IReadOnlyList<string>> rows)
    {
        if (node.Items is not { } items)
        {
            return;
        }
        if (items.All(item => item.Text is not null))
        {
            rows.Add(items.Select(item => item.Text!).ToList());
            return;
        }

        foreach (var item in items)
        {
            Rows(item, rows);
        }
    }

    /// <summary>
    /// The launch page on the way in: its script sends the window to
    /// <c>aco.php</c>, adding the time. Answers the blocked page when the
    /// provider refuses another session, and null when the page is neither.
    /// </summary>
    public static GlassEditorLaunch? LaunchPage(string text, string launchAddress, long now)
    {
        if (BlockedReplace().IsMatch(text) && !EditorAddress().IsMatch(text))
        {
            return GlassEditorLaunch.BlockedPage;
        }

        var found = LaunchReplace().Match(text);
        if (!found.Success)
        {
            return null;
        }

        var relative = found.Groups[1].Value;
        var query = relative[(relative.IndexOf('?', StringComparison.Ordinal) + 1)..];
        var at = query.IndexOf("&XmlPar=", StringComparison.Ordinal);
        if (!query.StartsWith("url0=", StringComparison.Ordinal) || at < 0)
        {
            return null;
        }

        var url0 = query["url0=".Length..at];
        var xmlPar = query[(at + "&XmlPar=".Length)..];
        var stamp = now.ToString(CultureInfo.InvariantCulture);
        var baseAddress = new Uri(new Uri(launchAddress, UriKind.Absolute), "../aco/").AbsoluteUri;
        var caller = CallerParameter().Match(url0);
        return new(
            false,
            baseAddress,
            url0,
            xmlPar,
            now,
            GlassEditorAddresses.Resolve(relative + "&WT=" + stamp, launchAddress),
            baseAddress + "aco2.php?url0=" + GlassEditorAddresses.EncodeUriComponent(url0) + "&XmlPar=" + xmlPar + "&WT=" + stamp,
            caller.Success ? caller.Groups[1].Value : null);
    }

    /// <summary><c>aco2.php</c>: the new session's key, in the address of its main frame.</summary>
    public static string? SessionKey(string text)
    {
        var found = FrameKey().Match(text);
        return found.Success ? found.Groups[1].Value : null;
    }

    /// <summary>
    /// The editor's main frame (<c>acofr.php</c>) as a staff member's browser
    /// holds it, handed over by the Pegasus helper: an HTTPS address on the
    /// configured estimator, its path ending <c>/ere/aco/acofr.php</c>, the
    /// session key as its first parameter and <c>url0</c> as
    /// <see cref="GlassEditorAddresses.Escape"/> writes it, whose
    /// <c>caller</c> is exactly this host's callback for a 64-character
    /// lower-case hex return token. Null when the address is anything else.
    /// The session carries what <see cref="GlassHelpFrame.Balancer"/> keeps
    /// of <paramref name="balancerCookies"/>, so its first request reaches
    /// the Glass's server the browser is on.
    /// </summary>
    public static GlassHelpFrame? HelpFrame(
        string? address, GlassRepairEstimateOptions options, IReadOnlyDictionary<string, string>? balancerCookies = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (address is null
            || address.Length > MaximumFrameAddress
            || !Uri.TryCreate(address, UriKind.Absolute, out var frame)
            || !options.IsEstimator(frame)
            || !frame.AbsolutePath.EndsWith(FramePath, StringComparison.Ordinal))
        {
            return null;
        }

        // The key is the query's first parameter, whole.
        var key = FrameKey().Match(address);
        var query = address.IndexOf('?', StringComparison.Ordinal);
        var keyEnd = key.Index + key.Length;
        if (!key.Success
            || query != key.Index + "acofr.php".Length
            || (keyEnd < address.Length && address[keyEnd] is not '&' and not '#'))
        {
            return null;
        }

        var fragment = address.IndexOf('#', StringComparison.Ordinal);
        var parameters = (fragment < 0 ? address[(query + 1)..] : address[(query + 1)..fragment]).Split('&');
        var escaped = parameters.FirstOrDefault(part => part.StartsWith("url0=", StringComparison.Ordinal));
        if (escaped is null)
        {
            return null;
        }

        var url0 = GlassEditorAddresses.Unescape(escaped["url0=".Length..]);
        var caller = CallerParameter().Match(url0);
        var callbacks = new Uri(options.CallbackBaseUri, GlassRepairEstimateOptions.CallbackPath).AbsoluteUri;
        if (!caller.Success || !caller.Groups[1].Value.StartsWith(callbacks, StringComparison.Ordinal))
        {
            return null;
        }

        var token = caller.Groups[1].Value[callbacks.Length..];
        return GlassHelpFrame.IsToken(token)
            && string.Equals(options.CallbackFor(token).AbsoluteUri, caller.Groups[1].Value, StringComparison.Ordinal)
            ? new GlassHelpFrame(
                token,
                new GlassEditorSession(
                    new Uri(frame, "./").AbsoluteUri, url0, key.Groups[1].Value, GlassHelpFrame.Balancer(balancerCookies)))
            : null;
    }

    /// <summary><c>service.php?savexit=1</c>: whether the answer tells the editor's top bar to close after a save.</summary>
    public static bool SavedAndClosing(string text) => CloseAfterSave().IsMatch(text);

    /// <summary>
    /// <c>close.php?dosave=1</c>: the launch page its script sends the window
    /// back through, resolved against the close page's address, or null.
    /// </summary>
    public static string? ClosePage(string text, string closeAddress)
    {
        var found = BackToLaunch().Match(text);
        return found.Success ? GlassEditorAddresses.Resolve(found.Groups[1].Value, closeAddress) : null;
    }

    /// <summary>
    /// The launch page on the way out: its script sends the window to the
    /// caller with the outcome in the query. The address is kept exactly as
    /// the page gives it. Null when the page names no such address.
    /// </summary>
    public static GlassEditorReturn? ReturnPage(string text)
    {
        var found = ReturnReplace().Match(text);
        if (!found.Success)
        {
            return null;
        }

        string? address;
        try
        {
            address = JsonSerializer.Deserialize<string>(found.Groups[1].Value);
        }
        catch (JsonException)
        {
            return null;
        }

        if (address is null || !Uri.TryCreate(address, UriKind.Absolute, out var url))
        {
            return null;
        }

        var query = QueryParameters(address);
        return new(
            address,
            url.GetLeftPart(UriPartial.Authority),
            url.AbsolutePath,
            query.FirstOrDefault(pair => pair.Key == "Total").Value,
            query.FirstOrDefault(pair => pair.Key == "DoSave").Value,
            query.Exists(pair => pair.Key == "ErrMsg"));
    }

    /// <summary>
    /// The address's query read as a browser's <c>URLSearchParams</c> reads
    /// it: pairs split at the first equals sign, plus as a space, percent
    /// escapes decoded.
    /// </summary>
    private static List<KeyValuePair<string, string>> QueryParameters(string address)
    {
        var at = address.IndexOf('?', StringComparison.Ordinal);
        var query = at < 0 ? string.Empty : address[(at + 1)..];
        var hash = query.IndexOf('#', StringComparison.Ordinal);
        query = hash < 0 ? query : query[..hash];
        return query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                var equals = part.IndexOf('=', StringComparison.Ordinal);
                var name = equals < 0 ? part : part[..equals];
                var value = equals < 0 ? string.Empty : part[(equals + 1)..];
                return KeyValuePair.Create(FormDecode(name), FormDecode(value));
            })
            .ToList();
    }

    private static string FormDecode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));

    /// <summary>
    /// One quoted script string from its opening quote, with the escapes a
    /// script string uses; leaves <paramref name="index"/> after the closing
    /// quote.
    /// </summary>
    private static string Quoted(string source, ref int index)
    {
        var quote = source[index];
        var value = new StringBuilder();
        for (index++; index < source.Length && source[index] != quote; index++)
        {
            if (source[index] != '\\')
            {
                value.Append(source[index]);
                continue;
            }

            index++;
            if (index >= source.Length)
            {
                break;
            }

            switch (source[index])
            {
                case 'n':
                    value.Append('\n');
                    break;
                case 't':
                    value.Append('\t');
                    break;
                case 'r':
                    value.Append('\r');
                    break;
                case 'x':
                    value.Append(Hex(source, index + 1, 2));
                    index += 2;
                    break;
                case 'u':
                    value.Append(Hex(source, index + 1, 4));
                    index += 4;
                    break;
                default:
                    value.Append(source[index]);
                    break;
            }
        }

        index++;
        return value.ToString();
    }

    /// <summary>The character a script's <c>\x</c> or <c>\u</c> escape names: its leading hex digits, or 0.</summary>
    private static char Hex(string source, int start, int length)
    {
        var code = 0;
        for (var index = start; index < Math.Min(start + length, source.Length) && char.IsAsciiHexDigit(source[index]); index++)
        {
            code = (code * 16) + int.Parse(source.AsSpan(index, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return (char)code;
    }

    /// <summary>A script value: a string, or a list of values.</summary>
    private sealed record ScriptValue(string? Text, IReadOnlyList<ScriptValue>? Items);

    /// <summary>
    /// A script's nested array literal (<c>var name = [[ 'a', 'b' ], …];</c>),
    /// read without running it. A bare value between commas is kept as its
    /// trimmed text. Null when the page has no such array or it is not closed.
    /// </summary>
    private static ScriptValue? ArrayLiteral(string source, string name)
    {
        var start = Regex.Match(source, @"var\s+" + Regex.Escape(name) + @"\s*=\s*\[", Patterns, MatchTimeout);
        if (!start.Success)
        {
            return null;
        }

        var index = start.Index + start.Length - 1;
        return Literal(source, ref index);
    }

    private static ScriptValue? Literal(string source, ref int index)
    {
        if (source[index] is '\'' or '"')
        {
            return new(Quoted(source, ref index), null);
        }
        if (source[index] != '[')
        {
            var bare = index;
            while (index < source.Length && source[index] is not ',' and not ']')
            {
                index++;
            }

            return new(source[bare..index].Trim(), null);
        }

        var items = new List<ScriptValue>();
        index++;
        while (true)
        {
            while (index < source.Length && (char.IsWhiteSpace(source[index]) || source[index] == ','))
            {
                index++;
            }
            if (index >= source.Length)
            {
                return null;
            }
            if (source[index] == ']')
            {
                index++;
                return new(null, items);
            }
            if (Literal(source, ref index) is not { } item)
            {
                return null;
            }

            items.Add(item);
        }
    }

    [GeneratedRegex(@"""([^""]*)""\.split\(\s*""\|""\s*\)", Patterns, 1000)]
    private static partial Regex SplitList();

    [GeneratedRegex(@"<tr\b[^>]*>(.*?)</tr>", Patterns | RegexOptions.Singleline | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex TableRow();

    [GeneratedRegex(@"<td\b[^>]*>(.*?)</td>", Patterns | RegexOptions.Singleline | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex CellOf();

    [GeneratedRegex(@"<[^>]*>", Patterns, 1000)]
    private static partial Regex Tag();

    [GeneratedRegex(@"document\.location\.replace\(\s*'blocked\.html", Patterns, 1000)]
    private static partial Regex BlockedReplace();

    [GeneratedRegex(@"aco/aco\.php", Patterns, 1000)]
    private static partial Regex EditorAddress();

    [GeneratedRegex(@"document\.location\.replace\(\s*'(\.\./aco/aco\.php\?[^']*)'\s*\+\s*Auth\(\)\s*\+\s*WT\(\)\s*\)", Patterns, 1000)]
    private static partial Regex LaunchReplace();

    [GeneratedRegex(@"[?&]caller=(.*)$", Patterns, 1000)]
    private static partial Regex CallerParameter();

    [GeneratedRegex(@"acofr\.php\?ksid=([A-Za-z0-9]+)", Patterns, 1000)]
    private static partial Regex FrameKey();

    [GeneratedRegex(@"CloseAco\(\s*true\s*\)", Patterns, 1000)]
    private static partial Regex CloseAfterSave();

    [GeneratedRegex(@"parent\.document\.location\s*=\s*'(\.\./acolib/aco_call_xml\.php\?[^']*)'", Patterns, 1000)]
    private static partial Regex BackToLaunch();

    [GeneratedRegex(@"document\.location\.replace\(\s*(""(?:[^""\\]|\\.)*"")\s*\)", Patterns, 1000)]
    private static partial Regex ReturnReplace();

    [GeneratedRegex(@"partlist\.ttReferenced\s*=\s*'([^'\r\n]*)'", Patterns, 1000)]
    private static partial Regex ReferencedTooltip();

    [GeneratedRegex(@"\.critRefresh\(\s*\)", Patterns, 1000)]
    private static partial Regex CriteriaRefresh();
}
