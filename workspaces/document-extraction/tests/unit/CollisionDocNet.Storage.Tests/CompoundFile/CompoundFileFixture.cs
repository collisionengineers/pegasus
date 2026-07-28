using System.Buffers.Binary;
using System.Text;
using CollisionDocNet.Storage.CompoundFile;

namespace CollisionDocNet.Storage.Tests.CompoundFile;

internal static class CompoundFileFixture
{
    internal const int FatSector = 0;
    internal const int DirectorySector = 1;
    internal const int MiniFatSector = 2;
    internal const int MiniStreamSector = 3;

    internal static byte[] CreateEmpty(ushort majorVersion)
    {
        int sectorSize = majorVersion == 3 ? 512 : 4096;
        byte[] bytes = CreateFile(majorVersion, sectorCount: 2, fatSectorCount: 1);
        Span<byte> fat = GetSector(bytes, sectorSize, FatSector);
        FillUInt32(fat, CompoundFileConstants.FreeSector);
        WriteUInt32(fat, FatSector * sizeof(uint), CompoundFileConstants.FatSector);
        WriteUInt32(fat, DirectorySector * sizeof(uint), CompoundFileConstants.EndOfChain);
        WriteRootEntry(GetDirectoryEntry(bytes, sectorSize, 0), CompoundFileConstants.NoStream);
        FillUnusedDirectoryEntries(bytes, sectorSize, 1);
        return bytes;
    }

    internal static byte[] CreateWithRegularStream(ushort majorVersion, byte fill = 0x5A)
    {
        int sectorSize = majorVersion == 3 ? 512 : 4096;
        int dataSectorCount = 4096 / sectorSize;
        byte[] bytes = CreateFile(majorVersion, 2 + dataSectorCount, 1);
        Span<byte> fat = GetSector(bytes, sectorSize, FatSector);
        FillUInt32(fat, CompoundFileConstants.FreeSector);
        WriteUInt32(fat, 0, CompoundFileConstants.FatSector);
        WriteUInt32(fat, sizeof(uint), CompoundFileConstants.EndOfChain);
        for (int index = 0; index < dataSectorCount; index++)
        {
            int sector = 2 + index;
            uint next = index + 1 == dataSectorCount
                ? CompoundFileConstants.EndOfChain
                : (uint)(sector + 1);
            WriteUInt32(fat, sector * sizeof(uint), next);
            GetSector(bytes, sectorSize, sector).Fill(fill);
        }

        WriteRootEntry(GetDirectoryEntry(bytes, sectorSize, 0), childId: 1);
        WriteStreamEntry(
            GetDirectoryEntry(bytes, sectorSize, 1),
            "Regular",
            startingSector: 2,
            size: 4096);
        FillUnusedDirectoryEntries(bytes, sectorSize, 2);
        return bytes;
    }

    internal static byte[] CreateWithMiniStream(ReadOnlySpan<byte> content)
    {
        const int sectorSize = 512;
        if (content.Length is <= 0 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(content));
        }

        byte[] bytes = CreateFile(majorVersion: 3, sectorCount: 4, fatSectorCount: 1);
        WriteUInt32(bytes, 60, MiniFatSector);
        WriteUInt32(bytes, 64, 1);

        Span<byte> fat = GetSector(bytes, sectorSize, FatSector);
        FillUInt32(fat, CompoundFileConstants.FreeSector);
        WriteUInt32(fat, 0, CompoundFileConstants.FatSector);
        WriteUInt32(fat, 4, CompoundFileConstants.EndOfChain);
        WriteUInt32(fat, 8, CompoundFileConstants.EndOfChain);
        WriteUInt32(fat, 12, CompoundFileConstants.EndOfChain);

        Span<byte> miniFat = GetSector(bytes, sectorSize, MiniFatSector);
        FillUInt32(miniFat, CompoundFileConstants.FreeSector);
        WriteUInt32(miniFat, 0, CompoundFileConstants.EndOfChain);
        content.CopyTo(GetSector(bytes, sectorSize, MiniStreamSector));

