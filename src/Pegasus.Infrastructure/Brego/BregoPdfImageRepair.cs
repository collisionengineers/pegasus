using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Pegasus.Infrastructure.Brego;

/// <summary>
/// Detects Chromium's 14×16 broken-image placeholder XObject in a Brego
/// Skia/PDF report (produced when a vehicle's historical dealer advert photos
/// return HTTP 404 during Brego's render) and replaces Page 1 and Page 2's
/// broken placeholders in place with Brego's <c>view=front</c> and
/// <c>view=side</c> studio stock images from <c>assets.brego.io</c> (ADR-0084).
/// </summary>
internal static partial class BregoPdfImageRepair
{
    private static ReadOnlySpan<byte> Width14Marker => "/Width 14"u8;
    private static ReadOnlySpan<byte> Height16Marker => "/Height 16"u8;

    public static async Task<byte[]> RepairIfBrokenAsync(
        byte[] pdfBytes,
        BregoVehicle vehicle,
        BregoClient client,
        CancellationToken cancellationToken)
    {
        if (!ContainsBrokenImageMarker(pdfBytes))
        {
            return pdfBytes;
        }

        try
        {
            var parsed = ParsePdf(pdfBytes);
            if (parsed is null || parsed.BrokenImageIds.Count == 0 || parsed.Page1 is null)
            {
                return pdfBytes;
            }

            var frontTask = client.TryFetchStockImageAsync(vehicle, "front", cancellationToken);
            var sideTask = client.TryFetchStockImageAsync(vehicle, "side", cancellationToken);
            await Task.WhenAll(frontTask, sideTask);

            var frontBytes = await frontTask;
            var sideBytes = await sideTask;
            if (frontBytes is null || sideBytes is null)
            {
                return pdfBytes;
            }

            if (!TryEncodeStockImage(frontBytes, out var frontImage)
                || !TryEncodeStockImage(sideBytes, out var sideImage))
            {
                return pdfBytes;
            }

            return RebuildPdf(pdfBytes, parsed, frontImage, sideImage);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return pdfBytes;
        }
    }

    private static bool ContainsBrokenImageMarker(ReadOnlySpan<byte> pdfBytes) =>
        pdfBytes.IndexOf(Width14Marker) >= 0 && pdfBytes.IndexOf(Height16Marker) >= 0;

    private static ParsedPdf? ParsePdf(byte[] pdfBytes)
    {
        var text = Encoding.Latin1.GetString(pdfBytes);
        var xrefIndex = text.LastIndexOf("\nxref", StringComparison.Ordinal);
        if (xrefIndex < 0)
        {
            xrefIndex = text.LastIndexOf("xref", StringComparison.Ordinal);
            if (xrefIndex < 0)
            {
                return null;
            }
        }

        var trailerMatch = TrailerRegex().Match(text, xrefIndex);
        if (!trailerMatch.Success)
        {
            return null;
        }

        var trailerDict = trailerMatch.Groups[1].Value;
        var sizeMatch = SizeRegex().Match(trailerDict);
        var rootMatch = RootRegex().Match(trailerDict);
        if (!sizeMatch.Success
            || !int.TryParse(sizeMatch.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var oldSize)
            || !rootMatch.Success)
        {
            return null;
        }

        var rootRef = rootMatch.Groups[1].Value;
        var infoMatch = InfoRegex().Match(trailerDict);
        var infoRef = infoMatch.Success ? infoMatch.Groups[1].Value : null;

        var matches = ObjectHeaderRegex().Matches(text, 0);
        if (matches.Count == 0)
        {
            return null;
        }

        var objects = new List<PdfObjectSpan>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            if (match.Index >= xrefIndex)
            {
                break;
            }

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                continue;
            }

            var start = match.Index;
            var end = i + 1 < matches.Count && matches[i + 1].Index < xrefIndex
                ? matches[i + 1].Index
                : xrefIndex;
            // Trim trailing line breaks before xref if at the end of the object section.
            while (end > start && (pdfBytes[end - 1] == (byte)'\n' || pdfBytes[end - 1] == (byte)'\r'))
            {
                end--;
            }

