using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pegasus.Infrastructure.Glass;

/// <summary>The estimate's positions as <c>selected.php</c> lists them.</summary>
/// <param name="Found">Whether the page carried the list at all.</param>
/// <param name="Positions">Each position's id, such as <c>C350898.20.C1060000.2464</c>.</param>
/// <param name="Codes">Each position's code, such as <c>C1060000</c> or <c>A73</c>.</param>
internal sealed record GlassEditorEstimate(bool Found, IReadOnlyList<string> Positions, IReadOnlyList<string> Codes);

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
/// <param name="ShownByEquipment">Whether the row is shown or hidden on its drawing by some equipment.</param>
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
    bool ShownByEquipment,
    string Name)
{
    public bool Has(char mark) => Marks.Contains(mark, StringComparison.Ordinal);

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
    /// The estimate's positions and codes (<c>selected.php</c>). The page's
    /// lists begin with an empty entry, which is not a position.
    /// </summary>
    public static GlassEditorEstimate Selected(string html)
    {
        var ids = StringArray(html, "Partids");
        var codes = StringArray(html, "CheckIds");
        return new(
            ids is not null,
            (ids ?? []).Where(id => id.Length > 0).ToList(),
            (codes ?? []).Where(code => code.Length > 0).ToList());
    }

    /// <summary>
    /// One position's record as <c>mcode.php</c> answers it
    /// (<c>{"part":[…]}</c>), one entry per line of the record, or null when
    /// the answer is not that.
    /// </summary>
    public static IReadOnlyList<string>? Position(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text.Trim('\uFEFF', ' ', '\t', '\r', '\n'));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("part", out var part)
                || part.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return part.EnumerateArray()
                .Select(entry => entry.ValueKind == JsonValueKind.String ? entry.GetString()! : entry.GetRawText())
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

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
                    Enumerable.Range(10, 4).Any(column => Field(fields, column).Length > 0),
                    above[3]));
            }

            at = index < html.Length ? html.IndexOf(Call, index, StringComparison.Ordinal) : -1;
        }

        return rows.Count == 0 ? null : rows;
    }

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
}
