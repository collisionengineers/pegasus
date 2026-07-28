using System.Buffers.Binary;
using System.Text;
using CollisionDocNet.Storage.Ole;

namespace CollisionDocNet.Storage.Tests.Ole;

[TestClass]
public sealed class OlePropertySetReaderTests
{
    [TestMethod]
    public void Read_CodePageAndUnicodeTitle_ReturnsTypedAndRawValues()
    {
        byte[] bytes = CreatePropertySet();

        OlePropertySetReadResult result = OlePropertySetReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        OlePropertySection section = Assert.ContainsSingle(result.PropertySet!.Sections);
        Assert.AreEqual(1252, section.CodePage);
        Assert.HasCount(2, section.Properties);
        OleProperty title = Assert.ContainsSingle(section.Properties.Where(static property => property.PropertyId == 2));
        Assert.AreEqual(OlePropertyValueKind.Text, title.Kind);
        Assert.AreEqual("Title", title.Value);
        Assert.IsNotEmpty(title.RawValue);
    }

    [TestMethod]
    public void Read_PropertyCountExceedsLimit_ReturnsPropertyLimitExceeded()
    {
        byte[] bytes = CreatePropertySet();
        var limits = OlePropertySetLimits.Default with { MaximumPropertiesPerSection = 1 };

        OlePropertySetReadResult result = OlePropertySetReader.Read(bytes, limits);

        Assert.AreEqual(OlePropertySetReadError.PropertyLimitExceeded, result.Error);
    }

    [TestMethod]
    public void Read_TruncatedValue_ReturnsInvalidSectionOrUnsupportedValue()
    {
        byte[] bytes = CreatePropertySet()[..^5];

        OlePropertySetReadResult result = OlePropertySetReader.Read(bytes);

        Assert.AreEqual(OlePropertySetReadError.InvalidSection, result.Error);
    }

    [TestMethod]
    public void Read_DuplicatePropertyIdentifier_ReturnsInvalidSection()
    {
        byte[] bytes = CreatePropertySet();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(48 + 16), 1);

        OlePropertySetReadResult result = OlePropertySetReader.Read(bytes);

