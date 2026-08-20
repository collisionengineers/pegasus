using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Pegasus.Infrastructure.Intake.DocumentExtraction.Cfb;

/// <summary>
/// Parses and validates the fixed header of a CFB v3/v4 container without taking
/// ownership of, or mutating, the caller's bytes.
/// </summary>
internal static class CompoundFileHeaderReader
{
    private const ushort Version3SectorShift = 9;
    private const ushort Version4SectorShift = 12;
    private const ushort RequiredMiniSectorShift = 6;
    private const ushort RequiredMinorVersion = 0x003E;
    private const ushort RequiredByteOrder = 0xFFFE;
    private const uint RequiredMiniStreamCutoff = 0x00001000;
    private const int HeaderDifatOffset = 76;

    private static ReadOnlySpan<byte> Signature =>
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public static CompoundFileHeaderReadResult Read(ReadOnlySpan<byte> fileBytes)
    {
        if (fileBytes.Length < CompoundFileConstants.HeaderLength)
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.HeaderTooShort);
        }

        ReadOnlySpan<byte> header = fileBytes[..CompoundFileConstants.HeaderLength];

        if (!header[..Signature.Length].SequenceEqual(Signature))
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.InvalidSignature);
        }

        if (ContainsNonZeroByte(header.Slice(8, 16)))
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.NonZeroClassIdentifier);
        }

        if (ReadUInt16(header, 24) != RequiredMinorVersion)
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.UnsupportedMinorVersion);
        }

        ushort majorVersion = ReadUInt16(header, 26);
        if (majorVersion is not (3 or 4))
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.UnsupportedMajorVersion);
        }

        if (ReadUInt16(header, 28) != RequiredByteOrder)
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.InvalidByteOrder);
        }

        ushort sectorShift = ReadUInt16(header, 30);
        ushort requiredSectorShift = majorVersion == 3
            ? Version3SectorShift
            : Version4SectorShift;
        if (sectorShift != requiredSectorShift)
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.InvalidSectorShift);
        }

        ushort miniSectorShift = ReadUInt16(header, 32);
        if (miniSectorShift != RequiredMiniSectorShift)
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.InvalidMiniSectorShift);
        }

        if (ContainsNonZeroByte(header.Slice(34, 6)))
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.NonZeroReservedBytes);
        }

        uint directorySectorCount = ReadUInt32(header, 40);
        if (majorVersion == 3 && directorySectorCount != 0)
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.InvalidVersion3DirectorySectorCount);
        }

        if (majorVersion == 4 && directorySectorCount == 0)
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.InvalidVersion4DirectorySectorCount);
        }

        uint miniStreamCutoff = ReadUInt32(header, 56);
        if (miniStreamCutoff != RequiredMiniStreamCutoff)
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.InvalidMiniStreamCutoff);
        }

        int sectorSize = 1 << sectorShift;
        int minimumFileLength = majorVersion == 3
            ? CompoundFileConstants.Version3MinimumFileLength
            : CompoundFileConstants.Version4MinimumFileLength;
        if (fileBytes.Length < minimumFileLength)
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.FileTooSmall);
        }

        if (fileBytes.Length % sectorSize != 0)
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.FileLengthNotSectorAligned);
        }


        if (majorVersion == 4 &&
            ContainsNonZeroByte(fileBytes.Slice(
                CompoundFileConstants.HeaderLength,
                sectorSize - CompoundFileConstants.HeaderLength)))
        {
            return CompoundFileHeaderReadResult.Failure(
                CompoundFileHeaderReadError.NonZeroVersion4HeaderPadding);
        }

        var headerDifat = ImmutableArray.CreateBuilder<uint>(
            CompoundFileConstants.HeaderDifatEntryCount);
        for (int index = 0; index < CompoundFileConstants.HeaderDifatEntryCount; index++)
        {
            headerDifat.Add(ReadUInt32(header, HeaderDifatOffset + (index * sizeof(uint))));
        }

        var parsedHeader = new CompoundFileHeader(
            MinorVersion: ReadUInt16(header, 24),
            MajorVersion: majorVersion,
            SectorSize: sectorSize,
            MiniSectorSize: 1 << miniSectorShift,
            DirectorySectorCount: directorySectorCount,
            FatSectorCount: ReadUInt32(header, 44),
            FirstDirectorySector: ReadUInt32(header, 48),
            TransactionSignature: ReadUInt32(header, 52),
            MiniStreamCutoff: miniStreamCutoff,
            FirstMiniFatSector: ReadUInt32(header, 60),
            MiniFatSectorCount: ReadUInt32(header, 64),
            FirstDifatSector: ReadUInt32(header, 68),
            DifatSectorCount: ReadUInt32(header, 72),
            HeaderDifat: headerDifat.MoveToImmutable());

        return CompoundFileHeaderReadResult.Success(parsedHeader);
    }

    private static bool ContainsNonZeroByte(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            if (value != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
