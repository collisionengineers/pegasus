using System.Buffers;
using System.IO.Compression;

namespace CollisionDocNet.Pdf;

public static class PdfStreamDecoder
{
    public static byte[] Decode(PdfStream stream, PdfLimits? limits = null, CancellationToken cancellationToken = default)
    {
        try { return DecodeCore(stream, limits, cancellationToken); }
        catch (OverflowException) { throw new PdfParseException("PDF_STREAM_LIMIT", stream.Span.Offset, "Decoded stream arithmetic exceeded its supported range."); }
        catch (InvalidDataException) { throw new PdfParseException("PDF_INVALID_FLATE", stream.Span.Offset, "Flate stream data is invalid or truncated."); }
    }

    private static byte[] DecodeCore(PdfStream stream, PdfLimits? limits, CancellationToken cancellationToken)
    {
        limits ??= new PdfLimits();
        string[] filters = ReadFilters(stream.Dictionary);
        PdfDictionary?[] parameters = ReadParameters(stream.Dictionary, filters.Length);
        byte[] current = stream.EncodedBytes;
        for (int i = 0; i < filters.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = filters[i] switch
            {
                "ASCIIHexDecode" or "AHx" => DecodeAsciiHex(current, limits, cancellationToken),
                "ASCII85Decode" or "A85" => DecodeAscii85(current, limits, cancellationToken),
                "FlateDecode" or "Fl" => DecodeFlate(current, limits, cancellationToken),
                "LZWDecode" or "LZW" => DecodeLzw(current, parameters[i], limits, cancellationToken),
                "RunLengthDecode" or "RL" => DecodeRunLength(current, limits, cancellationToken),
                _ => throw new PdfParseException("PDF_UNSUPPORTED_FILTER", stream.Span.Offset, $"Unsupported stream filter {filters[i]}.")
            };
            current = ApplyPredictor(current, parameters[i], limits, stream.Span.Offset, cancellationToken);
            CheckSize(current.Length, stream.EncodedBytes.Length, limits, stream.Span.Offset);
        }
        return current;
    }

    private static string[] ReadFilters(PdfDictionary dictionary)
    {
        if (!dictionary.TryGet("Filter", out PdfValue value)) return [];
        if (value is PdfName name) return [name.Value];
        if (value is PdfArray array)
        {
            var filters = new string[array.Values.Count];
            for (int i = 0; i < filters.Length; i++)
                filters[i] = array.Values[i] is PdfName n ? n.Value : throw new PdfParseException("PDF_INVALID_FILTER", array.Values[i].Span.Offset, "Filter array contains a non-name value.");
            return filters;
        }
        throw new PdfParseException("PDF_INVALID_FILTER", value.Span.Offset, "Filter must be a name or array.");
    }

    private static PdfDictionary?[] ReadParameters(PdfDictionary dictionary, int count)
    {
        var result = new PdfDictionary?[count];
        if (!dictionary.TryGet("DecodeParms", out PdfValue value)) return result;
        if (value is PdfDictionary item && count > 0) result[0] = item;
        else if (value is PdfArray array)
            for (int i = 0; i < Math.Min(count, array.Values.Count); i++) result[i] = array.Values[i] as PdfDictionary;
        return result;
    }

