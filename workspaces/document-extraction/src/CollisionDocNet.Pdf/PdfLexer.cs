using System.Globalization;
using System.Text;

namespace CollisionDocNet.Pdf;

public sealed class PdfLexer
{
    private readonly ReadOnlyMemory<byte> _data;
    private readonly PdfLimits _limits;
    private int _tokens;

    public PdfLexer(ReadOnlyMemory<byte> data, PdfLimits? limits = null)
    {
        _data = data;
        _limits = limits ?? new PdfLimits();
    }

    public PdfValue ReadValue(ref int offset, int depth = 0)
    {
        if (depth > _limits.MaxDepth) throw Error("PDF_DEPTH_LIMIT", offset, "Object nesting limit exceeded.");
        if (++_tokens > _limits.MaxTokens) throw Error("PDF_TOKEN_LIMIT", offset, "Token limit exceeded.");
        SkipTrivia(ref offset);
        int start = offset;
        ReadOnlySpan<byte> data = _data.Span;
        if (offset >= data.Length) throw Error("PDF_UNEXPECTED_EOF", offset, "Expected a PDF object.");

        byte b = data[offset];
        if (b == (byte)'/') return ReadName(ref offset);
        if (b == (byte)'(') return ReadLiteralString(ref offset);
        if (b == (byte)'[') return ReadArray(ref offset, depth);
        if (b == (byte)'<')
        {
            if (offset + 1 < data.Length && data[offset + 1] == (byte)'<') return ReadDictionary(ref offset, depth);
            return ReadHexString(ref offset);
        }
        if (MatchKeyword(offset, "true"u8)) { offset += 4; return new PdfBoolean(true, new(start, 4)); }
        if (MatchKeyword(offset, "false"u8)) { offset += 5; return new PdfBoolean(false, new(start, 5)); }
        if (MatchKeyword(offset, "null"u8)) { offset += 4; return new PdfNull(new(start, 4)); }
        if (IsNumberStart(b)) return ReadNumberOrReference(ref offset);
        throw Error("PDF_INVALID_TOKEN", offset, "Invalid PDF token.");
    }

    public void SkipTrivia(ref int offset)
    {
        ReadOnlySpan<byte> data = _data.Span;
        while (offset < data.Length)
        {
            byte b = data[offset];
            if (IsWhite(b)) { offset++; continue; }
            if (b != (byte)'%') break;
            while (offset < data.Length && data[offset] is not (byte)'\r' and not (byte)'\n') offset++;
        }
    }

    internal bool MatchKeyword(int offset, ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> data = _data.Span;
        if (offset < 0 || offset > data.Length - bytes.Length || !data.Slice(offset, bytes.Length).SequenceEqual(bytes)) return false;
        int end = offset + bytes.Length;
        return end == data.Length || IsWhite(data[end]) || IsDelimiter(data[end]);
    }

    private PdfValue ReadNumberOrReference(ref int offset)
    {
        int start = offset;
        PdfNumber first = ReadNumber(ref offset);
        if (!first.IsInteger || first.Value < 0 || first.Value > int.MaxValue) return first;
        int afterFirst = offset;
        SkipTrivia(ref offset);
        if (offset < _data.Length && IsNumberStart(_data.Span[offset]))
        {
            PdfNumber second = ReadNumber(ref offset);
            if (second.IsInteger && second.Value >= 0 && second.Value <= int.MaxValue)
            {
                int afterSecond = offset;
                SkipTrivia(ref offset);
                if (MatchKeyword(offset, "R"u8))
                {
                    offset++;
                    return new PdfReference((int)first.Value, (int)second.Value, new(start, offset - start));
                }
                offset = afterSecond;
            }
        }
        offset = afterFirst;
        return first;
    }

