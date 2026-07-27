using System.Buffers.Binary;
using System.Text;
using CollisionDocNet.Storage.CompoundFile;
using CollisionDocNet.Storage.Detection;
using CollisionDocNet.Storage.Tests.CompoundFile;
using CollisionDocNet.Storage.Tests.Zip;

namespace CollisionDocNet.Storage.Tests.Detection;

[TestClass]
public sealed class FileFormatDetectorTests
{
    [TestMethod]
    public void Detect_PdfStructure_ReturnsPdfDespiteWrongHint()
    {
        byte[] bytes = "%PDF-2.0\n1 0 obj<<>>endobj\n%%EOF\n"u8.ToArray();

        FormatDetectionResult result = FileFormatDetector.Detect(bytes, "wrong.docx", "application/msword");

        Assert.AreEqual(DetectedFormat.Pdf, result.Format);
        Assert.IsTrue(result.FilenameHintMismatch);
        Assert.IsTrue(result.MediaTypeHintMismatch);
    }

    [TestMethod]
    public void Detect_MinimalDocx_ReturnsWordprocessingMl()
    {
        FormatDetectionResult result = FileFormatDetector.Detect(
            ZipFixture.CreateMinimalDocx(), "input.docx", "application/octet-stream");

        Assert.AreEqual(DetectedFormat.WordprocessingMl, result.Format);
        Assert.IsFalse(result.FilenameHintMismatch);
    }

    [TestMethod]
    public void Detect_Rfc5322HeaderBlock_ReturnsInternetMessage()
    {
        byte[] bytes = Encoding.ASCII.GetBytes("From: a@example.test\r\nTo: b@example.test\r\nSubject: Evidence\r\n\r\nBody");

        FormatDetectionResult result = FileFormatDetector.Detect(bytes, "input.eml", "message/rfc822");

        Assert.AreEqual(DetectedFormat.InternetMessage, result.Format);
    }