    private static byte[] DecodeAsciiHex(ReadOnlySpan<byte> input, PdfLimits limits, CancellationToken cancellationToken)
    {
        var output = new List<byte>(Math.Min(input.Length / 2 + 1, limits.MaxDecodedStreamBytes)); int? high = null;
        foreach (byte b in input)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PdfLexer.IsWhite(b)) continue;
            if (b == (byte)'>') break;
            if (!PdfLexer.TryHex(b, out int nibble)) throw new PdfParseException("PDF_INVALID_ASCIIHEX", 0, "ASCIIHex stream has an invalid digit.");
            if (high is null) high = nibble; else { output.Add((byte)((high.Value << 4) | nibble)); high = null; CheckSize(output.Count, input.Length, limits, 0); }
        }
        if (high is not null) output.Add((byte)(high.Value << 4));
        return output.ToArray();
    }

    private static byte[] DecodeAscii85(ReadOnlySpan<byte> input, PdfLimits limits, CancellationToken cancellationToken)
    {
        var output = new List<byte>(Math.Min(input.Length, limits.MaxDecodedStreamBytes)); uint tuple = 0; int count = 0;
        bool terminated = false;
        for (int index = 0; index < input.Length; index++)
        {
            byte b = input[index];
            cancellationToken.ThrowIfCancellationRequested();
            if (PdfLexer.IsWhite(b)) continue;
            if (b == (byte)'<' && count == 0 && output.Count == 0) continue;
            if (b == (byte)'~')
            {
                if (index + 1 >= input.Length || input[index + 1] != (byte)'>') throw new PdfParseException("PDF_INVALID_ASCII85", index, "ASCII85 end marker must be ~>.");
                terminated = true;
                index++;
                while (++index < input.Length) if (!PdfLexer.IsWhite(input[index])) throw new PdfParseException("PDF_INVALID_ASCII85", index, "ASCII85 data follows the terminal marker.");
                break;
            }
            if (b == (byte)'z')
            {
                if (count != 0) throw new PdfParseException("PDF_INVALID_ASCII85", 0, "ASCII85 z occurs inside a tuple.");
                Add32(output, 0, 4); CheckSize(output.Count, input.Length, limits, 0); continue;
            }
            if (b is < (byte)'!' or > (byte)'u') throw new PdfParseException("PDF_INVALID_ASCII85", 0, "ASCII85 stream has an invalid digit.");
            tuple = checked(tuple * 85 + (uint)(b - (byte)'!')); count++;
            if (count == 5) { Add32(output, tuple, 4); tuple = 0; count = 0; CheckSize(output.Count, input.Length, limits, 0); }
        }
        if (!terminated) throw new PdfParseException("PDF_TRUNCATED_ASCII85", input.Length, "ASCII85 stream has no ~> terminal marker.");
        if (count == 1) throw new PdfParseException("PDF_INVALID_ASCII85", 0, "ASCII85 final tuple has one digit.");
        if (count > 1)
        {
            for (int i = count; i < 5; i++) tuple = checked(tuple * 85 + 84);
            Add32(output, tuple, count - 1);
        }
        return output.ToArray();
    }

    private static byte[] DecodeFlate(byte[] input, PdfLimits limits, CancellationToken cancellationToken)
    {
        using var source = new MemoryStream(input, writable: false);
        using var inflater = new ZLibStream(source, CompressionMode.Decompress);
        using var output = new MemoryStream();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = inflater.Read(buffer);
                if (read == 0) break;
                CheckSize(checked((int)(output.Length + read)), input.Length, limits, 0);
                output.Write(buffer, 0, read);
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
        return output.ToArray();
    }

    private static byte[] DecodeRunLength(ReadOnlySpan<byte> input, PdfLimits limits, CancellationToken cancellationToken)
    {
        var output = new List<byte>(InitialCapacity(input.Length, limits.MaxDecodedStreamBytes)); int index = 0;
        while (index < input.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = input[index++];
            if (length == 128) break;
            if (length <= 127)
            {
                int count = length + 1;
                if (index > input.Length - count) throw new PdfParseException("PDF_TRUNCATED_RUNLENGTH", index, "RunLength literal is truncated.");
                for (int i = 0; i < count; i++) output.Add(input[index++]);
            }
            else
            {
                if (index >= input.Length) throw new PdfParseException("PDF_TRUNCATED_RUNLENGTH", index, "RunLength repeat is truncated.");
                int count = 257 - length; byte value = input[index++];
                for (int i = 0; i < count; i++) output.Add(value);
            }
            CheckSize(output.Count, input.Length, limits, 0);
        }
        return output.ToArray();
    }

    private static byte[] DecodeLzw(ReadOnlySpan<byte> input, PdfDictionary? parameters, PdfLimits limits, CancellationToken cancellationToken)
    {
        int earlyChange = parameters is not null && TryInt(parameters, "EarlyChange", out int configured) ? configured : 1;
        if (earlyChange is not (0 or 1)) throw new PdfParseException("PDF_INVALID_LZW_EARLY_CHANGE", 0, "LZW EarlyChange must be zero or one.");
        var dictionary = new byte[4096][];
        ResetDictionary(dictionary); int nextCode = 258; int codeWidth = 9; int bitOffset = 0; byte[]? previous = null; var output = new List<byte>(InitialCapacity(input.Length, limits.MaxDecodedStreamBytes));
        while (TryReadBits(input, ref bitOffset, codeWidth, out int code))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (code == 256) { ResetDictionary(dictionary); nextCode = 258; codeWidth = 9; previous = null; continue; }
            if (code == 257) return output.ToArray();
            byte[] entry;
            if (code < nextCode && dictionary[code] is not null) entry = dictionary[code];
            else if (code == nextCode && previous is not null) entry = AppendByte(previous, previous[0]);
            else throw new PdfParseException("PDF_INVALID_LZW_CODE", bitOffset, "LZW stream contains an invalid dictionary code.");
            output.AddRange(entry); CheckSize(output.Count, input.Length, limits, 0);
            if (previous is not null && nextCode < 4096)
            {
                dictionary[nextCode++] = AppendByte(previous, entry[0]);
                if (codeWidth < 12 && nextCode + earlyChange == (1 << codeWidth)) codeWidth++;
            }
            previous = entry;
        }
        throw new PdfParseException("PDF_TRUNCATED_LZW", bitOffset, "LZW stream ended before the end-of-data code.");
    }

    private static byte[] ApplyPredictor(byte[] input, PdfDictionary? parameters, PdfLimits limits, int offset, CancellationToken cancellationToken)
    {
        if (parameters is null || !TryInt(parameters, "Predictor", out int predictor) || predictor <= 1) return input;
        int colors = TryInt(parameters, "Colors", out int c) ? c : 1;
        int bits = TryInt(parameters, "BitsPerComponent", out int b) ? b : 8;
        int columns = TryInt(parameters, "Columns", out int col) ? col : 1;
        if (colors <= 0 || bits is not (1 or 2 or 4 or 8 or 16) || columns <= 0) throw new PdfParseException("PDF_INVALID_PREDICTOR", offset, "Invalid predictor parameters.");
        int rowBytes = checked((colors * columns * bits + 7) / 8);
        int bytesPerPixel = Math.Max(1, checked((colors * bits + 7) / 8));
        if (predictor == 2)
        {
            if (bits != 8 || input.Length % rowBytes != 0) throw new PdfParseException("PDF_UNSUPPORTED_PREDICTOR", offset, "TIFF predictor currently requires 8-bit aligned rows.");
            byte[] decoded = (byte[])input.Clone();
            for (int row = 0; row < decoded.Length; row += rowBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int i = bytesPerPixel; i < rowBytes; i++) decoded[row + i] = unchecked((byte)(decoded[row + i] + decoded[row + i - bytesPerPixel]));
            }
            return decoded;
        }
        if (predictor is < 10 or > 15 || input.Length % (rowBytes + 1) != 0) throw new PdfParseException("PDF_INVALID_PREDICTOR", offset, "Invalid PNG predictor rows.");
        int rows = input.Length / (rowBytes + 1); byte[] result = new byte[checked(rows * rowBytes)];
        CheckSize(result.Length, input.Length, limits, offset);
        for (int row = 0; row < rows; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int source = row * (rowBytes + 1); int target = row * rowBytes; int filter = input[source];
            if (filter > 4) throw new PdfParseException("PDF_INVALID_PREDICTOR", offset + source, "Unknown PNG predictor filter.");
            for (int x = 0; x < rowBytes; x++)
            {
                int raw = input[source + 1 + x]; int left = x >= bytesPerPixel ? result[target + x - bytesPerPixel] : 0; int up = row > 0 ? result[target - rowBytes + x] : 0; int upperLeft = row > 0 && x >= bytesPerPixel ? result[target - rowBytes + x - bytesPerPixel] : 0;
                result[target + x] = unchecked((byte)(raw + filter switch { 0 => 0, 1 => left, 2 => up, 3 => (left + up) / 2, 4 => Paeth(left, up, upperLeft), _ => 0 }));
            }
        }
        return result;
    }

    private static bool TryInt(PdfDictionary dictionary, string key, out int value)
    {
        if (dictionary.TryGet(key, out PdfValue item) && item is PdfNumber { IsInteger: true } number && number.Value is >= int.MinValue and <= int.MaxValue) { value = (int)number.Value; return true; }
        value = 0; return false;
    }

    private static int Paeth(int a, int b, int c) { int p = a + b - c; int pa = Math.Abs(p - a); int pb = Math.Abs(p - b); int pc = Math.Abs(p - c); return pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
    private static int InitialCapacity(int encodedLength, int limit) => (int)Math.Min((long)encodedLength * 2, limit);
    private static void ResetDictionary(byte[][] dictionary) { Array.Clear(dictionary); for (int i = 0; i < 256; i++) dictionary[i] = [(byte)i]; }
    private static byte[] AppendByte(byte[] source, byte value) { byte[] result = new byte[source.Length + 1]; source.CopyTo(result, 0); result[^1] = value; return result; }
    private static bool TryReadBits(ReadOnlySpan<byte> input, ref int bitOffset, int count, out int value)
    {
        if (bitOffset > input.Length * 8 - count) { value = 0; return false; }
        value = 0;
        for (int i = 0; i < count; i++) { int absolute = bitOffset++; value = (value << 1) | ((input[absolute / 8] >> (7 - absolute % 8)) & 1); }
        return true;
    }
    private static void Add32(List<byte> output, uint value, int count) { for (int shift = 24; shift >= 32 - count * 8; shift -= 8) output.Add((byte)(value >> shift)); }
    private static void CheckSize(int decoded, int encoded, PdfLimits limits, int offset)
    {
        if (decoded > limits.MaxDecodedStreamBytes || (encoded > 0 && decoded > (long)encoded * limits.MaxExpansionRatio))
            throw new PdfParseException("PDF_STREAM_LIMIT", offset, "Decoded stream exceeded its byte or expansion-ratio limit.");
    }
}