        Assert.AreEqual(OlePropertySetReadError.InvalidSection, result.Error);
    }

    [TestMethod]
    public void Read_MisalignedPropertyOffset_ReturnsInvalidSection()
    {
        byte[] bytes = CreatePropertySet();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(48 + 12), 25);

        OlePropertySetReadResult result = OlePropertySetReader.Read(bytes);

        Assert.AreEqual(OlePropertySetReadError.InvalidSection, result.Error);
    }

    [TestMethod]
    public void Read_SectionOverlapsDescriptorTable_ReturnsInvalidSection()
    {
        byte[] bytes = CreatePropertySet();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), 44);

        OlePropertySetReadResult result = OlePropertySetReader.Read(bytes);

        Assert.AreEqual(OlePropertySetReadError.InvalidSection, result.Error);
    }

    [TestMethod]
    public void Read_SectionSizeIsNotFourByteAligned_ReturnsInvalidSection()
    {
        byte[] bytes = CreatePropertySet();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(48), 51);

        OlePropertySetReadResult result = OlePropertySetReader.Read(bytes);

        Assert.AreEqual(OlePropertySetReadError.InvalidSection, result.Error);
    }

    [TestMethod]
    public void Read_NonzeroVariantReservedBytes_PreservesAsUnsupported()
    {
        byte[] bytes = CreatePropertySet();
        bytes[48 + 32 + 2] = 1;

        OlePropertySetReadResult result = OlePropertySetReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        OleProperty value = Assert.ContainsSingle(result.PropertySet!.Sections[0].Properties
            .Where(static property => property.PropertyId == 2));
        Assert.AreEqual(OlePropertyValueKind.Unsupported, value.Kind);
    }

    [TestMethod]
    public void Read_AnsiValueWithoutDeclaredCodePage_DoesNotFabricateText()
    {
        byte[] bytes = CreatePropertySet();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(48 + 8), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(48 + 32), 30);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(48 + 36), 6);
        Encoding.ASCII.GetBytes("Title\0").CopyTo(bytes, 48 + 40);

        OlePropertySetReadResult result = OlePropertySetReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, result.PropertySet!.Sections[0].CodePage);
        Assert.AreEqual(OlePropertyValueKind.Unsupported,
            Assert.ContainsSingle(result.PropertySet.Sections[0].Properties
                .Where(static property => property.PropertyId == 2)).Kind);
    }

    [TestMethod]
    public void ReadOle10Native_CommonDescriptor_ReturnsPassivePayload()
    {
        byte[] bytes = CreateOle10Native();

        OleEmbeddedObjectDescriptorResult result = OleEmbeddedObjectDescriptorReader.ReadOle10Native(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("label", result.Descriptor!.Label);
        Assert.AreEqual("file.bin", result.Descriptor.OriginalFileName);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, result.Descriptor.Payload.ToArray());
    }

    [TestMethod]
    public void ReadOle10Native_PayloadDoesNotExactlyReachDeclaredEnd_ReturnsInvalidStructure()
    {
        byte[] bytes = CreateOle10Native();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(bytes.Length - 7), 2);

        OleEmbeddedObjectDescriptorResult result = OleEmbeddedObjectDescriptorReader.ReadOle10Native(bytes);

        Assert.AreEqual(OleEmbeddedObjectDescriptorError.InvalidStructure, result.Error);
    }

    [TestMethod]
    public void ReadOle10Native_DescriptorReadCannotEscapeDeclaredSize()
    {
        byte[] bytes = CreateOle10Native();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 8);

        OleEmbeddedObjectDescriptorResult result = OleEmbeddedObjectDescriptorReader.ReadOle10Native(bytes);

        Assert.AreEqual(OleEmbeddedObjectDescriptorError.InvalidStructure, result.Error);
    }

    [TestMethod]
    [DataRow(0u)]
    [DataRow(1u)]
    [DataRow(7u)]
    public void ReadOle10Native_DeclaredSizeBelowMinimum_ReturnsInvalidStructure(uint declaredSize)
    {
        byte[] bytes = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, declaredSize);

        OleEmbeddedObjectDescriptorResult result = OleEmbeddedObjectDescriptorReader.ReadOle10Native(bytes);

        Assert.AreEqual(OleEmbeddedObjectDescriptorError.InvalidStructure, result.Error);
    }

    private static byte[] CreatePropertySet()
    {
        const int sectionOffset = 48;
        const int sectionSize = 52;
        byte[] bytes = new byte[sectionOffset + sectionSize];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0xfffe);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), sectionOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(sectionOffset), sectionSize);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(sectionOffset + 4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(sectionOffset + 8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(sectionOffset + 12), 24);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(sectionOffset + 16), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(sectionOffset + 20), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(sectionOffset + 24), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(sectionOffset + 28), 1252);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(sectionOffset + 32), 31);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(sectionOffset + 36), 6);
        Encoding.Unicode.GetBytes("Title\0").CopyTo(bytes, sectionOffset + 40);
        return bytes;
    }

    private static byte[] CreateOle10Native()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);
        writer.Write(0u);
        writer.Write((ushort)2);
        WriteCString(writer, "label");
        WriteCString(writer, "file.bin");
        writer.Write(0u);
        WriteCString(writer, "command");
        writer.Write(3u);
        writer.Write(new byte[] { 1, 2, 3 });
        writer.Flush();
        byte[] bytes = stream.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)(bytes.Length - 4));
        return bytes;
    }

    private static void WriteCString(BinaryWriter writer, string value)
    {
        writer.Write(Encoding.Latin1.GetBytes(value));
        writer.Write((byte)0);
    }
}
