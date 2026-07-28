using System.Globalization;
using System.Text;

namespace CollisionDocNet.Pdf;

internal static class PdfContentInterpreter
{
    public static void Interpret(byte[] content, int pageIndex, IReadOnlyDictionary<string, PdfFontMap> fonts, List<PdfTextRun> output, List<PdfIssue> issues, PdfLimits limits, int sourceBase, Func<string, int, bool>? xObjectHandler = null, Action<string, PdfDictionary?, int>? markedContentHandler = null, Action<PdfInlineImage>? inlineImageHandler = null, PdfContentBudget? budget = null, CancellationToken cancellationToken = default)
    {
        budget ??= new PdfContentBudget(limits); var lexer = new PdfLexer(content, limits); var operands = new List<PdfValue>(); int offset = 0;
        var state = new TextState(); var stack = new Stack<TextState>(); bool inText = false;
        while (offset < content.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lexer.SkipTrivia(ref offset); if (offset >= content.Length) break;
            byte b = content[offset];
            if (IsValueStart(content, offset)) { operands.Add(lexer.ReadValue(ref offset)); continue; }
            int operatorOffset = offset; string op = ReadOperator(content, ref offset);
            if (op.Length == 0) throw new PdfParseException("PDF_CONTENT_TOKEN_INVALID", sourceBase + offset, "Invalid content-stream token.");
            budget.AddOperator(sourceBase + operatorOffset);
            switch (op)
            {
                case "BT": inText = true; state.ResetText(); break;
                case "ET": inText = false; break;
                case "q": stack.Push(state.Clone()); break;
                case "Q": if (stack.Count > 0) state = stack.Pop(); else issues.Add(Issue("PDF_GRAPHICS_STACK_UNDERFLOW", sourceBase + operatorOffset, "Graphics-state restore has no matching save.")); break;
                case "Tf" when operands.Count >= 2 && operands[^2] is PdfName font && Number(operands[^1], out double size): state.Font = font.Value; state.FontSize = size; break;
                case "Tc" when LastNumber(operands, out double tc): state.CharacterSpacing = tc; break;
                case "Tw" when LastNumber(operands, out double tw): state.WordSpacing = tw; break;
                case "Tz" when LastNumber(operands, out double tz): state.HorizontalScale = tz / 100d; break;
                case "TL" when LastNumber(operands, out double tl): state.Leading = tl; break;
                case "Ts" when LastNumber(operands, out double rise): state.Rise = rise; break;
                case "Tm" when SixNumbers(operands, out _, out _, out _, out _, out double e, out double f): state.X = e; state.Y = f; state.LineX = e; state.LineY = f; break;
                case "Td" when TwoNumbers(operands, out double tx, out double ty): state.LineX += tx; state.LineY += ty; state.X = state.LineX; state.Y = state.LineY; break;
                case "TD" when TwoNumbers(operands, out double tdx, out double tdy): state.Leading = -tdy; state.LineX += tdx; state.LineY += tdy; state.X = state.LineX; state.Y = state.LineY; break;
                case "T*": state.LineY -= state.Leading; state.X = state.LineX; state.Y = state.LineY; break;
                case "Tj" when inText && operands.LastOrDefault() is PdfString text: Show(text, pageIndex, operatorOffset, fonts, state, output, budget); break;
                case "TJ" when inText && operands.LastOrDefault() is PdfArray array: ShowArray(array, pageIndex, operatorOffset, fonts, state, output, budget); break;
                case "'" when inText && operands.LastOrDefault() is PdfString quoted: state.LineY -= state.Leading; state.X = state.LineX; state.Y = state.LineY; Show(quoted, pageIndex, operatorOffset, fonts, state, output, budget); break;
                case "\"" when inText && operands.Count >= 3 && Number(operands[^3], out double ws) && Number(operands[^2], out double cs) && operands[^1] is PdfString doubleQuoted: state.WordSpacing = ws; state.CharacterSpacing = cs; state.LineY -= state.Leading; state.X = state.LineX; state.Y = state.LineY; Show(doubleQuoted, pageIndex, operatorOffset, fonts, state, output, budget); break;
                case "Do" when operands.LastOrDefault() is PdfName xObject:
                    if (xObjectHandler?.Invoke(xObject.Value, sourceBase + operatorOffset) != true)
                        issues.Add(Issue("PDF_XOBJECT_NOT_INTERPRETED", sourceBase + operatorOffset, "Image or unsupported XObject occurrence was recorded but not interpreted by this subset."));
                    break;
                case "BI":
                    PdfInlineImage inlineImage = ReadInlineImage(content, ref offset, limits, sourceBase, operatorOffset, cancellationToken);
                    inlineImageHandler?.Invoke(inlineImage);
                    issues.Add(new PdfIssue("PDF_INLINE_IMAGE_RETAINED", PdfIssueSeverity.Information, sourceBase + operatorOffset, $"Inline image ({inlineImage.EncodedBytes.Length} encoded bytes) was retained as passive evidence without codec execution."));
                    break;
                case "f" or "f*" or "n": break;
                case "BMC" when operands.LastOrDefault() is PdfName markedTag: markedContentHandler?.Invoke(markedTag.Value, null, sourceBase + operatorOffset); break;
                case "BDC" when operands.Count >= 2 && operands[^2] is PdfName propertyTag: markedContentHandler?.Invoke(propertyTag.Value, operands[^1] as PdfDictionary, sourceBase + operatorOffset); break;
            }
            operands.Clear();
        }
        if (inText) issues.Add(Issue("PDF_TEXT_OBJECT_UNTERMINATED", sourceBase + content.Length, "Content stream ends inside a text object."));
    }

