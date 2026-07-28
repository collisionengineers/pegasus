using System.Text;
using CollisionDocNet.Storage.Xml;

namespace CollisionDocNet.Storage.Tests.Xml;

[TestClass]
public sealed class BoundedXmlReaderTests
{
    [TestMethod]
    public void Read_NamespacedDocument_PreservesNamesAndPositions()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("<r xmlns='urn:test'><c a='v'>text</c></r>");

        BoundedXmlReadResult result = BoundedXmlReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        BoundedXmlNode child = Assert.ContainsSingle(result.Document!.Nodes.Where(static node => node.LocalName == "c" && node.Kind == BoundedXmlNodeKind.ElementStart));
        Assert.AreEqual("urn:test", child.NamespaceUri);
        Assert.AreEqual("v", Assert.ContainsSingle(child.Attributes).Value);
        Assert.IsGreaterThan(0, child.Source.LineNumber);
    }

    [TestMethod]
    public void Read_Dtd_ReturnsDtdProhibited()
    {
        byte[] bytes = "<!DOCTYPE r [<!ENTITY x SYSTEM 'file:///secret'>]><r>&x;</r>"u8.ToArray();

        BoundedXmlReadResult result = BoundedXmlReader.Read(bytes);

        Assert.AreEqual(BoundedXmlReadError.DtdProhibited, result.Error);
    }

    [TestMethod]
    public void Read_Utf16Dtd_ReturnsDtdProhibitedWithoutMessageMatching()
    {
        byte[] bytes = Encoding.Unicode.GetBytes("<!DOCTYPE r><r/>");

        BoundedXmlReadResult result = BoundedXmlReader.Read(bytes);

        Assert.AreEqual(BoundedXmlReadError.DtdProhibited, result.Error);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Read_Utf32Dtd_ReturnsDtdProhibited(bool littleEndian)
    {
        byte[] bytes = new UTF32Encoding(littleEndian, byteOrderMark: true, throwOnInvalidCharacters: true)
            .GetBytes("<!DOCTYPE r><r/>");

        BoundedXmlReadResult result = BoundedXmlReader.Read(bytes);

        Assert.AreEqual(BoundedXmlReadError.DtdProhibited, result.Error);
    }

    [TestMethod]
    public void Read_InputExceedsLimit_ReturnsInputLimitExceeded()
    {
        byte[] bytes = "<root/>"u8.ToArray();

        BoundedXmlReadResult result = BoundedXmlReader.Read(
            bytes, BoundedXmlLimits.Default with { MaximumInputBytes = bytes.Length - 1 });

        Assert.AreEqual(BoundedXmlReadError.InputLimitExceeded, result.Error);
    }

    [TestMethod]
    public void Read_TextExceedsLimit_ReturnsTextLimitExceeded()
    {
        BoundedXmlReadResult result = BoundedXmlReader.Read(
            "<root>abcd</root>"u8.ToArray(),
            BoundedXmlLimits.Default with { MaximumTextCharacters = 3 });

        Assert.AreEqual(BoundedXmlReadError.TextLimitExceeded, result.Error);
    }

    [TestMethod]
    public void Read_DepthExceedsLimit_ReturnsDepthLimitExceeded()
    {
        var limits = BoundedXmlLimits.Default with { MaximumDepth = 1 };

        BoundedXmlReadResult result = BoundedXmlReader.Read("<a><b><c/></b></a>"u8.ToArray(), limits);

        Assert.AreEqual(BoundedXmlReadError.DepthLimitExceeded, result.Error);
    }

    [TestMethod]
    public void Read_NodeCountExceedsLimit_ReturnsNodeLimitExceeded()
    {
        var limits = BoundedXmlLimits.Default with { MaximumNodes = 2 };

        BoundedXmlReadResult result = BoundedXmlReader.Read("<a><b/></a>"u8.ToArray(), limits);

        Assert.AreEqual(BoundedXmlReadError.NodeLimitExceeded, result.Error);
    }

    [TestMethod]
    public void Read_AttributeCountExceedsLimit_ReturnsAttributeLimitExceeded()
    {
        var limits = BoundedXmlLimits.Default with { MaximumAttributesPerElement = 1 };

        BoundedXmlReadResult result = BoundedXmlReader.Read("<a x='1' y='2'/>"u8.ToArray(), limits);

        Assert.AreEqual(BoundedXmlReadError.AttributeLimitExceeded, result.Error);
    }

    [TestMethod]
    public void Read_Cancelled_ReturnsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        BoundedXmlReadResult result = BoundedXmlReader.Read("<a/>"u8.ToArray(), cancellationToken: cancellation.Token);

        Assert.AreEqual(BoundedXmlReadError.Cancelled, result.Error);
    }
}
