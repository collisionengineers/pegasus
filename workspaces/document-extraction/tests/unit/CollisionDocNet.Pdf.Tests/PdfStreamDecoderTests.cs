using System.IO.Compression;
using System.Text;

namespace CollisionDocNet.Pdf.Tests;

[TestClass]
public sealed class PdfStreamDecoderTests
{
    [TestMethod]
    [DataRow("ASCIIHexDecode", "48656C6C6F>", "Hello")]
    [DataRow("ASCII85Decode", "87cURDZ~>", "Hello")]
    public void Decode_TextFilters_ReturnExpectedBytes(string filter, string encoded, string expected)
    {
        byte[] result = PdfStreamDecoder.Decode(Stream(filter, Encoding.ASCII.GetBytes(encoded)));
        Assert.AreEqual(expected, Encoding.ASCII.GetString(result));
    }

    [TestMethod]
    public void Decode_RunLength_ExpandsLiteralAndRepeatedRuns()
    {
        byte[] result = PdfStreamDecoder.Decode(Stream("RunLengthDecode", [2, (byte)'A', (byte)'B', (byte)'C', 254, (byte)'Z', 128]));
        Assert.AreEqual("ABCZZZ", Encoding.ASCII.GetString(result));
    }

    [TestMethod]
    public void Decode_LzwClearLiteralCodesAndEod_ReturnsBytes()
    {
        byte[] encoded = PackNineBitCodes(256, 'A', 'B', 'C', 257);

        byte[] result = PdfStreamDecoder.Decode(Stream("LZWDecode", encoded));

        Assert.AreEqual("ABC", Encoding.ASCII.GetString(result));
    }

    [TestMethod]
    public void Decode_FlateAndPngSubPredictor_ReturnsDecodedRow()
    {
        byte[] predicted = [1, 10, 10, 10];
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true)) zlib.Write(predicted);
        var parms = new PdfDictionary(new Dictionary<string, PdfValue>
        {
            ["Predictor"] = Number(12),
            ["Columns"] = Number(3),
            ["Colors"] = Number(1),
            ["BitsPerComponent"] = Number(8)
        }, [], new(0, 0));
        PdfStream stream = Stream("FlateDecode", compressed.ToArray(), parms);

        byte[] result = PdfStreamDecoder.Decode(stream);

        CollectionAssert.AreEqual(new byte[] { 10, 20, 30 }, result);
    }

    [TestMethod]
    public void Decode_ExpansionLimit_ThrowsInsteadOfMaterialisingBomb()
    {
        byte[] encoded = [129, (byte)'X', 128];
        PdfParseException error = Assert.ThrowsExactly<PdfParseException>(() => PdfStreamDecoder.Decode(Stream("RunLengthDecode", encoded), new PdfLimits { MaxDecodedStreamBytes = 8, MaxExpansionRatio = 2 }));
        Assert.AreEqual("PDF_STREAM_LIMIT", error.Code);
    }

    [TestMethod]
    public void Decode_MediaFilter_IsExplicitlyUnsupported()
    {
        PdfParseException error = Assert.ThrowsExactly<PdfParseException>(() => PdfStreamDecoder.Decode(Stream("DCTDecode", [1, 2, 3])));
        Assert.AreEqual("PDF_UNSUPPORTED_FILTER", error.Code);
    }

    [TestMethod]
    [DataRow("87cURDZ", "PDF_TRUNCATED_ASCII85")]
    [DataRow("87cURDZ~x", "PDF_INVALID_ASCII85")]
    [DataRow("!~>", "PDF_INVALID_ASCII85")]
    public void Decode_Ascii85InvalidTerminalOrTuple_ThrowsStructuredError(string encoded, string expectedCode)
    {
        PdfParseException error = Assert.ThrowsExactly<PdfParseException>(() => PdfStreamDecoder.Decode(Stream("ASCII85Decode", Encoding.ASCII.GetBytes(encoded))));
        Assert.AreEqual(expectedCode, error.Code);
    }

    private static PdfNumber Number(int value) => new(value, true, value.ToString(System.Globalization.CultureInfo.InvariantCulture), new(0, 0));
    private static byte[] PackNineBitCodes(params int[] codes)
    {
        int bits = codes.Length * 9; byte[] result = new byte[(bits + 7) / 8]; int bit = 0;
        foreach (int code in codes)
            for (int shift = 8; shift >= 0; shift--) { if (((code >> shift) & 1) != 0) result[bit / 8] |= (byte)(1 << (7 - bit % 8)); bit++; }
        return result;
    }
    private static PdfStream Stream(string filter, byte[] bytes, PdfDictionary? parameters = null)
    {
        var values = new Dictionary<string, PdfValue> { ["Filter"] = new PdfName(filter, new(0, 0)), ["Length"] = Number(bytes.Length) };
        if (parameters is not null) values["DecodeParms"] = parameters;
        PdfDictionary dictionary = new(values, [], new(0, 0));
        return new(dictionary, bytes, new(0, bytes.Length));
    }
}