    private static void ShowArray(PdfArray array, int pageIndex, int operatorOffset, IReadOnlyDictionary<string, PdfFontMap> fonts, TextState state, List<PdfTextRun> output, PdfContentBudget budget)
    {
        foreach (PdfValue item in array.Values)
        {
            if (item is PdfString text) Show(text, pageIndex, operatorOffset, fonts, state, output, budget);
            else if (item is PdfNumber adjustment) state.X -= adjustment.Value / 1000d * state.FontSize * state.HorizontalScale;
        }
    }

    private static void Show(PdfString value, int pageIndex, int operatorOffset, IReadOnlyDictionary<string, PdfFontMap> fonts, TextState state, List<PdfTextRun> output, PdfContentBudget budget)
    {
        PdfFontMap map = fonts.TryGetValue(state.Font, out PdfFontMap? found) ? found : PdfFontMap.Default;
        (string decoded, int glyphs, string source) = map.Decode(value.Bytes);
        budget.AddText(decoded.Length, operatorOffset);
        if (decoded.Length > 0) output.Add(new(pageIndex, decoded, state.X, state.Y + state.Rise, operatorOffset, source + ";position-approximate"));
        int spaces = 0;
        foreach (char character in decoded) if (character == ' ') spaces++;
        state.X += glyphs * (state.FontSize * 0.5 + state.CharacterSpacing) * state.HorizontalScale + spaces * state.WordSpacing;
    }

    private static string ReadOperator(ReadOnlySpan<byte> data, ref int offset)
    {
        int start = offset;
        while (offset < data.Length && !PdfLexer.IsWhite(data[offset]) && !PdfLexer.IsDelimiter(data[offset])) offset++;
        if (offset == start && data[offset] is (byte)'\'' or (byte)'\"') offset++;
        return Encoding.ASCII.GetString(data[start..offset]);
    }

    private static bool IsValueStart(ReadOnlySpan<byte> data, int offset)
    {
        byte b = data[offset];
        if (b is (byte)'/' or (byte)'(' or (byte)'[' or (byte)'<' or (byte)'+' or (byte)'-' or (byte)'.' or >= (byte)'0' and <= (byte)'9') return true;
        return StartsKeyword(data, offset, "true"u8) || StartsKeyword(data, offset, "false"u8) || StartsKeyword(data, offset, "null"u8);
    }
    private static bool StartsKeyword(ReadOnlySpan<byte> data, int offset, ReadOnlySpan<byte> keyword) => offset <= data.Length - keyword.Length && data.Slice(offset, keyword.Length).SequenceEqual(keyword) && (offset + keyword.Length == data.Length || PdfLexer.IsWhite(data[offset + keyword.Length]) || PdfLexer.IsDelimiter(data[offset + keyword.Length]));

