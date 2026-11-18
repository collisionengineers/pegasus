using System.Globalization;
using System.Text;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// The character set and form encoding of Glass's editor. Its pages are
/// declared iso-8859-1, which a browser reads and posts as windows-1252, so
/// every answer is decoded and every form is encoded that way. No code-pages
/// provider is registered in this repository, so windows-1252 is written out
/// here: Latin-1 with its own 27 characters in 0x80 to 0x9F.
/// </summary>
internal static class GlassEditorText
{
    /// <summary>
    /// The characters windows-1252 places in 0x80 to 0x9F, by byte. The five
    /// bytes it leaves unassigned read as the control character of the same
    /// number, as a browser reads them.
    /// </summary>
    private static readonly char[] HighBytes =
    [
        '\u20AC', '\u0081', '\u201A', '\u0192', '\u201E', '\u2026', '\u2020', '\u2021',
        '\u02C6', '\u2030', '\u0160', '\u2039', '\u0152', '\u008D', '\u017D', '\u008F',
        '\u0090', '\u2018', '\u2019', '\u201C', '\u201D', '\u2022', '\u2013', '\u2014',
        '\u02DC', '\u2122', '\u0161', '\u203A', '\u0153', '\u009D', '\u017E', '\u0178',
    ];

    /// <summary>The same table the other way: a character above 0xFF to its windows-1252 byte.</summary>
    private static readonly Dictionary<int, byte> HighCharacters = HighBytes
        .Select((character, index) => (Character: (int)character, Byte: (byte)(0x80 + index)))
        .Where(entry => entry.Character > 0xFF)
        .ToDictionary(entry => entry.Character, entry => entry.Byte);

    /// <summary>An answer's bytes as the text a browser reads from them.</summary>
    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        var text = new StringBuilder(bytes.Length);
        foreach (var value in bytes)
        {
            text.Append(value is >= 0x80 and <= 0x9F ? HighBytes[value - 0x80] : (char)value);
        }

        return text.ToString();
    }

    /// <summary>
    /// Text as windows-1252 bytes, a character the set does not hold written
    /// as a question mark. The editor's own answers are made of these bytes;
    /// nothing posted is encoded this way (see <see cref="EncodeForm"/>).
    /// </summary>
    public static byte[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = new byte[text.Length];
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            bytes[index] = character <= 0xFF
                ? (byte)character
                : HighCharacters.TryGetValue(character, out var mapped) ? mapped : (byte)'?';
        }

        return bytes;
    }

    /// <summary>
    /// Name and value pairs as a browser posts a form of the editor's
    /// (application/x-www-form-urlencoded): in order, joined by ampersands.
    /// </summary>
    public static string EncodeForm(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        return string.Join('&', pairs.Select(pair => EncodeValue(pair.Key) + "=" + EncodeValue(pair.Value)));
    }

    /// <summary>
    /// One name or value as a browser encodes it for this page's character
    /// set: line ends as CRLF, a space as a plus, the letters, digits and
    /// <c>*-._</c> as they are, every other byte as upper-case percent hex,
    /// and a character windows-1252 does not hold as the encoding of its
    /// numeric character reference (<c>&amp;#8364;</c> and so on).
    /// </summary>
    public static string EncodeValue(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var encoded = new StringBuilder(text.Length);
        foreach (var rune in NormaliseLineEnds(text).EnumerateRunes())
        {
            var code = rune.Value;
            int value;
            if (code <= 0xFF)
            {
                value = code;
            }
            else if (HighCharacters.TryGetValue(code, out var mapped))
            {
                value = mapped;
            }
            else
            {
                encoded.Append(EncodeValue("&#" + code.ToString(CultureInfo.InvariantCulture) + ";"));
                continue;
            }

            if (value == 0x20)
            {
                encoded.Append('+');
            }
            else if (value < 0x80 && IsPlain((char)value))
            {
                encoded.Append((char)value);
            }
            else
            {
                encoded.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return encoded.ToString();
    }

    private static bool IsPlain(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '*' or '-' or '.' or '_';

    private static string NormaliseLineEnds(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\r\n", StringComparison.Ordinal);
}
