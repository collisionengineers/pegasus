using System.Globalization;
using System.Text;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// Every address the editor client asks for once a session key is known,
/// each in the exact form the editor's own pages were seen to ask for it:
/// the parameters in the same order, the same values left unencoded, and a
/// time stamp where the page adds one. The time comes from the injected
/// clock so a test can fix it.
/// </summary>
internal sealed class GlassEditorAddresses(GlassEditorSession session, TimeProvider timeProvider)
{
    /// <summary>The special list's own group and exclusion, as the editor's graph page asks for them.</summary>
    private const string SpecialListGroup = "SS-FREEPART";

    private string Q => "ksid=" + session.Key;

    /// <summary>
    /// The editor's main frame (<c>acofr.php</c>), which the editor's other
    /// pages name as where they were asked from.
    /// </summary>
    public string Frame =>
        session.BaseAddress + "acofr.php?" + Q + "&url0=" + Escape(session.Url0)
        + "&noacd=1&thsep=%2C&html5svg=1&ieversion=6";

    /// <summary>The estimate's positions (<c>selected.php</c>), stamped in seconds.</summary>
    public string Selected() =>
        session.BaseAddress + "selected.php?" + Q + "&dummy=" + Seconds() + "&html5svg=1&win7=1&actFrame3=1";

    /// <summary>
    /// The special-operation list (<c>list_1G.php</c>) as the editor's graph
    /// page first asks for it, with the editor's group list.
    /// </summary>
    public string SpecialList(string groups) =>
        session.BaseAddress + "list_1G.php?" + Q + "&cml=A&html5svg=1&tcode=" + SpecialListGroup + "&exclk=AC&ds="
        + groups + "&win7=1";

    /// <summary>
    /// The vehicle's part list (<c>list.php</c>, about 2 MB), with the
    /// editor's group list in its address as the graph page asks for it.
    /// </summary>
    public string PartList(string groups) =>
        session.BaseAddress + "list.php?" + Q + "&cml=C&html5svg=1&ds=" + groups + "&win7=1";

    /// <summary>One position's record (<c>mcode.php</c>).</summary>
    public string Position(string id) => session.BaseAddress + "mcode.php?" + Q + "&partid=" + id + "&mcode=";

    /// <summary>The equipment map of one row of the part list.</summary>
    public string Equipment(string row) => Service("rowid=" + row + "&reqcritmap3=1");

    /// <summary>A dialog page: <c>comp.php</c> or <c>part.php</c>, for rows or a code, in a mode (<c>C</c> or <c>A</c>).</summary>
    public string Dialog(string page, string rows, string mode) =>
        session.BaseAddress + page + "?" + Q + "&mcode=" + rows + "&cml=" + mode
        + "&dialogCodePage=iso-8859-1&dummy=" + Milliseconds();

    /// <summary>The dialog of a position already on the estimate.</summary>
    public string OpenPosition(string id) =>
        session.BaseAddress + "part.php?" + Q + "&cml=C&partid=" + id + "&dialogCodePage=iso-8859-1&dummy=" + Milliseconds();

    /// <summary>The removal of a position already on the estimate.</summary>
    public string Remove(string id) =>
        session.BaseAddress + "compproc.php?" + Q + "&cml=C&partid=" + id + "&dummy=" + Milliseconds();

    /// <summary>
    /// The first step of Save and Exit: this request saves, and its answer
    /// tells the editor to close.
    /// </summary>
    public string Save() => Service("savexit=1");

    /// <summary>The second step of Save and Exit: the page that sends the window back through the launch page.</summary>
    public string Close =>
        session.BaseAddress + "close.php?" + Q + "&url0=" + EncodeUriComponent(session.Url0) + "&dosave=1";

    private string Service(string rest) =>
        session.BaseAddress + "service.php?" + Q + "&html5svg=1&nationscodepage=iso-8859-1&" + rest
        + "&dummy=" + Milliseconds();

    private string Milliseconds() =>
        timeProvider.GetUtcNow().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

    private string Seconds() =>
        timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// JavaScript's <c>escape</c>: letters, digits and <c>@*_+-./</c> as they
    /// are, any other character below 256 as upper-case <c>%XX</c> and any
    /// above as <c>%uXXXX</c>. It is not <see cref="Uri.EscapeDataString"/>.
    /// </summary>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var escaped = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '@' or '*' or '_' or '+' or '-' or '.' or '/')
            {
                escaped.Append(character);
            }
            else if (character < 256)
            {
                escaped.Append('%').Append(((int)character).ToString("X2", CultureInfo.InvariantCulture));
            }
            else
            {
                escaped.Append("%u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
            }
        }

        return escaped.ToString();
    }

    /// <summary>
    /// JavaScript's <c>encodeURIComponent</c>: letters, digits and
    /// <c>-_.!~*'()</c> as they are, everything else as the upper-case
    /// percent hex of its UTF-8 bytes.
    /// </summary>
    public static string EncodeUriComponent(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var encoded = new StringBuilder(text.Length);
        Span<byte> bytes = stackalloc byte[4];
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.IsAscii && (char.IsAsciiLetterOrDigit((char)rune.Value)
                || (char)rune.Value is '-' or '_' or '.' or '!' or '~' or '*' or '\'' or '(' or ')'))
            {
                encoded.Append((char)rune.Value);
                continue;
            }

            var length = rune.EncodeToUtf8(bytes);
            foreach (var value in bytes[..length])
            {
                encoded.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return encoded.ToString();
    }

    /// <summary>
    /// A page's relative address resolved against the address it came from,
    /// as a browser resolves it: the path by the usual rules, the query kept
    /// as the page wrote it except for the characters a browser
    /// percent-encodes in a query (space, quotes, angle brackets, controls and
    /// anything beyond ASCII, the last as UTF-8).
    /// </summary>
    public static string Resolve(string reference, string baseAddress)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var at = reference.IndexOf('?', StringComparison.Ordinal);
        var path = at < 0 ? reference : reference[..at];
        var resolved = new Uri(new Uri(baseAddress, UriKind.Absolute), path).GetLeftPart(UriPartial.Path);
        return at < 0 ? resolved : resolved + EncodeQuery(reference[at..]);
    }

    private static string EncodeQuery(string query)
    {
        var encoded = new StringBuilder(query.Length);
        Span<byte> bytes = stackalloc byte[4];
        foreach (var rune in query.EnumerateRunes())
        {
            // Printable ASCII but for the double quote, angle brackets and single quote.
            if (rune.Value is > 0x20 and < 0x7F and not 0x22 and not 0x3C and not 0x3E and not 0x27)
            {
                encoded.Append((char)rune.Value);
                continue;
            }

            var length = rune.EncodeToUtf8(bytes);
            foreach (var value in bytes[..length])
            {
                encoded.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return encoded.ToString();
    }
}
