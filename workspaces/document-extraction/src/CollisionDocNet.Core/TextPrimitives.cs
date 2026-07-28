using System.Collections.Immutable;
using System.Text;

namespace CollisionDocNet.Core;

public enum DocumentEncoding
{
    Utf8 = 0,
    Utf16LittleEndian,
    Utf16BigEndian,
    Windows1252,
}

public enum InvalidTextPolicy
{
    Reject = 0,
    Replace,
}

public readonly record struct TextDecodeResult(
    bool IsSuccess,
    string Text,
    ImmutableArray<int> InvalidByteOffsets);

public static class DocumentTextDecoder
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly UTF8Encoding ReplacingUtf8 = new(false, false);
    private static readonly UnicodeEncoding StrictUtf16LittleEndian = new(false, false, true);
    private static readonly UnicodeEncoding ReplacingUtf16LittleEndian = new(false, false, false);
    private static readonly UnicodeEncoding StrictUtf16BigEndian = new(true, false, true);
    private static readonly UnicodeEncoding ReplacingUtf16BigEndian = new(true, false, false);

    private static ReadOnlySpan<char> Windows1252Controls =>
    [
        '\u20AC', '\u0081', '\u201A', '\u0192', '\u201E', '\u2026', '\u2020', '\u2021',
        '\u02C6', '\u2030', '\u0160', '\u2039', '\u0152', '\u008D', '\u017D', '\u008F',
        '\u0090', '\u2018', '\u2019', '\u201C', '\u201D', '\u2022', '\u2013', '\u2014',
        '\u02DC', '\u2122', '\u0161', '\u203A', '\u0153', '\u009D', '\u017E', '\u0178',
    ];

    public static TextDecodeResult Decode(
        ReadOnlySpan<byte> bytes,
        DocumentEncoding encoding,
        InvalidTextPolicy invalidPolicy)
    {
        if (!Enum.IsDefined(encoding))
        {
            throw new ArgumentOutOfRangeException(nameof(encoding));
        }

        if (!Enum.IsDefined(invalidPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(invalidPolicy));
        }

        return encoding switch
        {
            DocumentEncoding.Windows1252 => DecodeWindows1252(bytes, invalidPolicy),
            DocumentEncoding.Utf8 => DecodeUnicode(
                bytes,
                StrictUtf8,
                ReplacingUtf8,
                invalidPolicy),
            DocumentEncoding.Utf16LittleEndian => DecodeUnicode(
                bytes,
                StrictUtf16LittleEndian,
                ReplacingUtf16LittleEndian,
                invalidPolicy),
            DocumentEncoding.Utf16BigEndian => DecodeUnicode(
                bytes,
                StrictUtf16BigEndian,
                ReplacingUtf16BigEndian,
                invalidPolicy),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
        };
    }

    private static TextDecodeResult DecodeUnicode(
        ReadOnlySpan<byte> bytes,
        Encoding strictEncoding,
        Encoding replacingEncoding,
        InvalidTextPolicy invalidPolicy)
    {
        ImmutableArray<int> offsets = strictEncoding.CodePage == Encoding.UTF8.CodePage
            ? FindInvalidUtf8Offsets(bytes)
            : FindInvalidUtf16Offsets(bytes, strictEncoding.CodePage == 1201);
        string text = replacingEncoding.GetString(bytes);
        return invalidPolicy == InvalidTextPolicy.Reject && !offsets.IsEmpty
            ? new TextDecodeResult(false, string.Empty, offsets)
            : new TextDecodeResult(true, text, offsets);
    }

    private static TextDecodeResult DecodeWindows1252(
        ReadOnlySpan<byte> bytes,
        InvalidTextPolicy invalidPolicy)
    {
        char[] characters = new char[bytes.Length];
        var invalidOffsets = ImmutableArray.CreateBuilder<int>();
        ReadOnlySpan<char> controls = Windows1252Controls;

        for (int index = 0; index < bytes.Length; index++)
        {
            byte value = bytes[index];
            if (value is >= 0x80 and <= 0x9F)
            {
                char mapped = controls[value - 0x80];
                bool undefined = mapped is '\u0081' or '\u008D' or '\u008F' or '\u0090' or '\u009D';
                if (undefined)
                {
                    invalidOffsets.Add(index);
                    characters[index] = '\uFFFD';
                    continue;
                }

                characters[index] = mapped;
                continue;
            }

            characters[index] = (char)value;
        }

        ImmutableArray<int> offsets = invalidOffsets.ToImmutable();
        return invalidPolicy == InvalidTextPolicy.Reject && !offsets.IsEmpty
            ? new TextDecodeResult(false, string.Empty, offsets)
            : new TextDecodeResult(true, new string(characters), offsets);
    }


    private static ImmutableArray<int> FindInvalidUtf8Offsets(ReadOnlySpan<byte> bytes)
    {
        var invalid = ImmutableArray.CreateBuilder<int>();
        for (int index = 0; index < bytes.Length;)
        {
            byte first = bytes[index];
            int length;
            bool valid;
            if (first <= 0x7F)
            {
                index++;
                continue;
            }

            if (first is >= 0xC2 and <= 0xDF)
            {
                length = 2;
                valid = HasContinuation(bytes, index, length);
            }
            else if (first is >= 0xE0 and <= 0xEF)
            {
                length = 3;
                valid = HasContinuation(bytes, index, length)
                    && (first != 0xE0 || bytes[index + 1] >= 0xA0)
                    && (first != 0xED || bytes[index + 1] <= 0x9F);
            }
            else if (first is >= 0xF0 and <= 0xF4)
            {
                length = 4;
                valid = HasContinuation(bytes, index, length)
                    && (first != 0xF0 || bytes[index + 1] >= 0x90)
                    && (first != 0xF4 || bytes[index + 1] <= 0x8F);
            }
            else
            {
                length = 1;
                valid = false;
            }

            if (!valid)
            {
                invalid.Add(index);
                index++;
                continue;
            }

            index += length;
        }

        return invalid.ToImmutable();
    }

    private static bool HasContinuation(ReadOnlySpan<byte> bytes, int index, int length)
    {
        if (index > bytes.Length - length)
        {
            return false;
        }

        for (int offset = 1; offset < length; offset++)
        {
            if (bytes[index + offset] is not (>= 0x80 and <= 0xBF))
            {
                return false;
            }
        }

        return true;
    }

    private static ImmutableArray<int> FindInvalidUtf16Offsets(
        ReadOnlySpan<byte> bytes,
        bool bigEndian)
    {
        var invalid = ImmutableArray.CreateBuilder<int>();
        int evenLength = bytes.Length & ~1;
        for (int index = 0; index < evenLength; index += 2)
        {
            ushort value = bigEndian
                ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(bytes[index..])
                : System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes[index..]);
            if (value is >= 0xD800 and <= 0xDBFF)
            {
                if (index + 3 >= bytes.Length)
                {
                    invalid.Add(index);
                    continue;
                }

                ushort next = bigEndian
                    ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(bytes[(index + 2)..])
                    : System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes[(index + 2)..]);
                if (next is >= 0xDC00 and <= 0xDFFF)
                {
                    index += 2;
                }
                else
                {
                    invalid.Add(index);
                }
            }
            else if (value is >= 0xDC00 and <= 0xDFFF)
            {
                invalid.Add(index);
            }
        }

        if (evenLength != bytes.Length)
        {
            invalid.Add(evenLength);
        }

        return invalid.ToImmutable();
    }
}

public readonly record struct UtcDocumentTimestamp
{
    private UtcDocumentTimestamp(DateTimeOffset value) => Value = value;

    public DateTimeOffset Value { get; }

    public static bool TryFromFileTime(long fileTime, out UtcDocumentTimestamp timestamp)
    {
        timestamp = default;
        try
        {
            timestamp = new UtcDocumentTimestamp(DateTimeOffset.FromFileTime(fileTime).ToUniversalTime());
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}

public static class DeterministicText
{
    public const string PolicyId = "unicode-nfc-lf/1";

    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string unicode = value.Normalize(NormalizationForm.FormC);
        if (!unicode.AsSpan().Contains('\r'))
        {
            return unicode;
        }

        var builder = new StringBuilder(unicode.Length);
        for (int index = 0; index < unicode.Length; index++)
        {
            char character = unicode[index];
            if (character == '\r')
            {
                if (index + 1 < unicode.Length && unicode[index + 1] == '\n')
                {
                    index++;
                }

                builder.Append('\n');
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