        WriteRootEntry(
            GetDirectoryEntry(bytes, sectorSize, 0),
            childId: 1,
            startingSector: MiniStreamSector,
            streamSize: 64);
        WriteStreamEntry(
            GetDirectoryEntry(bytes, sectorSize, 1),
            "Mini",
            startingSector: 0,
            size: (ulong)content.Length);
        FillUnusedDirectoryEntries(bytes, sectorSize, 2);
        return bytes;
    }

    internal static byte[] CreateWithDifat()
    {
        const int sectorSize = 512;
        const int fatSectorCount = 110;
        const int directorySector = 110;
        const int difatSector = 111;
        byte[] bytes = CreateFile(3, sectorCount: 112, fatSectorCount);
        WriteUInt32(bytes, 48, directorySector);
        WriteUInt32(bytes, 68, difatSector);
        WriteUInt32(bytes, 72, 1);

        for (int index = 0; index < CompoundFileConstants.HeaderDifatEntryCount; index++)
        {
            WriteUInt32(bytes, 76 + (index * sizeof(uint)), (uint)index);
        }

        for (int sector = 0; sector < fatSectorCount; sector++)
        {
            FillUInt32(GetSector(bytes, sectorSize, sector), CompoundFileConstants.FreeSector);
        }

        for (int sector = 0; sector < fatSectorCount; sector++)
        {
            WriteFatEntry(bytes, sectorSize, (uint)sector, CompoundFileConstants.FatSector);
        }

        WriteFatEntry(bytes, sectorSize, directorySector, CompoundFileConstants.EndOfChain);
        WriteFatEntry(bytes, sectorSize, difatSector, CompoundFileConstants.DifatSector);

        Span<byte> difat = GetSector(bytes, sectorSize, difatSector);
        FillUInt32(difat, CompoundFileConstants.FreeSector);
        WriteUInt32(difat, 0, 109);
        WriteUInt32(difat, difat.Length - sizeof(uint), CompoundFileConstants.EndOfChain);

        WriteRootEntry(GetSector(bytes, sectorSize, directorySector)[..128],
            CompoundFileConstants.NoStream);
        Span<byte> directoryBytes = GetSector(bytes, sectorSize, directorySector);
        for (int streamId = 1; streamId < 4; streamId++)
        {
            Span<byte> entry = directoryBytes.Slice(streamId * 128, 128);
            WriteUInt32(entry, 68, CompoundFileConstants.NoStream);
            WriteUInt32(entry, 72, CompoundFileConstants.NoStream);
            WriteUInt32(entry, 76, CompoundFileConstants.NoStream);
        }

        return bytes;
    }

    internal static Span<byte> GetSector(byte[] bytes, int sectorSize, int sector) =>
        bytes.AsSpan((sector + 1) * sectorSize, sectorSize);

    internal static Span<byte> GetDirectoryEntry(byte[] bytes, int sectorSize, int streamId) =>
        GetSector(bytes, sectorSize, DirectorySector)
            .Slice(streamId * CompoundFileConstants.DirectoryEntryLength,
                CompoundFileConstants.DirectoryEntryLength);

    internal static void WriteUInt32(Span<byte> bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..], value);

    private static void WriteFatEntry(byte[] bytes, int sectorSize, uint index, uint value)
    {
        int entriesPerSector = sectorSize / sizeof(uint);
        int fatSector = checked((int)(index / (uint)entriesPerSector));
        int offset = checked((int)(index % (uint)entriesPerSector) * sizeof(uint));
        WriteUInt32(GetSector(bytes, sectorSize, fatSector), offset, value);
    }

    private static byte[] CreateFile(ushort majorVersion, int sectorCount, uint fatSectorCount)
    {
        int sectorSize = majorVersion == 3 ? 512 : 4096;
        byte[] bytes = new byte[(sectorCount + 1) * sectorSize];
        ReadOnlySpan<byte> signature =
            [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
        signature.CopyTo(bytes);
        WriteUInt16(bytes, 24, 0x003E);
        WriteUInt16(bytes, 26, majorVersion);
        WriteUInt16(bytes, 28, 0xFFFE);
        WriteUInt16(bytes, 30, majorVersion == 3 ? (ushort)9 : (ushort)12);
        WriteUInt16(bytes, 32, 6);
        WriteUInt32(bytes, 40, majorVersion == 3 ? 0U : 1U);
        WriteUInt32(bytes, 44, fatSectorCount);
        WriteUInt32(bytes, 48, DirectorySector);
        WriteUInt32(bytes, 56, 4096);
        WriteUInt32(bytes, 60, CompoundFileConstants.EndOfChain);
        WriteUInt32(bytes, 68, CompoundFileConstants.EndOfChain);
        for (int index = 0; index < CompoundFileConstants.HeaderDifatEntryCount; index++)
        {
            WriteUInt32(bytes, 76 + (index * sizeof(uint)), CompoundFileConstants.FreeSector);
        }

        WriteUInt32(bytes, 76, FatSector);
        return bytes;
    }

    private static void WriteRootEntry(
        Span<byte> entry,
        uint childId,
        uint startingSector = CompoundFileConstants.EndOfChain,
        ulong streamSize = 0)
    {
        WriteNamedEntry(entry, "Root Entry", CompoundFileObjectType.RootStorage);
        entry[67] = (byte)CompoundFileNodeColor.Black;
        WriteUInt32(entry, 68, CompoundFileConstants.NoStream);
        WriteUInt32(entry, 72, CompoundFileConstants.NoStream);
        WriteUInt32(entry, 76, childId);
        WriteUInt32(entry, 116, startingSector);
        WriteUInt64(entry, 120, streamSize);
    }

    private static void WriteStreamEntry(
        Span<byte> entry,
        string name,
        uint startingSector,
        ulong size)
    {
        WriteNamedEntry(entry, name, CompoundFileObjectType.Stream);
        entry[67] = (byte)CompoundFileNodeColor.Black;
        WriteUInt32(entry, 68, CompoundFileConstants.NoStream);
        WriteUInt32(entry, 72, CompoundFileConstants.NoStream);
        WriteUInt32(entry, 76, CompoundFileConstants.NoStream);
        WriteUInt32(entry, 116, startingSector);
        WriteUInt64(entry, 120, size);
    }

    private static void WriteNamedEntry(
        Span<byte> entry,
        string name,
        CompoundFileObjectType objectType)
    {
        int byteCount = Encoding.Unicode.GetBytes(name, entry);
        WriteUInt16(entry, 64, checked((ushort)(byteCount + 2)));
        entry[66] = (byte)objectType;
    }

    private static void FillUnusedDirectoryEntries(byte[] bytes, int sectorSize, int firstUnused)
    {
        int entriesPerSector = sectorSize / CompoundFileConstants.DirectoryEntryLength;
        for (int streamId = firstUnused; streamId < entriesPerSector; streamId++)
        {
            Span<byte> entry = GetDirectoryEntry(bytes, sectorSize, streamId);
            WriteUInt32(entry, 68, CompoundFileConstants.NoStream);
            WriteUInt32(entry, 72, CompoundFileConstants.NoStream);
            WriteUInt32(entry, 76, CompoundFileConstants.NoStream);
        }
    }

    private static void FillUInt32(Span<byte> bytes, uint value)
    {
        for (int offset = 0; offset < bytes.Length; offset += sizeof(uint))
        {
            WriteUInt32(bytes, offset, value);
        }
    }

    private static void WriteUInt16(Span<byte> bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes[offset..], value);

    private static void WriteUInt64(Span<byte> bytes, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[offset..], value);
}
