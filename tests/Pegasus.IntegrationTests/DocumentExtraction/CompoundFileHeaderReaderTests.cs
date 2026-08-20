using Xunit;
using System.Buffers.Binary;
using Pegasus.Infrastructure.Intake.DocumentExtraction.Cfb;

namespace Pegasus.IntegrationTests.DocumentExtraction;

public sealed class CompoundFileHeaderReaderTests
{
    private static ReadOnlySpan<byte> Signature =>
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    [Fact]
    public void DefaultResultIsNotSuccessful()
    {
        CompoundFileHeaderReadResult result = default;

        Assert.False(result.IsSuccess);
        Assert.Equal(CompoundFileHeaderReadError.Uninitialized, result.Error);
        Assert.Null(result.Header);
    }

    [Fact]
    public void ReadValidVersion3HeaderReturnsAllFields()
    {
        byte[] bytes = CreateVersion3File();

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        Assert.True(result.IsSuccess);
        Assert.Equal(CompoundFileHeaderReadError.None, result.Error);
        Assert.NotNull(result.Header);
        Assert.Equal((ushort)0x003E, result.Header.MinorVersion);
        Assert.Equal((ushort)3, result.Header.MajorVersion);
        Assert.Equal(512, result.Header.SectorSize);
        Assert.Equal(64, result.Header.MiniSectorSize);
        Assert.Equal((uint)0, result.Header.DirectorySectorCount);
        Assert.Equal((uint)1, result.Header.FatSectorCount);
        Assert.Equal((uint)1, result.Header.FirstDirectorySector);
        Assert.Equal((uint)7, result.Header.TransactionSignature);
        Assert.Equal((uint)4096, result.Header.MiniStreamCutoff);
        Assert.Equal(CompoundFileConstants.EndOfChain, result.Header.FirstMiniFatSector);
        Assert.Equal((uint)0, result.Header.MiniFatSectorCount);
        Assert.Equal(CompoundFileConstants.EndOfChain, result.Header.FirstDifatSector);
        Assert.Equal((uint)0, result.Header.DifatSectorCount);
        Assert.Equal(CompoundFileConstants.HeaderDifatEntryCount, result.Header.HeaderDifat.Length);
        Assert.Equal((uint)0, result.Header.HeaderDifat[0]);
        for (int index = 1; index < CompoundFileConstants.HeaderDifatEntryCount; index++)
        {
            Assert.Equal(CompoundFileConstants.FreeSector, result.Header.HeaderDifat[index]);
        }
    }