    [TestMethod]
    public void Detect_CfbOutlookPropertyStream_ReturnsOutlookItem()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3);
        Span<byte> entry = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1);
        entry[..64].Clear();
        Encoding.Unicode.GetBytes("__properties_version1.0\0").CopyTo(entry);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[64..], (ushort)(("__properties_version1.0".Length + 1) * 2));
        CompoundFileFixture.GetSector(bytes, 512, 2)[..32].Clear();

        FormatDetectionResult result = FileFormatDetector.Detect(bytes, "input.msg");

        Assert.AreEqual(DetectedFormat.OutlookItem, result.Format);
    }

    [TestMethod]
    public void Detect_CfbOutlookPropertyStreamWithInvalidHeader_DoesNotReturnOutlookItem()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3, fill: 0);
        Span<byte> entry = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1);
        WriteDirectoryName(entry, "__properties_version1.0");
        CompoundFileFixture.GetSector(bytes, 512, 2)[0] = 1;

        FormatDetectionResult result = FileFormatDetector.Detect(bytes, "input.msg");

        Assert.AreEqual(DetectedFormat.Unknown, result.Format);
    }

    [TestMethod]
    public void Detect_CfbOutlookPropertyStreamWithUnmatchedRecipientCount_DoesNotReturnOutlookItem()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3, fill: 0);
        Span<byte> entry = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1);
        WriteDirectoryName(entry, "__properties_version1.0");
        BinaryPrimitives.WriteUInt32LittleEndian(
            CompoundFileFixture.GetSector(bytes, 512, 2)[16..], 1);

        FormatDetectionResult result = FileFormatDetector.Detect(bytes, "input.msg");

        Assert.AreEqual(DetectedFormat.Unknown, result.Format);
    }

    [TestMethod]
    public void Detect_RootWordStreamsWithFib_ReturnsWordBinary()
    {
        byte[] bytes = CreateWordCompound(validFib: true);

        FormatDetectionResult result = FileFormatDetector.Detect(bytes, "input.doc");

        Assert.AreEqual(DetectedFormat.WordBinary, result.Format);
    }

    [TestMethod]
    public void Detect_InvalidCompoundDirectory_PreservesStructuralFailureWithoutHintMismatch()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3);
        CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1)[67] =
            (byte)CompoundFileNodeColor.Red;

        FormatDetectionResult result = FileFormatDetector.Detect(bytes, "input.doc");

        Assert.AreEqual(DetectedFormat.Unknown, result.Format);
        Assert.AreEqual("cfb-invalid-directory-tree", result.DiagnosticCode);
        Assert.AreEqual((uint)1, result.DiagnosticLocation);
        Assert.IsFalse(result.FilenameHintMismatch);
    }

    [TestMethod]
    public void Detect_WordNamedStreamsWithoutFib_DoesNotReturnWordBinary()
    {
        byte[] bytes = CreateWordCompound(validFib: false);

        FormatDetectionResult result = FileFormatDetector.Detect(bytes, "input.doc");

        Assert.AreEqual(DetectedFormat.Unknown, result.Format);
    }

    [TestMethod]
    public void Detect_NestedOutlookPropertyStream_DoesNotReturnOutlookItem()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3);
        Span<byte> nestedStorage = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1);
        WriteDirectoryName(nestedStorage, "Nested");
        nestedStorage[66] = (byte)CompoundFileObjectType.Storage;
        BinaryPrimitives.WriteUInt32LittleEndian(nestedStorage[76..], 2);
        BinaryPrimitives.WriteUInt32LittleEndian(nestedStorage[116..], CompoundFileConstants.EndOfChain);
        BinaryPrimitives.WriteUInt64LittleEndian(nestedStorage[120..], 0);
        Span<byte> property = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 2);
        WriteDirectoryName(property, "__properties_version1.0");
        property[66] = (byte)CompoundFileObjectType.Stream;
        property[67] = (byte)CompoundFileNodeColor.Black;
        BinaryPrimitives.WriteUInt32LittleEndian(property[68..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(property[72..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(property[76..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(property[116..], 2);
        BinaryPrimitives.WriteUInt64LittleEndian(property[120..], 4096);

        FormatDetectionResult result = FileFormatDetector.Detect(bytes, "input.msg");

        Assert.AreEqual(DetectedFormat.Unknown, result.Format);
    }

    [TestMethod]
    public void Detect_NestedWordProfile_DoesNotReturnWordBinary()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3, fill: 0);
        Span<byte> storage = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1);
        WriteDirectoryName(storage, "Nested");
        storage[66] = (byte)CompoundFileObjectType.Storage;
        BinaryPrimitives.WriteUInt32LittleEndian(storage[76..], 2);
        BinaryPrimitives.WriteUInt32LittleEndian(storage[116..], CompoundFileConstants.EndOfChain);
        BinaryPrimitives.WriteUInt64LittleEndian(storage[120..], 0);
        Span<byte> word = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 2);
        WriteDirectoryName(word, "WordDocument");
        word[66] = (byte)CompoundFileObjectType.Stream;
        word[67] = (byte)CompoundFileNodeColor.Black;
        BinaryPrimitives.WriteUInt32LittleEndian(word[68..], 3);
        BinaryPrimitives.WriteUInt32LittleEndian(word[72..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(word[76..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(word[116..], 2);
        BinaryPrimitives.WriteUInt64LittleEndian(word[120..], 4096);
        Span<byte> table = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 3);
        WriteDirectoryName(table, "0Table");
        table[66] = (byte)CompoundFileObjectType.Stream;
        table[67] = (byte)CompoundFileNodeColor.Red;
        BinaryPrimitives.WriteUInt32LittleEndian(table[68..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(table[72..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(table[76..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(table[116..], CompoundFileConstants.EndOfChain);
        Span<byte> content = CompoundFileFixture.GetSector(bytes, 512, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(content, 0xA5EC);
        BinaryPrimitives.WriteUInt16LittleEndian(content[2..], 0x00C1);

        FormatDetectionResult result = FileFormatDetector.Detect(bytes, "input.doc");

        Assert.AreEqual(DetectedFormat.Unknown, result.Format);
    }

    [TestMethod]
    public void Detect_WeakTextWithoutMessageHeaders_ReturnsUnsupported()
    {
        FormatDetectionResult result = FileFormatDetector.Detect("hello: world\n\ntext"u8.ToArray());

        Assert.AreEqual(DetectedFormat.Unknown, result.Format);
        Assert.AreEqual("unsupported-format", result.DiagnosticCode);
    }

    [TestMethod]
    public void Detect_PdfEmbeddedAfterEmailHeaders_ReturnsDeterministicAmbiguity()
    {
        byte[] bytes = Encoding.ASCII.GetBytes(
            "From: a@example.test\r\nTo: b@example.test\r\n\r\n%PDF-2.0\n%%EOF\n");

        FormatDetectionResult result = FileFormatDetector.Detect(bytes);

        Assert.IsTrue(result.IsAmbiguous);
        Assert.HasCount(2, result.Candidates);
        Assert.AreEqual("ambiguous-polyglot", result.DiagnosticCode);
    }

    [TestMethod]
    public void Detect_EncryptedOpenXmlStreams_ReturnsEncryptedWrapper()
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3);
        Span<byte> first = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1);
        WriteDirectoryName(first, "EncryptionInfo");
        BinaryPrimitives.WriteUInt32LittleEndian(first[72..], 2);
        Span<byte> second = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 2);
        WriteDirectoryName(second, "EncryptedPackage");
        second[66] = (byte)CompoundFileObjectType.Stream;
        second[67] = (byte)CompoundFileNodeColor.Red;
        BinaryPrimitives.WriteUInt32LittleEndian(second[68..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(second[72..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(second[76..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(second[116..], CompoundFileConstants.EndOfChain);

        FormatDetectionResult result = FileFormatDetector.Detect(bytes, "input.docx");

        Assert.AreEqual(DetectedFormat.EncryptedOpenXml, result.Format);
        Assert.IsTrue(result.FilenameHintMismatch);
    }

    [TestMethod]
    public void Detect_Cancelled_ReturnsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        FormatDetectionResult result = FileFormatDetector.Detect(
            ZipFixture.CreateMinimalDocx(), cancellationToken: cancellation.Token);

        Assert.AreEqual("cancelled", result.DiagnosticCode);
    }

    private static void WriteDirectoryName(Span<byte> entry, string name)
    {
        entry[..64].Clear();
        Encoding.Unicode.GetBytes(name + "\0").CopyTo(entry);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[64..], (ushort)((name.Length + 1) * 2));
    }

    private static byte[] CreateWordCompound(bool validFib)
    {
        byte[] bytes = CompoundFileFixture.CreateWithRegularStream(3, fill: 0);
        Span<byte> word = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 1);
        WriteDirectoryName(word, "WordDocument");
        BinaryPrimitives.WriteUInt32LittleEndian(word[68..], 2);
        Span<byte> table = CompoundFileFixture.GetDirectoryEntry(bytes, 512, 2);
        WriteDirectoryName(table, "0Table");
        table[66] = (byte)CompoundFileObjectType.Stream;
        table[67] = (byte)CompoundFileNodeColor.Red;
        BinaryPrimitives.WriteUInt32LittleEndian(table[68..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(table[72..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(table[76..], CompoundFileConstants.NoStream);
        BinaryPrimitives.WriteUInt32LittleEndian(table[116..], CompoundFileConstants.EndOfChain);
        Span<byte> content = CompoundFileFixture.GetSector(bytes, 512, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(content, validFib ? (ushort)0xA5EC : (ushort)0x0000);
        BinaryPrimitives.WriteUInt16LittleEndian(content[2..], 0x00C1);
        return bytes;
    }
}