            if (end < xrefIndex && pdfBytes[end] == (byte)'\r')
            {
                end++;
            }

            if (end < xrefIndex && pdfBytes[end] == (byte)'\n')
            {
                end++;
            }

            objects.Add(new PdfObjectSpan(id, start, end));
        }

        if (objects.Count == 0)
        {
            return null;
        }

        var brokenImageIds = new List<int>();
        PdfPageInfo? page1 = null;
        PdfPageInfo? page2 = null;

        foreach (var obj in objects)
        {
            var objText = text.AsSpan(obj.Start, obj.End - obj.Start);
            var streamPos = objText.IndexOf("stream", StringComparison.Ordinal);
            var headerSpan = streamPos >= 0 ? objText[..streamPos] : objText;
            var header = headerSpan.ToString();

            if (header.Contains("/Subtype /Image", StringComparison.Ordinal)
                && ImageWidth14Regex().IsMatch(header)
                && ImageHeight16Regex().IsMatch(header))
            {
                brokenImageIds.Add(obj.Id);
            }
            else if (PageTypeRegex().IsMatch(header))
            {
                var contentsMatch = ContentsRefRegex().Match(header);
                if (contentsMatch.Success
                    && int.TryParse(contentsMatch.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var contentsId))
                {
                    if (page1 is null)
                    {
                        page1 = new PdfPageInfo(obj.Id, contentsId, header);
                    }
                    else if (page2 is null)
                    {
                        page2 = new PdfPageInfo(obj.Id, contentsId, header);
                    }
                }
            }
        }

        return new ParsedPdf(oldSize, rootRef, infoRef, objects, brokenImageIds, page1, page2);
    }

    private static byte[] RebuildPdf(
        byte[] pdfBytes,
        ParsedPdf parsed,
        EncodedStockImage frontImage,
        EncodedStockImage sideImage)
    {
        var objectsById = new Dictionary<int, PdfObjectSpan>(parsed.Objects.Count);
        var maxObjectId = parsed.OldSize - 1;
        foreach (var obj in parsed.Objects)
        {
            objectsById[obj.Id] = obj;
            if (obj.Id > maxObjectId)
            {
                maxObjectId = obj.Id;
            }
        }

        var nextId = maxObjectId + 1;
        var frontMaskId = nextId++;
        var sideMaskId = nextId++;
        var sideImgId = nextId++;
        var newSize = nextId;

        var replacements = new Dictionary<int, byte[]>();
        var frontImageObjectBytes = BuildRgbImageObject(frontImage, frontMaskId);
        foreach (var brokenId in parsed.BrokenImageIds)
        {
            replacements[brokenId] = BuildIndirectObject(brokenId, frontImageObjectBytes);
        }

        if (parsed.Page1 is { } page1 && objectsById.TryGetValue(page1.ContentsObjectId, out var c1Span))
        {
            var c1Stream = DecompressObjectStream(pdfBytes.AsSpan(c1Span.Start, c1Span.End - c1Span.Start));
            if (c1Stream is not null)
            {
                var updatedC1 = RewritePage1Stream(c1Stream, parsed.BrokenImageIds);
                replacements[page1.ContentsObjectId] = BuildCompressedStreamObject(page1.ContentsObjectId, updatedC1);
            }
        }

        if (parsed.Page2 is { } page2 && objectsById.TryGetValue(page2.ContentsObjectId, out var c2Span))
        {
            var c2Stream = DecompressObjectStream(pdfBytes.AsSpan(c2Span.Start, c2Span.End - c2Span.Start));
            if (c2Stream is not null)
            {
                var updatedC2 = RewritePage2Stream(c2Stream, parsed.BrokenImageIds);
                replacements[page2.ContentsObjectId] = BuildCompressedStreamObject(page2.ContentsObjectId, updatedC2);
                var updatedP2Header = InjectPageXObject(page2.RawObjectText, "XSide", sideImgId);
                replacements[page2.PageObjectId] = Encoding.Latin1.GetBytes(updatedP2Header);
            }
        }

        var appendedObjects = new (int Id, byte[] Bytes)[]
        {
            (frontMaskId, BuildIndirectObject(frontMaskId, BuildMaskImageObject(frontImage))),
            (sideMaskId, BuildIndirectObject(sideMaskId, BuildMaskImageObject(sideImage))),
            (sideImgId, BuildIndirectObject(sideImgId, BuildRgbImageObject(sideImage, sideMaskId))),
        };

        var offsets = new long[newSize];
        using var output = new MemoryStream(pdfBytes.Length + 262_144);

        // Write header bytes before the first object.
        var firstObjectStart = parsed.Objects[0].Start;
        output.Write(pdfBytes, 0, firstObjectStart);

        foreach (var obj in parsed.Objects)
        {
            offsets[obj.Id] = output.Position;
            if (replacements.TryGetValue(obj.Id, out var replacement))
            {
                output.Write(replacement, 0, replacement.Length);
            }
            else
            {
                output.Write(pdfBytes, obj.Start, obj.End - obj.Start);
                if (pdfBytes[obj.End - 1] != (byte)'\n')
                {
                    output.WriteByte((byte)'\n');
                }
            }
        }

        foreach (var (id, bytes) in appendedObjects)
        {
            offsets[id] = output.Position;
            output.Write(bytes, 0, bytes.Length);
        }

        var xrefPosition = output.Position;
        var xrefBuilder = new StringBuilder((newSize * 20) + 128);
        xrefBuilder.Append(CultureInfo.InvariantCulture, $"xref\n0 {newSize}\n0000000000 65535 f \n");
        for (var id = 1; id < newSize; id++)
        {
            if (offsets[id] > 0)
            {
                xrefBuilder.Append(CultureInfo.InvariantCulture, $"{offsets[id]:D10} 00000 n \n");
            }
            else
            {
                xrefBuilder.Append("0000000000 65535 f \n");
            }
        }

        xrefBuilder.Append(CultureInfo.InvariantCulture, $"trailer\n<</Size {newSize}\n/Root {parsed.RootRef}");
        if (!string.IsNullOrWhiteSpace(parsed.InfoRef))
        {
            xrefBuilder.Append(CultureInfo.InvariantCulture, $"\n/Info {parsed.InfoRef}");
        }

        xrefBuilder.Append(CultureInfo.InvariantCulture, $">>\nstartxref\n{xrefPosition}\n%%EOF\n");
        var trailerBytes = Encoding.Latin1.GetBytes(xrefBuilder.ToString());
        output.Write(trailerBytes, 0, trailerBytes.Length);
        return output.ToArray();
    }

    private static string RewritePage1Stream(string content, List<int> brokenIds)
    {
        var updated = content;
        foreach (var bx in brokenIds)
        {
            var pattern = $@"[\d\.]+\s+0\s+0\s+-[\d\.]+\s+[\d\.]+\s+[\d\.]+\s+cm(\s+0 0 0 RG 0 0 0 rg\s+/G\d+ gs(?:\s+EMC)?\s+/X{bx} Do)";
            updated = Regex.Replace(
                updated,
                pattern,
                "2081.25 0 0 -1559.375 200 2021.875 cm$1",
                RegexOptions.CultureInvariant);
        }

        return updated;
    }

    private static string RewritePage2Stream(string content, List<int> brokenIds)
    {
        var updated = content;
        foreach (var bx in brokenIds)
        {
            var pattern = $@"[\d\.]+\s+0\s+0\s+-[\d\.]+\s+[\d\.]+\s+[\d\.]+\s+cm(\s+0 0 0 RG 0 0 0 rg\s+/G\d+ gs(?:\s+EMC)?\s+)/X{bx} Do";
            updated = Regex.Replace(
                updated,
                pattern,
                "903.12506 0 0 -562.5 168.75 1371.875 cm$1/XSide Do",
                RegexOptions.CultureInvariant);
        }

        return updated;
    }

    private static string InjectPageXObject(string pageObjectText, string name, int targetObjectId)
    {
        const string marker = "/XObject <<";
        var index = pageObjectText.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return pageObjectText;
        }

        var insertPos = index + marker.Length;
        var updated = pageObjectText.Insert(
            insertPos,
            string.Create(CultureInfo.InvariantCulture, $"/{name} {targetObjectId} 0 R\n"));
        return updated.EndsWith('\n') ? updated : updated + "\n";
    }

    private static string? DecompressObjectStream(ReadOnlySpan<byte> objectBytes)
    {
        var streamStart = IndexOfStreamData(objectBytes);
        var streamEnd = LastIndexOfEndstream(objectBytes);
        if (streamStart < 0 || streamEnd <= streamStart)
        {
            return null;
        }

        var compressed = objectBytes[streamStart..streamEnd].ToArray();
        using var input = new MemoryStream(compressed, writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return Encoding.Latin1.GetString(output.GetBuffer(), 0, (int)output.Length);
    }

    private static int IndexOfStreamData(ReadOnlySpan<byte> objectBytes)
    {
        var idx = objectBytes.IndexOf("stream"u8);
        while (idx >= 0)
        {
            var after = idx + 6;
            if (after < objectBytes.Length && objectBytes[after] == (byte)'\n')
            {
                return after + 1;
            }

            if (after + 1 < objectBytes.Length && objectBytes[after] == (byte)'\r' && objectBytes[after + 1] == (byte)'\n')
            {
                return after + 2;
            }

            var next = objectBytes[after..].IndexOf("stream"u8);
            idx = next >= 0 ? after + next : -1;
        }

        return -1;
    }

    private static int LastIndexOfEndstream(ReadOnlySpan<byte> objectBytes)
    {
        var idx = objectBytes.LastIndexOf("endstream"u8);
        if (idx <= 0)
        {
            return -1;
        }

        if (objectBytes[idx - 1] == (byte)'\n')
        {
            idx--;
            if (idx > 0 && objectBytes[idx - 1] == (byte)'\r')
            {
                idx--;
            }
        }

        return idx;
    }

    private static bool TryEncodeStockImage(byte[] imageBytes, out EncodedStockImage encoded)
    {
        encoded = default;
        using var stream = new SKMemoryStream(imageBytes);
        using var codec = SKCodec.Create(stream);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
        {
            return false;
        }

        var width = codec.Info.Width;
        var height = codec.Info.Height;
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = SKBitmap.Decode(codec, info);
        if (bitmap is null)
        {
            return false;
        }

        var pixels = bitmap.GetPixelSpan();
        var pixelCount = width * height;
        if (pixels.Length < pixelCount * 4)
        {
            return false;
        }

        var rgb = new byte[pixelCount * 3];
        var alpha = new byte[pixelCount];
        for (int i = 0, p = 0, r = 0; i < pixelCount; i++, p += 4, r += 3)
        {
            rgb[r] = pixels[p];
            rgb[r + 1] = pixels[p + 1];
            rgb[r + 2] = pixels[p + 2];
            alpha[i] = pixels[p + 3];
        }

        encoded = new EncodedStockImage(width, height, CompressZlib(rgb), CompressZlib(alpha));
        return true;
    }

    private static byte[] CompressZlib(ReadOnlySpan<byte> raw)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        return output.ToArray();
    }

    private static byte[] BuildCompressedStreamObject(int objectId, string content)
    {
        var compressed = CompressZlib(Encoding.Latin1.GetBytes(content));
        var header = Encoding.Latin1.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"<</Filter /FlateDecode\n/Length {compressed.Length}>> stream\n"));
        var footer = "\nendstream"u8;
        var body = new byte[header.Length + compressed.Length + footer.Length];
        Buffer.BlockCopy(header, 0, body, 0, header.Length);
        Buffer.BlockCopy(compressed, 0, body, header.Length, compressed.Length);
        footer.CopyTo(body.AsSpan(header.Length + compressed.Length));
        return BuildIndirectObject(objectId, body);
    }

    private static byte[] BuildRgbImageObject(EncodedStockImage image, int maskObjectId)
    {
        var header = Encoding.Latin1.GetBytes(
            string.Create(
                CultureInfo.InvariantCulture,
                $"<</Type /XObject\n/Subtype /Image\n/Width {image.Width}\n/Height {image.Height}\n/ColorSpace /DeviceRGB\n/SMask {maskObjectId} 0 R\n/BitsPerComponent 8\n/Filter /FlateDecode\n/Length {image.RgbCompressed.Length}>> stream\n"));
        var footer = "\nendstream"u8;
        var body = new byte[header.Length + image.RgbCompressed.Length + footer.Length];
        Buffer.BlockCopy(header, 0, body, 0, header.Length);
        Buffer.BlockCopy(image.RgbCompressed, 0, body, header.Length, image.RgbCompressed.Length);
        footer.CopyTo(body.AsSpan(header.Length + image.RgbCompressed.Length));
        return body;
    }

    private static byte[] BuildMaskImageObject(EncodedStockImage image)
    {
        var header = Encoding.Latin1.GetBytes(
            string.Create(
                CultureInfo.InvariantCulture,
                $"<</Type /XObject\n/Subtype /Image\n/Width {image.Width}\n/Height {image.Height}\n/ColorSpace /DeviceGray\n/BitsPerComponent 8\n/Filter /FlateDecode\n/Length {image.AlphaCompressed.Length}>> stream\n"));
        var footer = "\nendstream"u8;
        var body = new byte[header.Length + image.AlphaCompressed.Length + footer.Length];
        Buffer.BlockCopy(header, 0, body, 0, header.Length);
        Buffer.BlockCopy(image.AlphaCompressed, 0, body, header.Length, image.AlphaCompressed.Length);
        footer.CopyTo(body.AsSpan(header.Length + image.AlphaCompressed.Length));
        return body;
    }

    private static byte[] BuildIndirectObject(int objectId, byte[] body)
    {
        var prefix = Encoding.Latin1.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{objectId} 0 obj\n"));
        var suffix = "\nendobj\n"u8;
        var total = new byte[prefix.Length + body.Length + suffix.Length];
        Buffer.BlockCopy(prefix, 0, total, 0, prefix.Length);
        Buffer.BlockCopy(body, 0, total, prefix.Length, body.Length);
        suffix.CopyTo(total.AsSpan(prefix.Length + body.Length));
        return total;
    }

    [GeneratedRegex(@"(?m)^(\d+)\s+0\s+obj\b", RegexOptions.CultureInvariant)]
    private static partial Regex ObjectHeaderRegex();

    [GeneratedRegex(@"trailer\s*<<(.*?)>>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex TrailerRegex();

    [GeneratedRegex(@"/Size\s+(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex SizeRegex();

    [GeneratedRegex(@"/Root\s+(\d+\s+0\s+R)", RegexOptions.CultureInvariant)]
    private static partial Regex RootRegex();

    [GeneratedRegex(@"/Info\s+(\d+\s+0\s+R)", RegexOptions.CultureInvariant)]
    private static partial Regex InfoRegex();

    [GeneratedRegex(@"/Width\s+14\b", RegexOptions.CultureInvariant)]
    private static partial Regex ImageWidth14Regex();

    [GeneratedRegex(@"/Height\s+16\b", RegexOptions.CultureInvariant)]
    private static partial Regex ImageHeight16Regex();

    [GeneratedRegex(@"/Type\s*/Page\b(?!s)", RegexOptions.CultureInvariant)]
    private static partial Regex PageTypeRegex();

    [GeneratedRegex(@"/Contents\s+(\d+)\s+0\s+R", RegexOptions.CultureInvariant)]
    private static partial Regex ContentsRefRegex();

    private readonly record struct PdfObjectSpan(int Id, int Start, int End);

    private sealed record PdfPageInfo(int PageObjectId, int ContentsObjectId, string RawObjectText);

    private sealed record ParsedPdf(
        int OldSize,
        string RootRef,
        string? InfoRef,
        List<PdfObjectSpan> Objects,
        List<int> BrokenImageIds,
        PdfPageInfo? Page1,
        PdfPageInfo? Page2);

    private readonly record struct EncodedStockImage(
        int Width,
        int Height,
        byte[] RgbCompressed,
        byte[] AlphaCompressed);
}
