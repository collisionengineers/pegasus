using System.Buffers.Binary;
using CollisionDocNet.Storage.CompoundFile;

namespace CollisionDocNet.Storage.Tests.CompoundFile;

[TestClass]
public sealed class CompoundFileHeaderReaderTests
{
    private static ReadOnlySpan<byte> Signature =>
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    [TestMethod]
    public void DefaultResult_IsNotSuccessful()
    {
        CompoundFileHeaderReadResult result = default;

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(CompoundFileHeaderReadError.Uninitialized, result.Error);
        Assert.IsNull(result.Header);
    }

    [TestMethod]
    public void Read_ValidVersion3Header_ReturnsAllFields()
    {
        byte[] bytes = CreateVersion3File();

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(CompoundFileHeaderReadError.None, result.Error);
        Assert.IsNotNull(result.Header);
        Assert.AreEqual((ushort)0x003E, result.Header.MinorVersion);
        Assert.AreEqual((ushort)3, result.Header.MajorVersion);
        Assert.AreEqual(512, result.Header.SectorSize);
        Assert.AreEqual(64, result.Header.MiniSectorSize);
        Assert.AreEqual((uint)0, result.Header.DirectorySectorCount);
        Assert.AreEqual((uint)1, result.Header.FatSectorCount);
        Assert.AreEqual((uint)1, result.Header.FirstDirectorySector);
        Assert.AreEqual((uint)7, result.Header.TransactionSignature);
        Assert.AreEqual((uint)4096, result.Header.MiniStreamCutoff);
        Assert.AreEqual(CompoundFileConstants.EndOfChain, result.Header.FirstMiniFatSector);
        Assert.AreEqual((uint)0, result.Header.MiniFatSectorCount);
        Assert.AreEqual(CompoundFileConstants.EndOfChain, result.Header.FirstDifatSector);
        Assert.AreEqual((uint)0, result.Header.DifatSectorCount);
        Assert.HasCount(CompoundFileConstants.HeaderDifatEntryCount, result.Header.HeaderDifat);
        Assert.AreEqual((uint)0, result.Header.HeaderDifat[0]);
        for (int index = 1; index < CompoundFileConstants.HeaderDifatEntryCount; index++)
        {
            Assert.AreEqual(
                CompoundFileConstants.FreeSector,
                result.Header.HeaderDifat[index],
                $"DIFAT entry {index} was not read from its header slot.");
        }
    }