    private static PdfInlineImage ReadInlineImage(ReadOnlyMemory<byte> memory, ref int offset, PdfLimits limits, int sourceBase, int operatorOffset, CancellationToken cancellationToken)
    {
        ReadOnlySpan<byte> data = memory.Span;
        var values = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
        var lexer = new PdfLexer(memory, limits);
        while (offset < data.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lexer.SkipTrivia(ref offset);
            if (offset <= data.Length - 2 && data[offset] == (byte)'I' && data[offset + 1] == (byte)'D' && (offset + 2 == data.Length || PdfLexer.IsWhite(data[offset + 2])))
            {
                offset += 2;
                break;
            }
            PdfValue keyValue = lexer.ReadValue(ref offset);
            if (keyValue is not PdfName key) throw new PdfParseException("PDF_INLINE_IMAGE_DICTIONARY_INVALID", sourceBase + offset, "Inline image dictionary key is not a name.");
            lexer.SkipTrivia(ref offset);
            if (offset >= data.Length) throw new PdfParseException("PDF_INLINE_IMAGE_ID_MISSING", sourceBase + offset, "Inline image dictionary has no ID delimiter.");
            values[ExpandInlineKey(key.Value)] = ExpandInlineValue(lexer.ReadValue(ref offset));
        }
        if (offset < 2 || data[offset - 2] != (byte)'I' || data[offset - 1] != (byte)'D') throw new PdfParseException("PDF_INLINE_IMAGE_ID_MISSING", sourceBase + offset, "Inline image dictionary has no ID delimiter.");
        if (offset < data.Length && PdfLexer.IsWhite(data[offset])) offset++;
        int start = offset;
        int maximumEnd = Math.Min(data.Length, checked(start + limits.MaxInlineImageBytes + 3));
        while (offset < maximumEnd - 1)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (data[offset] == (byte)'E' && data[offset + 1] == (byte)'I' && (offset == start || PdfLexer.IsWhite(data[offset - 1])) && (offset + 2 == data.Length || PdfLexer.IsWhite(data[offset + 2]) || PdfLexer.IsDelimiter(data[offset + 2])))
            {
                int dataEnd = offset;
                if (dataEnd > start && PdfLexer.IsWhite(data[dataEnd - 1])) dataEnd--;
                int length = dataEnd - start;
                offset += 2;
                var dictionary = new PdfDictionary(values, [], new(sourceBase + operatorOffset, start - operatorOffset));
                return new PdfInlineImage(dictionary, data.Slice(start, length).ToArray(), sourceBase + operatorOffset, sourceBase + start);
            }
            offset++;
        }
        throw new PdfParseException("PDF_INLINE_IMAGE_LIMIT", sourceBase + start, "Inline image terminator was missing or exceeded its byte limit.");
    }
    private static string ExpandInlineKey(string key) => key switch { "BPC" => "BitsPerComponent", "CS" => "ColorSpace", "D" => "Decode", "DP" => "DecodeParms", "F" => "Filter", "H" => "Height", "IM" => "ImageMask", "I" => "Interpolate", "W" => "Width", _ => key };
    private static PdfValue ExpandInlineValue(PdfValue value) => value switch
    {
        PdfName name => name with { Value = name.Value switch { "AHx" => "ASCIIHexDecode", "A85" => "ASCII85Decode", "CCF" => "CCITTFaxDecode", "DCT" => "DCTDecode", "Fl" => "FlateDecode", "LZW" => "LZWDecode", "RL" => "RunLengthDecode", "G" => "DeviceGray", "RGB" => "DeviceRGB", "CMYK" => "DeviceCMYK", "I" => "Indexed", _ => name.Value } },
        PdfArray array => array with { Values = array.Values.Select(ExpandInlineValue).ToArray() },
        _ => value
    };
    private static bool LastNumber(List<PdfValue> values, out double a) { a = 0; return values.Count >= 1 && Number(values[^1], out a); }
    private static bool TwoNumbers(List<PdfValue> v, out double a, out double b) { a = b = 0; return v.Count >= 2 && Number(v[^2], out a) && Number(v[^1], out b); }
    private static bool SixNumbers(List<PdfValue> v, out double a, out double b, out double c, out double d, out double e, out double f) { a = b = c = d = e = f = 0; return v.Count >= 6 && Number(v[^6], out a) && Number(v[^5], out b) && Number(v[^4], out c) && Number(v[^3], out d) && Number(v[^2], out e) && Number(v[^1], out f); }
    private static bool Number(PdfValue value, out double number) { if (value is PdfNumber n) { number = n.Value; return true; } number = 0; return false; }
    private static PdfIssue Issue(string code, int offset, string message) => new(code, PdfIssueSeverity.Warning, offset, message);

    private sealed class TextState
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double LineX { get; set; }
        public double LineY { get; set; }
        public double Leading { get; set; }
        public double FontSize { get; set; } = 12;
        public string Font { get; set; } = string.Empty;
        public double CharacterSpacing { get; set; }
        public double WordSpacing { get; set; }
        public double HorizontalScale { get; set; } = 1;
        public double Rise { get; set; }
        public void ResetText() { X = Y = LineX = LineY = 0; }
        public TextState Clone() => (TextState)MemberwiseClone();
    }
}

