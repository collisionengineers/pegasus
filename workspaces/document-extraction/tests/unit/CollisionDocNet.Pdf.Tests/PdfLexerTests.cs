using System.Text;

namespace CollisionDocNet.Pdf.Tests;

[TestClass]
public sealed class PdfLexerTests
{
    [TestMethod]
    public void ReadValue_AllCoreCosTypes_PreservesValuesAndSpans()
    {
        byte[] bytes = Encoding.ASCII.GetBytes(" % note\r\n[null true false -12.5 /A#20Name (a\\nb\\050c\\051) <4142F> 7 2 R]");
        int offset = 0;

        PdfArray value = Assert.IsInstanceOfType<PdfArray>(new PdfLexer(bytes).ReadValue(ref offset));

        Assert.HasCount(8, value.Values);
        Assert.IsInstanceOfType<PdfNull>(value.Values[0]);
        Assert.IsTrue(Assert.IsInstanceOfType<PdfBoolean>(value.Values[1]).Value);
        Assert.AreEqual(-12.5, Assert.IsInstanceOfType<PdfNumber>(value.Values[3]).Value);
        Assert.AreEqual("A Name", Assert.IsInstanceOfType<PdfName>(value.Values[4]).Value);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("a\nb(c)"), Assert.IsInstanceOfType<PdfString>(value.Values[5]).Bytes);
        CollectionAssert.AreEqual(new byte[] { 0x41, 0x42, 0xF0 }, Assert.IsInstanceOfType<PdfString>(value.Values[6]).Bytes);
        Assert.AreEqual(new PdfReference(7, 2, value.Values[7].Span), value.Values[7]);
        Assert.IsGreaterThan(0, value.Span.Length);
    }

    [TestMethod]
    public void ReadValue_DuplicateDictionaryKey_RecordsAndLastWins()
    {
        byte[] bytes = Encoding.ASCII.GetBytes("<< /A 1 /A 2 >>"); int offset = 0;

        PdfDictionary value = Assert.IsInstanceOfType<PdfDictionary>(new PdfLexer(bytes).ReadValue(ref offset));

        Assert.HasCount(1, value.DuplicateKeys);
        Assert.AreEqual("A", value.DuplicateKeys[0]);
        Assert.AreEqual(2d, Assert.IsInstanceOfType<PdfNumber>(value.Values["A"]).Value);
    }

    [TestMethod]
    public void ReadValue_ExcessiveNesting_ThrowsVisibleLimit()
    {
        byte[] bytes = Encoding.ASCII.GetBytes("[[[0]]]"); int offset = 0;

        PdfParseException error = Assert.ThrowsExactly<PdfParseException>(() => new PdfLexer(bytes, new PdfLimits { MaxDepth = 1 }).ReadValue(ref offset));

        Assert.AreEqual("PDF_DEPTH_LIMIT", error.Code);
    }

    [TestMethod]
    public void ReadValue_UnterminatedString_ThrowsCorruption()
    {
        byte[] bytes = Encoding.ASCII.GetBytes("(unterminated"); int offset = 0;
        PdfParseException error = Assert.ThrowsExactly<PdfParseException>(() => new PdfLexer(bytes).ReadValue(ref offset));
        Assert.AreEqual("PDF_UNTERMINATED_STRING", error.Code);
    }
}
