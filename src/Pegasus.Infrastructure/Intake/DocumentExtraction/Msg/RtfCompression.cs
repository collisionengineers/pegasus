using System.Buffers.Binary;
using System.Text;

namespace Pegasus.Infrastructure.Intake.DocumentExtraction.Msg;

internal static class RtfCompression
{
    private const uint CompressedMagic = 0x75465A4C;
    private const uint UncompressedMagic = 0x414C454D;
    private static readonly byte[] InitialDictionary = Encoding.ASCII.GetBytes(
        "{\\rtf1\\ansi\\mac\\deff0\\deftab720{\\fonttbl;}{\\f0\\fnil \\froman \\fswiss \\fmodern \\fscript \\fdecor MS Sans SerifSymbolArialTimes New RomanCourier{\\colortbl\\red0\\green0\\blue0\r\n\\par \\pard\\plain\\f0\\fs20\\b\\i\\u\\tab\\tx");

    public static bool TryDecompress(
        ReadOnlySpan<byte> input,
        int maximumOutputBytes,
        out byte[] output,
        out string? error,
        CancellationToken cancellationToken = default)
    {
        output = [];
        error = null;
        if (input.Length < 16 || maximumOutputBytes < 0)
        {
            error = "RTF compressed header is truncated.";
            return false;
        }

        uint compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(input);
        uint rawSize = BinaryPrimitives.ReadUInt32LittleEndian(input[4..]);
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(input[8..]);
        uint expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(input[12..]);
        ReadOnlySpan<byte> payload = input[16..];
        if (compressedSize != input.Length - 4 || rawSize > maximumOutputBytes)
        {
            error = "RTF header size exceeds its input or configured output bound.";
            return false;
        }

        if (ComputeCrc32(payload, cancellationToken) != expectedCrc)
        {
            error = "RTF compressed payload CRC does not match.";
            return false;
        }

        if (magic == UncompressedMagic)
        {
            if (payload.Length != rawSize)
            {
                error = "Uncompressed RTF payload disagrees with its raw size.";
                return false;
            }

            output = payload.ToArray();
            return true;
        }

        if (magic != CompressedMagic)
        {
            error = "RTF compression magic is unsupported.";
            return false;
        }

        var result = new byte[rawSize];
        Span<byte> dictionary = stackalloc byte[4096];
        InitialDictionary.CopyTo(dictionary);
        int dictionaryPosition = InitialDictionary.Length;
        int source = 0;
        int destination = 0;
        while (source < payload.Length && destination < result.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte flags = payload[source++];
            for (int bit = 0; bit < 8 && destination < result.Length; bit++)
            {
                if ((flags & (1 << bit)) == 0)
                {
                    if (source >= payload.Length)
                    {
                        error = "RTF literal token is truncated.";
                        return false;
                    }

                    byte value = payload[source++];
                    result[destination++] = value;
                    dictionary[dictionaryPosition] = value;
                    dictionaryPosition = (dictionaryPosition + 1) & 0xFFF;
                    continue;
                }

                if (source + 2 > payload.Length)
                {
                    error = "RTF reference token is truncated.";
                    return false;
                }

                ushort token = BinaryPrimitives.ReadUInt16BigEndian(payload[source..]);
                source += 2;
                int offset = token >> 4;
                int length = (token & 0xF) + 2;
                for (int index = 0; index < length && destination < result.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    byte value = dictionary[(offset + index) & 0xFFF];
                    result[destination++] = value;
                    dictionary[dictionaryPosition] = value;
                    dictionaryPosition = (dictionaryPosition + 1) & 0xFFF;
                }
            }
        }

        if (destination != result.Length)
        {
            error = "RTF token stream ended before the declared raw size.";
            return false;
        }

        output = result;
        return true;
    }

    internal static uint ComputeCrc32(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default)
    {
        uint crc = 0;
        foreach (byte value in bytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }
        return crc;
    }
}

internal static class PassiveRtfText
{
    internal static string Extract(
        ReadOnlySpan<byte> bytes,
        List<MsgIssue> issues,
        CancellationToken cancellationToken = default)
    {
        string rtf = Encoding.Latin1.GetString(bytes);
        var output = new StringBuilder(Math.Min(rtf.Length, 4096));
        var skipStack = new Stack<bool>();
        bool skip = false;
        // MS-OXRTFEX encapsulated HTML wraps RTF-renderer-only content in
        // \htmlrtf ... \htmlrtf0 toggles; that content is suppressed so the
        // fallback body text is the document text, not markup artefacts.
        bool htmlRtfSuppressed = false;
        int unicodeFallback = 1;
        for (int index = 0; index < rtf.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            char character = rtf[index];
            if (character == '{')
            {
                skipStack.Push(skip);
                continue;
            }
            if (character == '}')
            {
                if (skipStack.Count == 0)
                {
                    issues.Add(new("MSG_RTF_GROUP_INVALID", "RTF contains an unmatched group terminator."));
                    continue;
                }
                skip = skipStack.Pop();
                continue;
            }
            if (character != '\\')
            {
                if (!skip && !htmlRtfSuppressed && character is not ('\r' or '\n')) output.Append(character);
                continue;
            }

            if (++index >= rtf.Length) break;
            char next = rtf[index];
            if (next is '\\' or '{' or '}')
            {
                if (!skip && !htmlRtfSuppressed) output.Append(next);
                continue;
            }
            if (next == '*')
            {
                skip = true;
                continue;
            }
            if (next == '\'')
            {
                if (index + 2 < rtf.Length && byte.TryParse(rtf.AsSpan(index + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out byte value))
                {
                    if (!skip && !htmlRtfSuppressed) output.Append((char)value);
                    index += 2;
                }
                continue;
            }

            int wordStart = index;
            while (index < rtf.Length && char.IsAsciiLetter(rtf[index])) index++;
            string word = rtf[wordStart..index];
            int sign = 1;
            if (index < rtf.Length && rtf[index] == '-') { sign = -1; index++; }
            int number = 0;
            bool hasNumber = false;
            while (index < rtf.Length && char.IsAsciiDigit(rtf[index]))
            {
                hasNumber = true;
                number = checked(number * 10 + (rtf[index++] - '0'));
            }
            number *= sign;
            if (index < rtf.Length && rtf[index] != ' ') index--;

            if (word is "fonttbl" or "colortbl" or "stylesheet" or "info" or "object" or "pict" or "filetbl" or "datastore") skip = true;
            else if (word == "htmlrtf") htmlRtfSuppressed = !hasNumber || number != 0;
            else if (!skip && !htmlRtfSuppressed && word is "par" or "line" or "row") output.AppendLine();
            else if (!skip && !htmlRtfSuppressed && word == "cell") output.Append('\t');
            else if (!skip && !htmlRtfSuppressed && word == "tab") output.Append('\t');
            else if (word == "uc" && hasNumber) unicodeFallback = Math.Clamp(number, 0, 16);
            else if (!skip && !htmlRtfSuppressed && word == "u" && hasNumber)
            {
                output.Append((char)(ushort)number);
                index = Math.Min(rtf.Length - 1, index + unicodeFallback);
            }
            else if (word == "bin" && hasNumber)
            {
                index = Math.Min(rtf.Length - 1, index + Math.Max(0, number));
                issues.Add(new("MSG_RTF_BINARY_PASSIVE", "RTF binary data was skipped without activation."));
            }
        }

        if (skipStack.Count != 0) issues.Add(new("MSG_RTF_GROUP_INVALID", "RTF contains unclosed groups."));
        return output.ToString().Trim();
    }
}