internal sealed record PdfInlineImage(PdfDictionary Dictionary, byte[] EncodedBytes, int OperatorOffset, int DataOffset);

internal sealed class PdfContentBudget(PdfLimits limits)
{
    private int _operators;
    private int _textCharacters;
    private readonly HashSet<PdfObjectId> _activeForms = [];
    public void AddOperator(int offset) { if (++_operators > limits.MaxOperators) throw new PdfParseException("PDF_OPERATOR_LIMIT", offset, "Cumulative content operator limit exceeded."); }
    public void AddText(int count, int offset) { _textCharacters = checked(_textCharacters + count); if (_textCharacters > limits.MaxTextCharacters) throw new PdfParseException("PDF_TEXT_LIMIT", offset, "Cumulative extracted text limit exceeded."); }
    public bool TryEnterForm(PdfObjectId? id) => id is null || _activeForms.Add(id.Value);
    public void ExitForm(PdfObjectId? id) { if (id is not null) _activeForms.Remove(id.Value); }
}

internal sealed class PdfFontMap(Dictionary<int, string>? toUnicode, string encoding)
{
    public static PdfFontMap Default { get; } = new(null, "StandardEncoding");

    public (string Text, int Glyphs, string Source) Decode(ReadOnlySpan<byte> bytes)
    {
        if (toUnicode is not null)
        {
            var result = new StringBuilder(); int glyphs = 0;
            for (int i = 0; i < bytes.Length;)
            {
                string? mapped = null; int consumed = 0; int code = 0;
                for (int length = Math.Min(4, bytes.Length - i); length >= 1; length--)
                {
                    code = 0;
                    for (int j = 0; j < length; j++) code = (code << 8) | bytes[i + j];
                    if (toUnicode.TryGetValue(CodeKey(length, code), out mapped)) { consumed = length; break; }
                }
                if (consumed == 0) consumed = 1;
                i += consumed;
                result.Append(mapped ?? "\uFFFD"); glyphs++;
            }
            return (result.ToString(), glyphs, "ToUnicode");
        }
        var fallback = new StringBuilder(bytes.Length);
        foreach (byte value in bytes) fallback.Append(DecodeSingleByte(value, encoding));
        return (fallback.ToString(), bytes.Length, encoding);
    }