    [Fact]
    public void ReadUnexpectedMinorVersionReturnsUnsupportedMinorVersion()
    {
        byte[] bytes = CreateVersion3File();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(24), 0x003D);

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, CompoundFileHeaderReadError.UnsupportedMinorVersion);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(511)]
    public void ReadTruncatedHeaderReturnsHeaderTooShort(int length)
    {
        byte[] bytes = new byte[length];

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, CompoundFileHeaderReadError.HeaderTooShort);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    public void ReadContainerBelowVersion3MinimumReturnsFileTooSmall(int length)
    {
        byte[] complete = CreateVersion3File();
        byte[] truncated = complete[..length];

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(truncated);

        AssertFailure(result, CompoundFileHeaderReadError.FileTooSmall);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(CompoundFileConstants.Version3SectorSize - 1)]
    public void ReadNonSectorAlignedContainerReturnsAlignmentError(int extraBytes)
    {
        byte[] bytes = new byte[
            CompoundFileConstants.Version3MinimumFileLength + extraBytes];
        CreateVersion3File().CopyTo(bytes, 0);

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, CompoundFileHeaderReadError.FileLengthNotSectorAligned);
    }

    [Fact]
    public void ReadAlignedContainerAboveMinimumReturnsSuccess()
    {
        byte[] bytes = new byte[
            CompoundFileConstants.Version3MinimumFileLength +
            CompoundFileConstants.Version3SectorSize];
        CreateVersion3File().CopyTo(bytes, 0);

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        Assert.True(result.IsSuccess);
        Assert.Equal(CompoundFileHeaderReadError.None, result.Error);
        Assert.NotNull(result.Header);
    }

    [Fact]
    public void ReadHeaderWithSecondFatSectorReadsLittleEndianDifatEntry()
    {
        byte[] bytes = new byte[
            CompoundFileConstants.Version3MinimumFileLength +
            CompoundFileConstants.Version3SectorSize];
        CreateVersion3File().CopyTo(bytes, 0);
        WriteUInt32(bytes, 44, 2);
        WriteUInt32(bytes, 76 + sizeof(uint), 2);

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Header);
        Assert.Equal((uint)2, result.Header.FatSectorCount);
        Assert.Equal((uint)2, result.Header.HeaderDifat[1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void ReadInvalidSignatureReturnsSignatureError(int offset)
    {
        byte[] bytes = CreateVersion3File();
        bytes[offset] ^= 0xFF;

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, CompoundFileHeaderReadError.InvalidSignature);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(23)]
    public void ReadNonZeroClassIdentifierReturnsClassIdentifierError(int offset)
    {
        byte[] bytes = CreateVersion3File();
        bytes[offset] = 1;

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, CompoundFileHeaderReadError.NonZeroClassIdentifier);
    }

    [Theory]
    [InlineData(26, 2, (int)CompoundFileHeaderReadError.UnsupportedMajorVersion)]
    [InlineData(28, 0, (int)CompoundFileHeaderReadError.InvalidByteOrder)]
    [InlineData(29, 0, (int)CompoundFileHeaderReadError.InvalidByteOrder)]
    [InlineData(30, 8, (int)CompoundFileHeaderReadError.InvalidSectorShift)]
    [InlineData(30, 10, (int)CompoundFileHeaderReadError.InvalidSectorShift)]
    [InlineData(32, 5, (int)CompoundFileHeaderReadError.InvalidMiniSectorShift)]
    [InlineData(32, 7, (int)CompoundFileHeaderReadError.InvalidMiniSectorShift)]
    [InlineData(34, 1, (int)CompoundFileHeaderReadError.NonZeroReservedBytes)]
    [InlineData(39, 1, (int)CompoundFileHeaderReadError.NonZeroReservedBytes)]
    [InlineData(40, 1, (int)CompoundFileHeaderReadError.InvalidVersion3DirectorySectorCount)]
    [InlineData(56, 1, (int)CompoundFileHeaderReadError.InvalidMiniStreamCutoff)]
    [InlineData(57, 15, (int)CompoundFileHeaderReadError.InvalidMiniStreamCutoff)]
    public void ReadInvalidHeaderFieldReturnsSpecificError(
        int offset,
        int value,
        int expectedError)
    {
        byte[] bytes = CreateVersion3File();
        bytes[offset] = checked((byte)value);

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, (CompoundFileHeaderReadError)expectedError);
    }

    [Fact]
    public void ReadDoesNotModifyCallerOwnedBytes()
    {
        byte[] bytes = CreateVersion3File();
        byte[] original = (byte[])bytes.Clone();

        _ = CompoundFileHeaderReader.Read(bytes);

        Assert.Equal(original, bytes);
    }

    [Fact]
    public void ReadValidVersion4HeaderReturnsVersion4SectorGeometry()
    {
        byte[] bytes = CompoundFileFixture.CreateEmpty(4);

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Header);
        Assert.Equal((ushort)4, result.Header.MajorVersion);
        Assert.Equal(CompoundFileConstants.Version4SectorSize, result.Header.SectorSize);
        Assert.Equal((uint)1, result.Header.DirectorySectorCount);
    }

    [Fact]
    public void ReadVersion4NonZeroHeaderPaddingReturnsSpecificError()
    {
        byte[] bytes = CompoundFileFixture.CreateEmpty(4);
        bytes[CompoundFileConstants.HeaderLength] = 1;

        CompoundFileHeaderReadResult result = CompoundFileHeaderReader.Read(bytes);

        AssertFailure(result, CompoundFileHeaderReadError.NonZeroVersion4HeaderPadding);
    }

    [Fact]
    public void ReadVersion4WithoutDirectoryCountReturnsSpecificError()
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
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
        Assert.Null(result.Header);
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