    [TestMethod]
    public void Read_UnexpectedMinorVersion_ReturnsUnsupportedMinorVersion()
    {
        byte[] bytes = CreateVersion3File();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(24), 0x003D);

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, CompoundFileHeaderReadError.UnsupportedMinorVersion);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(8)]
    [DataRow(511)]
    public void Read_TruncatedHeader_ReturnsHeaderTooShort(int length)
    {
        byte[] bytes = new byte[length];

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, CompoundFileHeaderReadError.HeaderTooShort);
    }

    [TestMethod]
    [DataRow(512)]
    [DataRow(1024)]
    public void Read_ContainerBelowVersion3Minimum_ReturnsFileTooSmall(int length)
    {
        byte[] complete = CreateVersion3File();
        byte[] truncated = complete[..length];

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(truncated);

        AssertFailure(result, CompoundFileHeaderReadError.FileTooSmall);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(CompoundFileConstants.Version3SectorSize - 1)]
    public void Read_NonSectorAlignedContainer_ReturnsAlignmentError(int extraBytes)
    {
        byte[] bytes = new byte[
            CompoundFileConstants.Version3MinimumFileLength + extraBytes];
        CreateVersion3File().CopyTo(bytes, 0);

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, CompoundFileHeaderReadError.FileLengthNotSectorAligned);
    }

    [TestMethod]
    public void Read_AlignedContainerAboveMinimum_ReturnsSuccess()
    {
        byte[] bytes = new byte[
            CompoundFileConstants.Version3MinimumFileLength +
            CompoundFileConstants.Version3SectorSize];
        CreateVersion3File().CopyTo(bytes, 0);

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(CompoundFileHeaderReadError.None, result.Error);
        Assert.IsNotNull(result.Header);
    }

    [TestMethod]
    public void Read_HeaderWithSecondFatSector_ReadsLittleEndianDifatEntry()
    {
        byte[] bytes = new byte[
            CompoundFileConstants.Version3MinimumFileLength +
            CompoundFileConstants.Version3SectorSize];
        CreateVersion3File().CopyTo(bytes, 0);
        WriteUInt32(bytes, 44, 2);
        WriteUInt32(bytes, 76 + sizeof(uint), 2);

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Header);
        Assert.AreEqual((uint)2, result.Header.FatSectorCount);
        Assert.AreEqual((uint)2, result.Header.HeaderDifat[1]);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(7)]
    public void Read_InvalidSignature_ReturnsSignatureError(int offset)
    {
        byte[] bytes = CreateVersion3File();
        bytes[offset] ^= 0xFF;

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, CompoundFileHeaderReadError.InvalidSignature);
    }

    [TestMethod]
    [DataRow(8)]
    [DataRow(15)]
    [DataRow(23)]
    public void Read_NonZeroClassIdentifier_ReturnsClassIdentifierError(int offset)
    {
        byte[] bytes = CreateVersion3File();
        bytes[offset] = 1;

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, CompoundFileHeaderReadError.NonZeroClassIdentifier);
    }

    [TestMethod]
    [DataRow(26, 2, CompoundFileHeaderReadError.UnsupportedMajorVersion)]
    [DataRow(28, 0, CompoundFileHeaderReadError.InvalidByteOrder)]
    [DataRow(29, 0, CompoundFileHeaderReadError.InvalidByteOrder)]
    [DataRow(30, 8, CompoundFileHeaderReadError.InvalidSectorShift)]
    [DataRow(30, 10, CompoundFileHeaderReadError.InvalidSectorShift)]
    [DataRow(32, 5, CompoundFileHeaderReadError.InvalidMiniSectorShift)]
    [DataRow(32, 7, CompoundFileHeaderReadError.InvalidMiniSectorShift)]
    [DataRow(34, 1, CompoundFileHeaderReadError.NonZeroReservedBytes)]
    [DataRow(39, 1, CompoundFileHeaderReadError.NonZeroReservedBytes)]
    [DataRow(40, 1, CompoundFileHeaderReadError.InvalidVersion3DirectorySectorCount)]
    [DataRow(56, 1, CompoundFileHeaderReadError.InvalidMiniStreamCutoff)]
    [DataRow(57, 15, CompoundFileHeaderReadError.InvalidMiniStreamCutoff)]
    public void Read_InvalidHeaderField_ReturnsSpecificError(
        int offset,
        int value,
        CompoundFileHeaderReadError expectedError)
    {
        byte[] bytes = CreateVersion3File();
        bytes[offset] = checked((byte)value);

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, expectedError);
    }

    [TestMethod]
    public void Read_DoesNotModifyCallerOwnedBytes()
    {
        byte[] bytes = CreateVersion3File();
        byte[] original = (byte[])bytes.Clone();

        _ = CompoundFileHeaderReader.Read(bytes);

        CollectionAssert.AreEqual(original, bytes);
    }

    [TestMethod]
    public void Read_ValidVersion4Header_ReturnsVersion4SectorGeometry()
    {
        byte[] bytes = CompoundFileFixture.CreateEmpty(4);

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Header);
        Assert.AreEqual((ushort)4, result.Header.MajorVersion);
        Assert.AreEqual(CompoundFileConstants.Version4SectorSize, result.Header.SectorSize);
        Assert.AreEqual((uint)1, result.Header.DirectorySectorCount);
    }

    [TestMethod]
    public void Read_Version4NonZeroHeaderPadding_ReturnsSpecificError()
    {
        byte[] bytes = CompoundFileFixture.CreateEmpty(4);
        bytes[CompoundFileConstants.HeaderLength] = 1;

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, CompoundFileHeaderReadError.NonZeroVersion4HeaderPadding);
    }

    [TestMethod]
    public void Read_Version4WithoutDirectoryCount_ReturnsSpecificError()
    {
        byte[] bytes = CompoundFileFixture.CreateEmpty(4);
        WriteUInt32(bytes, 40, 0);

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, CompoundFileHeaderReadError.InvalidVersion4DirectorySectorCount);
    }

    private static void AssertFailure(
        CompoundFileHeaderReadResult result,
        CompoundFileHeaderReadError expectedError)
    {
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(expectedError, result.Error);
        Assert.IsNull(result.Header);
    }

    private static byte[] CreateVersion3File()
    {
        byte[] bytes = new byte[CompoundFileConstants.Version3MinimumFileLength];
        Signature.CopyTo(bytes);

        WriteUInt16(bytes, 24, 0x003E);
        WriteUInt16(bytes, 26, 3);
        WriteUInt16(bytes, 28, 0xFFFE);
        WriteUInt16(bytes, 30, 9);
        WriteUInt16(bytes, 32, 6);
        WriteUInt32(bytes, 40, 0);
        WriteUInt32(bytes, 44, 1);
        WriteUInt32(bytes, 48, 1);
        WriteUInt32(bytes, 52, 7);
        WriteUInt32(bytes, 56, 4096);
        WriteUInt32(bytes, 60, CompoundFileConstants.EndOfChain);
        WriteUInt32(bytes, 64, 0);
        WriteUInt32(bytes, 68, CompoundFileConstants.EndOfChain);
        WriteUInt32(bytes, 72, 0);

        for (int index = 0; index < CompoundFileConstants.HeaderDifatEntryCount; index++)
        {
            WriteUInt32(bytes, 76 + (index * sizeof(uint)), CompoundFileConstants.FreeSector);
        }

        WriteUInt32(bytes, 76, 0);
        return bytes;
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
