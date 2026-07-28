using System.Buffers.Binary;
using CollisionDocNet.Storage.Zip;

namespace CollisionDocNet.Storage.Tests.Zip;

[TestClass]
public sealed class BoundedZipReaderTests
{
    [TestMethod]
    public void Read_StoredOrDeflatedEntry_ReturnsVerifiedContent()
    {
        byte[] expected = "bounded ZIP content"u8.ToArray();
        byte[] bytes = ZipFixture.Create(("folder/item.txt", expected));

        BoundedZipReadResult result = BoundedZipReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        BoundedZipEntry entry = Assert.ContainsSingle(result.Archive!.Entries);
        CollectionAssert.AreEqual(expected, entry.Content.ToArray());
    }

    [TestMethod]
    public void Read_DuplicateName_ReturnsDuplicateName()
    {
        byte[] bytes = ZipFixture.Create(("same.txt", [1]), ("same.txt", [2]));

        BoundedZipReadResult result = BoundedZipReader.Read(bytes);

        Assert.AreEqual(BoundedZipReadError.DuplicateName, result.Error);
    }

    [TestMethod]
    public void Read_Zip64EndRecords_ReturnsArchiveMarkedZip64()
    {
        byte[] bytes = ZipFixture.PromoteEndRecordToZip64(
            ZipFixture.Create(("item.txt", "zip64"u8.ToArray())));

        BoundedZipReadResult result = BoundedZipReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.Archive!.UsesZip64);
        Assert.HasCount(1, result.Archive.Entries);
    }

    [TestMethod]
    [DataRow("../escape.txt")]
    [DataRow("/absolute.txt")]
    [DataRow("folder\\item.txt")]
    [DataRow("C:/drive.txt")]
    public void Read_UnsafeName_ReturnsInvalidName(string name)
    {
        byte[] bytes = ZipFixture.Create((name, new byte[] { 1 }));

        BoundedZipReadResult result = BoundedZipReader.Read(bytes);

        Assert.AreEqual(BoundedZipReadError.InvalidName, result.Error);
    }

    [TestMethod]
    public void Read_ExpandedEntryExceedsLimit_ReturnsEntrySizeLimitExceeded()
    {
        byte[] bytes = ZipFixture.Create(("large.bin", new byte[1024]));
        var limits = BoundedZipLimits.Default with { MaximumEntryBytes = 100 };

        BoundedZipReadResult result = BoundedZipReader.Read(bytes, limits);

        Assert.AreEqual(BoundedZipReadError.EntrySizeLimitExceeded, result.Error);
    }

    [TestMethod]
    public void Read_CompressionRatioExceedsLimit_ReturnsCompressionRatioLimitExceeded()
    {
        byte[] bytes = ZipFixture.Create(("ratio.bin", new byte[16_384]));
        var limits = BoundedZipLimits.Default with { MaximumCompressionRatio = 2 };

        BoundedZipReadResult result = BoundedZipReader.Read(bytes, limits);

        Assert.AreEqual(BoundedZipReadError.CompressionRatioLimitExceeded, result.Error);
    }

    [TestMethod]
    public void Read_CrcChanged_ReturnsCrcMismatchOrDecompressionFailure()
    {
        byte[] bytes = ZipFixture.Create(("item.bin", Enumerable.Repeat((byte)0x41, 256).ToArray()));
        int local = Array.IndexOf(bytes, (byte)0x50);
        ushort nameLength = BitConverter.ToUInt16(bytes, local + 26);
        ushort extraLength = BitConverter.ToUInt16(bytes, local + 28);
        bytes[local + 30 + nameLength + extraLength] ^= 0xff;

        BoundedZipReadResult result = BoundedZipReader.Read(bytes);

        Assert.Contains(result.Error, new[] { BoundedZipReadError.CrcMismatch, BoundedZipReadError.DecompressionFailed });
    }

    [TestMethod]
    public void Read_Cancelled_ReturnsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        BoundedZipReadResult result = BoundedZipReader.Read(
            ZipFixture.Create(("item.txt", new byte[] { 1 })),
            cancellationToken: cancellation.Token);

        Assert.AreEqual(BoundedZipReadError.Cancelled, result.Error);
    }

    [TestMethod]
    public void Read_EncryptedFlag_ReturnsEncryptedEntry()
    {
        byte[] bytes = ZipFixture.Create(("item.txt", [1]));
        int central = ZipFixture.FindSignature(bytes, 0x02014b50);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(central + 8), 1);

        BoundedZipReadResult result = BoundedZipReader.Read(bytes);

        Assert.AreEqual(BoundedZipReadError.EncryptedEntry, result.Error);
    }

    [TestMethod]
    public void Read_UnsupportedCompression_ReturnsUnsupportedCompression()
    {
        byte[] bytes = ZipFixture.Create(("item.txt", [1]));
        int central = ZipFixture.FindSignature(bytes, 0x02014b50);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 99);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(central + 10), 99);

        BoundedZipReadResult result = BoundedZipReader.Read(bytes);

        Assert.AreEqual(BoundedZipReadError.UnsupportedCompression, result.Error);
    }

    [TestMethod]
    public void Read_EntryCountExceedsLimit_ReturnsEntryCountLimitExceeded()
    {
        byte[] bytes = ZipFixture.Create(("one", [1]), ("two", [2]));

        BoundedZipReadResult result = BoundedZipReader.Read(
            bytes, BoundedZipLimits.Default with { MaximumEntries = 1 });

        Assert.AreEqual(BoundedZipReadError.EntryCountLimitExceeded, result.Error);
    }

    [TestMethod]
    public void Read_TotalExpandedExceedsLimit_ReturnsTotalExpandedLimitExceeded()
    {
        byte[] bytes = ZipFixture.Create(("one", new byte[8]), ("two", new byte[8]));

        BoundedZipReadResult result = BoundedZipReader.Read(
            bytes, BoundedZipLimits.Default with { MaximumTotalExpandedBytes = 15 });

        Assert.AreEqual(BoundedZipReadError.TotalExpandedLimitExceeded, result.Error);
    }

    [TestMethod]
    public void Read_SignedDataDescriptor_ReconcilesDescriptor()
    {
        byte[] bytes = ZipFixture.CreateWithDataDescriptor("item.txt", "descriptor"u8.ToArray());

        BoundedZipReadResult result = BoundedZipReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("descriptor", System.Text.Encoding.UTF8.GetString(
            Assert.ContainsSingle(result.Archive!.Entries).Content.AsSpan()));
    }

    [TestMethod]
    public void Read_DataDescriptorMismatch_ReturnsInvalidStructure()
    {
        byte[] bytes = ZipFixture.CreateWithDataDescriptor("item.txt", "descriptor"u8.ToArray());
        int descriptor = ZipFixture.FindSignature(bytes, 0x08074b50);
        Assert.IsGreaterThanOrEqualTo(0, descriptor);
        bytes[descriptor + 4] ^= 0xff;

        BoundedZipReadResult result = BoundedZipReader.Read(bytes);

        Assert.AreEqual(BoundedZipReadError.InvalidStructure, result.Error);
    }

    [TestMethod]
    public void Read_UnsignedDataDescriptor_ReconcilesDescriptor()
    {
        byte[] bytes = ZipFixture.RemoveDataDescriptorSignature(
            ZipFixture.CreateWithDataDescriptor("item.txt", "unsigned"u8.ToArray()));

        BoundedZipReadResult result = BoundedZipReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("unsigned", System.Text.Encoding.UTF8.GetString(
            Assert.ContainsSingle(result.Archive!.Entries).Content.AsSpan()));
    }

    [TestMethod]
    public void Read_Zip64DataDescriptorWithSmallSizes_ReconcilesUsingSixtyFourBitFields()
    {
        byte[] bytes = ZipFixture.PromoteDataDescriptorSizesToZip64(
            ZipFixture.CreateWithDataDescriptor("item.txt", "zip64 descriptor"u8.ToArray()));

        BoundedZipReadResult result = BoundedZipReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("zip64 descriptor", System.Text.Encoding.UTF8.GetString(
            Assert.ContainsSingle(result.Archive!.Entries).Content.AsSpan()));
    }

    [TestMethod]
    public void Read_UnsignedZip64DataDescriptorWithSmallSizes_ReconcilesUsingSixtyFourBitFields()
    {
        byte[] bytes = ZipFixture.RemoveDataDescriptorSignature(
            ZipFixture.PromoteDataDescriptorSizesToZip64(
                ZipFixture.CreateWithDataDescriptor("item.txt", "unsigned zip64"u8.ToArray())));

        BoundedZipReadResult result = BoundedZipReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("unsigned zip64", System.Text.Encoding.UTF8.GetString(
            Assert.ContainsSingle(result.Archive!.Entries).Content.AsSpan()));
    }

    [TestMethod]
    public void Read_DeflatePayloadHasTrailingByte_ReturnsDecompressionFailed()
    {
        byte[] bytes = ZipFixture.AddTrailingByteToDeflatePayload(
            ZipFixture.Create(("item.txt", System.Text.Encoding.UTF8.GetBytes(new string('a', 4096)))));

        BoundedZipReadResult result = BoundedZipReader.Read(bytes);

        Assert.AreEqual(BoundedZipReadError.DecompressionFailed, result.Error);
    }

    [TestMethod]
    public void Read_FractionallyAboveCompressionRatioLimit_ReturnsCompressionRatioLimitExceeded()
    {
        byte[] content = Enumerable.Range(0, 1000).Select(static value => (byte)(value % 251)).ToArray();
        byte[] bytes = ZipFixture.Create(("ratio.bin", content));
        int central = ZipFixture.FindSignature(bytes, 0x02014b50);
        uint compressed = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(central + 20));
        uint expanded = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(central + 24));
        int quotient = checked((int)(expanded / compressed));
        Assert.AreNotEqual(0u, expanded % compressed, "Fixture must exercise a fractional ratio.");

        BoundedZipReadResult result = BoundedZipReader.Read(
            bytes, BoundedZipLimits.Default with { MaximumCompressionRatio = quotient });

        Assert.AreEqual(BoundedZipReadError.CompressionRatioLimitExceeded, result.Error);
    }

    [TestMethod]
    public void Read_LocalCrcDoesNotMatchCentral_ReturnsInvalidStructure()
    {
        byte[] bytes = ZipFixture.Create(("item.txt", "content"u8.ToArray()));
        bytes[14] ^= 0xff;

        BoundedZipReadResult result = BoundedZipReader.Read(bytes);

        Assert.AreEqual(BoundedZipReadError.InvalidStructure, result.Error);
    }

    [TestMethod]
    public void Read_OverlappingLocalRecords_ReturnsOverlappingEntries()
    {
        BoundedZipReadResult result = BoundedZipReader.Read(ZipFixture.CreateOverlappingLocalRecords());

        Assert.AreEqual(BoundedZipReadError.OverlappingEntries, result.Error);
    }
}