    private PdfNumber ReadNumber(ref int offset)
    {
        int start = offset;
        ReadOnlySpan<byte> data = _data.Span;
        if (offset < data.Length && data[offset] is (byte)'+' or (byte)'-') offset++;
        bool dot = false;
        int digits = 0;
        while (offset < data.Length)
        {
            byte b = data[offset];
            if (b is >= (byte)'0' and <= (byte)'9') { digits++; offset++; continue; }
            if (b == (byte)'.' && !dot) { dot = true; offset++; continue; }
            break;
        }
        if (digits == 0) throw Error("PDF_INVALID_NUMBER", start, "Numeric token has no digits.");
        string raw = Encoding.ASCII.GetString(data[start..offset]);
        if (!double.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
            throw Error("PDF_INVALID_NUMBER", start, "Numeric token is outside the supported finite range.");
        return new(value, !dot, raw, new(start, offset - start));
    }

    private PdfName ReadName(ref int offset)
    {
        int start = offset++;
        ReadOnlySpan<byte> data = _data.Span;
        var bytes = new List<byte>();
        while (offset < data.Length && !IsWhite(data[offset]) && !IsDelimiter(data[offset]))
        {
            if (data[offset] == (byte)'#' && offset + 2 < data.Length && TryHex(data[offset + 1], out int hi) && TryHex(data[offset + 2], out int lo))
            { bytes.Add((byte)((hi << 4) | lo)); offset += 3; }
            else bytes.Add(data[offset++]);
        }
        return new(Encoding.Latin1.GetString(bytes.ToArray()), new(start, offset - start));
    }

    private PdfString ReadLiteralString(ref int offset)
    {
        int start = offset++;
        ReadOnlySpan<byte> data = _data.Span;
        var bytes = new List<byte>();
        int nesting = 1;
        while (offset < data.Length && nesting > 0)
        {
            byte b = data[offset++];
            if (b == (byte)'\\')
            {
                if (offset >= data.Length) break;
                b = data[offset++];
                if (b == (byte)'\r') { if (offset < data.Length && data[offset] == (byte)'\n') offset++; continue; }
                if (b == (byte)'\n') continue;
                if (b is >= (byte)'0' and <= (byte)'7')
                {
                    int value = b - (byte)'0'; int count = 1;
                    while (count < 3 && offset < data.Length && data[offset] is >= (byte)'0' and <= (byte)'7') { value = (value << 3) + data[offset++] - (byte)'0'; count++; }
                    bytes.Add((byte)value); continue;
                }
                bytes.Add(b switch { (byte)'n' => (byte)'\n', (byte)'r' => (byte)'\r', (byte)'t' => (byte)'\t', (byte)'b' => (byte)'\b', (byte)'f' => (byte)'\f', _ => b });
            }
            else if (b == (byte)'(') { nesting++; bytes.Add(b); }
            else if (b == (byte)')') { nesting--; if (nesting > 0) bytes.Add(b); }
            else bytes.Add(b);
        }
        if (nesting != 0) throw Error("PDF_UNTERMINATED_STRING", start, "Literal string is not terminated.");
        return new(bytes.ToArray(), false, new(start, offset - start));
    }

    private PdfString ReadHexString(ref int offset)
    {
        int start = offset++; var bytes = new List<byte>(); int? high = null;
        ReadOnlySpan<byte> data = _data.Span;
        while (offset < data.Length && data[offset] != (byte)'>')
        {
            if (IsWhite(data[offset])) { offset++; continue; }
            if (!TryHex(data[offset++], out int nibble)) throw Error("PDF_INVALID_HEX", offset - 1, "Invalid hexadecimal string digit.");
            if (high is null) high = nibble; else { bytes.Add((byte)((high.Value << 4) | nibble)); high = null; }
        }
        if (offset >= data.Length) throw Error("PDF_UNTERMINATED_HEX", start, "Hexadecimal string is not terminated.");
        offset++;
        if (high is not null) bytes.Add((byte)(high.Value << 4));
        return new(bytes.ToArray(), true, new(start, offset - start));
    }

    private PdfArray ReadArray(ref int offset, int depth)
    {
        int start = offset++; var values = new List<PdfValue>();
        while (true)
        {
            SkipTrivia(ref offset);
            if (offset >= _data.Length) throw Error("PDF_UNTERMINATED_ARRAY", start, "Array is not terminated.");
            if (_data.Span[offset] == (byte)']') { offset++; break; }
            values.Add(ReadValue(ref offset, depth + 1));
        }
        return new(values, new(start, offset - start));
    }

    private PdfDictionary ReadDictionary(ref int offset, int depth)
    {
        int start = offset; offset += 2;
        var values = new Dictionary<string, PdfValue>(StringComparer.Ordinal); var duplicates = new List<string>();
        while (true)
        {
            SkipTrivia(ref offset);
            if (offset + 1 >= _data.Length) throw Error("PDF_UNTERMINATED_DICTIONARY", start, "Dictionary is not terminated.");
            if (_data.Span[offset] == (byte)'>' && _data.Span[offset + 1] == (byte)'>') { offset += 2; break; }
            PdfName key = ReadName(ref offset);
            PdfValue value = ReadValue(ref offset, depth + 1);
            if (!values.TryAdd(key.Value, value)) { duplicates.Add(key.Value); values[key.Value] = value; }
        }
        return new(values, duplicates, new(start, offset - start));
    }

    private static bool IsNumberStart(byte b) => b is (byte)'+' or (byte)'-' or (byte)'.' or >= (byte)'0' and <= (byte)'9';
    internal static bool IsWhite(byte b) => b is 0 or 9 or 10 or 12 or 13 or 32;
    internal static bool IsDelimiter(byte b) => b is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';
    internal static bool TryHex(byte b, out int value) { if (b is >= (byte)'0' and <= (byte)'9') { value = b - '0'; return true; } if (b is >= (byte)'A' and <= (byte)'F') { value = b - 'A' + 10; return true; } if (b is >= (byte)'a' and <= (byte)'f') { value = b - 'a' + 10; return true; } value = 0; return false; }
    private static PdfParseException Error(string code, int offset, string message) => new(code, offset, message);
}