    public static Dictionary<int, string> ParseToUnicode(ReadOnlySpan<byte> bytes, PdfLimits limits)
    {
        string text = Encoding.ASCII.GetString(bytes); string[] tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var result = new Dictionary<int, string>(); int index = 0; int entries = 0;
        while (index < tokens.Length)
        {
            if (tokens[index] == "beginbfchar")
            {
                int count = index > 0 && int.TryParse(tokens[index - 1], CultureInfo.InvariantCulture, out int n) ? n : 0; index++;
                for (int i = 0; i < count && index + 1 < tokens.Length; i++, index += 2) AddMap(tokens[index], tokens[index + 1], result, ref entries, limits);
                continue;
            }
            if (tokens[index] == "beginbfrange")
            {
                int count = index > 0 && int.TryParse(tokens[index - 1], CultureInfo.InvariantCulture, out int n) ? n : 0; index++;
                for (int i = 0; i < count && index + 2 < tokens.Length; i++, index += 3)
                {
                    if (!TryCode(tokens[index], out int first, out int length) || !TryCode(tokens[index + 1], out int last, out int lastLength) || length != lastLength || first > last || last - first > 65535) continue;
                    if (tokens[index + 2].Length > 0 && tokens[index + 2][0] == '[')
                    {
                        int cursor = index + 2; string token = tokens[cursor].TrimStart('['); int code = first;
                        while (code <= last)
                        {
                            if (token.Length > 0 && TryUnicode(token.TrimEnd(']'), out string arrayValue)) AddEntry(result, CodeKey(length, code++), arrayValue, ref entries, limits);
                            if (tokens[cursor].EndsWith(']') || ++cursor >= tokens.Length) break;
                            token = tokens[cursor];
                        }
                        index = cursor - 2;
                        continue;
                    }
                    if (!TryUnicode(tokens[index + 2], out string value)) continue;
                    int scalar = value.Length > 0 ? value[0] : 0;
                    for (int code = first; code <= last; code++) AddEntry(result, CodeKey(length, code), char.ConvertFromUtf32(scalar + code - first), ref entries, limits);
                }
                continue;
            }
            index++;
        }
        return result;
    }

    private static void AddMap(string source, string destination, Dictionary<int, string> result, ref int entries, PdfLimits limits)
    {
        if (!TryCode(source, out int code, out int length) || !TryUnicode(destination, out string value)) return;
        AddEntry(result, CodeKey(length, code), value, ref entries, limits);
    }
    private static void AddEntry(Dictionary<int, string> result, int key, string value, ref int entries, PdfLimits limits) { if (++entries > limits.MaxTokens) throw new PdfParseException("PDF_CMAP_LIMIT", 0, "CMap entry limit exceeded."); result[key] = value; }
    private static int CodeKey(int length, int code) => unchecked((length << 28) | code);
    private static bool TryCode(string token, out int code, out int length) { code = 0; length = 0; if (token.Length < 4 || token[0] != '<' || token[^1] != '>' || (token.Length - 2) % 2 != 0 || token.Length > 10) return false; length = (token.Length - 2) / 2; return int.TryParse(token.AsSpan(1, token.Length - 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code); }
    private static char DecodeSingleByte(byte value, string encoding)
    {
        if (string.Equals(encoding, "WinAnsiEncoding", StringComparison.Ordinal) && value is >= 0x80 and <= 0x9F)
            return "€�‚ƒ„…†‡ˆ‰Š‹Œ�Ž��‘’“”•–—˜™š›œ�žŸ"[value - 0x80];
        if (string.Equals(encoding, "StandardEncoding", StringComparison.Ordinal))
            return value switch { 0x27 => '\u2019', 0x60 => '\u2018', _ when value is >= 0x20 and <= 0x7E => (char)value, _ => '\uFFFD' };
        return (char)value;
    }
    private static bool TryUnicode(string token, out string value)
    {
        value = string.Empty; if (token.Length < 6 || token[0] != '<' || token[^1] != '>') return false;
        string hex = token[1..^1]; if (hex.Length % 4 != 0) return false; var result = new StringBuilder();
        for (int i = 0; i < hex.Length; i += 4) { if (!ushort.TryParse(hex.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort unit)) return false; result.Append((char)unit); }
        value = result.ToString(); return true;
    }
}
